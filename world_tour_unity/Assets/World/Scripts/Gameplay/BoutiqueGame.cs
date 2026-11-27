using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game;
using WorldTour.Game.Audio;
using WorldTour.Game.Prefs;
using WorldTour.World.Wardrobe;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// Phase 10B.1: the clothing boutique. A door on the coffee street ("BoutiqueDoor", in the front of the shop row) leads to its own small scene,
    /// Interior_Boutique, where one scene is loaded at a time as for a travel. Inside, by the names the scene gives its empties: a shopkeeper
    /// ("Shopkeeper"), seven racks ("Rack_top", "Rack_bottom", "Rack_shoes", "Rack_hair", "Rack_hat", "Rack_glasses", "Rack_bag"), a mirror ("Mirror",
    /// with a "Stand" child where she stands) and the way out ("DoorOut", a mat at the front).
    /// Everything works by mouse (click a rack, the mirror, the shopkeeper, or the door mat) and by keyboard (walk up and press E; the doors also
    /// open by walking onto them). A rack, the mirror or the shopkeeper opens the same store: cards with a name, a price and a few buttons, while the
    /// camera comes in on her at the mirror and she wears what she is trying (<see cref="AvatarPreview.TryOn"/>; null puts the saved look back).
    /// Buying is <see cref="CoinRules.TrySpend"/> then <see cref="AvatarRules.Grant"/> (see <see cref="BoutiqueRules.Buy"/>), fans are never touched,
    /// nothing bought is ever taken away. Log lines: WT_BOUTIQUE_DOOR, WT_BOUTIQUE_OPEN, WT_BOUTIQUE_RACK, WT_BOUTIQUE_ITEM, WT_BOUTIQUE_BUY, WT_BOUTIQUE_CLOSE;
    /// test only (page URL has wt_test): WT_BOUTIQUE_TARGETS with each spot's screen position.
    /// </summary>
    public sealed class BoutiqueGame : MonoBehaviour
    {
        public const float DoorRadius = 1.1f, SpotRange = 2.6f;
        const float CloseDistance = 4.4f, CloseHeight = 1.5f, CloseFov = 38f;

        public static BoutiqueGame Instance { get; private set; }

        sealed class Spot
        {
            public string Key, Label, Verb = "look";
            public Transform T;
            public Vector3 Stand;
            /// <summary>A door's spot just outside, where the save keeps her while she is inside.</summary>
            public Vector3 Return;
            public float Range = SpotRange, PinLift = 2.4f, ClickLift = 1.1f;
            public bool Auto, Armed = true;
            public Action Act;
        }

        WorldGame _game;
        WorldUi _ui;
        readonly List<Spot> _spots = new List<Spot>();
        Spot _pending;
        string _interior = "";
        float _nextTargets;
        const int MaxTags = 4;
        readonly List<Vector3> _shown = new List<Vector3>();

        // the store
        bool _trying;
        int _slot, _item, _color, _pattern;
        Transform _mirrorStand;
        Coroutine _cam;
        DogFollower _dog;
        float _savedFov = 44f;

        public bool IsOpen { get { return _trying; } }
        public bool Inside { get { return _interior == "boutique"; } }

        static SaveBlob Blob { get { return Boot.Store != null ? Boot.Store.Blob : null; } }

        public void Init(WorldGame game, WorldUi ui)
        {
            Instance = this;
            _game = game; _ui = ui;
            var prev = WorldInput.ClickUsed;
            WorldInput.ClickUsed = p => OnClick(p) || (prev != null && prev(p));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        // ---------- a place loaded ----------

        /// <summary>After every scene loads (WorldGame.Bind): find this scene's doors, racks and mirror by name.</summary>
        public void PlaceLoaded(string placeId, string interior)
        {
            _interior = interior ?? "";
            _spots.Clear();
            _pending = null;
            _trying = false;
            _mirrorStand = null;
            if (_cam != null) { StopCoroutine(_cam); _cam = null; }
            if (_ui != null) _ui.CardOffset = Vector2.zero;
            if (_interior == "boutique") FindInside();
            else if (placeId == "coffee") FindDoor();
            Vector3 p = _game.Player != null ? _game.Player.transform.position : Vector3.zero;
            foreach (Spot s in _spots) if (s.Auto) s.Armed = Flat(p, s.T.position) > s.Range + 0.6f;   // never opens a door she arrived beside
            Debug.Log("WT_BOUTIQUE_SPOTS place=" + placeId + " interior=" + (_interior.Length > 0 ? _interior : "none") + " spots=" + _spots.Count);
        }

        static Transform Find(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        static Vector3 StandOf(Transform t, Vector3 fallback)
        {
            Transform s = t.Find("Stand");
            return s != null ? s.position : fallback;
        }

        void FindDoor()
        {
            Transform door = Find("BoutiqueDoor");
            if (door == null) return;
            _spots.Add(new Spot
            {
                Key = "boutique_door", Label = BoutiqueRules.DoorOutside, Verb = "go in", T = door, Stand = door.position, Return = StandOf(door, door.position + new Vector3(0.4f, 0f, -1.6f)),
                Range = DoorRadius, Auto = true, PinLift = 3.3f, ClickLift = 1.2f, Act = AskEnter,
            });
        }

        void FindInside()
        {
            Transform mat = Find("DoorOut");
            if (mat != null)
                _spots.Add(new Spot { Key = "door_out", Label = "exit", Verb = "go out", T = mat, Stand = mat.position, Range = 1.3f, Auto = true, PinLift = 3.0f, ClickLift = 0.2f, Act = AskLeave });
            Transform keeper = Find("Shopkeeper");
            if (keeper != null)
                _spots.Add(new Spot { Key = "shopkeeper", Label = BoutiqueRules.Shopkeeper, Verb = "talk", T = keeper, Stand = keeper.position + new Vector3(-0.5f, 0f, -1.8f), Range = 2.8f, PinLift = 2.9f, ClickLift = 1.0f, Act = Talk });
            Transform mirror = Find("Mirror");
            if (mirror != null)
            {
                _mirrorStand = mirror.Find("Stand");
                _spots.Add(new Spot { Key = "mirror", Label = BoutiqueRules.Mirror, Verb = "try on", T = mirror, Stand = StandOf(mirror, mirror.position + new Vector3(0f, 0f, -2.0f)), Range = 3.4f, PinLift = 2.7f, ClickLift = 1.3f, Act = () => OpenRack(_slot) });
            }
            for (int i = 0; i < BoutiqueRules.RackSlots.Count; i++)
            {
                string slot = BoutiqueRules.RackSlots[i];
                Transform rack = Find("Rack_" + slot);
                if (rack == null) continue;
                int idx = i;
                _spots.Add(new Spot { Key = "rack_" + slot, Label = BoutiqueRules.RackName(slot), Verb = "look", T = rack, Stand = StandOf(rack, rack.position + new Vector3(0f, 0f, -1.7f)), PinLift = 2.3f, ClickLift = 1.0f, Act = () => OpenRack(idx) });
            }
        }

        // ---------- near a door, a rack, the mirror ----------

        static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        void Update()
        {
            if (_game == null || _spots.Count == 0) return;
            Camera cam = _game.Cam;
            if (WorldGame.TestMode && cam != null && Time.unscaledTime >= _nextTargets)
            {
                _nextTargets = Time.unscaledTime + 0.4f;
                LogTargets(cam);
            }
            if (!_game.IsFree || _trying || _game.Player == null) return;
            Vector3 p = _game.Player.transform.position;
            Keyboard k = Keyboard.current;
            bool eKey = k != null && k.eKey.wasPressedThisFrame;
            Spot nearest = null; float best = float.MaxValue;
            _shown.Clear();
            var order = new List<Spot>(_spots);
            order.Sort((a, b) => Flat(p, a.T.position).CompareTo(Flat(p, b.T.position)));
            foreach (Spot s in order)
            {
                float d = Flat(p, s.T.position);
                bool near = d <= s.Range;
                if (near && d < best) { best = d; nearest = s; }
                if (cam != null)
                {
                    // only the nearest few name tags, and never two that would touch: nothing overlaps and nothing is cut off at the edge
                    Vector3 sp = cam.WorldToScreenPoint(s.T.position + Vector3.up * s.PinLift);
                    string text = near ? s.Label + " - e " + s.Verb : s.Label;
                    float w = Mathf.Clamp(40f + 14.5f * text.Length, 150f, 360f);
                    bool onScreen = sp.z > 0f && sp.x > w * 0.5f + 20f && sp.x < Screen.width - w * 0.5f - 20f && sp.y > 60f && sp.y < Screen.height - 110f;
                    bool show = onScreen && _shown.Count < MaxTags;
                    if (show)
                        foreach (Vector3 o in _shown)
                            if (Mathf.Abs(o.x - sp.x) < (o.z + w) * 0.5f + 12f && Mathf.Abs(o.y - sp.y) < 64f) { show = false; break; }
                    if (show) _shown.Add(new Vector3(sp.x, sp.y, w));
                    _ui.SetPin("b_" + s.Key, text, sp, show);
                }
                if (s.Auto)
                {
                    if (s.Armed && d < s.Range * 0.85f) { s.Armed = false; s.Act(); return; }
                    if (!s.Armed && d > s.Range + 1.2f) s.Armed = true;
                }
            }
            if (eKey && nearest != null) { _pending = null; nearest.Act(); return; }
            if (_pending != null)
            {
                float dp = Flat(p, _pending.T.position);
                if (dp <= _pending.Range - 0.2f) { Spot s = _pending; _pending = null; s.Act(); }
                else if (!_game.Player.HasTarget)
                {
                    // she has walked to the spot's stand and stopped: that is "there", even if the stand is a little farther than the key range (the mirror)
                    Spot s = _pending; _pending = null;
                    if (dp <= s.Range + 1.5f) s.Act();
                }
            }
        }

        bool OnClick(Vector2 screen)
        {
            if (_spots.Count == 0 || _trying || _game == null || !_game.IsFree || _game.Cam == null) return false;
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

        void LogTargets(Camera cam)
        {
            var sb = new System.Text.StringBuilder("WT_BOUTIQUE_TARGETS");
            foreach (Spot s in _spots)
            {
                Vector3 a = cam.WorldToScreenPoint(s.T.position + Vector3.up * s.ClickLift);
                sb.Append(' ').Append(s.Key).Append('=').Append(Mathf.RoundToInt(a.x).ToString(CultureInfo.InvariantCulture)).Append(':')
                  .Append(Mathf.RoundToInt(Screen.height - a.y).ToString(CultureInfo.InvariantCulture))
                  .Append(";d=").Append(Flat(_game.Player.transform.position, s.T.position).ToString("0.0", CultureInfo.InvariantCulture));
            }
            Debug.Log(sb.ToString());
        }

        // ---------- the doors ----------

        void AskEnter()
        {
            _game.BeginCard();
            Debug.Log("WT_BOUTIQUE_DOOR ask=enter");
            Spot door = _spots.Find(s => s.Key == "boutique_door");
            var buttons = new List<DialogButton>
            {
                new DialogButton { Label = BoutiqueRules.DoorGoIn, OnClick = () => _game.GoThroughDoor(WorldGame.BoutiqueScene, door != null ? door.Return : (Vector3?)null) },
                new DialogButton { Label = BoutiqueRules.DoorStay, Ghost = true, OnClick = _game.EndCard },
            };
            _ui.OpenDialog(BoutiqueRules.DoorOutside, BoutiqueRules.DoorLine, null, buttons, null);
        }

        void AskLeave()
        {
            _game.BeginCard();
            Debug.Log("WT_BOUTIQUE_DOOR ask=leave");
            var buttons = new List<DialogButton>
            {
                new DialogButton { Label = BoutiqueRules.LeaveGo, OnClick = () => _game.LeaveInterior() },
                new DialogButton { Label = BoutiqueRules.LeaveStay, Ghost = true, OnClick = _game.EndCard },
            };
            _ui.OpenDialog("the street", BoutiqueRules.LeaveLine, null, buttons, null);
        }

        void Talk()
        {
            _game.BeginCard();
            Spot keeper = _spots.Find(s => s.Key == "shopkeeper");
            if (keeper != null) _game.Player.Face(keeper.T.position);
            var buttons = new List<DialogButton>
            {
                new DialogButton { Label = BoutiqueRules.Look, OnClick = () => OpenRack(_slot) },
                new DialogButton { Label = "bye", Ghost = true, OnClick = _game.EndCard },
            };
            _ui.OpenDialog(BoutiqueRules.Shopkeeper, BoutiqueRules.Welcome, BoutiqueRules.WelcomeLooking, buttons, null);
        }

        // ---------- the store: a rack, then its items ----------

        void OpenRack(int slotIndex)
        {
            if (Blob == null) return;
            _slot = Mathf.Clamp(slotIndex, 0, BoutiqueRules.RackSlots.Count - 1);
            if (!_trying) StartTrying();
            ShowRack();
        }

        /// <summary>Test only (WorldGame.TestBoutique): opens a rack, steps through <paramref name="steps"/> items and colors as a player's clicks would, and closes the store.</summary>
        public void TestBrowse(int steps) { if (Blob != null && !_trying) StartCoroutine(BrowseRoutine(steps)); }

        bool _heapLog;

        IEnumerator BrowseRoutine(int steps)
        {
            _heapLog = true;
            Heap("open");
            OpenRack(0);
            Heap("rack");
            OpenItems();
            Heap("items");
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForSecondsRealtime(0.2f);   // one click at a time, as a player does, so the garbage collector gets its frames
                _item++; _color = (_color + 1) % AvatarCatalogue.PaletteHex.Count; ShowItem(); Heap("step" + i);
            }
            yield return new WaitForSecondsRealtime(0.2f);
            Finish();
            Heap("closed");
            _heapLog = false;
        }

        static void Heap(string tag) { Debug.Log("WT_BROWSE_HEAP " + tag + " monoUsed=" + (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() >> 20) + " monoHeap=" + (UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() >> 20) + " t=" + Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f)); }

        void StartTrying()
        {
            _trying = true;
            _pending = null;
            _game.BeginCard();
            _ui.CardOffset = new Vector2(190f, -40f);   // the card sits right of the mirror view, so she stays in sight
            Debug.Log("WT_BOUTIQUE_OPEN coins=" + CoinRules.Coins(Blob));
            if (_cam != null) StopCoroutine(_cam);
            _cam = StartCoroutine(MirrorView());
        }

        void StopTrying()
        {
            if (!_trying) return;
            _trying = false;
            AvatarPreview.TryOn(null);   // the saved look again
            _ui.CardOffset = Vector2.zero;
            Debug.Log("WT_BOUTIQUE_CLOSE coins=" + CoinRules.Coins(Blob));
            if (_cam != null) StopCoroutine(_cam);
            _cam = StartCoroutine(ReturnView());
        }

        /// <summary>The camera comes in on her at the mirror, her a little left of the middle so the cards have the rest of the screen.</summary>
        IEnumerator MirrorView()
        {
            Camera cam = _game.Cam;
            FollowCamera follow = _game.Follow;
            if (cam == null || _game.Player == null) yield break;
            if (follow != null) follow.frozen = true;
            _savedFov = cam.fieldOfView;
            Vector3 stand = _mirrorStand != null ? _mirrorStand.position : _game.Player.transform.position;
            stand.y = 0f;
            _game.Player.Place(stand);
            _game.Player.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            _dog = _game.Dog;
            if (_dog != null)
            {
                // riff sits beside her (on the viewer's right) instead of walking in front of the lens
                Vector3 dp; Quaternion dr;
                WorldShop.DogPose(stand, Vector3.back, 0f, out dp, out dr);
                _dog.enabled = false;
                _dog.transform.SetPositionAndRotation(dp, dr);
            }
            Vector3 pos = stand + Vector3.back * CloseDistance + Vector3.up * CloseHeight;
            Quaternion rot = Quaternion.LookRotation((stand + Vector3.right * 1.7f + Vector3.up * 0.95f) - pos, Vector3.up);
            Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            float dur = GameSettings.ReducedMotion ? 0.01f : 0.55f, t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                cam.transform.position = Vector3.Lerp(p0, pos, e);
                cam.transform.rotation = Quaternion.Slerp(r0, rot, e);
                cam.fieldOfView = Mathf.Lerp(f0, CloseFov, e);
                yield return null;
            }
            cam.transform.position = pos; cam.transform.rotation = rot; cam.fieldOfView = CloseFov;
            _cam = null;
        }

        IEnumerator ReturnView()
        {
            Camera cam = _game.Cam;
            FollowCamera follow = _game.Follow;
            if (cam != null && follow != null)
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
            if (_dog != null) { _dog.enabled = true; _dog = null; }
            _cam = null;
        }

        string Slot { get { return BoutiqueRules.RackSlots[_slot]; } }

        /// <summary>A rack's card: which rack, what she has, and the way into its items.</summary>
        void ShowRack()
        {
            SaveBlob s = Blob;
            AvatarPreview.TryOn(null);
            Debug.Log("WT_BOUTIQUE_RACK slot=" + Slot + " owned=" + BoutiqueRules.Owned(s, Slot));
            var buttons = new List<DialogButton>
            {
                new DialogButton { Label = BoutiqueRules.Look, OnClick = OpenItems },
                new DialogButton { Label = BoutiqueRules.OtherRack, Ghost = true, Closes = false, OnClick = () => { _slot = (_slot + 1) % BoutiqueRules.RackSlots.Count; ShowRack(); } },
                new DialogButton { Label = BoutiqueRules.Done, Ghost = true, OnClick = Finish },
            };
            _ui.OpenDialog(BoutiqueRules.RackName(Slot), BoutiqueRules.RackLine(Slot), BoutiqueRules.Wallet(CoinRules.Coins(s)) + " - " + BoutiqueRules.Owned(s, Slot) + " yours", buttons, null, true, 640f);
        }

        void OpenItems()
        {
            SaveBlob s = Blob;
            List<AvatarItem> rack = BoutiqueRules.Rack(Slot);
            AvatarPick worn = AvatarRules.Look(s).Get(Slot);
            _item = Mathf.Max(0, rack.FindIndex(i => worn != null && i.Id == worn.Id));
            _color = worn != null ? worn.Color : 0;
            _pattern = worn != null ? worn.Pattern : 0;
            ShowItem();
        }

        /// <summary>One item: its name, its price or "yours", the mirror shows it on her. Buttons: buy or wear, before, next, colour, pattern, back.</summary>
        void ShowItem()
        {
            SaveBlob s = Blob;
            List<AvatarItem> rack = BoutiqueRules.Rack(Slot);
            _item = (_item % rack.Count + rack.Count) % rack.Count;
            AvatarItem it = rack[_item];
            AvatarPick worn = AvatarRules.Look(s).Get(Slot);
            AvatarPick shown = new AvatarPick(it.Id, AvatarCatalogue.TakesColor(Slot) ? _color : 0, AvatarCatalogue.TakesPattern(Slot) ? _pattern : 0);
            if (_heapLog) Heap("beforeTryOn");
            AvatarPreview.TryOn(BoutiqueRules.TryLook(s, it, shown.Color, shown.Pattern));
            if (_heapLog) Heap("afterTryOn");
            ItemState st = BoutiqueRules.StateOf(s, it);
            if (st == ItemState.Wearing && !shown.Same(worn)) st = ItemState.Yours;   // her item in another colour: she can still put that on
            Debug.Log("WT_BOUTIQUE_ITEM id=" + it.Id + " state=" + st + " price=" + it.Price + " color=" + shown.Color + " pattern=" + shown.Pattern + " coins=" + CoinRules.Coins(s));

            var buttons = new List<DialogButton>();
            switch (st)
            {
                case ItemState.CanBuy:
                    buttons.Add(new DialogButton { Label = BoutiqueRules.BuyLabel(it.Price), Closes = false, OnClick = () => Buy(it) });
                    break;
                case ItemState.TooMuch:
                    buttons.Add(new DialogButton { Label = BoutiqueRules.NeedLabel(it.Price - CoinRules.Coins(s)), Closes = false, OnClick = () => { } });
                    break;
                case ItemState.Yours:
                    buttons.Add(new DialogButton { Label = BoutiqueRules.WearIt, Closes = false, OnClick = () => WearIt(it) });
                    break;
                default:
                    buttons.Add(new DialogButton { Label = BoutiqueRules.OnNow, Closes = false, OnClick = () => { } });
                    break;
            }
            buttons.Add(new DialogButton { Label = BoutiqueRules.Before, Ghost = true, Closes = false, OnClick = () => { _item--; ShowItem(); } });
            buttons.Add(new DialogButton { Label = BoutiqueRules.Next, Ghost = true, Closes = false, OnClick = () => { _item++; ShowItem(); } });
            bool none = AvatarCatalogue.Key(it.Id) == "none";
            if (AvatarCatalogue.TakesColor(Slot) && !none)
                buttons.Add(new DialogButton { Label = BoutiqueRules.Colour, Ghost = true, Closes = false, OnClick = () => { _color = (_color + 1) % AvatarCatalogue.PaletteHex.Count; ShowItem(); } });
            if (AvatarCatalogue.TakesPattern(Slot))
                buttons.Add(new DialogButton { Label = "pattern", Ghost = true, Closes = false, OnClick = () => { _pattern = (_pattern + 1) % AvatarCatalogue.PatternNames.Count; ShowItem(); } });
            buttons.Add(new DialogButton { Label = BoutiqueRules.Back, Ghost = true, OnClick = ShowRack });

            string sub;
            switch (st)
            {
                case ItemState.CanBuy: sub = BoutiqueRules.Price(it.Price); break;
                case ItemState.TooMuch: sub = BoutiqueRules.Price(it.Price) + " - " + BoutiqueRules.Wallet(CoinRules.Coins(s)); break;
                case ItemState.Yours: sub = "yours"; break;
                default: sub = "yours - on now"; break;
            }
            if (!none && AvatarCatalogue.TakesColor(Slot))
            {
                string pat = AvatarCatalogue.TakesPattern(Slot) && shown.Pattern > 0 ? " " + AvatarCatalogue.PatternNames[shown.Pattern] : "";
                sub += " - " + AvatarCatalogue.PaletteNames[shown.Color] + pat;
            }
            _ui.OpenDialog(BoutiqueRules.RackName(Slot), it.Name, sub, buttons, null, true, 820f);
            if (_heapLog) Heap("afterDialog");
        }

        void Buy(AvatarItem it)
        {
            SaveBlob s = Blob;
            BuyResult r = BoutiqueRules.Buy(s, it.Id, _color, _pattern);
            Debug.Log("WT_BOUTIQUE_BUY id=" + it.Id + " result=" + r + " coins=" + CoinRules.Coins(s) + " fans=" + s.Fans.Lifetime + "/" + s.Fans.Balance);
            if (r.Ok)
            {
                if (Boot.Store != null) Boot.Store.Save();
                Sfx.Play(SfxId.Strum);
            }
            ShowItem();
        }

        void WearIt(AvatarItem it)
        {
            SaveBlob s = Blob;
            bool ok = BoutiqueRules.WearOwned(s, it.Id, _color, _pattern);
            Debug.Log("WT_BOUTIQUE_WEAR id=" + it.Id + " ok=" + ok);
            if (ok && Boot.Store != null) Boot.Store.Save();
            ShowItem();
        }

        void Finish()
        {
            StopTrying();
            _game.EndCard();
        }
    }
}
