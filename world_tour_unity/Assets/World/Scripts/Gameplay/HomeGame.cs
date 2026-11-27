using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game;
using WorldTour.Game.Audio;
using WorldTour.Game.Prefs;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;
using WorldTour.World;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// Phase 10B.2: her home. A door on the coffee street ("HomeDoor") and the tour map's "go home" button lead to the scene Home, where one of four rooms is
    /// switched on at a time (her room, the den, the studio, the garden: the last three are bought with coins at their doors). Inside, by the names the scene
    /// gives its empties: "DoorOut" (her room only), "PutAwayBox", "Door_W" and "Door_E" (each a HomeDoor). The box lists what she has and, after it, the
    /// pieces she can buy with coins (choosing one buys it, saves, and holds it ready to place, as if it came out of the box).
    /// Decorating (E anywhere inside, the box, or the "decorate" button) lifts one piece at a time. By mouse: click a piece to pick it up, move the pointer to
    /// slide its ghost over the grid (green fits, red does not), click to drop it, buttons turn it, put it away or close. By keyboard: Tab picks the next
    /// piece, the arrows move it, R turns it a quarter, Enter drops it, Delete or Backspace puts it away, B or E opens the box, C the colors, Esc backs out.
    /// A piece is never lost: it is either standing, or held, or in the box. The layout is HomeRules over the save (see Core), saved on every drop.
    /// Log lines: WT_HOME_SPOTS, WT_HOME_DOOR, WT_HOME_ROOM, WT_HOME_DECO, WT_HOME_DROP, WT_HOME_AWAY, WT_HOME_COLOR, WT_HOME_BUY; test only (page URL has wt_test):
    /// WT_HOME_TARGETS (screen spots of the buttons and pieces), WT_HOME_STATE and WT_HOME_LAYOUT (the home key of the save), WT_HOME_CELL.
    /// </summary>
    public sealed class HomeGame : MonoBehaviour
    {
        public const float DoorRadius = 1.1f, SpotRange = 2.4f;

        public static HomeGame Instance { get; private set; }

        sealed class Spot
        {
            public string Key, Label, Verb = "go";
            public Transform T;
            public Vector3 Stand, Return;
            public float Range = SpotRange, PinLift = 2.4f, ClickLift = 1.0f;
            public bool Auto, Armed = true;
            public Action Act;
        }

        WorldGame _game;
        WorldUi _ui;
        readonly List<Spot> _spots = new List<Spot>();
        Spot _pending;
        bool _inside;
        int _room;
        float _nextTargets;
        HomeStock _stock;
        readonly List<HomeSpace> _spaces = new List<HomeSpace>();
        readonly List<HomeView> _views = new List<HomeView>();   // parallel to Blob.Home.Placed (null for pieces in another room)
        static readonly MaterialPropertyBlock Mpb = new MaterialPropertyBlock();

        // decorating
        bool _deco, _boxOpen, _colorOpen;
        int _openFrame;
        int _cursor = -1;                   // index in Placed of the selected standing piece, or -1
        HomeView _held;                     // the lifted piece (a standing one, or a new one from the box)
        int _heldFrom = -1;                 // its index in Placed, or -1 for a new one
        string _heldId;
        int _hx, _hz, _hrot;
        PlaceCheck _hcheck = PlaceCheck.Ok;
        string _msg = "";
        float _msgUntil;
        Vector2 _lastMouse;
        float _repeatAt;
        Key _repeatKey = Key.None;
        int _cat, _boxPick, _boxPage;
        readonly List<HomePiece> _boxList = new List<HomePiece>();

        // ui
        RectTransform _bar, _hintRt, _boxRt, _colorRt, _decorBtn;
        TMP_Text _hint;
        ShowButton _bTurn, _bAway, _bDrop;
        readonly Dictionary<string, RectTransform> _targets = new Dictionary<string, RectTransform>();
        readonly List<string> _dynamic = new List<string>();
        readonly StringBuilder _sb = new StringBuilder();

        static SaveBlob Blob { get { return Boot.Store != null ? Boot.Store.Blob : null; } }
        public bool Inside { get { return _inside; } }
        public bool Decorating { get { return _deco; } }
        public int Room { get { return _room; } }

        public void Init(WorldGame game, WorldUi ui)
        {
            Instance = this;
            _game = game; _ui = ui;
            var prev = WorldInput.ClickUsed;
            WorldInput.ClickUsed = p => OnClick(p) || (prev != null && prev(p));
            var prevSuppress = FocusNavigator.Suppress;
            FocusNavigator.Suppress = () => _deco || (prevSuppress != null && prevSuppress());   // Tab picks the next piece while decorating
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        // ---------- a place loaded ----------

        public void PlaceLoaded(string placeId, string interior)
        {
            _spots.Clear();
            _pending = null;
            if (_deco) CloseDecorUi();
            _deco = false; _boxOpen = false; _colorOpen = false;
            _held = null; _heldFrom = -1; _cursor = -1;
            _views.Clear();   // the scene is gone and took them
            _spaces.Clear();
            _inside = interior == "home";
            if (_inside)
            {
                _stock = UnityEngine.Object.FindFirstObjectByType<HomeStock>();
                foreach (HomeSpace s in UnityEngine.Object.FindObjectsByType<HomeSpace>(FindObjectsInactive.Include, FindObjectsSortMode.None)) _spaces.Add(s);
                SaveBlob b = Blob;
                if (b != null && HomeRules.EnsureStarters(b) && Boot.Store != null) Boot.Store.Save();
                _room = 0;
                ShowRoom(_room);
            }
            else
            {
                Transform door = FindOutside("HomeDoor");
                if (placeId == "coffee" && door != null)
                {
                    Transform stand = door.Find("Stand");
                    _spots.Add(new Spot
                    {
                        Key = "home_door", Label = HomeRules.DoorOutside, Verb = "go in", T = door, Stand = door.position,
                        Return = stand != null ? stand.position : door.position + new Vector3(-0.4f, 0f, -1.6f),
                        Range = DoorRadius, Auto = true, PinLift = 3.3f, ClickLift = 1.2f, Act = AskEnter,
                    });
                }
            }
            ArmSpots();
            Debug.Log("WT_HOME_SPOTS place=" + placeId + " inside=" + _inside + " spots=" + _spots.Count);
        }

        static Transform FindOutside(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        void ArmSpots()
        {
            if (_game == null || _game.Player == null) return;
            Vector3 p = _game.Player.transform.position;
            foreach (Spot s in _spots) if (s.Auto) s.Armed = Flat(p, s.T.position) > s.Range + 0.6f;   // never opens a door she arrived beside
        }

        // ---------- the rooms ----------

        HomeSpace SpaceOf(int room)
        {
            foreach (HomeSpace s in _spaces) if (s.index == room) return s;
            return null;
        }

        /// <summary>Switches one room on (the others off), paints it, stands its pieces up and finds its doors and box.</summary>
        void ShowRoom(int room)
        {
            _room = room;
            foreach (HomeSpace s in _spaces) s.gameObject.SetActive(s.index == room);
            _spots.Clear();
            HomeSpace sp = SpaceOf(room);
            if (sp != null)
            {
                Transform t = sp.transform;
                Transform outDoor = t.Find("DoorOut");
                if (outDoor != null)
                    _spots.Add(new Spot { Key = "door_out", Label = "exit", Verb = "go out", T = outDoor, Stand = outDoor.position, Range = 1.3f, Auto = true, PinLift = 3.0f, ClickLift = 0.2f, Act = AskLeave });
                Transform box = t.Find("PutAwayBox");
                if (box != null)
                    _spots.Add(new Spot { Key = "box", Label = HomeRules.BoxName, Verb = "decorate", T = box, Stand = box.position + new Vector3(-1.2f, 0f, 0.4f), PinLift = 1.9f, ClickLift = 0.5f, Act = EnterDecorate });
                for (int side = 0; side < 2; side++)
                {
                    bool east = side == 1;
                    Transform d = t.Find(east ? "Door_E" : "Door_W");
                    HomeDoor hd = d != null ? d.GetComponent<HomeDoor>() : null;
                    if (hd == null) continue;
                    int target = hd.target;
                    _spots.Add(new Spot { Key = east ? "door_e" : "door_w", Label = "", Verb = "go", T = d, Stand = d.position + new Vector3(east ? -1.3f : 1.3f, 0f, 0f), Range = DoorRadius + 0.2f, Auto = true, PinLift = 2.6f, ClickLift = 1.0f, Act = () => AskRoom(target, east) });
                }
            }
            PaintRoom();
            DestroyViews();
            SpawnViews(room);
            ArmSpots();
            Debug.Log("WT_HOME_ROOM room=" + room + " pieces=" + HomeRules.InRoom(Blob, room).Count);
        }

        void PaintRoom()
        {
            SaveBlob b = Blob;
            if (b == null) return;
            foreach (HomePaint hp in UnityEngine.Object.FindObjectsByType<HomePaint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int idx = hp.wall ? b.Home.Wall : b.Home.Floor;
                Color c = idx >= 0 ? HexColor(AvatarCatalogue.PaletteHex[idx]) : hp.own;
                Mpb.SetColor("_BaseColor", c);
                foreach (Renderer r in hp.targets) if (r != null) r.SetPropertyBlock(Mpb);
            }
        }

        static Color HexColor(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white;
        }

        void DestroyViews()
        {
            foreach (HomeView v in _views) if (v != null) Destroy(v.gameObject);
            _views.Clear();
            if (_held != null) { Destroy(_held.gameObject); _held = null; }
            if (_stock != null && _stock.plate != null) _stock.plate.SetActive(false);
        }

        void SpawnViews(int room)
        {
            SaveBlob b = Blob;
            _views.Clear();
            if (b == null) return;
            for (int i = 0; i < b.Home.Placed.Count; i++)
            {
                HomeItem it = b.Home.Placed[i];
                HomeView v = null;
                if (it.Room == room) { v = Make(it.Id); if (v != null) Pose(v, it.Id, it.X, it.Z, it.Rot); }
                _views.Add(v);
            }
        }

        HomeView Make(string id)
        {
            if (_stock == null) return null;
            GameObject pf = _stock.PrefabOf(id);
            if (pf == null) return null;
            GameObject go = Instantiate(pf);
            go.name = id;
            HomeView v = go.AddComponent<HomeView>();
            v.id = id;
            v.Capture(_stock);
            HomePiece p = HomeCatalogue.ById(id);
            if (p != null && p.Blocks) v.MakeBlockers(p.W, p.D);
            return v;
        }

        static void Pose(HomeView v, string id, int x, int z, int rot)
        {
            HomePiece p = HomeCatalogue.ById(id);
            int w, d;
            HomeRules.Footprint(p, rot, out w, out d);
            if (p.Layer == HomeLayer.Wall) { z = HomeRules.Cells - 1; rot = 0; }
            v.transform.SetPositionAndRotation(new Vector3(HomeRules.WorldX(x + w * 0.5f), 0f, HomeRules.WorldZ(z + d * 0.5f)), Quaternion.Euler(0f, rot * 90f, 0f));
        }

        // ---------- near a door, the box ----------

        static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        string LabelOf(Spot s)
        {
            if (s.Key == "door_w" || s.Key == "door_e")
            {
                HomeDoor hd = s.T.GetComponent<HomeDoor>();
                return hd == null ? "door" : HomeRules.RoomPin(hd.target, HomeRules.RoomUnlocked(Blob, hd.target));
            }
            return s.Label;
        }

        void Update()
        {
            if (_game == null) return;
            if (_decorBtn != null)
            {
                bool want = _inside && _game.IsFree && !_deco;
                if (_decorBtn.gameObject.activeSelf != want) _decorBtn.gameObject.SetActive(want);
            }
            if (_deco) { UpdateDeco(); return; }
            if (_spots.Count == 0) return;
            Camera cam = _game.Cam;
            if (WorldGame.TestMode && cam != null && Time.unscaledTime >= _nextTargets) { _nextTargets = Time.unscaledTime + 0.4f; LogTargets(cam); }
            if (!_game.IsFree || _game.Player == null) return;
            Vector3 p = _game.Player.transform.position;
            Keyboard k = Keyboard.current;
            bool eKey = k != null && k.eKey.wasPressedThisFrame;
            Spot nearest = null; float best = float.MaxValue;
            for (int i = 0; i < _spots.Count; i++)
            {
                Spot s = _spots[i];
                float d = Flat(p, s.T.position);
                bool near = d <= s.Range;
                if (near && d < best) { best = d; nearest = s; }
                if (cam != null)
                {
                    Vector3 sp = cam.WorldToScreenPoint(s.T.position + Vector3.up * s.PinLift);
                    string label = LabelOf(s);
                    string text = near ? label + " - e " + s.Verb : label;
                    float w = Mathf.Clamp(40f + 14.5f * text.Length, 150f, 360f);
                    bool show = sp.z > 0f && sp.x > w * 0.5f + 20f && sp.x < Screen.width - w * 0.5f - 20f && sp.y > 60f && sp.y < Screen.height - 110f;
                    _ui.SetPin("h_" + s.Key, text, sp, show);
                }
                if (s.Auto)
                {
                    if (s.Armed && d < s.Range * 0.85f) { s.Armed = false; s.Act(); return; }
                    if (!s.Armed && d > s.Range + 1.2f) s.Armed = true;
                }
            }
            if (eKey)
            {
                _pending = null;
                if (nearest != null && nearest.Key != "box") nearest.Act();
                else if (_inside) EnterDecorate();
                return;
            }
            if (_pending != null)
            {
                float dp = Flat(p, _pending.T.position);
                if (dp <= _pending.Range - 0.2f) { Spot s = _pending; _pending = null; s.Act(); }
                else if (!_game.Player.HasTarget)
                {
                    Spot s = _pending; _pending = null;
                    if (dp <= s.Range + 1.5f) s.Act();
                }
            }
        }

        bool OnClick(Vector2 screen)
        {
            if (_spots.Count == 0 || _deco || _game == null || !_game.IsFree || _game.Cam == null) return false;
            Camera cam = _game.Cam;
            Spot hit = null; float best = Screen.height * 0.09f;
            foreach (Spot s in _spots)
            {
                Vector3 a = cam.WorldToScreenPoint(s.T.position + Vector3.up * s.ClickLift), b = cam.WorldToScreenPoint(s.T.position + Vector3.up * s.PinLift);
                if (a.z <= 0f) continue;
                float d = Mathf.Min(Vector2.Distance(screen, a), Vector2.Distance(screen, b));
                if (d < best) { best = d; hit = s; }
            }
            if (hit == null) return false;
            Vector3 p = _game.Player.transform.position;
            if (Flat(p, hit.T.position) <= hit.Range - 0.2f) { hit.Act(); return true; }
            _pending = hit;
            _game.Player.WalkTo(hit.Stand);
            return true;
        }

        // ---------- the doors ----------

        void AskEnter()
        {
            _game.BeginCard();
            Debug.Log("WT_HOME_DOOR ask=enter");
            Spot door = _spots.Find(s => s.Key == "home_door");
            var buttons = new List<DialogButton>
            {
                new DialogButton { Label = HomeRules.DoorGoIn, OnClick = () => _game.GoThroughDoor(WorldGame.HomeScene, door != null ? door.Return : (Vector3?)null) },
                new DialogButton { Label = HomeRules.DoorStay, Ghost = true, OnClick = _game.EndCard },
            };
            _ui.OpenDialog(HomeRules.DoorOutside, HomeRules.DoorLine, null, buttons, null);
        }

        void AskLeave()
        {
            _game.BeginCard();
            Debug.Log("WT_HOME_DOOR ask=leave");
            var buttons = new List<DialogButton>
            {
                new DialogButton { Label = HomeRules.LeaveGo, OnClick = () => _game.LeaveInterior() },
                new DialogButton { Label = HomeRules.LeaveStay, Ghost = true, OnClick = _game.EndCard },
            };
            _ui.OpenDialog("the street", HomeRules.LeaveLine, null, buttons, null);
        }

        /// <summary>A door to another room: through it when the room is hers, else a card with the price.</summary>
        void AskRoom(int target, bool east)
        {
            SaveBlob b = Blob;
            if (b == null) return;
            if (HomeRules.RoomUnlocked(b, target)) { StartCoroutine(GoRoom(target, east)); return; }
            _game.BeginCard();
            long coins = CoinRules.Coins(b), price = HomeRules.RoomPrices[target];
            Debug.Log("WT_HOME_DOOR ask=buy room=" + target + " price=" + price + " coins=" + coins);
            var buttons = new List<DialogButton>();
            if (coins >= price)
                buttons.Add(new DialogButton
                {
                    Label = HomeRules.UnlockLabel(target),
                    OnClick = () =>
                    {
                        if (HomeRules.UnlockRoom(b, target))
                        {
                            Debug.Log("WT_HOME_BUY room=" + target + " coins=" + CoinRules.Coins(b));
                            if (Boot.Store != null) Boot.Store.Save();
                            Sfx.Play(SfxId.Strum);
                            StartCoroutine(GoRoom(target, east));
                        }
                    },
                });
            else buttons.Add(new DialogButton { Label = BoutiqueRules.NeedLabel(price - coins), Closes = false, OnClick = () => { } });
            buttons.Add(new DialogButton { Label = HomeRules.DoorStay, Ghost = true, OnClick = () => { _game.EndCard(); MoveBack(); } });
            _ui.OpenDialog(HomeRules.RoomName(target), HomeRules.RoomLine(target), BoutiqueRules.Wallet(coins), buttons, null);
        }

        /// <summary>After "not now" at a locked door she steps back so the door does not ask again at once.</summary>
        void MoveBack()
        {
            if (_game.Player == null) return;
            Vector3 p = _game.Player.transform.position;
            p.x = Mathf.Clamp(p.x, -4.2f, 4.2f);
            _game.Player.Place(p);
            ArmSpots();
        }

        IEnumerator GoRoom(int target, bool east)
        {
            _game.BeginCard();
            _ui.CloseDialog();
            _ui.FadeTo(1f, 0.2f);
            float t = 0f;
            while (t < 0.25f) { t += Time.unscaledDeltaTime; yield return null; }
            ShowRoom(target);
            // she comes in by the door that faces back: going east, she arrives at the west door of the next room
            Vector3 at = new Vector3(east ? -4.4f : 4.4f, 0f, 0f);
            if (_game.Player != null)
            {
                _game.Player.Place(at);
                _game.Player.transform.rotation = Quaternion.LookRotation(new Vector3(east ? 1f : -1f, 0f, 0f), Vector3.up);
            }
            FollowCamera follow = _game.Follow;
            if (follow != null) { follow.transform.position = follow.FollowPosition; follow.ResetVelocity(); }
            ArmSpots();
            Debug.Log("WT_HOME_DOOR through room=" + target);
            _ui.FadeTo(0f, 0.25f);
            t = 0f;
            while (t < 0.25f) { t += Time.unscaledDeltaTime; yield return null; }
            _game.EndCard();
        }

        // ---------- decorating: open, close ----------

        void EnterDecorate()
        {
            if (!_inside || _deco || Blob == null) return;
            _deco = true; _boxOpen = false; _colorOpen = false;
            _openFrame = Time.frameCount;
            _held = null; _heldFrom = -1; _cursor = -1;
            _game.BeginCard();
            BuildUi();
            _bar.gameObject.SetActive(true);
            _hintRt.gameObject.SetActive(true);
            _boxRt.gameObject.SetActive(false); _colorRt.gameObject.SetActive(false);
            Say(HomeRules.KeysLine, 6f);
            Sfx.Play(SfxId.Tap);
            Debug.Log("WT_HOME_DECO open room=" + _room);
            LogState();
        }

        void ExitDecorate()
        {
            if (!_deco) return;
            if (_held != null) Cancel();
            _deco = false; _boxOpen = false; _colorOpen = false;
            _cursor = -1;
            CloseDecorUi();
            if (_stock != null && _stock.plate != null) _stock.plate.SetActive(false);
            SelectNone();
            _game.EndCard();
            Debug.Log("WT_HOME_DECO close");
            LogState();
        }

        void CloseDecorUi()
        {
            if (_bar != null) _bar.gameObject.SetActive(false);
            if (_hintRt != null) _hintRt.gameObject.SetActive(false);
            if (_boxRt != null) _boxRt.gameObject.SetActive(false);
            if (_colorRt != null) _colorRt.gameObject.SetActive(false);
        }

        static void SelectNone()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void Say(string text, float seconds = 2.2f)
        {
            _msg = text; _msgUntil = Time.unscaledTime + seconds;
            if (_hint != null) _hint.SetLower(text);
        }

        // ---------- decorating: each frame ----------

        void UpdateDeco()
        {
            Camera cam = _game.Cam;
            if (cam == null) return;
            if (WorldGame.TestMode && Time.unscaledTime >= _nextTargets) { _nextTargets = Time.unscaledTime + 0.4f; LogTargets(cam); }
            if (_hint != null && _msg.Length > 0 && Time.unscaledTime > _msgUntil)
            {
                _msg = "";
                _hint.SetLower(_held != null ? HomeRules.KeysLine : HomeRules.KeysLine2);
            }
            if (Time.frameCount <= _openFrame) return;
            SaveBlob b = Blob;
            if (b == null) return;
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            int held = _held != null ? 1 : 0;
            if (_bTurn != null) { bool on = held == 1 || _cursor >= 0; _bTurn.Interactable = on; _bAway.Interactable = on; _bDrop.Interactable = held == 1; }

            if (k != null)
            {
                if (k.escapeKey.wasPressedThisFrame)
                {
                    if (_colorOpen) CloseColor();
                    else if (_boxOpen) CloseBox();
                    else if (_held != null) Cancel();
                    else ExitDecorate();
                    return;
                }
                if (k.bKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame) { if (_boxOpen) CloseBox(); else OpenBox(); return; }
                if (k.cKey.wasPressedThisFrame) { if (_colorOpen) CloseColor(); else OpenColor(); return; }
                if (_colorOpen) { ColorKeys(k); return; }
                if (_boxOpen) { BoxKeys(k); return; }
                if (k.tabKey.wasPressedThisFrame) { TabNext(k.shiftKey.isPressed ? -1 : 1); return; }
                if (k.rKey.wasPressedThisFrame) { Rotate(); return; }
                if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) { Enter(); return; }
                if (k.deleteKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) { PutAway(); return; }
                ArrowKeys(k);
            }
            if (m != null && !_colorOpen)
            {
                Vector2 mp = m.position.ReadValue();
                bool overUi = _ui.PointerOverUi();
                if (_held != null && !overUi && (mp - _lastMouse).sqrMagnitude > 1f) HoverTo(cam, mp);
                _lastMouse = mp;
                if (m.leftButton.wasPressedThisFrame && !overUi) Click(cam, mp);
            }
            else if (m != null) _lastMouse = m.position.ReadValue();
            if (m != null && m.leftButton.wasPressedThisFrame && !_ui.PointerOverUi() && _colorOpen) CloseColor();
        }

        void ArrowKeys(Keyboard k)
        {
            int dx = 0, dz = 0;
            Key pressed = Key.None;
            if (k.leftArrowKey.wasPressedThisFrame) { dx = -1; pressed = Key.LeftArrow; }
            else if (k.rightArrowKey.wasPressedThisFrame) { dx = 1; pressed = Key.RightArrow; }
            else if (k.upArrowKey.wasPressedThisFrame) { dz = 1; pressed = Key.UpArrow; }
            else if (k.downArrowKey.wasPressedThisFrame) { dz = -1; pressed = Key.DownArrow; }
            else if (_repeatKey != Key.None && k[_repeatKey].isPressed && Time.unscaledTime >= _repeatAt)
            {
                _repeatAt = Time.unscaledTime + 0.07f;
                switch (_repeatKey) { case Key.LeftArrow: dx = -1; break; case Key.RightArrow: dx = 1; break; case Key.UpArrow: dz = 1; break; default: dz = -1; break; }
            }
            else if (_repeatKey != Key.None && !k[_repeatKey].isPressed) _repeatKey = Key.None;
            if (pressed != Key.None) { _repeatKey = pressed; _repeatAt = Time.unscaledTime + 0.35f; }
            if (dx == 0 && dz == 0) return;
            if (_held == null)
            {
                if (_cursor < 0) { Say(HomeRules.NothingHeld); return; }
                Lift(_cursor);
            }
            MoveHeld(dx, dz);
        }

        // ---------- decorating: the held piece ----------

        HomePiece HeldPiece { get { return HomeCatalogue.ById(_heldId); } }

        void Lift(int index)
        {
            SaveBlob b = Blob;
            if (index < 0 || index >= b.Home.Placed.Count || index >= _views.Count || _views[index] == null) return;
            HomeItem it = b.Home.Placed[index];
            _held = _views[index]; _heldFrom = index; _heldId = it.Id;
            _hx = it.X; _hz = it.Z; _hrot = it.Rot;
            _held.SetBlockers(false);
            _cursor = -1;
            Refresh();
            Debug.Log("WT_HOME_DECO lift id=" + _heldId + " x=" + _hx + " z=" + _hz + " rot=" + _hrot);
        }

        /// <summary>Takes a new piece out of the box and holds it, at the first free spot nearest the middle of the room.</summary>
        void TakeOut(HomePiece p)
        {
            SaveBlob b = Blob;
            if (_held != null) return;
            if (HomeRules.Boxed(b, p.Id) <= 0) return;
            HomeView v = Make(p.Id);
            if (v == null) { Say(HomeRules.NoRoom); return; }
            _held = v; _heldFrom = -1; _heldId = p.Id; _hrot = 0; _cursor = -1;
            int w, d;
            HomeRules.Footprint(p, 0, out w, out d);
            int cx = (HomeRules.Cells - w) / 2, cz = (HomeRules.Cells - d) / 2;
            int bx = cx, bz = cz, bestD = int.MaxValue;
            for (int z = 0; z <= HomeRules.Cells - d; z++)
                for (int x = 0; x <= HomeRules.Cells - w; x++)
                {
                    int dist = (x - cx) * (x - cx) + (z - cz) * (z - cz);
                    if (dist < bestD && HomeRules.Check(b, p.Id, x, z, 0, _room, -1) == PlaceCheck.Ok) { bestD = dist; bx = x; bz = z; }
                }
            _hx = bx; _hz = p.Layer == HomeLayer.Wall ? HomeRules.Cells - 1 : bz;
            CloseBox();
            Refresh();
            Debug.Log("WT_HOME_DECO take id=" + p.Id + " x=" + _hx + " z=" + _hz);
        }

        void MoveHeld(int dx, int dz)
        {
            HomePiece p = HeldPiece;
            int w, d;
            HomeRules.Footprint(p, _hrot, out w, out d);
            _hx = Mathf.Clamp(_hx + dx, 0, HomeRules.Cells - w);
            _hz = p.Layer == HomeLayer.Wall ? HomeRules.Cells - 1 : Mathf.Clamp(_hz + dz, 0, HomeRules.Cells - d);
            Refresh();
        }

        void Rotate()
        {
            if (_held == null)
            {
                if (_cursor < 0) { Say(HomeRules.NothingHeld); return; }
                Lift(_cursor);
            }
            HomePiece p = HeldPiece;
            if (p.Layer == HomeLayer.Wall) { Say("posters stay flat."); return; }
            _hrot = (_hrot + 1) & 3;
            int w, d;
            HomeRules.Footprint(p, _hrot, out w, out d);
            _hx = Mathf.Clamp(_hx, 0, HomeRules.Cells - w);
            _hz = Mathf.Clamp(_hz, 0, HomeRules.Cells - d);
            Refresh();
            Debug.Log("WT_HOME_DECO turn rot=" + _hrot);
        }

        void HoverTo(Camera cam, Vector2 mp)
        {
            HomePiece p = HeldPiece;
            int w, d;
            HomeRules.Footprint(p, _hrot, out w, out d);
            Ray ray = cam.ScreenPointToRay(mp);
            if (p.Layer == HomeLayer.Wall)
            {
                var wall = new Plane(Vector3.back, new Vector3(0f, 0f, HomeRules.WorldZ(HomeRules.Cells)));
                float t;
                if (!wall.Raycast(ray, out t)) return;
                _hx = Mathf.Clamp(HomeRules.CellOf(ray.GetPoint(t).x) - w / 2, 0, HomeRules.Cells - w);
                _hz = HomeRules.Cells - 1;
            }
            else
            {
                var ground = new Plane(Vector3.up, Vector3.zero);
                float t;
                if (!ground.Raycast(ray, out t)) return;
                Vector3 hit = ray.GetPoint(t);
                _hx = Mathf.Clamp(HomeRules.CellOf(hit.x) - w / 2, 0, HomeRules.Cells - w);
                _hz = Mathf.Clamp(HomeRules.CellOf(hit.z) - d / 2, 0, HomeRules.Cells - d);
            }
            Refresh();
        }

        /// <summary>The held piece's ghost, plate and tint for where it is now; the one place the check is made while moving.</summary>
        void Refresh()
        {
            SaveBlob b = Blob;
            if (_held == null) { UpdatePlate(); return; }
            _hcheck = HomeRules.Check(b, _heldId, _hx, _hz, _hrot, _room, _heldFrom);
            Pose(_held, _heldId, _hx, _hz, _hrot);
            _held.Show(_hcheck == PlaceCheck.Ok ? 1 : 2);
            UpdatePlate();
            LogState();
        }

        void UpdatePlate()
        {
            if (_stock == null || _stock.plate == null) return;
            HomeView v = _held; string id = _heldId; int x = _hx, z = _hz, rot = _hrot;
            int mode = _hcheck == PlaceCheck.Ok ? 1 : 2;
            if (v == null)
            {
                if (_cursor < 0 || _cursor >= Blob.Home.Placed.Count) { _stock.plate.SetActive(false); return; }
                HomeItem it = Blob.Home.Placed[_cursor];
                id = it.Id; x = it.X; z = it.Z; rot = it.Rot; mode = 3;
            }
            HomePiece p = HomeCatalogue.ById(id);
            int w, d;
            HomeRules.Footprint(p, rot, out w, out d);
            if (p.Layer == HomeLayer.Wall) z = HomeRules.Cells - 1;
            Transform t = _stock.plate.transform;
            t.position = new Vector3(HomeRules.WorldX(x + w * 0.5f), 0.09f, HomeRules.WorldZ(z + d * 0.5f));
            t.localScale = new Vector3(w * 0.5f + 0.08f, 0.03f, d * 0.5f + 0.08f);
            _stock.plate.GetComponent<Renderer>().sharedMaterial = mode == 1 ? _stock.ok : mode == 2 ? _stock.bad : _stock.pick;
            _stock.plate.SetActive(true);
        }

        /// <summary>Drops the held piece where it is. A red spot says why not and keeps holding it.</summary>
        void Enter()
        {
            if (_held == null)
            {
                if (_cursor >= 0) Lift(_cursor);
                else Say(HomeRules.NothingHeld);
                return;
            }
            Drop();
        }

        bool Drop()
        {
            SaveBlob b = Blob;
            if (_held == null) return false;
            PlaceCheck c = _heldFrom >= 0 ? HomeRules.Move(b, _heldFrom, _hx, _hz, _hrot, _room) : HomeRules.Place(b, _heldId, _hx, _hz, _hrot, _room);
            if (c != PlaceCheck.Ok)
            {
                _hcheck = c;
                _held.Show(2);
                UpdatePlate();
                Say(HomeRules.Why(c));
                Debug.Log("WT_HOME_DROP result=" + c + " id=" + _heldId + " x=" + _hx + " z=" + _hz);
                return false;
            }
            int index = _heldFrom;
            if (index < 0) { index = b.Home.Placed.Count - 1; _views.Add(_held); }
            Pose(_held, _heldId, b.Home.Placed[index].X, b.Home.Placed[index].Z, b.Home.Placed[index].Rot);
            _held.Show(0);
            _held.SetBlockers(true);
            Debug.Log("WT_HOME_DROP result=Ok id=" + _heldId + " x=" + b.Home.Placed[index].X + " z=" + b.Home.Placed[index].Z + " rot=" + b.Home.Placed[index].Rot);
            _held = null; _heldFrom = -1; _cursor = index; _hcheck = PlaceCheck.Ok;
            Saved();
            Sfx.Play(SfxId.Tick);
            Say(HomeRules.Dropped, 1.2f);
            UpdatePlate();
            RefreshBox();
            return true;
        }

        /// <summary>Back to where it was (a standing piece) or back in the box (a new one).</summary>
        void Cancel()
        {
            if (_held == null) return;
            SaveBlob b = Blob;
            if (_heldFrom >= 0)
            {
                HomeItem it = b.Home.Placed[_heldFrom];
                Pose(_held, it.Id, it.X, it.Z, it.Rot);
                _held.Show(0); _held.SetBlockers(true);
                _cursor = _heldFrom;
            }
            else Destroy(_held.gameObject);
            _held = null; _heldFrom = -1; _hcheck = PlaceCheck.Ok;
            UpdatePlate();
            Say(HomeRules.PutBack, 1.2f);
            LogState();
        }

        void PutAway()
        {
            SaveBlob b = Blob;
            int index = _held != null ? _heldFrom : _cursor;
            if (_held == null && _cursor < 0) { Say(HomeRules.NothingHeld); return; }
            if (index >= 0)
            {
                HomeView v = _held != null ? _held : _views[index];
                string id = b.Home.Placed[index].Id;
                HomeRules.PutAway(b, index);
                _views.RemoveAt(index);
                if (v != null) Destroy(v.gameObject);
                Debug.Log("WT_HOME_AWAY id=" + id + " boxed=" + HomeRules.Boxed(b, id));
                Saved();
            }
            else if (_held != null) Destroy(_held.gameObject);   // a new one: it never left the box
            _held = null; _heldFrom = -1; _cursor = -1; _hcheck = PlaceCheck.Ok;
            Sfx.Play(SfxId.Tap);
            Say(HomeRules.PutBack, 1.2f);
            UpdatePlate();
            RefreshBox();
        }

        /// <summary>Tab: drops a held piece that fits, then selects the next standing piece in this room (Shift+Tab the one before).</summary>
        void TabNext(int dir)
        {
            SaveBlob b = Blob;
            if (_held != null && !Drop()) return;
            List<int> here = HomeRules.InRoom(b, _room);
            if (here.Count == 0) { Say(HomeRules.NothingHeld); return; }
            int at = _cursor >= 0 ? here.IndexOf(_cursor) : (dir > 0 ? -1 : 0);
            at = ((at + dir) % here.Count + here.Count) % here.Count;
            _cursor = here[at];
            UpdatePlate();
            Say(HomeCatalogue.ById(b.Home.Placed[_cursor].Id).Name + ". arrows move it.", 2.5f);
            Debug.Log("WT_HOME_DECO select index=" + _cursor + " id=" + b.Home.Placed[_cursor].Id);
            LogState();
        }

        // ---------- decorating: the mouse ----------

        void Click(Camera cam, Vector2 mp)
        {
            if (_held != null)
            {
                HoverTo(cam, mp);
                Drop();
                return;
            }
            int hit = PickAt(cam, mp);
            if (hit >= 0) { if (_boxOpen) CloseBox(); if (_colorOpen) CloseColor(); _cursor = hit; Lift(hit); }
            else { _cursor = -1; UpdatePlate(); }
        }

        /// <summary>The standing piece under the pointer: the nearest along the ray, rugs only when nothing stands over them.</summary>
        int PickAt(Camera cam, Vector2 mp)
        {
            SaveBlob b = Blob;
            Ray ray = cam.ScreenPointToRay(mp);
            int best = -1, bestRug = -1; float bd = float.MaxValue, bdr = float.MaxValue;
            for (int i = 0; i < b.Home.Placed.Count && i < _views.Count; i++)
            {
                HomeItem it = b.Home.Placed[i];
                if (it.Room != _room) continue;
                HomePiece p = HomeCatalogue.ById(it.Id);
                int w, d;
                HomeRules.Footprint(p, it.Rot, out w, out d);
                int z = p.Layer == HomeLayer.Wall ? HomeRules.Cells - 1 : it.Z;
                float h = p.Layer == HomeLayer.Rug ? 0.12f : p.Layer == HomeLayer.Wall ? 1.0f : Mathf.Max(0.4f, p.H);
                float y0 = p.Layer == HomeLayer.Wall ? 0.9f : 0f;
                Vector3 size = new Vector3(w * 0.5f, h, d * 0.5f);
                Vector3 center = new Vector3(HomeRules.WorldX(it.X + w * 0.5f), y0 + h * 0.5f, HomeRules.WorldZ(z + d * 0.5f));
                var bounds = new Bounds(center, size);
                float dist;
                if (!bounds.IntersectRay(ray, out dist)) continue;
                if (p.Layer == HomeLayer.Rug) { if (dist < bdr) { bdr = dist; bestRug = i; } }
                else if (dist < bd) { bd = dist; best = i; }
            }
            return best >= 0 ? best : bestRug;
        }

        // ---------- the box ----------

        void OpenBox()
        {
            if (_held != null) Say("drop it first, or esc.");
            if (_held != null) return;
            _boxOpen = true; _colorOpen = false; _colorRt.gameObject.SetActive(false);
            if (_cat < 0 || _cat >= HomeCatalogue.Categories.Count) _cat = 0;
            RebuildBox(true);
            _boxRt.gameObject.SetActive(true);
            Say(HomeRules.KeysBox, 6f);
            Debug.Log("WT_HOME_DECO box");
        }

        void CloseBox()
        {
            _boxOpen = false;
            if (_boxRt != null) _boxRt.gameObject.SetActive(false);
            if (_hint != null && _held == null) Say(HomeRules.KeysLine2, 3f);
        }

        void RefreshBox() { if (_boxOpen) RebuildBox(false); }

        void BoxKeys(Keyboard k)
        {
            if (k.leftArrowKey.wasPressedThisFrame) { _boxPick = Mathf.Max(0, _boxPick - 1); RebuildBox(false); }
            else if (k.rightArrowKey.wasPressedThisFrame) { _boxPick = Mathf.Min(Mathf.Max(0, _boxList.Count - 1), _boxPick + 1); RebuildBox(false); }
            else if (k.upArrowKey.wasPressedThisFrame) { _cat = (_cat + 1) % HomeCatalogue.Categories.Count; _boxPick = 0; _boxPage = 0; RebuildBox(true); }
            else if (k.downArrowKey.wasPressedThisFrame) { _cat = (_cat + HomeCatalogue.Categories.Count - 1) % HomeCatalogue.Categories.Count; _boxPick = 0; _boxPage = 0; RebuildBox(true); }
            else if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
            {
                if (_boxPick >= 0 && _boxPick < _boxList.Count) Choose(_boxList[_boxPick]);
            }
        }

        /// <summary>A piece chosen in the box: hers comes out at once; one for sale is bought with coins (saved, as the boutique does) and then comes out the same way.
        /// Short of coins: the hint says how many more and nothing changes.</summary>
        void Choose(HomePiece p)
        {
            SaveBlob b = Blob;
            if (b == null || p == null) return;
            if (HomeRules.Boxed(b, p.Id) > 0) { TakeOut(p); return; }
            if (!HomeRules.ForSale(b, p.Id)) return;
            long need = HomeRules.Short(b, p.Id);
            if (need > 0 || !HomeRules.Buy(b, p.Id))
            {
                Say(BoutiqueRules.NeedLabel(need > 0 ? need : p.Price), 2.5f);
                Sfx.Play(SfxId.Tap);
                Debug.Log("WT_HOME_BUY id=" + p.Id + " result=Short need=" + need + " coins=" + CoinRules.Coins(b));
                LogState();
                return;
            }
            Debug.Log("WT_HOME_BUY id=" + p.Id + " result=Ok price=" + p.Price + " coins=" + CoinRules.Coins(b) + " fans=" + b.Fans.Lifetime + "/" + b.Fans.Balance);
            if (Boot.Store != null) Boot.Store.Save();
            Sfx.Play(SfxId.Strum);
            TakeOut(p);
            Say(HomeRules.Bought, 2.5f);
        }

        const int PerPage = 6;

        void ClearDynamic()
        {
            foreach (string n in _dynamic) _targets.Remove(n);
            _dynamic.Clear();
        }

        /// <summary>Redraws the box: a row of groups and a row of what is in the box for the chosen group (six at a time).</summary>
        void RebuildBox(bool pickFirst)
        {
            SaveBlob b = Blob;
            // clear
            for (int i = _boxRt.childCount - 1; i >= 0; i--) Destroy(_boxRt.GetChild(i).gameObject);
            ClearDynamic();
            _boxList.Clear();
            _boxList.AddRange(HomeRules.BoxList(b, HomeCatalogue.Categories[_cat]));
            if (_boxPick >= _boxList.Count) _boxPick = Mathf.Max(0, _boxList.Count - 1);
            _boxPage = _boxPick / PerPage;

            StickerButton prefab = _ui.Assets != null ? _ui.Assets.buttonPrefab : null;
            int nc = HomeCatalogue.Categories.Count;
            float cw = 118f, gap = 6f;
            for (int i = 0; i < nc; i++)
            {
                int ci = i;
                int have = 0;
                foreach (HomePiece p in HomeCatalogue.InCategory(HomeCatalogue.Categories[i])) if (HomeRules.Boxed(b, p.Id) > 0) have++;
                RectTransform box = ShowUi.Rect("Cat" + i, _boxRt);
                box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f); box.pivot = new Vector2(0.5f, 1f);
                box.sizeDelta = new Vector2(cw, 46f);
                box.anchoredPosition = new Vector2((i - (nc - 1) / 2f) * (cw + gap), -10f);
                ShowButton sb = ShowUi.Button(prefab, box, "Cat" + i, HomeCatalogue.CategoryNames[i] + (have > 0 ? " " + have : ""), i != _cat, () => { _cat = ci; _boxPick = 0; RebuildBox(true); });
                ShowUi.Stretch(sb.Rect);
                NoNav(sb);
                Track("cat_" + HomeCatalogue.Categories[i], box);
            }
            RectTransform info = ShowUi.Rect("Info", _boxRt);
            info.anchorMin = info.anchorMax = new Vector2(0.5f, 1f); info.pivot = new Vector2(0.5f, 1f);
            info.sizeDelta = new Vector2(1100f, 34f); info.anchoredPosition = new Vector2(0f, -62f);
            if (_boxList.Count == 0)
            {
                ShowUi.Label(info, "Empty", HomeRules.BoxEmpty, 24f, FontRole.BodyBold, ColorRole.Muted);
                return;
            }
            ShowUi.Label(info, "Wallet", BoutiqueRules.Wallet(CoinRules.Coins(b)), 22f, FontRole.BodyBold, ColorRole.Muted);
            int first = _boxPage * PerPage, count = Mathf.Min(PerPage, _boxList.Count - first);
            float bw = 170f;
            int slots = count + (_boxList.Count > PerPage ? 1 : 0);
            for (int j = 0; j < count; j++)
            {
                int idx = first + j;
                HomePiece p = _boxList[idx];
                int n = HomeRules.Boxed(b, p.Id);
                RectTransform box = ShowUi.Rect("Piece" + idx, _boxRt);
                box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f); box.pivot = new Vector2(0.5f, 1f);
                box.sizeDelta = new Vector2(bw, 50f);
                box.anchoredPosition = new Vector2((j - (slots - 1) / 2f) * (bw + gap), -98f);
                ShowButton sb = ShowUi.Button(prefab, box, "Piece" + idx, p.Name + (n > 1 ? " x" + n : ""), idx != _boxPick, () => { _boxPick = idx; Choose(_boxList[idx]); });
                ShowUi.Stretch(sb.Rect);
                NoNav(sb);
                FitLabel(sb);
                Track("piece_" + idx, box);
                // under it: "in the box" for hers, the price for one she can buy
                RectTransform tag = ShowUi.Rect("Tag" + idx, _boxRt);
                tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 1f); tag.pivot = new Vector2(0.5f, 1f);
                tag.sizeDelta = new Vector2(bw, 28f);
                tag.anchoredPosition = new Vector2((j - (slots - 1) / 2f) * (bw + gap), -152f);
                ShowUi.Label(tag, "T", HomeRules.BoxTag(b, p), 20f, FontRole.BodyBold, n > 0 ? ColorRole.Muted : ColorRole.Ink);
            }
            if (_boxList.Count > PerPage)
            {
                RectTransform box = ShowUi.Rect("More", _boxRt);
                box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f); box.pivot = new Vector2(0.5f, 1f);
                box.sizeDelta = new Vector2(bw, 50f);
                box.anchoredPosition = new Vector2((count - (slots - 1) / 2f) * (bw + gap), -98f);
                ShowButton sb = ShowUi.Button(prefab, box, "More", "more", true, () => { _boxPick = ((_boxPage + 1) * PerPage) % _boxList.Count; RebuildBox(false); });
                ShowUi.Stretch(sb.Rect);
                NoNav(sb);
                Track("more", box);
            }
        }

        // ---------- the colors ----------

        void OpenColor()
        {
            if (_held != null) { Say("drop it first, or esc."); return; }
            _colorOpen = true; _boxOpen = false; _boxRt.gameObject.SetActive(false);
            RebuildColor();
            _colorRt.gameObject.SetActive(true);
            Say(HomeRules.KeysColor, 6f);
            Debug.Log("WT_HOME_DECO colors");
        }

        void CloseColor()
        {
            _colorOpen = false;
            if (_colorRt != null) _colorRt.gameObject.SetActive(false);
            Say(HomeRules.KeysLine2, 3f);
        }

        void ColorKeys(Keyboard k)
        {
            SaveBlob b = Blob;
            int n = AvatarCatalogue.PaletteHex.Count;
            if (k.leftArrowKey.wasPressedThisFrame) SetColor(true, ((b.Home.Wall + 1 + n - 1) % (n + 1)) - 1);
            else if (k.rightArrowKey.wasPressedThisFrame) SetColor(true, ((b.Home.Wall + 1 + 1) % (n + 1)) - 1);
            else if (k.downArrowKey.wasPressedThisFrame) SetColor(false, ((b.Home.Floor + 1 + n - 1) % (n + 1)) - 1);
            else if (k.upArrowKey.wasPressedThisFrame) SetColor(false, ((b.Home.Floor + 1 + 1) % (n + 1)) - 1);
            else if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) CloseColor();
        }

        void SetColor(bool wall, int color)
        {
            SaveBlob b = Blob;
            if (wall) HomeRules.SetWall(b, color); else HomeRules.SetFloor(b, color);
            PaintRoom();
            Debug.Log("WT_HOME_COLOR wall=" + b.Home.Wall + " floor=" + b.Home.Floor);
            Saved();
            RebuildColor();
        }

        void RebuildColor()
        {
            SaveBlob b = Blob;
            for (int i = _colorRt.childCount - 1; i >= 0; i--) Destroy(_colorRt.GetChild(i).gameObject);
            StickerButton prefab = _ui.Assets != null ? _ui.Assets.buttonPrefab : null;
            int n = AvatarCatalogue.PaletteHex.Count;
            for (int row = 0; row < 2; row++)
            {
                bool wall = row == 0;
                int cur = wall ? b.Home.Wall : b.Home.Floor;
                float y = -12f - row * 78f;
                RectTransform lab = ShowUi.Rect(wall ? "WallLabel" : "FloorLabel", _colorRt);
                lab.anchorMin = lab.anchorMax = new Vector2(0f, 1f); lab.pivot = new Vector2(0f, 1f);
                lab.sizeDelta = new Vector2(150f, 62f); lab.anchoredPosition = new Vector2(14f, y);
                ShowUi.Label(lab, "T", (wall ? "wall - " : "floor - ") + (cur >= 0 ? AvatarCatalogue.PaletteNames[cur] : "own"), 22f, FontRole.BodyBold, ColorRole.Ink, TextAlignmentOptions.Left);
                RectTransform ownBox = ShowUi.Rect("Own", _colorRt);
                ownBox.anchorMin = ownBox.anchorMax = new Vector2(0f, 1f); ownBox.pivot = new Vector2(0f, 1f);
                ownBox.sizeDelta = new Vector2(60f, 62f); ownBox.anchoredPosition = new Vector2(166f, y);
                bool isWall = wall;
                ShowButton ob = ShowUi.Button(prefab, ownBox, "Own", "own", cur >= 0, () => SetColor(isWall, -1));
                ShowUi.Stretch(ob.Rect); NoNav(ob);
                Track((wall ? "wall_" : "floor_") + "own", ownBox);
                for (int c = 0; c < n; c++)
                {
                    int ci = c;
                    RectTransform sw = ShowUi.Rect("Sw" + c, _colorRt);
                    sw.anchorMin = sw.anchorMax = new Vector2(0f, 1f); sw.pivot = new Vector2(0f, 1f);
                    sw.sizeDelta = new Vector2(44f, 62f);
                    sw.anchoredPosition = new Vector2(234f + c * 46f, y);
                    var img = sw.gameObject.AddComponent<Image>();
                    img.color = HexColor(AvatarCatalogue.PaletteHex[c]);
                    img.raycastTarget = true;
                    var btn = sw.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;
                    btn.navigation = new Navigation { mode = Navigation.Mode.None };
                    btn.onClick.AddListener(() => { SetColor(isWall, ci); SelectNone(); });
                    if (c == cur)
                    {
                        RectTransform dot = ShowUi.Rect("On", sw);
                        ShowUi.Place(dot, new Vector2(0.5f, 0.5f), new Vector2(16f, 16f), Vector2.zero);
                        ShowUi.Panel(dot, ColorRole.Ink).raycastTarget = false;
                    }
                    Track((wall ? "wall_" : "floor_") + c, sw);
                }
            }
        }

        // ---------- the ui ----------

        void Track(string name, RectTransform rt)
        {
            _targets[name] = rt;
            _dynamic.Add(name);
        }

        /// <summary>Lets a long piece name shrink to fit its button instead of spilling out.</summary>
        static void FitLabel(ShowButton b)
        {
            TMP_Text t = b.Go.GetComponentInChildren<TMP_Text>(true);
            if (t == null) return;
            t.fontSizeMax = t.fontSize; t.fontSizeMin = 16f;
            t.enableAutoSizing = true;
        }

        static void NoNav(ShowButton b)
        {
            Selectable sel = b.Go.GetComponent<Selectable>();
            if (sel != null) sel.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        ShowButton Btn(RectTransform parent, string name, string label, bool ghost, Action act, float x, float w)
        {
            RectTransform box = ShowUi.Rect(name, parent);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(w, 56f);
            box.anchoredPosition = new Vector2(x, 0f);
            ShowButton sb = ShowUi.Button(_ui.Assets != null ? _ui.Assets.buttonPrefab : null, box, name, label, ghost, () => { act(); SelectNone(); });
            ShowUi.Stretch(sb.Rect);
            NoNav(sb);
            _targets["bar_" + name] = box;
            return sb;
        }

        void BuildUi()
        {
            if (_bar != null) return;
            RectTransform root = _ui.CanvasRect;
            // the "decorate" button for the mouse, beside the way she walks
            _decorBtn = ShowUi.Rect("DecorateButton", root);
            ShowUi.Place(_decorBtn, new Vector2(0.5f, 0f), new Vector2(210f, 60f), new Vector2(0f, 34f));
            ShowButton db = ShowUi.Button(_ui.Assets != null ? _ui.Assets.buttonPrefab : null, _decorBtn, "Decorate", HomeRules.Decorate, false, () => { EnterDecorate(); SelectNone(); });
            ShowUi.Stretch(db.Rect);
            NoNav(db);
            _targets["decorate"] = _decorBtn;
            _decorBtn.gameObject.SetActive(false);

            // the tool bar
            _bar = ShowUi.Rect("DecorBar", root);
            ShowUi.Place(_bar, new Vector2(0.5f, 0f), new Vector2(960f, 76f), new Vector2(0f, 46f));
            ShowUi.Panel(_bar, ColorRole.Card, 0.96f).raycastTarget = true;
            float w = 140f, g = 10f, x0 = -(6 * w + 5 * g) / 2f + w / 2f;
            Btn(_bar, "box", "box", false, () => { if (_boxOpen) CloseBox(); else OpenBox(); }, x0, w);
            _bTurn = Btn(_bar, "turn", "turn", true, Rotate, x0 + (w + g), w);
            _bAway = Btn(_bar, "away", "put away", true, PutAway, x0 + 2 * (w + g), w);
            _bDrop = Btn(_bar, "drop", "drop", false, () => { if (_held != null) Drop(); else if (_cursor >= 0) Lift(_cursor); }, x0 + 3 * (w + g), w);
            Btn(_bar, "colors", "colors", true, () => { if (_colorOpen) CloseColor(); else OpenColor(); }, x0 + 4 * (w + g), w);
            Btn(_bar, "done", "done", true, ExitDecorate, x0 + 5 * (w + g), w);

            // what to press
            _hintRt = ShowUi.Rect("DecorHint", root);
            ShowUi.Place(_hintRt, new Vector2(0.5f, 1f), new Vector2(820f, 50f), new Vector2(0f, -150f));
            ShowUi.Panel(_hintRt, ColorRole.Card, 0.92f);
            _hint = ShowUi.Label(_hintRt, "Text", HomeRules.KeysLine, 24f, FontRole.BodyBold, ColorRole.Ink);

            _boxRt = ShowUi.Rect("DecorBox", root);
            ShowUi.Place(_boxRt, new Vector2(0.5f, 0f), new Vector2(1300f, 196f), new Vector2(0f, 154f));   // bottom edge stays at 56
            ShowUi.Panel(_boxRt, ColorRole.Card, 0.96f).raycastTarget = true;
            _boxRt.gameObject.SetActive(false);

            _colorRt = ShowUi.Rect("DecorColors", root);
            ShowUi.Place(_colorRt, new Vector2(0.5f, 0f), new Vector2(1300f, 170f), new Vector2(0f, 136f));
            ShowUi.Panel(_colorRt, ColorRole.Card, 0.96f).raycastTarget = true;
            _colorRt.gameObject.SetActive(false);
            _bar.gameObject.SetActive(false);
            _hintRt.gameObject.SetActive(false);
        }

        void Saved()
        {
            if (Boot.Store != null) Boot.Store.Save();
            if (WorldGame.TestMode) LogLayout();
            LogState();
        }

        // ---------- test logs (only with ?wt_test) ----------

        void LogLayout()
        {
            JsonOut(HomeJson.Write(Blob.Home));
        }

        static void JsonOut(WorldTour.Core.Maths.Gen.JsonValue v)
        {
            Debug.Log("WT_HOME_LAYOUT " + (v == null ? "{}" : v.ToCanonicalJson()));
        }

        void LogState()
        {
            if (!WorldGame.TestMode || Blob == null) return;
            int boxed = 0;
            foreach (HomePiece p in HomeCatalogue.All) boxed += Mathf.Max(0, HomeRules.Boxed(Blob, p.Id));
            Debug.Log("WT_HOME_STATE room=" + _room + " deco=" + _deco + " held=" + (_held != null ? _heldId : "none") + " x=" + _hx + " z=" + _hz + " rot=" + _hrot
                + " check=" + _hcheck + " cursor=" + _cursor + " placed=" + Blob.Home.Placed.Count + " boxed=" + boxed + " coins=" + CoinRules.Coins(Blob)
                + " box=" + _boxOpen + " colors=" + _colorOpen);
        }

        void LogTargets(Camera cam)
        {
            _sb.Length = 0;
            _sb.Append("WT_HOME_TARGETS");
            foreach (KeyValuePair<string, RectTransform> kv in _targets)
            {
                if (kv.Value == null || !kv.Value.gameObject.activeInHierarchy) continue;
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, kv.Value.position);
                _sb.Append(' ').Append(kv.Key).Append('=').Append(Mathf.RoundToInt(sp.x).ToString(CultureInfo.InvariantCulture)).Append(':')
                   .Append(Mathf.RoundToInt(Screen.height - sp.y).ToString(CultureInfo.InvariantCulture));
            }
            foreach (Spot s in _spots)
            {
                Vector3 a = cam.WorldToScreenPoint(s.T.position + Vector3.up * s.ClickLift);
                _sb.Append(' ').Append(s.Key).Append('=').Append(Mathf.RoundToInt(a.x).ToString(CultureInfo.InvariantCulture)).Append(':')
                   .Append(Mathf.RoundToInt(Screen.height - a.y).ToString(CultureInfo.InvariantCulture));
            }
            if (_deco)
            {
                SaveBlob b = Blob;
                for (int i = 0; i < b.Home.Placed.Count; i++)
                {
                    HomeItem it = b.Home.Placed[i];
                    if (it.Room != _room) continue;
                    HomePiece p = HomeCatalogue.ById(it.Id);
                    int w, d;
                    HomeRules.Footprint(p, it.Rot, out w, out d);
                    int z = p.Layer == HomeLayer.Wall ? HomeRules.Cells - 1 : it.Z;
                    float lift = p.Layer == HomeLayer.Wall ? 1.4f : p.Layer == HomeLayer.Rug ? 0.05f : Mathf.Max(0.3f, p.H * 0.5f);
                    Vector3 a = cam.WorldToScreenPoint(new Vector3(HomeRules.WorldX(it.X + w * 0.5f), lift, HomeRules.WorldZ(z + d * 0.5f)));
                    _sb.Append(" item").Append(i).Append('=').Append(Mathf.RoundToInt(a.x).ToString(CultureInfo.InvariantCulture)).Append(':')
                       .Append(Mathf.RoundToInt(Screen.height - a.y).ToString(CultureInfo.InvariantCulture)).Append(':').Append(it.Id);
                }
            }
            Debug.Log(_sb.ToString());
        }

        /// <summary>Test only: logs where a room cell is on the screen ("WT_HOME_CELL x z sx sy").</summary>
        public void TestCell(int x, int z)
        {
            Camera cam = _game != null ? _game.Cam : null;
            if (cam == null) return;
            Vector3 a = cam.WorldToScreenPoint(new Vector3(HomeRules.WorldX(x + 0.5f), 0f, HomeRules.WorldZ(z + 0.5f)));
            Debug.Log("WT_HOME_CELL " + x + " " + z + " " + Mathf.RoundToInt(a.x).ToString(CultureInfo.InvariantCulture) + " " + Mathf.RoundToInt(Screen.height - a.y).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Test only (the memory test): twelve rounds of what a player does: take a piece out, drop it, put it away, repaint a wall or floor.</summary>
        IEnumerator Churn()
        {
            SaveBlob b = Blob;
            EnterDecorate();
            var gap = new WaitForSecondsRealtime(0.12f);
            yield return gap;
            for (int i = 0; i < 12; i++)
            {
                HomePiece p = HomeCatalogue.All[(i * 5) % HomeCatalogue.All.Count];
                if (HomeRules.Boxed(b, p.Id) <= 0) HomeRules.Grant(b, p.Id);
                TakeOut(p);
                yield return gap;
                if (_held != null && !Drop()) Cancel();
                yield return gap;
                if (_held == null && _cursor >= 0) PutAway();
                SetColor(i % 2 == 0, (i * 3) % AvatarCatalogue.PaletteHex.Count);
                yield return gap;
            }
            ExitDecorate();
            Debug.Log("WT_HOME_CHURN_DONE");
        }

        /// <summary>Test only: "grant" every piece and 1000 coins, "deco" opens decorating, "layout" and "state" log, "room:n" goes to a room.</summary>
        public void TestCommand(string cmd)
        {
            if (!WorldGame.TestMode || Blob == null) return;
            if (cmd == "grant")
            {
                foreach (HomePiece p in HomeCatalogue.All) if (HomeRules.Boxed(Blob, p.Id) < 1) HomeRules.Grant(Blob, p.Id);
                Blob.Fans.Coins += 1000;
                if (Boot.Store != null) Boot.Store.Save();
                LogLayout(); LogState();
            }
            else if (cmd == "churn" && _inside && !_deco) StartCoroutine(Churn());
            else if (cmd == "deco") EnterDecorate();
            else if (cmd == "layout") LogLayout();
            else if (cmd == "state") LogState();
            else if (cmd.StartsWith("room:") && _inside)
            {
                int r;
                if (int.TryParse(cmd.Substring(5), out r) && HomeRules.RoomUnlocked(Blob, r)) { ShowRoom(r); if (_game.Player != null) _game.Player.Place(Vector3.zero); }
            }
            else if (cmd.StartsWith("cell:"))
            {
                string[] p = cmd.Substring(5).Split(':');
                int x, z;
                if (p.Length == 2 && int.TryParse(p[0], out x) && int.TryParse(p[1], out z)) TestCell(x, z);
            }
        }
    }
}
