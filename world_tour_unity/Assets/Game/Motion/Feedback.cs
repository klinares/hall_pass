using System;
using UnityEngine;
using WorldTour.Game.Audio;

namespace WorldTour.Game.Motion
{
    /// <summary>
    /// The feedback vocabulary (motion.js): pop for a correct answer, wobble for a wrong one (movement only; nothing
    /// ever turns red), peel-on when a sticker appears, slap when a sticker or letter lands. Each is under 400 ms,
    /// returns at once (never awaited by input handlers) and turns into a short fade when reduce motion is on.
    /// Sounds follow the web game's pairings where it has one; show code plays the answer sounds (strum, thud) itself.
    /// </summary>
    public static class Feedback
    {
        /// <summary>Correct: scale 1.22 with overshoot, keeping the element's tilt.</summary>
        public static TweenHandle Pop(RectTransform target, Action onDone = null)
        {
            return Tweener.Play(target, Presets.Pop, onDone);
        }

        /// <summary>Wrong: a short shake. Never pair this with a red tint.</summary>
        public static TweenHandle Wobble(RectTransform target, Action onDone = null)
        {
            return Tweener.Play(target, Presets.Wobble, onDone);
        }

        /// <summary>A sticker appears: lifts from a corner and slaps down (map.js and journal.js pair it with the peel sound).</summary>
        public static TweenHandle PeelOn(RectTransform target, bool sound = true, Action onDone = null)
        {
            if (sound) Sfx.Play(SfxId.Peel);
            return Tweener.Play(target, Presets.PeelOn, onDone);
        }

        /// <summary>A sticker or letter lands: drops in, squashes, settles (journal.js pairs it with the pop sound).</summary>
        public static TweenHandle Slap(RectTransform target, bool sound = false, Action onDone = null)
        {
            if (sound) Sfx.Play(SfxId.Pop);
            return Tweener.Play(target, Presets.Slap, onDone);
        }

        /// <summary>A tile leaving the rack. Ends invisible (alpha 0); deactivate it in <paramref name="onDone"/>.</summary>
        public static TweenHandle Shrink(RectTransform target, Action onDone = null)
        {
            return Tweener.Play(target, Presets.Shrink, onDone);
        }

        /// <summary>The dog's hop.</summary>
        public static TweenHandle Hop(RectTransform target, Action onDone = null)
        {
            return Tweener.Play(target, Presets.Hop, onDone);
        }
    }
}
