using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// One of read mode's number pickers (stage.js picker(), math.css .picker): a label, big minus and plus keys and the value.
    /// The value box takes the keyboard: Up and Down change the number, Left and Right move between the pickers and the
    /// keys as usual, Enter or Space locks the pair in.
    /// </summary>
    public sealed class StagePicker : Selectable, ISubmitHandler
    {
        /// <summary>A minus (-1) or plus (+1), from a key or the arrows.</summary>
        public Action<int> Stepped;
        /// <summary>Enter or Space on the value.</summary>
        public Action Submitted;

        private TMP_Text _value;
        private ShowButton _minus, _plus;
        private int _askedFrame = -1;

        public RectTransform Rect { get { return (RectTransform)transform; } }

        public static StagePicker Make(RectTransform parent, string label, StickerButton prefab)
        {
            RectTransform row = SpellingUi.Row(parent, "Picker " + label, 6, 56);
            // .plbl: hand font 1.3rem, on-ink, right aligned, min-width 3.4em
            RectTransform lb = SpellingUi.Box("Label", row, 76, 56);
            TMP_Text lt = ShowUi.Label(lb, "Text", label, 20.8f, FontRole.Display, ColorRole.OnInk, TextAlignmentOptions.MidlineRight);
            lt.textWrappingMode = TextWrappingModes.NoWrap;

            StagePicker p = null;
            ShowButton minus = SpellingUi.Control(ShowUi.Button(prefab, row, "Minus", Glyph.Minus, true, () => { if (p != null) p.Step(-1); }), 56);

            // .pval: card, ink border 3px, radius 8, mono bold 1.6rem, 56 x 52
            RectTransform box = SpellingUi.Box("Value " + label, row, 64, 52);
            var ring = box.gameObject.AddComponent<Image>();
            ring.color = Theme.Color(ColorRole.Ink);
            Sprite rr = Theme.Sprite(SpriteRole.RoundedRect);
            if (rr != null) { ring.sprite = rr; ring.type = Image.Type.Sliced; }
            RectTransform face = ShowUi.Rect("Face", box);
            ShowUi.Stretch(face, Tokens.BorderWidth);
            ShowUi.Panel(face, ColorRole.Card);
            p = box.gameObject.AddComponent<StagePicker>();
            p.targetGraphic = ring;
            p.transition = Transition.None;
            p._value = ShowUi.Label(box, "Text", "?", 25.6f, FontRole.BodyBold, ColorRole.Ink);
            p._value.richText = false;

            ShowButton plus = SpellingUi.Control(ShowUi.Button(prefab, row, "Plus", "+", true, () => { if (p != null) p.Step(1); }), 56);
            p._minus = minus; p._plus = plus;
            return p;
        }

        private void Step(int d) { if (IsInteractable() && Stepped != null) Stepped(d); }

        /// <summary>The number shown, or "?" before she's set it.</summary>
        public void Show(int? v) { _value.text = v.HasValue ? v.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?"; }

        public void Lock()
        {
            interactable = false;
            if (_minus != null) _minus.Interactable = false;
            if (_plus != null) _plus.Interactable = false;
        }

        public override void OnMove(AxisEventData e)
        {
            if (IsInteractable() && (e.moveDir == MoveDirection.Up || e.moveDir == MoveDirection.Down))
            {
                e.Use();
                Step(e.moveDir == MoveDirection.Up ? 1 : -1);
                return;
            }
            base.OnMove(e);
        }

        public void OnSubmit(BaseEventData e) { if (IsInteractable()) Ask(); }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !IsInteractable() || EventSystem.current == null || EventSystem.current.currentSelectedGameObject != gameObject) return;
            if (kb.spaceKey.wasPressedThisFrame) Ask();
            // the number keys' + and - also step it
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) Step(1);
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) Step(-1);
        }

        private void Ask()
        {
            if (_askedFrame == Time.frameCount) return;
            _askedFrame = Time.frameCount;
            if (Submitted != null) Submitted();
        }
    }
}
