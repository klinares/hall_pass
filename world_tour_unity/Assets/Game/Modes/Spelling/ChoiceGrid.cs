using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// parts.js:86-87 choiceButtons in a two-column grid (.choices.grid2), drawn as fan mail draws its choices: sticker
    /// buttons tilted by position, .good (ink), .bad/.gone (struck through, faded). After the song is decided the grid stops
    /// taking clicks (the web disables the buttons, which keeps their look).
    /// </summary>
    public sealed class ChoiceGrid
    {
        public readonly RectTransform Rect;
        public readonly List<ShowButton> Buttons = new List<ShowButton>();
        private readonly List<string> _labels;
        private readonly CanvasGroup _group;

        public ChoiceGrid(RectTransform parent, StickerButton prefab, IReadOnlyList<string> labels, Action<int> onPick, float width = 640)
        {
            _labels = new List<string>(labels);
            int n = labels.Count, rows = (n + 1) / 2;
            Rect = SpellingUi.Box("Choices", parent, width, rows * 64 + Math.Max(0, rows - 1) * 12);
            _group = Rect.gameObject.AddComponent<CanvasGroup>();
            var grid = Rect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2((width - 12) / 2f, 64);
            grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                ShowButton b = ShowUi.Button(prefab, Rect, "Choice " + i, labels[i], true, () => onPick(k));
                b.Rect.localEulerAngles = new Vector3(0, 0, -SpellingLines.ChoiceTilts[i % 4]);   // --r, CSS clockwise
                Buttons.Add(b);
            }
        }

        /// <summary>.choice.good: ink face.</summary>
        public void Good(int i, bool pop)
        {
            if (i < 0 || i >= Buttons.Count) return;
            ShowButton b = Buttons[i];
            if (b.Sticker != null) b.Sticker.ButtonStyle = StickerButton.Style.Ink;
            else
            {
                var img = b.Go.GetComponent<Image>();
                if (img != null) img.color = Theme.Color(ColorRole.Ink);
            }
            b.SetText(_labels[i]);
            if (pop) Feedback.Pop(b.Rect);
        }

        /// <summary>.choice.bad + .gone (a wrong pick) or .gone (crossed out): struck through and disabled, which fades it.</summary>
        public void Gone(int i, bool wobble)
        {
            if (i < 0 || i >= Buttons.Count) return;
            ShowButton b = Buttons[i];
            b.SetText("<s>" + _labels[i] + "</s>");
            b.Interactable = false;
            if (wobble) Feedback.Wobble(b.Rect);
        }

        /// <summary>btns.forEach(x => x.disabled = true): no more clicks, the look kept.</summary>
        public void Freeze()
        {
            _group.blocksRaycasts = false;
        }

        /// <summary>"&lt;tag&gt; pos=x:y,..." each button's centre in page pixels from the top left (for mouse-only browser tests).</summary>
        public void LogPositions(string tag)
        {
            if (Rect == null || Buttons.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            var sb = new System.Text.StringBuilder(tag + " pos=");
            var corners = new Vector3[4];
            for (int i = 0; i < Buttons.Count; i++)
            {
                Buttons[i].Rect.GetWorldCorners(corners);
                Vector3 c = (corners[0] + corners[2]) * .5f;
                if (i > 0) sb.Append(',');
                sb.Append(Mathf.RoundToInt(c.x)).Append(':').Append(Mathf.RoundToInt(UnityEngine.Screen.height - c.y));
            }
            Debug.Log(sb.ToString());
        }
    }

    /// <summary>The show side of a typed round for studio and lyric: the song context and show.js's ladder.</summary>
    public sealed class SongTypedHost : ITypedHost
    {
        private readonly SongContext _c;
        private readonly Action _onMiss;
        private readonly HintFloor _floor;

        /// <param name="onMiss">What wobbles on a wrong try besides the field (show.js:163 [data-wobble], or the play area).</param>
        /// <param name="floor">The song's hint floor: the ladder lines count with the hints (QUESTIONS 11).</param>
        public SongTypedHost(SongContext c, Action onMiss = null, HintFloor floor = null) { _c = c; _onMiss = onMiss; _floor = floor; }

        public bool Open { get { return _c.Open; } }
        public int Tries { get { return _c.Tries; } }
        public int Miss() { int t = _c.Miss(); if (_onMiss != null) _onMiss(); return t; }
        public bool Win() { return _c.Win(); }
        public bool Reveal() { return _c.Reveal(); }
        public void Say(string line, string pose) { _c.Say(line, pose); }
        public void Next(float seconds) { _c.Next(seconds); }
        public string Ladder(int tries) { return _floor != null ? _floor.Ladder(tries, _c.Random) : RemixLines.Ladder(_c.Item.W, _c.Item.S, tries, _c.Random); }
        public void Log(string line) { Debug.Log(line); }
        public void Error(Exception e) { Debug.LogException(e); }
    }
}
