using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Game.Audio;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: remix (world_tour/modes/spelling/remix.js). Tap the letter stickers in order to spell the word. Level 2 adds
    /// one decoy letter, level 3 adds two. Hint: clears any wrong letters, then glows the next right tile (pressing again
    /// while one is glowing is free). Misses follow the spelling ladder; the third shows the word.
    /// Input: click a tile (as the web game), drag it onto the slots, or type its letter (Backspace = undo, Delete = clear);
    /// every button and tile can also be reached with Tab and pressed with Enter or Space.
    /// Wrong answers wobble the slots and thud; nothing turns red.
    /// </summary>
    public sealed class RemixMode : MonoBehaviour, ISongMode
    {
        private SongContext _ctx;
        private RemixBoard _board;
        private RemixDeal _deal;
        private readonly List<LetterTile> _tiles = new List<LetterTile>();
        private readonly List<Image> _slotBg = new List<Image>();
        private readonly List<TMP_Text> _slotText = new List<TMP_Text>();
        private readonly List<WorldTour.Game.Map.UiLine> _slotDash = new List<WorldTour.Game.Map.UiLine>();
        private RectTransform _slotRow;
        private readonly HashSet<int> _final = new HashSet<int>();
        private Keyboard _keyboard;

        public RemixBoard Board { get { return _board; } }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            string word = ctx.Item.W;
            _deal = RemixRules.Deal(word, ctx.Level, ctx.Random);       // remix.js:19-22
            _board = new RemixBoard(word, _deal.Mix) { Syllables = ctx.Item.S };
            _round = new RemixRound(_board, new Host(ctx, _board), new View(this));
            Build(ctx.Area, word);
            Paint(-1);
            ctx.Later(LogRack, .45f);
            // remix.js:111-122
            ctx.SetupHints(RemixRules.HintMax(word), n =>
            {
                RemixHint h = _board.Hint();
                if (h.Line == null) return false;                       // busy: nothing at all
                Paint(-1);
                ctx.Say(h.Line, "think");
                return h.Counted;
            });
        }

        /// <summary>
        /// For the browser tests' mouse-only pass: "WT_RACK letters=tac pos=x:y,..." with each tile's centre in page
        /// pixels from the top left (an overlay canvas's world units are screen pixels; y is flipped).
        /// </summary>
        private void LogRack()
        {
            if (this == null || _tiles.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            var sb = new System.Text.StringBuilder("WT_RACK letters=");
            foreach (char c in _board.Mix) sb.Append(c);
            sb.Append(" pos=");
            var corners = new Vector3[4];
            for (int i = 0; i < _tiles.Count; i++)
            {
                ((RectTransform)_tiles[i].transform).GetWorldCorners(corners);
                Vector3 c = (corners[0] + corners[2]) * .5f;
                if (i > 0) sb.Append(',');
                sb.Append(Mathf.RoundToInt(c.x)).Append(':').Append(Mathf.RoundToInt(UnityEngine.Screen.height - c.y));
            }
            Debug.Log(sb.ToString());
        }

        // ---------- play (the rules are in RemixRound) ----------

        private RemixRound _round;

        public RemixRound Round { get { return _round; } }

        private void OnTile(LetterTile t)
        {
            if (_round == null) return;
            _round.Pick(t.Index);                                        // logs WT_PICK, taken or not
        }

        private void Undo()
        {
            if (_round != null && _round.Undo()) Sfx.Play(SfxId.Tap);    // remix.js:108
        }

        private void Clear()
        {
            if (_round != null && _round.Clear()) Sfx.Play(SfxId.Tap);   // remix.js:109
        }

        /// <summary>The show side of a round: the song context, unscaled time, and the console.</summary>
        private sealed class Host : IRemixHost
        {
            private readonly SongContext _c;
            private readonly RemixBoard _b;
            public Host(SongContext c, RemixBoard b) { _c = c; _b = b; }
            public bool Open { get { return _c.Open; } }
            public bool Resolved { get { return _c.Engine.Resolved; } }
            public int Tries { get { return _c.Tries; } }
            public float Now { get { return Time.unscaledTime; } }
            public int Miss() { return _c.Miss(); }
            public bool Win() { return _c.Win(); }
            public bool Reveal() { return _c.Reveal(); }
            public void Say(string line, string pose) { _c.Say(line, pose); }
            public void Later(System.Action action, float seconds) { _c.Later(action, seconds); }
            public void Next(float seconds) { _c.Next(seconds); }
            public void BlockHints(bool blocked) { _c.BlockHints(blocked); }
            // QUESTIONS 11 / task Q tester D1: the ladder counts the glows she has seen, and the glows count the ladder
            public string Ladder(int tries) { return _b.LadderLine(tries, _c.Random); }
            public void Log(string line) { Debug.Log(line); }
            public void Error(System.Exception e) { Debug.LogException(e); }
        }

        /// <summary>The drawing side of a round.</summary>
        private sealed class View : IRemixView
        {
            private readonly RemixMode _m;
            public View(RemixMode m) { _m = m; }

            public void Leave(int tile, System.Action landed)
            {
                LetterTile t = _m._tiles[tile];
                t.interactable = false;
                Feedback.Shrink(t.Body, landed);                         // remix.js:69-74 (paint once it has left the rack)
            }

            public void Paint(int landed) { _m.Paint(landed); }

            public void Wobble() { Feedback.Wobble(_m._slotRow); }       // show.js:163 wobble [data-wobble]

            public void LockTiles() { foreach (LetterTile x in _m._tiles) x.interactable = false; }

            public void Celebrate()
            {
                for (int i = 0; i < _m._slotBg.Count; i++)
                {
                    int k = i;
                    _m._ctx.Later(() => { _m.SlotStyle(k, SlotLook.Ok); Feedback.Pop((RectTransform)_m._slotBg[k].transform); }, k * .035f);
                }
            }

            public void ShowWord()
            {
                string word = _m._board.Word;
                for (int i = 0; i < word.Length; i++)
                {
                    _m._slotText[i].text = word[i].ToString();
                    _m.SlotStyle(i, SlotLook.Given);
                    Feedback.Slap((RectTransform)_m._slotBg[i].transform);
                }
            }

            public void ResetTiles() { foreach (LetterTile x in _m._tiles) x.ResetLook(); }
        }

        // ---------- keyboard ----------

        private void OnEnable()
        {
            _keyboard = Keyboard.current;
            if (_keyboard != null) _keyboard.onTextInput += OnText;
        }

        private void OnDisable()
        {
            if (_keyboard != null) _keyboard.onTextInput -= OnText;
            _keyboard = null;
        }

        private void OnText(char c)
        {
            if (_round == null || !char.IsLetter(c)) return;
            int i = _board.TileForKey(c);
            if (i >= 0) OnTile(_tiles[i]);
            else _round.IgnoredKey(char.ToLowerInvariant(c));
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k != null && _keyboard != k)
            {
                // the keyboard device can appear (or change) after this song started
                if (_keyboard != null) _keyboard.onTextInput -= OnText;
                _keyboard = k;
                _keyboard.onTextInput += OnText;
            }
            if (_round != null) _round.Tick();                             // ends a wrong-try wait whose reset never came
            if (k == null || _ctx == null || !_ctx.Open) return;
            if (k.backspaceKey.wasPressedThisFrame) Undo();
            else if (k.deleteKey.wasPressedThisFrame) Clear();
        }

        // ---------- drawing ----------

        private enum SlotLook { Empty, Fill, Given, Ok }

        /// <summary>remix.js:56-64 paint(): the slots from the picks, the rack's used and glowing tiles.</summary>
        private void Paint(int landed)
        {
            if (this == null || _board == null) return;                  // a late animation callback after the song left
            for (int i = 0; i < _slotBg.Count; i++)
            {
                if (_final.Contains(i)) continue;                         // .ok and .given stay once set, as the classes do
                bool has = i < _board.Picked.Count;
                _slotText[i].text = has ? _board.Mix[_board.Picked[i]].ToString() : "";
                SlotStyle(i, has ? SlotLook.Fill : SlotLook.Empty);
                if (i == landed && has) Feedback.Slap((RectTransform)_slotBg[i].transform);
            }
            PaintTiles();
        }

        private void PaintTiles()
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                bool used = _board.IsUsed(i);
                if (_tiles[i].Used != used) _tiles[i].Used = used;
                _tiles[i].interactable = _board.TileInteractable(i);
                _tiles[i].Glow = _board.Glow == i;
            }
        }

        /// <summary>screens.css .desk .slot: empty (faint card), .fill (card), .given (accent), .ok (paper-deep).</summary>
        private void SlotStyle(int i, SlotLook look)
        {
            if (look == SlotLook.Ok || look == SlotLook.Given) _final.Add(i);
            Color c;
            switch (look)
            {
                case SlotLook.Fill: c = Theme.Color(ColorRole.Card); break;
                case SlotLook.Given: c = Theme.Color(ColorRole.Accent); break;
                case SlotLook.Ok: c = Theme.Color(ColorRole.PaperDeep); break;
                default: c = Theme.Color(ColorRole.Card); c.a = EmptySlotAlpha; break;
            }
            _slotBg[i].color = c;
            if (i < _slotDash.Count && _slotDash[i] != null) _slotDash[i].enabled = look == SlotLook.Empty;
            _slotText[i].color = Theme.Color(look == SlotLook.Empty ? ColorRole.OnInk : ColorRole.Ink);
        }

        /// <summary>
        /// components.css .slot border:2.5px dashed, recoloured by screens.css:200 .desk .slot to card at 80%: a dashed
        /// rectangle just inside the slot, drawn with the map's UiLine. Shown only while the slot is empty (.fill and the
        /// later looks have a solid border the same colour as their face).
        /// </summary>
        /// <summary>
        /// screens.css:200 card at 8%, mixed in sRGB by the browser. The project blends in linear space, where 8% white
        /// over the black desk shows as about 31% grey; 1% in linear lands near the web's sRGB 8% (about #141414).
        /// </summary>
        private const float EmptySlotAlpha = .01f;

        private static WorldTour.Game.Map.UiLine DashedBorder(RectTransform slot, float w, float h)
        {
            var go = new GameObject("Dash", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, .5f);
            var line = go.AddComponent<WorldTour.Game.Map.UiLine>();
            line.raycastTarget = false;
            const float bw = 2.5f;
            float x = w / 2f - bw / 2f, y = h / 2f - bw / 2f;
            line.Width = bw;
            line.SetDash(5f, 4f);
            line.SetPoints(new[] { new Vector2(-x, y), new Vector2(x, y), new Vector2(x, -y), new Vector2(-x, -y), new Vector2(-x, y) });
            Color c = Theme.Color(ColorRole.Card); c.a = .8f;
            line.color = c;
            return line;
        }

        private void Build(RectTransform area, string word)
        {
            var col = area.gameObject.AddComponent<VerticalLayoutGroup>();
            col.childAlignment = TextAnchor.UpperCenter;
            col.spacing = 18;
            col.childControlWidth = true; col.childControlHeight = true;
            col.childForceExpandWidth = false; col.childForceExpandHeight = false;

            // the clue card (remix.js:25-31; .clue p keeps its own case, text-transform:none)
            RectTransform clue = Box("Clue", area, 640, -1);
            ShowUi.Panel(clue, ColorRole.Card, 1f, SpriteRole.PaperPanel);
            var clueCol = clue.gameObject.AddComponent<VerticalLayoutGroup>();
            clueCol.padding = new RectOffset(20, 20, 16, 18);
            clueCol.spacing = 4;
            clueCol.childControlWidth = true; clueCol.childControlHeight = true; clueCol.childForceExpandHeight = false;
            Text(clue, "Label", RemixLines.ClueLabel, Tokens.Fs0, FontRole.BodyBold, ColorRole.Accent2, true);
            TMP_Text clueText = Text(clue, "Text", "", 17.6f, FontRole.Body, ColorRole.Ink, false);
            clueText.text = _ctx.Clue;

            // the desk (remix.js:32-43)
            RectTransform desk = Box("Desk", area, 680, -1);
            ShowUi.Panel(desk, ColorRole.Ink);
            var deskCol = desk.gameObject.AddComponent<VerticalLayoutGroup>();
            deskCol.padding = new RectOffset(14, 14, 16, 22);
            deskCol.spacing = 14;
            deskCol.childAlignment = TextAnchor.UpperCenter;
            deskCol.childControlWidth = true; deskCol.childControlHeight = true; deskCol.childForceExpandWidth = false; deskCol.childForceExpandHeight = false;
            Text(desk, "Tag", RemixLines.DeskTag, Tokens.Fs2, FontRole.Display, ColorRole.OnInk, true);

            int n = word.Length;
            float slotW = Mathf.Min(46f, (640f - (n - 1) * 5f) / n);
            _slotRow = Row(desk, "Slots", 5, slotW * 1.25f);
            for (int i = 0; i < n; i++)
            {
                RectTransform s = Box("Slot " + i, _slotRow, slotW, slotW * 1.25f);
                _slotBg.Add(ShowUi.Panel(s, ColorRole.Card, EmptySlotAlpha));
                _slotDash.Add(DashedBorder(s, slotW, slotW * 1.25f));
                _slotText.Add(ShowUi.Label(s, "Letter", "", Mathf.Min(28f, slotW * .66f), FontRole.BodyBold, ColorRole.Ink));
            }

            // the rack: up to 8 tiles a row (.tiles max-width 520px, 52px tiles, 10px gaps), each row centred
            RectTransform rack = Box("Rack", desk, 520, -1);
            var rackCol = rack.gameObject.AddComponent<VerticalLayoutGroup>();
            rackCol.spacing = 10; rackCol.childAlignment = TextAnchor.UpperCenter;
            rackCol.childControlWidth = true; rackCol.childControlHeight = true; rackCol.childForceExpandWidth = false; rackCol.childForceExpandHeight = false;
            RectTransform row = null;
            for (int i = 0; i < _deal.Mix.Count; i++)
            {
                if (i % 8 == 0) row = Row(rack, "Row " + (i / 8), 10, 58);
                LetterTile t = LetterTile.Make(row, i, _deal.Mix[i], _deal.Tilts[i]);
                var le = t.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 52; le.preferredHeight = 58;
                t.Picked = OnTile;
                t.IsOverSlots = (p, cam) => RectTransformUtility.RectangleContainsScreenPoint(_slotRow, p, cam);
                _tiles.Add(t);
            }
            string note = RemixLines.DecoyNote(_deal.Decoys.Count);
            if (note != null) Text(desk, "DecoyNote", note, Tokens.Fs0, FontRole.Body, ColorRole.OnInk, true);

            // controls (remix.js:44-48)
            RectTransform controls = Row(area, "Controls", 12, Tokens.Tap);
            Control(ShowUi.Button(_ctx.ButtonPrefab, controls, "Undo", RemixLines.Undo, true, Undo));
            Control(ShowUi.Button(_ctx.ButtonPrefab, controls, "Clear", RemixLines.Clear, true, Clear));
            ShowButton hint = _ctx.HintButton(controls);
            if (hint != null) Control(hint);
        }

        private static void Control(ShowButton b)
        {
            // not ??: in the Editor a missing component is a fake-null object that ?? treats as present
            var le = b.Go.GetComponent<LayoutElement>();
            if (le == null) le = b.Go.AddComponent<LayoutElement>();
            le.preferredHeight = Tokens.Tap;
            le.preferredWidth = 150;
        }

        private static RectTransform Box(string name, RectTransform parent, float width, float height)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = width;
            if (height > 0) le.preferredHeight = height;
            return rt;
        }

        private static RectTransform Row(RectTransform parent, string name, float gap, float height)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = gap; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return rt;
        }

        private static TMP_Text Text(RectTransform parent, string name, string text, float size, FontRole font, ColorRole color, bool lower)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, font);
            t.fontSize = size;
            t.color = Theme.Color(color);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            if (lower) t.SetLower(text); else t.text = text;
            return t;
        }
    }
}
