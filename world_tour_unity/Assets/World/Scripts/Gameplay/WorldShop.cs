using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game;
using WorldTour.Game.Meta;
using WorldTour.Game.Prefs;
using WorldTour.Game.Save;
using WorldTour.Game.Show;
using WorldTour.World.Wardrobe;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// Phase 8.3: backstage in the world. Every place has a wardrobe trunk (with a gold star over it and a "wardrobe" pin); walk up and press E (or click it) and the
    /// backstage shop opens as the compact panel (<see cref="BackstageScreen.OpenCompact"/>, same rules and two-tap buy). What she has equipped is on her in every place:
    /// outfit and hair on the Star, her guitar in her hands during a stage show, riff's outfit on riff, the stage light colour on every spotlight. While the shop is open the
    /// item she has selected is tried on live: the camera comes in on the Star and riff (or looks at the stage for the lights), and closing the shop without buying puts
    /// the saved look back. WorldGame owns the modes; this class only asks it for the few things it needs (see the "wardrobe" region there).
    /// Log lines: WT_WARDROBE look=..., WT_WARDROBE_OPEN, WT_WARDROBE_CLOSE, WT_WARDROBE_TRUNK place= pos=. Test only (page URL has wt_test): ?wt_fans=N adds fans, ?wt_buy=id buys and
    /// wears an item, ?wt_shop=id opens the shop on that item at start, and WT_SHOP_TARGET trunk=x:y gives the trunk's screen position.
    /// </summary>
    public sealed class WorldShop : MonoBehaviour
    {
        public const float OpenRange = 2.7f;
        // the stage camera of the show (WorldGame.ComputeStageCamera, copied) for trying on stage lights
        const float StageBack = 7f, StageUp = 6.5f, StageAimUp = 3.0f, StageAimToward = 2.5f, StageFov = 57f;
        const float CloseDistance = 4.8f, CloseHeight = 1.5f, CloseFov = 38f;

        public static WorldShop Instance { get; private set; }

        WorldGame _game;
        WorldUi _ui;
        WardrobeRig _rig;
        GameObject _trunk;
        Vector3 _spot;
        bool _pendingOpen, _guitarLogged;
        string _placeId;

        // the open shop
        bool _open;
        enum View { None, Close, Stage }
        View _view = View.None;
        Coroutine _cam;
        Vector3 _savedStar, _savedDog, _savedCam;
        Quaternion _savedStarRot, _savedDogRot, _savedCamRot;
        float _savedFov;
        bool _dogWasOn;
        string _testSelect;
        float _nextTarget;

        public WardrobeRig Rig { get { return _rig; } }
        public bool IsOpen { get { return _open; } }
        public GameObject Trunk { get { return _trunk; } }

        static SaveBlob Blob { get { return Boot.Store != null ? Boot.Store.Blob : null; } }

        /// <summary>The open save (null before the game has booted), for the avatar try-on hook.</summary>
        public static SaveBlob CurrentSave { get { return Blob; } }

        public void Init(WorldGame game, WorldUi ui)
        {
            Instance = this;
            _game = game; _ui = ui;
            _rig = new WardrobeRig(gameObject);
            AvatarPreview.Rig = _rig; AvatarPreview.Save = () => Blob;
            var prev = WorldInput.ClickUsed;
            WorldInput.ClickUsed = p => OnClick(p) || (prev != null && prev(p));
            if (WorldGame.TestMode) TestHooks();
        }

        void OnDestroy() { if (Instance == this) Instance = null; AvatarEditorScreen.LookChanged -= OnAvatar; BackstageScreen.TryOnChanged -= OnTryOn; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        // ---------- a place loaded ----------

        /// <summary>After every place loads (WorldGame.Bind): put the saved look on, and set out the trunk.</summary>
        public void PlaceLoaded(string placeId)
        {
            _placeId = placeId;
            _open = false; _pendingOpen = false; _view = View.None;
            _rig.Bind();
            SaveBlob blob = Blob;
            if (blob != null) ApplyAvatar(AvatarRules.Look(blob));
            if (_trunk != null) Destroy(_trunk);
            _trunk = null;
            Transform spawn = _game.Spawn;
            if (spawn == null || _game.InInterior) return;   // 10B.1: no wardrobe trunk inside the boutique (its own mirror is the try-on)
            if (!PickSpot(placeId, spawn.position, out _spot)) { Debug.Log("WT_WARDROBE_TRUNK place=" + placeId + " none"); return; }
            _trunk = WardrobeTrunk.Build(_spot, WardrobeAssets.Load());
            if (_trunk != null)
            {
                // the front faces the camera (the follow camera looks toward +z)
                Debug.Log("WT_WARDROBE_TRUNK place=" + placeId + " pos=" + _spot.x.ToString("0.0", CultureInfo.InvariantCulture) + ":" + _spot.z.ToString("0.0", CultureInfo.InvariantCulture));
            }
        }

        void ApplyLook(WardrobeLook look)
        {
            _rig.Apply(look);
            Debug.Log("WT_WARDROBE look " + look);
        }

        /// <summary>Where each place's trunk stands: beside the spawn on the open ground, clear of every blocker, crew member, the stage and the exit. First free spot of the list wins.</summary>
        bool PickSpot(string placeId, Vector3 spawn, out Vector3 spot)
        {
            return PickSpot(spawn, Obstacle.IsBlocked, _game.CrewPositions(), _game.StagePos, _game.ExitPos, out spot, placeId);
        }

        /// <summary>The trunk's spot, from the blockers (a test for "is this ground point blocked, grown by this margin"), the crew, the stage and the exit. False when nothing near the spawn is free.</summary>
        public static bool PickSpot(Vector3 spawn, Func<Vector3, float, bool> blocked, IEnumerable<Vector3> crew, Vector3? stage, Vector3? exit, out Vector3 spot, string placeId = null)
        {
            float[] dxs = { 4.2f, 5.6f, 3.2f, 7.0f, 2.4f, 8.4f };
            float[] dzs = { -0.8f, 0.4f, -1.6f, 1.4f, -2.4f, 2.4f, 3.4f };
            var cands = new List<Vector3>();
            foreach (float dz in dzs)
                foreach (float dx in dxs)
                {
                    cands.Add(new Vector3(spawn.x + dx, 0f, spawn.z + dz));
                    cands.Add(new Vector3(spawn.x - dx, 0f, spawn.z + dz));
                }
            // round 8.4: a hand-picked spot first where the search put the trunk behind a foreground tree (rec) or inside the rope lane of the foyer (theater)
            Vector3 chosen;
            if (placeId != null && ChosenSpots.TryGetValue(placeId, out chosen)) cands.Insert(0, chosen);
            // first the tidy list beside the spawn; then (a crowded place such as the theater foyer) the nearest free ground on a half-metre grid, first with the same room, then with less
            for (int pass = 0; pass < 3; pass++)
            {
                float k = pass == 2 ? 0.6f : 1f;
                IEnumerable<Vector3> list = cands;
                if (pass > 0)
                {
                    var grid = new List<Vector3>();
                    for (float x = -11f; x <= 11f; x += 0.5f)
                        for (float z = -12f; z <= 11f; z += 0.5f) grid.Add(new Vector3(x, 0f, z));
                    grid.Sort((a, b) => Flat(a, spawn).CompareTo(Flat(b, spawn)));
                    list = grid;
                }
                foreach (Vector3 c in list)
                {
                    if (Mathf.Abs(c.x) > 11f || c.z < -12f || c.z > 12.5f) continue;
                    // the trunk, the ends of its rail and the walking room in front of it
                    if (blocked(c, 1.0f * k) || blocked(c + new Vector3(-1.1f, 0f, 0.72f), 0.6f * k) || blocked(c + new Vector3(1.1f, 0f, 0.72f), 0.6f * k) || blocked(c + new Vector3(0f, 0f, -1.9f), 0.5f * k)) continue;
                    if (Near(c, crew, 3.4f) || Near(c, stage, 5f) || Near(c, exit, 3.4f) || Flat(c, spawn) < 2.2f) continue;
                    if (pass < 2 && !SeenWell(c, spawn, crew)) continue;
                    spot = c;
                    return true;
                }
            }
            spot = Vector3.zero;
            return false;
        }

        // ---------- what the player sees from the spawn camera (8.4 fix round 2) ----------

        /// <summary>
        /// From the camera she starts with (FollowCamera's offset and tilt, 44 degree lens, 16:9), the trunk, its rack, its ring and its "wardrobe" label must not hide any crew member
        /// (the label is a flat overlay, so it hides them whatever the depth; the rack and trunk only when they are nearer the lens), and the ring must stay inside the frame (not at the very edge).
        /// A pure function of the numbers, so a test can ask it of any scene.
        /// </summary>
        public static bool SeenWell(Vector3 spot, Vector3 spawn, IEnumerable<Vector3> crew)
        {
            Vector3 camPos = spawn + new Vector3(0f, 6.6f, -11.4f);
            Quaternion rot = Quaternion.LookRotation(-new Vector3(0f, 6.6f, -11.4f), Vector3.up) * Quaternion.Euler(-7f, 0f, 0f);
            Matrix4x4 view = Matrix4x4.TRS(camPos, rot, Vector3.one).inverse;
            float th = Mathf.Tan(22f * Mathf.Deg2Rad), aspect = 16f / 9f;
            Vector3 P(Vector3 w) { Vector3 v = view.MultiplyPoint3x4(w); return new Vector3(v.x / (v.z * th * aspect), v.y / (v.z * th), v.z); }
            Vector3 g = new Vector3(spot.x, 0f, spot.z);
            // the ring on the ground: about 2.1 m round the trunk, well inside the frame at the sides and above the foot of the screen
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                Vector3 r = P(g + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2.1f);
                if (r.z <= 0.5f || Mathf.Abs(r.x) > 0.9f || r.y < -0.9f) return false;
            }
            Vector3 centre = P(g);
            float minX = 9f, maxX = -9f, minY = 9f, maxY = -9f;
            foreach (float x in new[] { -1.2f, 1.2f }) foreach (float y in new[] { 0f, 2.3f }) foreach (float z in new[] { -0.7f, 0.7f })
            {
                Vector3 q = P(g + new Vector3(x, y, z));
                minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x); minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y);
            }
            Vector3 pin = P(g + new Vector3(0f, 2.65f, 0.3f));
            float lx0 = pin.x - 0.115f, lx1 = pin.x + 0.115f, ly0 = pin.y, ly1 = pin.y + 0.13f;
            foreach (Vector3 cw in crew)
            {
                Vector3 feet = P(new Vector3(cw.x, 0f, cw.z)), head = P(new Vector3(cw.x, 2.3f, cw.z));
                if (feet.z <= 0.5f) continue;
                float hw = 0.4f / (feet.z * th * aspect);
                float cx0 = feet.x - hw, cx1 = feet.x + hw, cy0 = feet.y, cy1 = head.y;
                if (cx1 > lx0 && cx0 < lx1 && cy1 > ly0 && cy0 < ly1) return false;
                if (centre.z < feet.z + 0.3f && cx1 > minX && cx0 < maxX && cy1 > minY && cy0 < maxY) return false;
            }
            return true;
        }

        static readonly Dictionary<string, Vector3> ChosenSpots = new Dictionary<string, Vector3>
        {
            // round 8.4 fix round 2: west of the spawn in each of these, where the rack and its label hide no crew member from the spawn camera and the ring is wholly in frame (WorldShop.SeenWell, pinned by WardrobePlacesTests)
            { "records", new Vector3(-5.5f, 0f, -7.5f) },
            { "rec", new Vector3(-6.0f, 0f, -8.0f) },      // west of the spawn, on open paving at the court's edge
            { "theater", new Vector3(-5.5f, 0f, -12.0f) }, // west of the spawn on the kerb side of the foyer (WalkableAreaTests: the old east spot at 4.5 closed the way to the exit)
            { "festival", new Vector3(3.0f, 0f, -11.0f) }, // beside the boardwalk, in front of the crew on the east
            { "arena", new Vector3(-5.5f, 0f, -10.5f) },   // west of the spawn, off the far-right stage table the crew stand behind
        };
        public static bool TryChosenSpot(string placeId, out Vector3 spot) { return ChosenSpots.TryGetValue(placeId, out spot); }

        static bool Near(Vector3 c, IEnumerable<Vector3> list, float r) { foreach (Vector3 p in list) if (Flat(c, p) < r) return true; return false; }
        static bool Near(Vector3 c, Vector3? p, float r) { return p.HasValue && Flat(c, p.Value) < r; }
        static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        // ---------- near the trunk ----------

        void Update()
        {
            if (_game == null) return;
            // the guitar is in her hands for a stage show
            if (!_open && _rig != null)
            {
                bool on = _game.StageShowOn;
                _rig.ShowGuitar(on);
                if (on != _guitarLogged) { _guitarLogged = on; Debug.Log("WT_WARDROBE_GUITAR on=" + (on ? "true" : "false")); }
            }
            if (_trunk == null) return;
            if (WorldGame.TestMode && Time.unscaledTime >= _nextTarget && _game.Cam != null)
            {
                _nextTarget = Time.unscaledTime + 0.4f;
                Vector3 s = _game.Cam.WorldToScreenPoint(_spot + Vector3.up * 0.6f);
                Debug.Log("WT_SHOP_TARGET trunk=" + Mathf.RoundToInt(s.x).ToString(CultureInfo.InvariantCulture) + ":" + Mathf.RoundToInt(Screen.height - s.y).ToString(CultureInfo.InvariantCulture)
                    + " d=" + Flat(_game.Player.transform.position, _spot).ToString("0.0", CultureInfo.InvariantCulture) + " free=" + _game.IsFree + " open=" + _open);
            }
            if (!_game.IsFree || _open) return;
            Vector3 p = _game.Player.transform.position;
            float d = Flat(p, _spot);
            bool near = d <= OpenRange;
            Camera cam = _game.Cam;
            if (cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(_spot + new Vector3(0f, 2.65f, 0.3f));
                bool onScreen = sp.z > 0f && sp.x > 60f && sp.x < Screen.width - 60f && sp.y > 60f && sp.y < Screen.height - 60f;
                _ui.SetPin("wardrobe", near ? "wardrobe - e open" : "wardrobe", sp, onScreen);
            }
            Keyboard k = Keyboard.current;
            bool eKey = k != null && k.eKey.wasPressedThisFrame;
            if ((near && eKey) || (_pendingOpen && near))
            {
                _pendingOpen = false;
                Open(null);
            }
            else if (_pendingOpen && !_game.Player.HasTarget) _pendingOpen = false;
        }

        bool OnClick(Vector2 screen)
        {
            if (_trunk == null || _open || _game == null || !_game.IsFree || _game.Cam == null) return false;
            Vector3 sp = _game.Cam.WorldToScreenPoint(_spot + Vector3.up * 0.9f);
            if (sp.z <= 0f || Vector2.Distance(screen, sp) > Screen.height * 0.11f) return false;
            Vector3 p = _game.Player.transform.position;
            if (Flat(p, _spot) <= OpenRange - 0.2f) { Open(null); return true; }
            // stand in front of it (the camera side), then open
            Vector3 stand = _spot + new Vector3(0f, 0f, -1.9f);
            _pendingOpen = true;
            _game.Player.WalkTo(stand);
            return true;
        }

        // ---------- the shop ----------

        /// <summary>Opens the compact shop, on <paramref name="select"/> (an item id) or on her outfit.</summary>
        public void Open(string select)
        {
            if (_open || _game == null || Blob == null) return;
            _open = true;
            Debug.Log("WT_WARDROBE_OPEN place=" + _placeId + " balance=" + Blob.Fans.Balance + " coins=" + Blob.Fans.Coins);
            _game.Player.StopWalking();
            Save();
            _view = View.None;
            AvatarEditorScreen.LookChanged += OnAvatar;
            _game.OpenHostedPanel(() => AvatarEditorScreen.Open(_ui.Stack, select));
        }

        void Save()
        {
            Transform star = _game.Player.transform;
            _savedStar = star.position; _savedStarRot = star.rotation;
            DogFollower dog = _game.Dog;
            if (dog != null) { _savedDog = dog.transform.position; _savedDogRot = dog.transform.rotation; _dogWasOn = dog.enabled; }
            Camera cam = _game.Cam;
            if (cam != null) { _savedCam = cam.transform.position; _savedCamRot = cam.transform.rotation; _savedFov = cam.fieldOfView; }
        }

        /// <summary>The shop's selection changed (or it closed, with null): show it on the Star, riff or the stage, or put the saved look back.</summary>
        void OnTryOn(string id)
        {
            SaveBlob blob = Blob;
            if (blob == null || !_open) return;
            if (id == null)
            {
                BackstageScreen.TryOnChanged -= OnTryOn;
                _open = false;
                ApplyLook(WardrobeLook.Of(blob));   // leaving without buying: the saved look
                _rig.ShowGuitar(false);
                Debug.Log("WT_WARDROBE_CLOSE look " + WardrobeLook.Of(blob));
                StartCoroutine(Leave());
                return;
            }
            ShopItem it = Shop.ById(id);
            WardrobeLook look = WardrobeLook.Of(blob, id);
            ApplyLook(look);
            bool stage = it != null && it.Slot == Shop.Lights;
            _rig.ShowGuitar(stage || (it != null && it.Slot == Shop.Guitar));
            SetView(stage ? View.Stage : View.Close);
        }

        /// <summary>Phase 10B.1: the wardrobe editor changed what she is trying on (look, and the slot being edited), or closed (null).</summary>
        void OnAvatar(AvatarLook look, string slot)
        {
            SaveBlob blob = Blob;
            if (blob == null || !_open) return;
            if (look == null)
            {
                AvatarEditorScreen.LookChanged -= OnAvatar;
                _open = false;
                AvatarLook saved = AvatarRules.Look(blob);
                ApplyAvatar(saved);   // leaving: whatever she saved is what shows
                _rig.ShowGuitar(false);
                Debug.Log("WT_WARDROBE_CLOSE look " + saved);
                StartCoroutine(Leave());
                return;
            }
            ApplyAvatar(look);
            bool stage = slot == AvatarCatalogue.Lights;
            _rig.ShowGuitar(stage || slot == AvatarCatalogue.Guitar);
            SetView(stage ? View.Stage : View.Close);
        }

        void ApplyAvatar(AvatarLook look)
        {
            _rig.Apply(look);
            Debug.Log("WT_WARDROBE look " + look);
        }

        float NoseYaw() { return _rig != null && _rig.Dog != null ? _rig.Dog.NoseYaw : 0f; }

        void SetView(View v)
        {
            if (v == _view) return;
            View from = _view;
            _view = v;
            if (_cam != null) StopCoroutine(_cam);
            _cam = StartCoroutine(MoveTo(v, from == View.None));
        }

        IEnumerator MoveTo(View v, bool first)
        {
            Camera cam = _game.Cam;
            FollowCamera follow = _game.Follow;
            if (cam == null) yield break;
            if (follow != null) follow.frozen = true;
            Transform star = _game.Player.transform;
            DogFollower dog = _game.Dog;
            if (dog != null) dog.enabled = false;
            Vector3 pos; Quaternion rot; float fov;
            if (v == View.Stage && _game.StagePos.HasValue)
            {
                if (follow != null) follow.ShowAllOccluders();
                Vector3 stage = _game.StagePos.Value, crowd = _game.CrowdPos ?? stage + Vector3.back * 6f;
                Vector3 dir = StagePose(stage, crowd, out pos, out rot);
                fov = StageFov;
                _game.Player.Place(new Vector3(stage.x, 0f, stage.z));
                star.rotation = Quaternion.LookRotation(dir);
                if (dog != null) { Vector3 dp; Quaternion dr; DogPose(stage, dir, NoseYaw(), out dp, out dr); dog.transform.SetPositionAndRotation(dp, dr); }
            }
            else
            {
                Vector3 s = _savedStar; s.y = 0f;
                _game.Player.Place(s);
                Vector3 toCam = _savedCam - s; toCam.y = 0f;
                Vector3 dir = toCam.sqrMagnitude > 0.01f ? toCam.normalized : Vector3.back;
                star.rotation = Quaternion.LookRotation(dir);
                if (dog != null) { Vector3 dp; Quaternion dr; DogPose(s, dir, NoseYaw(), out dp, out dr); dog.transform.SetPositionAndRotation(dp, dr); }
                ClosePose(s, dir, out pos, out rot);
                fov = CloseFov;
            }
            Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            float dur = GameSettings.ReducedMotion ? 0.01f : (first ? 0.55f : 0.7f), t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                cam.transform.position = Vector3.Lerp(p0, pos, e);
                cam.transform.rotation = Quaternion.Slerp(r0, rot, e);
                cam.fieldOfView = Mathf.Lerp(f0, fov, e);
                yield return null;
            }
            cam.transform.position = pos; cam.transform.rotation = rot; cam.fieldOfView = fov;
            _cam = null;
        }

        /// <summary>Riff beside her (on the viewer's right), his nose turned toward the camera and a little toward her. <paramref name="noseYaw"/> is <see cref="DogWardrobe.NoseYaw"/>.</summary>
        public static void DogPose(Vector3 star, Vector3 towardCamera, float noseYaw, out Vector3 pos, out Quaternion rot)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -towardCamera);
            pos = star + right * 1.15f + towardCamera * 0.35f; pos.y = 0f;
            rot = Quaternion.LookRotation(Quaternion.Euler(0f, 28f, 0f) * towardCamera, Vector3.up) * Quaternion.Euler(0f, noseYaw, 0f);
        }

        /// <summary>The close-up on the Star and riff: in front of her along the camera's side, a little above, aimed between the two (field of view <see cref="CloseFov"/>).</summary>
        public static void ClosePose(Vector3 star, Vector3 towardCamera, out Vector3 pos, out Quaternion rot)
        {
            Vector3 mid = star + Vector3.Cross(Vector3.up, -towardCamera) * 0.5f;
            pos = star + towardCamera * CloseDistance + Vector3.up * CloseHeight;
            rot = Quaternion.LookRotation((mid + Vector3.up * 0.9f) - pos, Vector3.up);
        }

        /// <summary>The show's stage camera (field of view <see cref="StageFov"/>); returns the direction from the stage toward the crowd.</summary>
        public static Vector3 StagePose(Vector3 stage, Vector3 crowd, out Vector3 pos, out Quaternion rot)
        {
            Vector3 dir = crowd - stage; dir.y = 0; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back;
            pos = crowd + dir * StageBack + Vector3.up * StageUp;
            rot = Quaternion.LookRotation((stage + Vector3.up * StageAimUp + dir * StageAimToward) - pos, Vector3.up);
            return dir;
        }

        public const float CloseFovValue = CloseFov, StageFovValue = StageFov;

        /// <summary>The shop closed: the camera, the Star and riff go back as they were, and the follow camera takes over again.</summary>
        IEnumerator Leave()
        {
            if (_cam != null) { StopCoroutine(_cam); _cam = null; }
            Camera cam = _game.Cam;
            FollowCamera follow = _game.Follow;
            View was = _view;
            _view = View.None;
            if (was != View.None)
            {
                _game.Player.Place(new Vector3(_savedStar.x, 0f, _savedStar.z));
                _game.Player.transform.rotation = _savedStarRot;
                DogFollower dog = _game.Dog;
                if (dog != null) { dog.transform.position = _savedDog; dog.transform.rotation = _savedDogRot; dog.enabled = true; }
            }
            if (cam != null && follow != null && was != View.None)
            {
                Vector3 from = cam.transform.position; Quaternion rotFrom = cam.transform.rotation; float fovFrom = cam.fieldOfView;
                float dur = GameSettings.ReducedMotion ? 0.01f : 0.5f, t = 0f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                    cam.transform.position = Vector3.Lerp(from, follow.FollowPosition, e);
                    cam.transform.rotation = Quaternion.Slerp(rotFrom, follow.FollowRotation, e);
                    cam.fieldOfView = Mathf.Lerp(fovFrom, _savedFov, e);
                    yield return null;
                }
                cam.fieldOfView = _savedFov;
            }
            if (follow != null) { follow.ResetVelocity(); follow.frozen = false; }
            DogFollower d2 = _game.Dog;
            if (d2 != null) d2.enabled = true;
        }

        // ---------- the browser test ----------

        static string QueryParam(string name)
        {
            string url = Application.absoluteURL ?? "";
            int q = url.IndexOf('?');
            if (q < 0) return null;
            foreach (string part in url.Substring(q + 1).Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0 && string.Equals(part.Substring(0, eq), name, StringComparison.OrdinalIgnoreCase)) return Uri.UnescapeDataString(part.Substring(eq + 1));
            }
            return null;
        }

        void TestHooks()
        {
            SaveBlob blob = Blob;
            if (blob == null) return;
            long n;
            if (long.TryParse(QueryParam("wt_fans") ?? "", NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0)
            {
                blob.Fans.Balance += n; blob.Fans.Lifetime += n; blob.Fans.Coins += n / CoinRules.FansPerGrantCoin;   // test only: the fans hook also gives the matching spending money, as the old fans prices did
                Boot.Store.Save();
                Debug.Log("WT_WARDROBE_TEST fans +" + n);
            }
            if (long.TryParse(QueryParam("wt_coins") ?? "", NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0)
            {
                blob.Fans.Coins += n;   // test only: spending money for the store tests
                Boot.Store.Save();
                Debug.Log("WT_WARDROBE_TEST coins +" + n);
            }
            string buy = QueryParam("wt_buy");
            if (!string.IsNullOrEmpty(buy))
            {
                foreach (string id in buy.Split(','))
                {
                    BuyResult r = MetaRules.Buy(blob, id);
                    bool worn = (r.Ok || MetaRules.Owns(blob, id)) && MetaRules.Equip(blob, id);
                    Debug.Log("WT_WARDROBE_TEST buy " + id + " result=" + r + " worn=" + worn + " coins=" + blob.Fans.Coins);
                }
                Boot.Store.Save();
            }
            string shop = QueryParam("wt_shop");
            if (!string.IsNullOrEmpty(shop)) { _testSelect = shop == "1" ? "" : shop; StartCoroutine(OpenSoon()); }
        }

        IEnumerator OpenSoon()
        {
            while (_game == null || !_game.IsFree || _rig == null || _rig.Star == null) yield return null;
            yield return new WaitForSecondsRealtime(1.0f);
            Open(string.IsNullOrEmpty(_testSelect) ? null : _testSelect);
        }
    }
}
