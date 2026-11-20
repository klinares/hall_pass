using System;

namespace WorldTour.Game.Motion
{
    /// <summary>Which parts of a RectTransform a motion drives. Two motions on the same channel of one target never run at once.</summary>
    [Flags]
    public enum Channel
    {
        None = 0,
        Move = 1,
        Rotate = 2,
        Scale = 4,
        Alpha = 8,
        All = Move | Rotate | Scale | Alpha,
    }

    /// <summary>
    /// One keyframe, in CSS terms: X and Y in px with Y pointing down, Rot in degrees clockwise, as in the web game's
    /// transform strings. The Unity side flips Y and Rot when it applies a pose. Offsets are 0..1.
    /// Move, rotate and scale are relative to the element's resting pose; Alpha is absolute opacity.
    /// </summary>
    public struct Pose
    {
        public float Offset, X, Y, Rot, Sx, Sy, Alpha;

        public static Pose At(float offset, float x = 0f, float y = 0f, float rot = 0f, float sx = 1f, float sy = 1f, float alpha = 1f)
        {
            return new Pose { Offset = offset, X = x, Y = y, Rot = rot, Sx = sx, Sy = sy, Alpha = alpha };
        }

        public static Pose Lerp(Pose a, Pose b, float f)
        {
            return new Pose
            {
                Offset = a.Offset + (b.Offset - a.Offset) * f,
                X = a.X + (b.X - a.X) * f,
                Y = a.Y + (b.Y - a.Y) * f,
                Rot = a.Rot + (b.Rot - a.Rot) * f,
                Sx = a.Sx + (b.Sx - a.Sx) * f,
                Sy = a.Sy + (b.Sy - a.Sy) * f,
                Alpha = a.Alpha + (b.Alpha - a.Alpha) * f,
            };
        }
    }

    /// <summary>
    /// A keyframed motion, evaluated like Element.animate(frames, { duration, easing }): the easing applies to the
    /// whole run, and keyframes are joined linearly by offset. When the easing overshoots (the spring), progress
    /// beyond 0..1 extrapolates the first or last segment, as the Web Animations spec does. Plain C#, immutable.
    /// </summary>
    public sealed class MotionPreset
    {
        public readonly string Name;
        public readonly Pose[] Keys;
        public readonly float Duration;
        public readonly CubicBezier Easing;
        public readonly Channel Channels;

        public MotionPreset(string name, float duration, CubicBezier easing, Channel channels, params Pose[] keys)
        {
            if (keys == null || keys.Length < 2) throw new ArgumentException("a motion needs at least two keyframes", nameof(keys));
            for (int i = 1; i < keys.Length; i++)
                if (keys[i].Offset < keys[i - 1].Offset) throw new ArgumentException("keyframe offsets must not go down: " + name, nameof(keys));
            Name = name; Duration = duration; Easing = easing; Channels = channels; Keys = keys;
        }

        /// <summary>The pose <paramref name="seconds"/> into the motion (clamped to its duration).</summary>
        public Pose Sample(float seconds)
        {
            float t = Duration <= 0f ? 1f : seconds / Duration;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return SampleProgress(Easing.Evaluate(t));
        }

        /// <summary>The pose at eased progress <paramref name="p"/>, which may be outside 0..1.</summary>
        public Pose SampleProgress(float p)
        {
            int last = Keys.Length - 1;
            int seg;
            if (p <= Keys[0].Offset) seg = 0;
            else if (p >= Keys[last].Offset) seg = last - 1;
            else
            {
                seg = 0;
                while (seg < last - 1 && p > Keys[seg + 1].Offset) seg++;
            }
            Pose a = Keys[seg], b = Keys[seg + 1];
            float span = b.Offset - a.Offset;
            float f = span <= 0f ? 1f : (p - a.Offset) / span;
            return Pose.Lerp(a, b, f);
        }
    }

    /// <summary>
    /// The web game's motions (world_tour/js/motion.js), keyframe for keyframe, plus the outgoing half of a screen
    /// change. All under 400 ms. Reduced motion replaces any of them with <see cref="ReducedFade"/>.
    /// </summary>
    public static class Presets
    {
        static readonly float Deg = (float)(Math.PI / 180.0);

        /// <summary>motion.js reduced(): opacity .6 to 1 over 120 ms, linear, nothing moves.</summary>
        public static readonly MotionPreset ReducedFade = new MotionPreset("reduced", 0.12f, Ease.Linear, Channel.Alpha,
            Pose.At(0f, alpha: 0.6f), Pose.At(1f, alpha: 1f));

        /// <summary>pop: correct answer, scale 1 / 1.22 at .45 / 1, 340 ms spring. Keeps the element's own tilt.</summary>
        public static readonly MotionPreset Pop = new MotionPreset("pop", 0.34f, Ease.Spring, Channel.Scale,
            Pose.At(0f), Pose.At(0.45f, sx: 1.22f, sy: 1.22f), Pose.At(1f));

        /// <summary>wobble: wrong answer, a short side-to-side shake with no colour change. 340 ms ease-out.</summary>
        public static readonly MotionPreset Wobble = new MotionPreset("wobble", 0.34f, Ease.CssEaseOut, Channel.Move | Channel.Rotate,
            Pose.At(0f), Pose.At(0.25f, x: -7f, rot: -2f), Pose.At(0.5f, x: 6f, rot: 1.5f), Pose.At(0.75f, x: -3f, rot: -0.5f), Pose.At(1f));

        /// <summary>slap: a letter or sticker landing; drops in, squashes, settles. 260 ms spring.</summary>
        public static readonly MotionPreset Slap = new MotionPreset("slap", 0.26f, Ease.Spring, Channel.All,
            Pose.At(0f, y: -14f, rot: -6f, sx: 1.25f, sy: 1.25f, alpha: 0f),
            Pose.At(0.55f, rot: 2f, sx: 0.9f, sy: 1.08f),
            Pose.At(1f));

        /// <summary>
        /// peelOn: a sticker lifting from a corner then slapping down, 380 ms spring. The web tilts it in 3D
        /// (rotateX 55, rotateY -25 with perspective); a flat canvas shows that as scale by the cosines.
        /// </summary>
        public static readonly MotionPreset PeelOn = new MotionPreset("peelOn", 0.38f, Ease.Spring, Channel.Scale | Channel.Alpha,
            Pose.At(0f, sx: 1.15f * Cos(-25f), sy: 1.15f * Cos(55f), alpha: 0f),
            Pose.At(0.6f, sx: 1.05f * Cos(4f), sy: 1.05f * Cos(-6f)),
            Pose.At(1f));

        /// <summary>shrink: a tile leaving the rack, scale to 0 and fade, 160 ms ease-in. Ends hidden (the caller deactivates it).</summary>
        public static readonly MotionPreset Shrink = new MotionPreset("shrink", 0.16f, Ease.CssEaseIn, Channel.Scale | Channel.Alpha,
            Pose.At(0f), Pose.At(1f, sx: 0f, sy: 0f, alpha: 0f));

        /// <summary>slideIn: a new screen, a quick paper slide from the right, 280 ms --ease.</summary>
        public static readonly MotionPreset SlideIn = new MotionPreset("slideIn", 0.28f, Ease.Out, Channel.Move | Channel.Rotate | Channel.Alpha,
            Pose.At(0f, x: 28f, rot: 0.8f, alpha: 0f), Pose.At(1f));

        /// <summary>Going back: the same slide from the left.</summary>
        public static readonly MotionPreset SlideInBack = new MotionPreset("slideInBack", 0.28f, Ease.Out, Channel.Move | Channel.Rotate | Channel.Alpha,
            Pose.At(0f, x: -28f, rot: -0.8f, alpha: 0f), Pose.At(1f));

        /// <summary>The covered screen leaving (new in Unity: the web swaps instantly). 160 ms ease-in, ends hidden.</summary>
        public static readonly MotionPreset SlideOut = new MotionPreset("slideOut", 0.16f, Ease.CssEaseIn, Channel.Move | Channel.Rotate | Channel.Alpha,
            Pose.At(0f), Pose.At(1f, x: -28f, rot: -0.8f, alpha: 0f));

        /// <summary>The popped screen leaving to the right, 160 ms ease-in, ends hidden.</summary>
        public static readonly MotionPreset SlideOutBack = new MotionPreset("slideOutBack", 0.16f, Ease.CssEaseIn, Channel.Move | Channel.Rotate | Channel.Alpha,
            Pose.At(0f), Pose.At(1f, x: 28f, rot: 0.8f, alpha: 0f));

        /// <summary>hop: the dog's little jump, 380 ms spring.</summary>
        public static readonly MotionPreset Hop = new MotionPreset("hop", 0.38f, Ease.Spring, Channel.Move | Channel.Scale,
            Pose.At(0f), Pose.At(0.4f, y: -16f, sx: 1.04f, sy: 0.96f), Pose.At(1f));

        /// <summary>
        /// Button press (components.css .btn:active): translate(2px,3px) scale(1.04,.9) in 60 ms. Held at its end
        /// while the pointer is down.
        /// </summary>
        public static readonly MotionPreset Press = new MotionPreset("press", 0.06f, Ease.CssEase, Channel.Move | Channel.Scale,
            Pose.At(0f), Pose.At(1f, x: 2f, y: 3f, sx: 1.04f, sy: 0.9f));

        /// <summary>Button release: back from the pressed pose with the spring (transition: transform var(--t-med) var(--spring)).</summary>
        public static readonly MotionPreset Release = new MotionPreset("release", 0.24f, Ease.Spring, Channel.Move | Channel.Scale,
            Pose.At(0f, x: 2f, y: 3f, sx: 1.04f, sy: 0.9f), Pose.At(1f));

        /// <summary>Every preset, for the under-400-ms test.</summary>
        public static readonly MotionPreset[] All =
            { ReducedFade, Pop, Wobble, Slap, PeelOn, Shrink, SlideIn, SlideInBack, SlideOut, SlideOutBack, Hop, Press, Release };

        static float Cos(float deg) { return (float)Math.Cos(deg * Deg); }
    }
}
