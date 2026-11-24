using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldTour.World
{
    /// <summary>WASD or arrow keys, plus click to move (a ray from the camera against the y=0 plane, no Physics).</summary>
    public class PlayerController : MonoBehaviour
    {
        public Camera cam;
        public float speed = 5f;
        public float bounds = 13.5f;
        public Transform marker;
        [Tooltip("Optional. Found on the figure when empty. Gets the float parameter \"Speed\" (0 idle, about 5 at a run).")]
        public Animator animator;
        public float planarSpeed { get; private set; }
        /// <summary>True while she is walking toward a clicked point.</summary>
        public bool HasTarget { get { return hasTarget; } }

        /// <summary>Metres kept clear of a blocker when planning a click path (a little more than the 0.35 m the walk pushes out by).</summary>
        public const float PlanMargin = 0.4f;
        /// <summary>Where the last click path ended up: the click itself, or the nearest reachable spot when it was closed off.</summary>
        public bool LastPathReached { get; private set; } = true;

        bool hasTarget;
        int lastPausedFrame = -10;
        float blockedFor;
        int replans;
        Vector3 target;
        List<Vector3> path = new List<Vector3>();
        int pathIndex;
        static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        /// <summary>Walks to a ground point as if it had been clicked (phase 8.1: click on a crew member, walk up to them).</summary>
        public void WalkTo(Vector3 point)
        {
            SetTarget(point);
        }

        /// <summary>Plans a path over the flat ground round every obstacle and keep-out box (A* on a grid, see <see cref="PathGrid"/>) and starts along it.</summary>
        void SetTarget(Vector3 point)
        {
            target = Obstacle.PushOut(Clamp(point), PlanMargin + 0.05f);
            replans = 0; blockedFor = 0f;
            Plan();
        }

        void Plan()
        {
            bool reached;
            path = PathGrid.Find(transform.position, target, bounds, p => Obstacle.IsBlocked(p, PlanMargin), out reached);
            pathIndex = 0;
            LastPathReached = reached;
            if (path.Count == 0) path.Add(target);   // boxed in where she stands: walk straight and let the push-out sort it
            if (!reached) target = path[path.Count - 1];   // closed off: walk to the nearest spot she can reach
            hasTarget = true;
        }

        public void StopWalking() { hasTarget = false; }

        /// <summary>Puts her on a ground point at once (loading a saved position, travel).</summary>
        public void Place(Vector3 point)
        {
            hasTarget = false;
            Vector3 p = Clamp(point);
            transform.position = p;
        }

        /// <summary>Turns her toward a point on the ground without walking.</summary>
        public void Face(Vector3 point)
        {
            Vector3 to = point - transform.position; to.y = 0;
            if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        void Update()
        {
            if (WorldInput.Paused)
            {
                hasTarget = false;
                planarSpeed = 0f;
                AnimSafe.Float(animator, "Speed", 0f);
                lastPausedFrame = Time.frameCount;
                if (marker != null) marker.gameObject.SetActive(false);
                return;
            }

            Vector2 key = ReadKeys();
            Vector3 move = Vector3.zero;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && cam != null)
            {
                Vector2 sp = mouse.position.ReadValue();
                // the click that closed a card or a panel arrives in the first frame the world is hers again (a quick click is down and up in one frame): it must not also walk her
                bool closedACard = Time.frameCount - lastPausedFrame <= 1;
                bool onUi = !closedACard && WorldInput.ClickIsOnUi != null && WorldInput.ClickIsOnUi(sp);
                bool used = !closedACard && !onUi && WorldInput.ClickUsed != null && WorldInput.ClickUsed(sp);
                if (!onUi && !used && !closedACard)
                {
                    Ray ray = cam.ScreenPointToRay(sp);
                    if (Ground.Raycast(ray, out float t))
                    {
                        SetTarget(ray.GetPoint(t));
                    }
                }
            }

            if (key.sqrMagnitude > 0.01f)
            {
                hasTarget = false;
                move = new Vector3(key.x, 0, key.y).normalized;
            }
            else if (hasTarget)
            {
                Vector3 to = target - transform.position; to.y = 0;
                // pass the waypoints (0.25 m is near enough to turn), then the target itself (0.12 m)
                while (pathIndex < path.Count - 1 && FlatDistance(transform.position, path[pathIndex]) < 0.25f) pathIndex++;
                if (to.magnitude < 0.12f) hasTarget = false;
                else
                {
                    Vector3 aim = (pathIndex < path.Count ? path[pathIndex] : target) - transform.position; aim.y = 0;
                    move = aim.sqrMagnitude > 0.0001f ? aim.normalized : to.normalized;
                }
            }

            Vector3 before = transform.position;
            Vector3 pos = before + move * (speed * Time.deltaTime);
            pos = Obstacle.PushOut(Clamp(pos), 0.35f);
            pos.y = 0;
            transform.position = pos;

            planarSpeed = (pos - before).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            AnimSafe.Float(animator, "Speed", planarSpeed);
            // Held up on the way (the crowd grew, a prop moved): plan again from here, a few times, then give up on it
            if (hasTarget && move.sqrMagnitude > 0 && planarSpeed < speed * 0.15f)
            {
                blockedFor += Time.deltaTime;
                if (blockedFor > 0.4f)
                {
                    blockedFor = 0f;
                    if (replans >= 3) hasTarget = false;
                    else { replans++; Plan(); }
                }
            }
            else if (planarSpeed > speed * 0.5f) blockedFor = 0f;

            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-14f * Time.deltaTime));
            }

            if (marker != null)
            {
                marker.gameObject.SetActive(hasTarget);
                if (hasTarget)
                {
                    marker.position = new Vector3(target.x, 0.03f, target.z);
                    float s = 0.7f + 0.08f * Mathf.Sin(Time.time * 6f);
                    marker.localScale = new Vector3(s, 0.02f, s);
                }
            }
        }

        static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        Vector3 Clamp(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -bounds, bounds);
            p.z = Mathf.Clamp(p.z, -bounds, bounds);
            p.y = 0;
            return p;
        }

        static Vector2 ReadKeys()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            float x = 0, z = 0;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) z -= 1;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) z += 1;
            return new Vector2(x, z);
        }
    }
}
