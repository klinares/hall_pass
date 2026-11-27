using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;
using WorldTour.Core.Tour;
using WorldTour.Game;
using WorldTour.Game.Audio;
using WorldTour.Game.Data;
using WorldTour.Game.Flow;
using WorldTour.Game.Modes;
using WorldTour.Game.Prefs;
using WorldTour.Game.Save;
using WorldTour.Game.Show;
using WorldTour.Game.Speech;
using WorldTour.Game.Ticket;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;
using WorldTour.World.Audio;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// Phase 8.1: the game on top of the 3D world. It starts itself when a scene has a <see cref="PlayerController"/>, opens the same
    /// save as the 2D game (<see cref="Boot.BootForWorld"/>), and binds the scene by name: the empties PlayerSpawn, Crew_1 to Crew_3,
    /// StageSpot, CrowdArea and ExitSpot. Then:
    /// talk (E, Space or a click on a crew member, within 2.5 m) gives one line and a gig offer; a gig opens the 2D mode screen as a
    /// panel over the world, 3 to 5 questions; the stage spot offers the full ten-song show over a stage camera, with a crowd that grows
    /// with her fans, then the ticket; the exit spot opens the tour map card: go to the next place when it is unlocked, or back to an earlier one.
    /// Nothing here can lose fans or progress, and nothing runs on a timer that costs her anything.
    /// Log lines for the browser test (the ones marked test only print when the page URL has wt_test): WT_WORLD_GAME_READY, WT_WORLD_TALK,
    /// WT_GIG_START, WT_GIG_DONE, WT_WORLD_STAGE_OFFER, WT_WORLD_SHOW_START, WT_WORLD_TRAVEL, WT_WORLD_TARGETS (test only).
    /// </summary>
    public sealed class WorldGame : MonoBehaviour
    {
        public const string ReadyLog = "WT_WORLD_GAME_READY";
        public const float TalkRange = 2.5f, StageRadius = 1.4f, ExitRadius = 1.5f;
        /// <summary>From this far a crew member's name shows under their marker, so she can see whom to walk to.</summary>
        public const float TagRange = 4f;
        const float HintSeconds = 6f, HintFade = 0.6f;   // the walking hint goes at her first step, or after six seconds (it sits over the path she walks down)
        const string WalkHint = "walk with the arrow keys or click the ground";

        enum Mode { Boot, Free, Dialog, Panel, Travel }
        enum Run { None, Gig, Show }

        sealed class Crew
        {
            public string Name;      // Crew_1
            public int Index;
            public Transform T;
            public Animator Anim;
            public string Mode = "";
            /// <summary>Every gig this crew member can offer, in order; Mode is Pool[PoolIndex] ("another gig" steps through them).</summary>
            public string[] Pool = new string[0];
            public int PoolIndex;
        }

        public static WorldGame Instance { get; private set; }
        /// <summary>The page was opened with ?wt_test, so the browser test gets screen positions to click.</summary>
        public static bool TestMode { get; private set; }

        // scene
        PlayerController _player;
        Camera _cam;
        FollowCamera _follow;
        Transform _spawn, _stage, _crowdArea, _exit;
        readonly List<Crew> _crew = new List<Crew>();
        CrowdField _crowd;
        StageLights _lights;
        PanelLensShift _lens;
        WorldAudio _audio;
        DogFollower _dog;
        string _placeId = Venues.First;
        /// <summary>Phase 10B.1: "boutique" while a small interior is open, else "". Its WorldPlace names the street it opens off in placeId.</summary>
        string _interior = "";

        // game
        WorldUi _ui;
        WorldGameplayAssets _assets;
        WorldState _world = new WorldState();
        Mode _mode = Mode.Boot;
        Run _run = Run.None;
        int _modeFrame;
        Crew _pending, _talking;
        bool _stageArmed = true, _exitArmed = true;
        List<ModeInfo> _stageModes = new List<ModeInfo>();
        int _stageModeIndex;
        bool _soundUnlocked;
        bool _hintOn;
        Vector3 _hintFrom;
        float _hintUntil;
        float _nextSave, _nextTargets;
        Vector3 _lastSavedPos = new Vector3(9999, 0, 9999);
        Coroutine _cameraRoutine;
        Quaternion _stageRot;
        Vector3 _stagePos;
        readonly Rng _rng = new Rng(DateTime.UtcNow.Ticks ^ Environment.TickCount);

        // ---------- start ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            if (UnityEngine.Object.FindFirstObjectByType<PlayerController>() == null) return;
            var go = new GameObject("WorldGame");
            DontDestroyOnLoad(go);
            go.AddComponent<WorldGame>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; TestMode = false; }

        void Awake()
        {
            Instance = this;
            string url = Application.absoluteURL ?? "";
            TestMode = url.IndexOf("wt_test", StringComparison.OrdinalIgnoreCase) >= 0;
            FollowCamera.LogFades = TestMode;
        }

        IEnumerator Start()
        {
            _assets = WorldGameplayAssets.Load();
            yield return Boot.BootForWorld();
            SaveStore store = Boot.Store;
            if (store != null) GameSettings.Bind(store); else GameSettings.Apply();
            Sfx.Prepare();
            _audio = WorldAudio.Attach(gameObject);

            _ui = WorldUi.Create(_assets);
            _ui.AddHud(store);
            gameObject.AddComponent<PlayTimer>();
            ThemeSkin.Attach(gameObject, _ui.Stack);
            gameObject.AddComponent<FocusNavigator>().Init(_ui.Stack, _ui.Hud);
            _ui.Stack.TopChanged += OnTopChanged;
            WorldInput.ClickIsOnUi = _ => _ui != null && _ui.PointerOverUi();
            WorldInput.ClickUsed = OnWorldClick;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _shop = gameObject.AddComponent<WorldShop>();   // phase 8.3: the wardrobe trunk and the backstage shop (see the wardrobe region below)
            _shop.Init(this, _ui);
            if (TestMode) gameObject.AddComponent<HitchProbe>();   // test only: says what a long frame was made of (WT_HITCH)
            _boutique = gameObject.AddComponent<BoutiqueGame>();   // phase 10B.1: the boutique door, the shop inside and its cards
            _boutique.Init(this, _ui);

            Bind();
            SetMode(Mode.Free);
            Debug.Log(ReadyLog + " place=" + _placeId + " crew=" + string.Join(",", _crew.Select(c => c.Mode))
                + " fans=" + Fans() + " crowd=" + (_crowd != null ? _crowd.Count : 0));
            // test hook (only with ?wt_test): ?wt_goto=<venue id> travels straight to that place once the game is ready, so the browser tests can visit every place
            string go = TestMode ? QueryParam("wt_goto") : null;
            if (go == "boutique" && Application.CanStreamedLevelBeLoaded(BoutiqueScene))
            {
                Debug.Log("WT_WORLD_GOTO place=boutique");
                StartCoroutine(PassDoor(BoutiqueScene, null));
            }
            else if (!string.IsNullOrEmpty(go) && Venues.Exists(go) && go != _placeId && Application.CanStreamedLevelBeLoaded(WorldPlace.SceneName(go)))
            {
                Debug.Log("WT_WORLD_GOTO place=" + go);
                StartCoroutine(Travel(Venues.ById(go), true));
            }
            else if (!string.IsNullOrEmpty(go) && go == _placeId)
            {
                _world.Place = _placeId; _world.HasPosition = false;   // the hook asked for the place the game opens in: nothing to travel to
            }
            else if (string.IsNullOrEmpty(go) && _world.Place != null && _world.Place != _placeId && Venues.Exists(_world.Place)
                     && Application.CanStreamedLevelBeLoaded(WorldPlace.SceneName(_world.Place)))
            {
                // a reload: the game always opens in the coffee shop, so go back to where the save says she was (and where she stood) before anything is saved
                Debug.Log("WT_WORLD_RESUME place=" + _world.Place + " pos=" + _world.HasPosition);
                StartCoroutine(Travel(Venues.ById(_world.Place), true, true));
            }
        }

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

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_ui != null && _ui.Stack != null) _ui.Stack.TopChanged -= OnTopChanged;
            if (WorldInput.ClickUsed == (Func<Vector2, bool>)OnWorldClick) WorldInput.ClickUsed = null;
            WorldInput.Paused = false;
            ShowLayout.Compact = false;
            if (Instance == this) Instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (_mode == Mode.Boot || _travelLoading) return;
            Bind();
        }

        // ---------- binding the scene by name ----------

        void Bind()
        {
            _player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (_player == null) { Debug.LogWarning("[world] no PlayerController in the scene"); return; }
            _cam = _player.cam != null ? _player.cam : Camera.main;
            _follow = _cam != null ? _cam.GetComponent<FollowCamera>() : null;
            if (_cam != null && _cam.GetComponent<AudioListener>() == null && UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
                _cam.gameObject.AddComponent<AudioListener>();
            _lens = null;
            if (_cam != null)
            {
                _lens = _cam.GetComponent<PanelLensShift>();
                if (_lens == null) _lens = _cam.gameObject.AddComponent<PanelLensShift>();
            }
            _dog = UnityEngine.Object.FindFirstObjectByType<DogFollower>();
            if (_lights == null) _lights = gameObject.AddComponent<StageLights>();
            _lights.Find();
            _spawn = Find("PlayerSpawn");
            _stage = Find("StageSpot");
            _crowdArea = Find("CrowdArea");
            _exit = Find("ExitSpot");
            _crew.Clear();
            for (int i = 1; i <= 3; i++)
            {
                Transform t = Find("Crew_" + i);
                if (t == null) continue;
                _crew.Add(new Crew { Name = "Crew_" + i, Index = i - 1, T = t, Anim = t.GetComponentInChildren<Animator>() });
            }
            _placeId = WorldPlace.Resolve(SceneManager.GetActiveScene().name);
            _interior = WorldPlace.InteriorOf();
            PropMotion.Reduced = GameSettings.ReducedMotion;

            // which mode each crew member offers
            Week week = CurrentWeek();
            List<string> math = MathModes();
            List<string> spelling = SpellingModes(week);
            string[][] offers = GigPlan.Offers(_placeId, _crew.Count, math, spelling);
            for (int i = 0; i < _crew.Count; i++)
            {
                _crew[i].Pool = offers[i];
                _crew[i].PoolIndex = 0;
                _crew[i].Mode = offers[i].Length > 0 ? offers[i][0] : "";
            }
            _stageModes = ModeList.All(Config()).Where(m => math.Contains(m.Id) || spelling.Contains(m.Id)).ToList();
            // tonight's set starts on the unit she is working on (snack table) when it is switched on, else on the first crew member's gig
            string first = GigPlan.UnitMode(math) ?? (offers.Length > 0 && offers[0].Length > 0 ? offers[0][0] : "");
            _stageModeIndex = Mathf.Max(0, _stageModes.FindIndex(m => m.Id == first));

            // where she starts: the saved spot in this place, else PlayerSpawn
            SaveStore store = Boot.Store;
            _world = store != null ? WorldSave.Read(store.Blob) : new WorldState();
            // (a saved spot that is now inside a prop is not used: she starts at the spawn instead)
            bool savedSpot = _interior.Length == 0 && _world.Place == _placeId && _world.HasPosition && !Obstacle.IsBlocked(new Vector3(_world.X, 0, _world.Z), 0.2f);
            if (savedSpot) _player.Place(new Vector3(_world.X, 0, _world.Z));
            else if (_spawn != null) { _player.Place(_spawn.position); _player.transform.rotation = _spawn.rotation; }
            if (_follow != null) { _follow.frozen = false; _follow.transform.position = _follow.FollowPosition; _follow.ResetVelocity(); }
            _hintOn = !savedSpot;   // first visit: say how to walk until she has
            _hintFrom = _player.transform.position;
            _hintUntil = Time.time + HintSeconds;
            // the crowd is somewhere she walks round, never into: the way to the stage stays clear of figures
            if (_crowdArea != null && _crowdArea.GetComponent<KeepOutZone>() == null) _crowdArea.gameObject.AddComponent<KeepOutZone>();

            // the crowd
            if (_crowd != null) Destroy(_crowd);
            if (_crowdArea != null)
            {
                _crowd = gameObject.AddComponent<CrowdField>();
                Vector3 face = _stage != null ? _stage.position : _crowdArea.position + Vector3.forward;
                _crowd.Setup(_crowdArea, face, _assets, GigPlan.PlaceIndex(_placeId) * 31 + 7, _placeId);
                _crowd.SetFans(Fans());
            }
            _stageArmed = _stage == null || FlatDist(_player.transform.position, _stage.position) > StageRadius + 0.6f;
            _exitArmed = _exit == null || FlatDist(_player.transform.position, _exit.position) > ExitRadius + 0.6f;
            if (_audio != null) { _audio.Bind(_interior.Length > 0 ? _interior : _placeId, _player, _crowdArea); _audio.CrowdFill = _crowd != null ? _crowd.Fill : 0f; }
            if (_shop != null) _shop.PlaceLoaded(_placeId);
            if (_boutique != null) _boutique.PlaceLoaded(_placeId, _interior);
        }

        // ---------- wardrobe (phase 8.3): the few things WorldShop asks of this class ----------

        WorldShop _shop;
        public bool IsFree { get { return _mode == Mode.Free && Time.frameCount > _modeFrame; } }
        /// <summary>A stage show (or its ticket) is on: the Star plays her guitar.</summary>
        public bool StageShowOn { get { return _mode == Mode.Panel && _run == Run.Show; } }
        public Camera Cam { get { return _cam; } }
        public FollowCamera Follow { get { return _follow; } }
        public PlayerController Player { get { return _player; } }
        public DogFollower Dog { get { return _dog; } }
        public Transform Spawn { get { return _spawn; } }
        public Vector3? StagePos { get { return _stage != null ? _stage.position : (Vector3?)null; } }
        public Vector3? CrowdPos { get { return _crowdArea != null ? _crowdArea.position : (Vector3?)null; } }
        public Vector3? ExitPos { get { return _exit != null ? _exit.position : (Vector3?)null; } }
        public IEnumerable<Vector3> CrewPositions() { foreach (Crew c in _crew) yield return c.T.position; }

        /// <summary>Opens a screen in the docked panel (the backstage shop); popping back to the root ends it as it ends a gig.</summary>
        public void OpenHostedPanel(Action open)
        {
            _run = Run.None;
            SetMode(Mode.Panel);
            DockPanel();
            open();
        }

        static Transform Find(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        // ---------- what can be offered ----------

        static MathConfig Config() { return DataLoader.Math != null ? DataLoader.Math.Config : null; }

        static List<string> MathModes()
        {
            return ModeList.All(Config()).Where(m => m.Kind == ModeInfo.Math).Select(m => m.Id).ToList();
        }

        static List<string> SpellingModes(Week week)
        {
            if (week == null) return new List<string>();
            return ModeList.All(null).Where(m => m.Kind == ModeInfo.Spelling && ModeRules.IsAvailable(m.Id, week)).Select(m => m.Id).ToList();
        }

        static Week CurrentWeek()
        {
            var weeks = DataLoader.Weeks != null && DataLoader.Weeks.File != null && DataLoader.Weeks.File.weeks != null
                ? DataLoader.Weeks.File.weeks : new Week[0];
            return WeekRules.DefaultWeek(weeks, LocalClock.TodayIso());
        }

        static long Fans() { return Boot.Store != null ? Boot.Store.Blob.Fans.Lifetime : 0; }

        // ---------- the loop ----------

        void SetMode(Mode m)
        {
            _mode = m;
            if (m == Mode.Free) RestoreOfferView(true);
            _modeFrame = Time.frameCount;
            if (TestMode) HitchProbe.Mode = m.ToString();
            WorldInput.Paused = m != Mode.Free;
            if (m != Mode.Free && _ui != null) { _ui.HideTag(); _ui.HidePins(); _ui.HideHint(); }
            if (TestMode) Debug.Log("WT_WORLD_MODE mode=" + m + " run=" + _run);
        }

        void Update()
        {
            if (_mode == Mode.Boot || _player == null) return;
            UnlockSoundOnFirstPress();
            SyncAudio();
            if (_mode == Mode.Free && Time.frameCount > _modeFrame) UpdateFree();
            else if (_mode == Mode.Dialog && _talking != null && _ui.DialogOpen && _cam != null) _ui.MoveDialogAnchor(HeadScreen(_talking.T));
            else if (_mode == Mode.Panel && _ui.PanelCompact && _cam != null) _ui.PlaceDogBubble(DogScreen(), _cam.WorldToScreenPoint(_player.transform.position), _cam.WorldToScreenPoint(_player.transform.position + Vector3.up * 1.9f), _run == Run.Show ? 130f : 0f);
            if (TestMode && Time.unscaledTime >= _nextTargets) { _nextTargets = Time.unscaledTime + 0.4f; LogTargets(); }
            if (TestMode && _mode == Mode.Free && Time.unscaledTime >= _nextRoutes) { _nextRoutes = Time.unscaledTime + 1f; LogRoutes(); }
        }

        /// <summary>Tells the world sound how full the crowd is and whether a panel or the show is up (the bed ducks, the murmur holds).</summary>
        void SyncAudio()
        {
            if (_audio == null) return;
            _audio.CrowdFill = _crowd != null ? _crowd.Fill : 0f;
            _audio.PanelOpen = _mode == Mode.Panel;
            _audio.ShowOn = _mode == Mode.Panel && _run == Run.Show;
        }

        void UnlockSoundOnFirstPress()
        {
            if (_soundUnlocked) return;
            Mouse m = Mouse.current; Keyboard k = Keyboard.current;
            if ((m != null && m.leftButton.wasPressedThisFrame) || (k != null && k.anyKey.wasPressedThisFrame))
            {
                _soundUnlocked = true;
                Sfx.Unlock();
                ReadAloud.Unlock();   // 9.4: the browser speaks only after a gesture; logs WT_SPEECH_UNLOCKED
            }
        }

        void UpdateFree()
        {
            Vector3 p = _player.transform.position;
            Keyboard k = Keyboard.current;

            Crew near = Nearest(p, TalkRange);
            UpdateMarkers(p);
            bool talkKey = k != null && (k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame);
            if (talkKey && near != null) { StartTalk(near); return; }

            // a click on a crew member: she walks up, then they talk
            if (_pending != null)
            {
                if (FlatDist(p, _pending.T.position) <= TalkRange - 0.2f) { StartTalk(_pending); return; }
                if (!_player.HasTarget)
                {
                    // she stopped a little short (the way round was long, or the spot was closed off): close enough still talks
                    Crew c = _pending; _pending = null;
                    if (FlatDist(p, c.T.position) <= TalkRange + 1.0f) { StartTalk(c); return; }
                }
            }

            if (_stage != null)
            {
                float d = FlatDist(p, _stage.position);
                if (_stageArmed && d < StageRadius) { OfferStage(); return; }
                if (!_stageArmed && d > StageRadius + 1.2f) _stageArmed = true;
            }
            if (_exit != null)
            {
                float d = FlatDist(p, _exit.position);
                if (_exitArmed && d < ExitRadius) { OfferExit(); return; }
                if (!_exitArmed && d > ExitRadius + 1.3f) _exitArmed = true;
            }

            // keep where she stands in the save, now and then (never during a gig or show: those save themselves)
            if (Time.unscaledTime >= _nextSave)
            {
                _nextSave = Time.unscaledTime + 6f;
                if ((p - _lastSavedPos).sqrMagnitude > 0.16f) SaveWorld();
            }
        }

        /// <summary>The name tag under the nearest crew marker (from 4 m, with "e talk" inside talking range), the "the stage" and "next stop" pins, and the first-visit hint.</summary>
        void UpdateMarkers(Vector3 p)
        {
            if (_cam == null) return;
            Crew seen = Nearest(p, TagRange);
            if (seen != null)
            {
                string name = CrewLines.Role(seen.Index);
                bool close = FlatDist(p, seen.T.position) <= TalkRange;
                _ui.ShowTag(_cam.WorldToScreenPoint(seen.T.position + Vector3.up * 2.6f), close ? name + " - e talk" : name);
            }
            else _ui.HideTag();

            // a place can hang the pin lower with a PinAnchor child on StageSpot (the club's sign sits where the usual pin would)
            Transform anchor = _stage != null ? _stage.Find("PinAnchor") : null;
            if (anchor != null) PinOver("stage", "the stage", anchor, Vector3.zero, p);
            else PinOver("stage", "the stage", _stage, new Vector3(0f, 3.0f, 0f), p);
            PinOver("exit", "next stop", _exit, new Vector3(1.7f, 3.55f, 0.3f), p);

            if (_hintOn)
            {
                if (FlatDist(p, _hintFrom) > 0.8f || Time.time >= _hintUntil) { _hintOn = false; _ui.HideHint(); }
                else _ui.ShowHint(WalkHint, Mathf.Clamp01((_hintUntil - Time.time) / HintFade));
            }
        }

        void PinOver(string key, string text, Transform spot, Vector3 lift, Vector3 player)
        {
            if (spot == null) { _ui.SetPin(key, text, Vector2.zero, false); return; }
            Vector3 sp = _cam.WorldToScreenPoint(spot.position + lift);
            bool onScreen = sp.z > 0f && sp.x > 60f && sp.x < Screen.width - 60f && sp.y > 60f && sp.y < Screen.height - 60f;
            _ui.SetPin(key, text, sp, onScreen && FlatDist(player, spot.position) > 3.5f);
        }

        Crew Nearest(Vector3 p, float range)
        {
            Crew best = null; float bd = range;
            foreach (Crew c in _crew)
            {
                float d = FlatDist(p, c.T.position);
                if (d <= bd) { bd = d; best = c; }
            }
            return best;
        }

        static float FlatDist(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        Vector2 HeadScreen(Transform t)
        {
            return _cam.WorldToScreenPoint(t.position + Vector3.up * 2.1f);
        }

        /// <summary>Where riff's bubble hangs: over the 3D riff (or over her when he is not in the scene).</summary>
        Vector2 DogScreen()
        {
            Transform t = _dog != null ? _dog.transform : _player.transform;
            return _cam.WorldToScreenPoint(t.position + Vector3.up * (_dog != null ? 0.9f : 2.1f));
        }

        // ---------- the panel: docked beside the world for a gig or a show song, full for the ticket ----------

        /// <summary>The compact panel for a gig or a show: the street or the stage stays visible and lit beside it (the lens shift centres the view in the open part).</summary>
        void DockPanel()
        {
            ShowLayout.Compact = true;
            _ui.OpenCompactPanel();
            SetLens(_ui.OpenLensShift);
        }

        /// <summary>The whole-screen panel (the ticket): the 2D screens as the 2D game draws them.</summary>
        void FullPanel(float scale)
        {
            ShowLayout.Compact = false;
            _ui.OpenPanel(scale, false);
            SetLens(0f);
        }

        void SetLens(float shift)
        {
            if (_lens == null) return;
            _lens.Instant = GameSettings.ReducedMotion;
            _lens.Target = shift;
        }

        // ---------- clicking a crew member ----------

        bool OnWorldClick(Vector2 screen)
        {
            if (_mode != Mode.Free || _cam == null) return false;
            // the crew member whose body (feet to head, on the screen) is nearest the click, within about 46 px
            Crew hit = null; float best = Screen.height * 0.06f;
            foreach (Crew c in _crew)
            {
                Vector3 feet = _cam.WorldToScreenPoint(c.T.position), head = _cam.WorldToScreenPoint(c.T.position + Vector3.up * 2f);
                if (feet.z <= 0 || head.z <= 0) continue;
                float d = DistanceToSegment(screen, feet, head);
                if (d < best) { best = d; hit = c; }
            }
            if (hit == null) return false;
            Vector3 p = _player.transform.position;
            if (FlatDist(p, hit.T.position) <= TalkRange - 0.2f) { StartTalk(hit); return true; }
            // stand a little short of them, on her side
            Vector3 to = p - hit.T.position; to.y = 0;
            Vector3 stand = hit.T.position + (to.sqrMagnitude > 0.01f ? to.normalized : Vector3.back) * 1.7f;
            _pending = hit;
            _player.WalkTo(stand);
            return true;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------- talking and gigs ----------

        void StartTalk(Crew c)
        {
            _pending = null;
            _talking = c;
            SetMode(Mode.Dialog);
            _ui.HideTag();
            _player.StopWalking();
            _player.Face(c.T.position);
            FaceEachOther(c);
            AnimSafe.Trigger(c.Anim, "Talk");
            AnimSafe.Bool(_player.animator, "Listen", true);

            string key = _placeId + "/" + c.Name;
            if (!_world.MetCrew.Contains(key)) { _world.MetCrew.Add(key); SaveWorld(); }
            c.PoolIndex = 0;
            c.Mode = c.Pool.Length > 0 ? c.Pool[0] : "";
            Debug.Log("WT_WORLD_TALK crew=" + c.Name + " mode=" + c.Mode);
            ShowTalkCard(c);
        }

        /// <summary>The crew member's offer: play it, hear another of their gigs, or not now (Tab moves between the buttons, Enter or Space presses the focused one).</summary>
        void ShowTalkCard(Crew c)
        {
            ModeInfo info = string.IsNullOrEmpty(c.Mode) ? null : ModeList.ById(c.Mode, Config());
            var buttons = new List<DialogButton>();
            string line, sub = null;
            if (info != null)
            {
                line = CrewLines.For(info.Id);
                sub = "quick gig: " + info.Name;
                buttons.Add(new DialogButton { Label = "play", OnClick = () => StartGig(c, info) });
                if (c.Pool.Length > 1)
                    buttons.Add(new DialogButton
                    {
                        Label = "another gig", Ghost = true, Closes = false,
                        OnClick = () => { c.PoolIndex = (c.PoolIndex + 1) % c.Pool.Length; c.Mode = c.Pool[c.PoolIndex]; ShowTalkCard(c); }
                    });
                buttons.Add(new DialogButton { Label = "not now", Ghost = true, OnClick = () => Decline(c), OnEscape = EndTalk });   // Escape just closes the talk: no second card
            }
            else
            {
                line = CrewLines.Generic;
                buttons.Add(new DialogButton { Label = "ok", OnClick = EndTalk });
            }
            _ui.OpenDialog(CrewLines.Role(c.Index), line, sub, buttons, HeadScreen(c.T));
        }

        void FaceEachOther(Crew c)
        {
            Vector3 to = _player.transform.position - c.T.position; to.y = 0;
            if (to.sqrMagnitude > 0.01f) c.T.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        void Decline(Crew c)
        {
            var ok = new List<DialogButton> { new DialogButton { Label = "ok", OnClick = EndTalk } };
            _ui.OpenDialog(CrewLines.Role(c.Index), CrewLines.Decline, null, ok, HeadScreen(c.T));
        }

        void EndTalk()
        {
            _talking = null;
            AnimSafe.Bool(_player.animator, "Listen", false);
            SetMode(Mode.Free);
        }

        void StartGig(Crew c, ModeInfo info)
        {
            AnimSafe.Bool(_player.animator, "Listen", false);
            int n = GigPlan.Length(_rng.Next);
            _run = Run.Gig;
            SetMode(Mode.Panel);
            DockPanel();
            Debug.Log("WT_GIG_START crew=" + c.Name + " mode=" + info.Id + " n=" + n);
            OpenShow(() => ShowScreen.Open(_ui.Stack, new ShowRequest
            {
                ModeId = info.Id, VenueId = _placeId, Week = CurrentWeek(), Length = n, Gig = true, Store = Boot.Store
            }));
        }

        // ---------- the stage ----------

        void OfferStage()
        {
            if (_stageModes.Count == 0) { _stageArmed = false; return; }
            SetMode(Mode.Dialog);
            _talking = null;
            _player.StopWalking();
            _ui.HideTag();
            Debug.Log("WT_WORLD_STAGE_OFFER mode=" + _stageModes[_stageModeIndex].Id);
            // round 8.4: the arena's deck is 13 m from the follow camera, so while the offer is open the lens comes nearer and she is drawn 1.3x, as on the show's stage
            if (_placeId == "arena" && _follow != null && !_offerView)
            {
                _offerView = true; _offerOffset = _follow.offset;
                _follow.offset = new Vector3(0f, 6.0f, -8.4f);
                _player.transform.localScale = Vector3.one * StageStarScale;
            }
            ShowStageOffer();
        }

        bool _offerView;
        Vector3 _offerOffset;
        void RestoreOfferView(bool scaleToo)
        {
            if (!_offerView) return;
            _offerView = false;
            if (_follow != null) _follow.offset = _offerOffset;
            if (scaleToo && _player != null) _player.transform.localScale = Vector3.one;
        }

        void ShowStageOffer()
        {
            ModeInfo m = _stageModes[_stageModeIndex];
            var buttons = new List<DialogButton>
            {
                new DialogButton { Label = "play the show", OnClick = StartShow },
                new DialogButton { Label = "change set", Ghost = true, Closes = false, OnClick = () =>
                    {
                        _stageModeIndex = (_stageModeIndex + 1) % _stageModes.Count;
                        ShowStageOffer();
                    } },
                new DialogButton { Label = "not now", Ghost = true, OnClick = () => { _stageArmed = false; SetMode(Mode.Free); } },
            };
            _ui.OpenDialog("the crowd", CrewLines.Stage, "tonight: " + m.Name + ", 10 songs", buttons, HeadScreen(_player.transform));
        }

        void StartShow()
        {
            ModeInfo m = _stageModes[_stageModeIndex];
            _run = Run.Show;
            SetMode(Mode.Panel);
            Debug.Log("WT_WORLD_SHOW_START mode=" + m.Id);
            if (_cameraRoutine != null) StopCoroutine(_cameraRoutine);
            _cameraRoutine = StartCoroutine(StageIntro(m));
        }

        IEnumerator StageIntro(ModeInfo m)
        {
            if (_crowd != null) _crowd.SetFans(Fans());
            ComputeStageCamera();
            RestoreOfferView(false);
            SetLens(_ui.OpenLensShift);   // the view slides left with the camera move, so the stage lands in the part the panel leaves open
            if (_follow != null) { _follow.frozen = true; _follow.ShowAllOccluders(); }
            Vector3 camFrom = _cam.transform.position; Quaternion rotFrom = _cam.transform.rotation;
            _fovBefore = _cam.fieldOfView;
            Vector3 starFrom = _player.transform.position;
            Vector3 spot = _stage.position; spot.y = 0;
            float dur = GameSettings.ReducedMotion ? 0.01f : 0.9f, t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                _cam.transform.position = Vector3.Lerp(camFrom, _stagePos, e);
                _cam.transform.rotation = Quaternion.Slerp(rotFrom, _stageRot, e);
                _cam.fieldOfView = Mathf.Lerp(_fovBefore, StageFov, e);
                _player.transform.localScale = Vector3.one * Mathf.Lerp(1f, StageStarScale, e);
                _player.transform.position = Vector3.Lerp(starFrom, spot, e);
                FaceCrowd();
                yield return null;
            }
            _cam.transform.position = _stagePos; _cam.transform.rotation = _stageRot; _cam.fieldOfView = StageFov;
            _player.transform.localScale = Vector3.one * StageStarScale;
            _player.Place(spot);
            FaceCrowd();
            _ui.CloseDialog();
            DockPanel();
            OpenShow(() => ShowScreen.Open(_ui.Stack, new ShowRequest { ModeId = m.Id, VenueId = _placeId, Week = CurrentWeek(), Store = Boot.Store }));
            _cameraRoutine = null;
        }

        void FaceCrowd()
        {
            if (_crowdArea != null) _player.Face(_crowdArea.position);
        }

        /// <summary>A fixed camera behind the crowd, looking at the Star from a little above and behind the front rows. The panel docks to the right, so the lens shift (PanelLensShift)
        /// puts this view in the open left half: the whole lit stage, the Star on the deck and the front of the crowd. Chosen from renders at 1366x768 (the 1920x1080 canvas is the same 16:9 picture), phase 8.1 stage show.</summary>
        const float StageBack = 6f, StageUp = 5.4f, StageAimUp = 2.8f, StageAimToward = 2.5f, StageFov = 47f;
        /// <summary>Round 8.4: she is drawn this much bigger on the stage (the view is also closer), so the Star reads from the back of the crowd.</summary>
        const float StageStarScale = 1.3f;
        const float ArenaStageBack = 1.0f, ArenaStageUp = 7.0f;
        float _fovBefore = 44f;

        void ComputeStageCamera()
        {
            Vector3 stage = _stage.position;
            Vector3 crowd = _crowdArea != null ? _crowdArea.position : stage + Vector3.back * 6f;
            Vector3 dir = crowd - stage; dir.y = 0;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back;
            float back = StageBack, up = StageUp;
            if (_placeId == "arena") { back = ArenaStageBack; up = ArenaStageUp; }   // round 8.4: the arena's crowd stands far from the deck; from behind it she was a speck, so the lens sits nearer and higher
            _stagePos = crowd + dir * back + Vector3.up * up;
            _stageRot = Quaternion.LookRotation((stage + Vector3.up * StageAimUp + dir * StageAimToward) - _stagePos, Vector3.up);
        }

        IEnumerator ReturnCamera()
        {
            if (_follow == null || _cam == null) { if (_player != null) _player.transform.localScale = Vector3.one; yield break; }
            Vector3 from = _cam.transform.position; Quaternion rotFrom = _cam.transform.rotation;
            Vector3 to = _follow.FollowPosition; Quaternion rotTo = _follow.FollowRotation;
            float dur = GameSettings.ReducedMotion ? 0.01f : 0.7f, t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                _cam.transform.position = Vector3.Lerp(from, _follow.FollowPosition, e);
                _cam.transform.rotation = Quaternion.Slerp(rotFrom, rotTo, e);
                _cam.fieldOfView = Mathf.Lerp(StageFov, _fovBefore, e);
                _player.transform.localScale = Vector3.one * Mathf.Lerp(StageStarScale, 1f, e);
                yield return null;
            }
            _cam.transform.rotation = rotTo;
            _cam.fieldOfView = _fovBefore;
            _player.transform.localScale = Vector3.one;
            _follow.ResetVelocity();
            _follow.frozen = false;
            _cameraRoutine = null;
        }

        // ---------- running a 2D screen over the world ----------

        long _coinsAtStart;

        ShowScreen OpenShow(Func<ShowScreen> open)
        {
            _coinsAtStart = Boot.Store != null ? CoinRules.Coins(Boot.Store.Blob) : 0;
            Action<ShowScreen> started = s =>
            {
                if (s == null) return;
                s.Engine.Raised += OnEngineEvent;
                s.Finished += OnShowFinished;
            };
            ShowScreen.Started += started;
            try { return DogHosts.OpenWithDog(_ui.Stack, open); }
            catch (Exception e)
            {
                Debug.LogWarning("[world] could not open the show: " + e.Message);
                if (_mode == Mode.Panel) EndPanel();
                return null;
            }
            finally { ShowScreen.Started -= started; }
        }

        void OnEngineEvent(ShowEvent e)
        {
            switch (e.Kind)
            {
                case ShowEventKind.Answered:
                    // every right answer: Star cheers; at the stage the crowd hops and a burst goes up
                    AnimSafe.Trigger(_player.animator, "Cheer");
                    if (_talking != null) AnimSafe.Trigger(_talking.Anim, "Cheer");
                    if (_run == Run.Show) WorldAudio.Play(Cue.Cheer);   // applause, as loud as the crowd is full
                    if (_run == Run.Show && _lights != null) _lights.Flare();
                    if (_run == Run.Show && _crowd != null) _crowd.Cheer(_player.transform.position + (_crowdArea != null ? (_crowdArea.position - _player.transform.position).normalized * 2f : Vector3.zero));
                    break;
                case ShowEventKind.FansAwarded:
                    if (_crowd != null) _crowd.SetFans(Fans());   // the save already holds these fans: ProgressRules.Attach heard the event first
                    break;
            }
        }

        void OnShowFinished(ShowResult r)
        {
            if (r.Request.Gig) FinishGig(r); else FinishShow(r);
        }

        void FinishGig(ShowResult r)
        {
            ShowSummary s = r.Summary;
            Debug.Log("WT_GIG_DONE fans=" + s.Fans + " n=" + s.N + " first=" + s.First);
            SaveWorld();   // 8.4: flush at the end of every gig, not only on the timer
            WorldAudio.Play(Cue.GigDone);
            string fans = "+" + s.Fans.ToString("#,##0", CultureInfo.InvariantCulture) + " fans";
            long coins = Boot.Store != null ? CoinRules.Coins(Boot.Store.Blob) - _coinsAtStart : 0;
            if (coins > 0) fans += " and +" + coins.ToString(CultureInfo.InvariantCulture) + " coins";
            string sub = s.First + " of " + s.N + " right on the first try";
            var ok = new List<DialogButton> { new DialogButton { Label = "keep exploring", OnClick = () => _ui.Stack.PopToRoot() } };
            _ui.OpenDialog("gig done", "nice set. " + fans + ".", sub, ok, null);
        }

        void FinishShow(ShowResult r)
        {
            SaveWorld();   // 8.4: flush at the end of every show, before the ticket
            var mode = ModeList.ById(r.Request.ModeId, null);
            FullPanel(0.96f);   // the ticket stays a full screen
            TicketScreen t = TicketScreen.Open(_ui.Stack, new TicketData
            {
                Summary = r.Summary,
                Venue = Venues.ById(r.Request.VenueId),
                WeekLabel = r.Week != null ? r.Week.label : null,
                ModeName = mode != null ? mode.Name : null,
                NewStickers = r.Stickers,
                Unlocks = r.Unlocks,
                OnePage = true
            });
            t.StartEncore += () => { DockPanel(); OpenShow(() => ShowScreen.OpenEncore(_ui.Stack, r, replaceTop: true)); };
            t.PlayAgain += () => { DockPanel(); OpenShow(() => ShowScreen.OpenAgain(_ui.Stack, r, replaceTop: true)); };
            t.BackToMap += () => _ui.Stack.PopToRoot();
        }

        /// <summary>Quit, "back to the map" or "keep exploring": the 2D screens are gone and the world is hers again.</summary>
        void OnTopChanged(GameScreen top)
        {
            if (_ui == null || top != _ui.RootScreen || _mode != Mode.Panel) return;
            EndPanel();
        }

        void EndPanel()
        {
            _ui.CloseDialog();
            _ui.ClosePanel();
            ShowLayout.Compact = false;
            SetLens(0f);
            bool wasShow = _run == Run.Show;
            _run = Run.None;
            _talking = null;
            if (wasShow)
            {
                if (_cameraRoutine != null) StopCoroutine(_cameraRoutine);
                _cameraRoutine = StartCoroutine(ReturnCamera());
                _stageArmed = false;
            }
            SaveWorld();
            List<string> fresh = ProgressRules.UnseenUnlocks(Boot.Store != null ? Boot.Store.Blob : SaveBlob.Defaults(LocalClock.TodayIso()))
                .Where(id => id != Venues.First).ToList();
            if (fresh.Count > 0) ShowUnlock(fresh);
            else SetMode(Mode.Free);
        }

        void ShowUnlock(List<string> ids)
        {
            SaveBlob blob = Boot.Store.Blob;
            Venue last = Venues.ById(ids[ids.Count - 1]);
            foreach (string id in ids) ProgressRules.MarkSeen(blob, id);
            Boot.Store.Save();
            SetMode(Mode.Dialog);
            Debug.Log("WT_WORLD_UNLOCK venue=" + last.Id);
            var ok = new List<DialogButton> { new DialogButton { Label = "nice", OnClick = () => SetMode(Mode.Free) } };
            _ui.OpenDialog("new stop unlocked!", last.Name, "your fans opened it. head to the exit.", ok, null, true);
            WorldAudio.Play(Cue.Unlock);
            _ui.Celebrate();
        }

        // ---------- travel ----------

        bool _travelLoading;
        int _backIndex;

        /// <summary>
        /// The tour map card at the exit: "next stop: record store" with a go button when it is open (the fans needed when it is not), a button
        /// back to an earlier unlocked stop ("other stop" steps through them), or stay. Tab and Enter work as on every card.
        /// </summary>
        void OfferExit()
        {
            _exitArmed = false;
            SetMode(Mode.Dialog);
            _talking = null;
            _player.StopWalking();
            _ui.HideTag();
            SaveBlob blob = Boot.Store != null ? Boot.Store.Blob : SaveBlob.Defaults(LocalClock.TodayIso());
            ExitOffer offer = Journey.Offer(_placeId, blob.Fans.Lifetime, id => ProgressRules.Unlocked(blob, id),
                id => Application.CanStreamedLevelBeLoaded(WorldPlace.SceneName(id)));
            _backIndex = 0;
            Debug.Log("WT_WORLD_MAP place=" + _placeId + " next=" + (offer.Next != null ? offer.Next.Id : "none") + " open=" + offer.NextOpen
                + " back=" + string.Join(",", offer.Earlier.Select(v => v.Id)) + " detail=" + offer.Detail);
            ShowExitCard(offer);
        }

        void ShowExitCard(ExitOffer offer)
        {
            var buttons = new List<DialogButton>();
            if (offer.NextOpen)
                buttons.Add(new DialogButton { Label = "go: " + offer.Next.Short, OnClick = () => StartCoroutine(Travel(offer.Next, true)) });
            if (offer.Earlier.Count > 0)
            {
                Venue back = offer.Earlier[_backIndex % offer.Earlier.Count];
                buttons.Add(new DialogButton { Label = Journey.BackLine(back), Ghost = offer.NextOpen, OnClick = () => StartCoroutine(Travel(back, false)) });
                if (offer.Earlier.Count > 1)
                    buttons.Add(new DialogButton
                    {
                        Label = "other stop", Ghost = true, Closes = false,
                        OnClick = () => { _backIndex = (_backIndex + 1) % offer.Earlier.Count; ShowExitCard(offer); }
                    });
            }
            buttons.Add(new DialogButton { Label = "stay here", Ghost = buttons.Count > 0, OnClick = () => SetMode(Mode.Free) });
            _ui.OpenDialog("tour map", offer.Line, string.IsNullOrEmpty(offer.Detail) ? null : offer.Detail, buttons, null);
        }

        /// <summary>Longest the bus takes to cross, seconds (the whole moment, with the fade back in, is under 2 s plus a load that runs behind it).</summary>
        const float BusSeconds = 1.4f;

        /// <summary>
        /// The travel moment: the street dims, a card says "next stop: ..." and a tour bus drives across under it (1.4 s, parked and still under reduce motion), while the
        /// next place loads behind it; then the fade lifts and she is at its PlayerSpawn. About 1.7 s in all.
        /// </summary>
        IEnumerator Travel(Venue dest, bool forward, bool resume = false)
        {
            _exitArmed = false;
            SetMode(Mode.Travel);
            _ui.HideTag();
            string scene = WorldPlace.SceneName(dest.Id);
            string line = forward ? Journey.NextStopLine(dest) : Journey.BackLine(dest);
            Debug.Log("WT_WORLD_TRAVEL next=" + dest.Id + " forward=" + forward + " built=" + Application.CanStreamedLevelBeLoaded(scene));

            float seconds = GameSettings.ReducedMotion ? 1.0f : BusSeconds;
            _ui.FadeTo(0.82f, 0.25f);
            _ui.ShowTravel("on the road", line, dest.Name);
            WorldAudio.Play(Cue.Whoosh);
            _world.Place = dest.Id; if (!resume) _world.HasPosition = false;   // a resume keeps the saved spot; Bind reads it on arrival
            if (Boot.Store != null) { WorldSave.Write(Boot.Store.Blob, _world); Boot.Store.Save(); }
            _travelLoading = true;
            AsyncOperation op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (op != null) op.allowSceneActivation = false;
            float t = 0f;
            while (t < seconds || (op != null && op.progress < 0.9f))
            {
                t += Time.unscaledDeltaTime;
                _ui.TravelProgress(t / seconds);
                yield return null;
            }
            if (op != null) { op.allowSceneActivation = true; while (!op.isDone) yield return null; }
            // 8.4: the place she left unloads fully (meshes, textures, materials, clips) before the next one is drawn, so GPU memory
            // does not climb place over place on a laptop with little video memory
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();
            _travelLoading = false;
            Bind();
            Debug.Log("WT_WORLD_ARRIVED place=" + _placeId + " crew=" + string.Join(",", _crew.Select(c => c.Mode)) + " crowd=" + (_crowd != null ? _crowd.Count : 0));
            _ui.CloseDialog();
            _ui.HideTravel();
            _ui.FadeTo(0f, 0.3f);
            yield return new WaitForSecondsRealtime(0.3f);
            SetMode(Mode.Free);
        }

        // ---------- doors and interiors (phase 10B.1): the few things BoutiqueGame asks of this class ----------

        BoutiqueGame _boutique;
        /// <summary>The boutique's scene (Assets/World/Scenes/Interior_Boutique.unity). Only one scene is loaded at a time, as for a travel.</summary>
        public const string BoutiqueScene = "Interior_Boutique";
        public bool InInterior { get { return _interior.Length > 0; } }
        public string PlaceId { get { return _placeId; } }

        /// <summary>A card or a store is up: she stops, the world waits (the same pause a crew member's talk gives).</summary>
        public void BeginCard()
        {
            _pending = null; _talking = null;
            if (_player != null) _player.StopWalking();
            SetMode(Mode.Dialog);
            _ui.HideTag();
        }

        /// <summary>The card is done: she can walk again.</summary>
        public void EndCard() { SetMode(Mode.Free); }

        /// <summary>
        /// Through a door: a quick fade, the other scene loads (only one is ever loaded), and she is at its PlayerSpawn. Going in from the street,
        /// <paramref name="streetSpot"/> is where the save keeps her (just outside the door), so a reload inside puts her back on the street there.
        /// Going back out passes null: the save already holds the spot.
        /// </summary>
        public void GoThroughDoor(string scene, Vector3? streetSpot)
        {
            if (_mode == Mode.Travel || _mode == Mode.Boot) return;
            StartCoroutine(PassDoor(scene, streetSpot));
        }

        /// <summary>Back out of an interior to the street it opens off.</summary>
        public void LeaveInterior() { GoThroughDoor(WorldPlace.SceneName(_placeId), null); }

        IEnumerator PassDoor(string scene, Vector3? streetSpot)
        {
            _exitArmed = false;
            SetMode(Mode.Travel);
            _ui.HideTag();
            _ui.CloseDialog();
            if (streetSpot.HasValue && Boot.Store != null)
            {
                _world.Place = _placeId; _world.HasPosition = true; _world.X = streetSpot.Value.x; _world.Z = streetSpot.Value.z;
                WorldSave.Write(Boot.Store.Blob, _world);
                Boot.Store.Save();
            }
            Debug.Log("WT_WORLD_DOOR scene=" + scene + " from=" + (_interior.Length > 0 ? _interior : _placeId));
            _ui.FadeTo(1f, 0.25f);
            _travelLoading = true;
            AsyncOperation op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (op != null) op.allowSceneActivation = false;
            float t = 0f, least = GameSettings.ReducedMotion ? 0.05f : 0.3f;
            while (t < least || (op != null && op.progress < 0.9f)) { t += Time.unscaledDeltaTime; yield return null; }
            if (op != null) { op.allowSceneActivation = true; while (!op.isDone) yield return null; }
            yield return Resources.UnloadUnusedAssets();   // as a travel does: the scene she left unloads fully before the next is drawn
            GC.Collect();
            _travelLoading = false;
            Bind();
            Debug.Log("WT_WORLD_ARRIVED place=" + _placeId + " interior=" + (_interior.Length > 0 ? _interior : "none") + " crew=" + string.Join(",", _crew.Select(c => c.Mode)));
            _ui.FadeTo(0f, 0.3f);
            yield return new WaitForSecondsRealtime(0.3f);
            SetMode(Mode.Free);
        }

        // ---------- saving where she is ----------

        void SaveWorld()
        {
            if (Boot.Store == null || _player == null) return;
            if (_interior.Length > 0) { Boot.Store.Save(); return; }   // inside the boutique the save keeps her spot on the street, at the door
            _world.Place = _placeId;
            _world.HasPosition = true;
            Vector3 p = _player.transform.position;
            _world.X = p.x; _world.Z = p.z;
            _lastSavedPos = p;
            WorldSave.Write(Boot.Store.Blob, _world);
            Boot.Store.Save();
        }

        // ---------- the audio probe (phase 10A; only with ?wt_test) ----------

        /// <summary>Test hook: SendMessage("WorldGame","AudioProbeStart","label") starts a fresh level window (see AudioMeter) with the crowd forced to its loudest.</summary>
        public void AudioProbeStart(string label)
        {
            if (!TestMode) return;
            Sfx.Unlock();
            if (_audio != null) { _audio.ProbeLoudest = true; _audio.WarmCueClips(); }
            AudioMeter.Begin(label);
        }

        /// <summary>Test hook: plays every one-off sound once, a second or so apart (the guitar twice, with its gap between).</summary>
        public void AudioProbeCues()
        {
            if (TestMode) StartCoroutine(ProbeCues());
        }

        IEnumerator ProbeCues()
        {
            var gap = new WaitForSecondsRealtime(1.2f);
            Sfx.Play(SfxId.Tap); yield return gap;
            Sfx.Play(SfxId.Pop); yield return gap;
            Sfx.Play(SfxId.Peel); yield return gap;
            Sfx.Play(SfxId.Thud); yield return gap;
            Sfx.Play(SfxId.Tick); yield return gap;
            WorldAudio.Play(Cue.CardOpen); yield return gap;
            WorldAudio.Play(Cue.CardClose); yield return gap;
            WorldAudio.Play(Cue.Whoosh); yield return gap;
            WorldAudio.Play(Cue.GigDone); yield return gap;
            WorldAudio.Play(Cue.Unlock); yield return gap;
            WorldAudio.Play(Cue.Cheer); yield return gap;
            yield return gap;
            Sfx.Crowd(1.0); yield return new WaitForSecondsRealtime(4f);
            for (int k = 0; k < 4; k++) { Sfx.Play(SfxId.Strum); yield return new WaitForSecondsRealtime(7f); }
            Sfx.Play(SfxId.StrumBig); yield return new WaitForSecondsRealtime(3f);
            Debug.Log("WT_AUDIO_PROBE_CUES_DONE");
        }

        /// <summary>Test hook: logs the peak and RMS (dBFS) of every source since AudioProbeStart.</summary>
        public void AudioProbeReport()
        {
            if (!TestMode) return;
            foreach (string line in AudioMeter.Report()) Debug.Log(line);
            Debug.Log("WT_AUDIO_PROBE_REPORT_DONE");
        }

        /// <summary>Test hook (only with ?wt_test): the browser memory test travels place to place in one session by SendMessage("WorldGame","TestTravel","<venue id>").</summary>
        public void TestTravel(string id)
        {
            if (!TestMode || _mode != Mode.Free || !Venues.Exists(id) || !Application.CanStreamedLevelBeLoaded(WorldPlace.SceneName(id))) return;
            StartCoroutine(Travel(Venues.ById(id), true));
        }

        /// <summary>Test hook (only with ?wt_test), for the memory test: logs the live object counts and the managed and native memory ("WT_MEM tag=.. meshes=.. materials=.. textures=.. mono=.. native=..", MB).</summary>
        public void TestMem(string tag)
        {
            if (!TestMode) return;
            long mb = 1048576;
            Debug.Log("WT_MEM tag=" + tag
                + " meshes=" + Resources.FindObjectsOfTypeAll<Mesh>().Length
                + " materials=" + Resources.FindObjectsOfTypeAll<Material>().Length
                + " textures=" + Resources.FindObjectsOfTypeAll<Texture>().Length
                + " objects=" + Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Length
                + " mono=" + (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / mb)
                + " monoHeap=" + (UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / mb)
                + " native=" + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / mb)
                + " reserved=" + (UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / mb));
        }

        /// <summary>Test hook (only with ?wt_test), for the memory test: "in" goes through the boutique door, "out" leaves it, "browse" tries 12 items on at the mirror and closes the store.</summary>
        public void TestBoutique(string cmd)
        {
            if (!TestMode || _mode != Mode.Free || !Application.CanStreamedLevelBeLoaded(BoutiqueScene)) return;
            if (cmd == "in" && _interior.Length == 0) GoThroughDoor(BoutiqueScene, null);
            else if (cmd == "out" && _interior.Length > 0) LeaveInterior();
            else if (cmd == "browse" && _interior == "boutique" && _boutique != null) _boutique.TestBrowse(12);
        }

        /// <summary>Called from the page (SendMessage) the moment the WebGL context is lost: write the save before she reloads.</summary>
        public void FlushSaveFromJs()
        {
            Debug.Log("WT_SAVE_FLUSH_JS");
            if (_player != null) SaveWorld();
            else if (Boot.Store != null) Boot.Store.Save();
        }

        void OnApplicationFocus(bool focused) { if (!focused && _mode == Mode.Free) SaveWorld(); }
        void OnApplicationPause(bool paused) { if (paused && _mode == Mode.Free) SaveWorld(); }

        // ---------- the browser test ----------

        float _nextRoutes;

        /// <summary>Test only: the way (the path finder's waypoints, last one the goal) from where she stands to each crew member's talking spot, the stage spot and the exit, in world metres, so the keyboard test can walk it with the keys.</summary>
        void LogRoutes()
        {
            if (_player == null) return;
            var sb = new System.Text.StringBuilder("WT_WORLD_ROUTE");
            Vector3 from = _player.transform.position;
            void Route(string key, Vector3 goal)
            {
                goal.y = 0f;
                goal = Obstacle.PushOut(goal, PlayerController.PlanMargin + 0.05f);
                bool reached;
                List<Vector3> path = PathGrid.Find(from, goal, _player.bounds, p => Obstacle.IsBlocked(p, PlayerController.PlanMargin), out reached);
                sb.Append(" ").Append(key).Append("=").Append(reached ? "ok" : "short").Append(";");
                for (int i = 0; i < path.Count; i++)
                    sb.Append(i > 0 ? "," : "").Append(path[i].x.ToString("0.0", CultureInfo.InvariantCulture)).Append(":").Append(path[i].z.ToString("0.0", CultureInfo.InvariantCulture));
                // where the mouse test clicks to walk that way: a point on the path about 7 m ahead that is on the screen (nearer when it is not)
                if (_cam == null || path.Count == 0) return;
                float want = 7f;
                Vector3 click = path[path.Count - 1], sp = Vector3.zero;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    float left = want; Vector3 at = from; click = path[path.Count - 1];
                    for (int i = 0; i < path.Count; i++)
                    {
                        float seg = Vector3.Distance(at, path[i]);
                        if (seg >= left) { click = at + (path[i] - at).normalized * left; break; }
                        left -= seg; at = path[i];
                    }
                    sp = _cam.WorldToScreenPoint(click);
                    if (sp.z > 0f && sp.x > 80f && sp.x < Screen.width - 80f && sp.y > 130f && sp.y < Screen.height - 90f) break;
                    want *= 0.6f;
                }
                sb.Append(" click_").Append(key).Append("=").Append(Mathf.RoundToInt(sp.x).ToString(CultureInfo.InvariantCulture)).Append(":").Append(Mathf.RoundToInt(Screen.height - sp.y).ToString(CultureInfo.InvariantCulture));
            }
            foreach (Crew c in _crew)
            {
                Vector3 to = from - c.T.position; to.y = 0;
                Route(c.Name.ToLowerInvariant().Replace("_", ""), c.T.position + (to.sqrMagnitude > 0.01f ? to.normalized : Vector3.back) * 1.7f);
            }
            if (_stage != null) Route("stage", _stage.position);
            if (_exit != null) Route("exit", _exit.position);
            Debug.Log(sb.ToString());
        }

        void LogTargets()
        {
            if (_cam == null || _mode == Mode.Boot) return;
            var sb = new System.Text.StringBuilder("WT_WORLD_TARGETS");
            string P(Vector3 world)
            {
                Vector3 s = _cam.WorldToScreenPoint(world);
                return Mathf.RoundToInt(s.x).ToString(CultureInfo.InvariantCulture) + ":" + Mathf.RoundToInt(Screen.height - s.y).ToString(CultureInfo.InvariantCulture);
            }
            foreach (Crew c in _crew) sb.Append(" ").Append(c.Name.ToLowerInvariant().Replace("_", "")).Append("=").Append(P(c.T.position + Vector3.up)).Append(";").Append(c.Mode);
            if (_stage != null) sb.Append(" stage=").Append(P(_stage.position));
            if (_exit != null) sb.Append(" exit=").Append(P(_exit.position));
            if (_dog != null) sb.Append(" riff=").Append(P(_dog.transform.position + Vector3.up * 0.5f));
            if (_mode == Mode.Panel)
            {
                GameObject quit = GameObject.Find("QuitButton");
                if (quit != null && quit.activeInHierarchy) sb.Append(" quit=").Append(MathUi.Centre((RectTransform)quit.transform));
            }
            Vector3 pp = _player.transform.position;
            sb.Append(" star=").Append(pp.x.ToString("0.00", CultureInfo.InvariantCulture)).Append(":").Append(pp.z.ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(" mode=").Append(_mode);
            string buttons = _ui != null ? _ui.ButtonCentres() : null;
            if (buttons != null) Debug.Log("WT_WORLD_BUTTONS " + buttons);
            if (_follow != null && _follow.HiddenCount > 0) sb.Append(" hidn=").Append(_follow.HiddenNames());
            if (_follow != null) sb.Append(" hid=").Append(_follow.HiddenCount).Append(" lift=").Append(_follow.Lift.ToString("0.0", CultureInfo.InvariantCulture)).Append(" cam=").Append(_cam.transform.position.x.ToString("0.0", CultureInfo.InvariantCulture)).Append(":").Append(_cam.transform.position.z.ToString("0.0", CultureInfo.InvariantCulture));
            Debug.Log(sb.ToString());
        }
    }
}
