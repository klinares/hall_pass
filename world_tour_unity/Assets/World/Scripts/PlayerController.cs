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

        bool hasTarget;
        int lastPausedFrame = -10;
        float blockedFor, sidestepUntil;
        int sidesteps;
        Vector3 sidestep;
        Vector3 target;
        static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        /// <summary>Walks to a ground point as if it had been clicked (phase 8.1: click on a crew member, walk up to them).</summary>
        public void WalkTo(Vector3 point)
        {
            target = Obstacle.PushOut(Clamp(point), 0.35f);
            hasTarget = true; sidesteps = 0; blockedFor = 0f;
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
                        target = Obstacle.PushOut(Clamp(ray.GetPoint(t)), 0.35f);
                        hasTarget = true; sidesteps = 0; blockedFor = 0f;
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
                if (to.magnitude < 0.12f) hasTarget = false;
                else
                {
                    Vector3 aim = Obstacle.SteerAround(transform.position, target) - transform.position; aim.y = 0;
                    move = aim.sqrMagnitude > 0.0001f ? aim.normalized : to.normalized;
                    if (Time.time < sidestepUntil) move = sidestep;   // stepping out of a dead end (a deck side, a table) before trying the straight way again
                }
            }

            Vector3 before = transform.position;
            Vector3 pos = before + move * (speed * Time.deltaTime);
            pos = Obstacle.PushOut(Clamp(pos), 0.35f);
            pos.y = 0;
            transform.position = pos;

            planarSpeed = (pos - before).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            AnimSafe.Float(animator, "Speed", planarSpeed);
            // Walking into a blocker on the way to a click target: step out sideways (toward the freer side) a few times, then give up on it.
            if (hasTarget && move.sqrMagnitude > 0 && planarSpeed < speed * 0.15f)
            {
                blockedFor += Time.deltaTime;
                if (blockedFor > 0.25f)
                {
                    blockedFor = 0f;
                    if (sidesteps >= 8) hasTarget = false;
                    else
                    {
                        sidesteps++;
                        Vector3 left = new Vector3(-move.z, 0f, move.x), right = -left;
                        float l = (Obstacle.PushOut(Clamp(pos + left * 1.5f), 0.35f) - pos).magnitude, r = (Obstacle.PushOut(Clamp(pos + right * 1.5f), 0.35f) - pos).magnitude;
                        sidestep = l >= r ? left : right;
                        sidestepUntil = Time.time + 1.0f;
                    }
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
