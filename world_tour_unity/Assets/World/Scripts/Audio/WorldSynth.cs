using System;

namespace WorldTour.World.Audio
{
    /// <summary>
    /// Phase 8.3: the world's own sounds, made from maths at start-up (no audio files, so no licence to check): gentle bird chirps, soft footsteps that suit the ground,
    /// applause from bursts of filtered noise, and a crowd murmur from filtered noise with slow swells. Plain C# (floats in, floats out) so edit-mode tests can look at them.
    /// Every call is seeded, so the same sound comes out every time. Mono, 22.05 kHz.
    /// </summary>
    public static class WorldSynth
    {
        public const int SampleRate = 22050;

        // ---------------- helpers ----------------

        /// <summary>One-pole low-pass state.</summary>
        struct Lp
        {
            public float y;
            public float Run(float x, float k) { y += (x - y) * k; return y; }
        }

        static float White(Random r) { return (float)(r.NextDouble() * 2.0 - 1.0); }

        /// <summary>One-pole coefficient for a cut-off in Hz.</summary>
        static float K(float hz) { return 1f - (float)Math.Exp(-2.0 * Math.PI * hz / SampleRate); }

        static void Normalise(float[] d, float peakTo)
        {
            float p = 1e-4f;
            for (int i = 0; i < d.Length; i++) p = Math.Max(p, Math.Abs(d[i]));
            float g = peakTo / p;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
        }

        /// <summary>Make a loop from a longer render: the extra tail after the loop end is blended into the start, so the join has no click.</summary>
        static float[] Loopify(float[] raw, int n)
        {
            int fade = raw.Length - n;
            var d = new float[n];
            Array.Copy(raw, d, n);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                d[i] = d[i] * w + raw[n + i] * (1f - w);
            }
            return d;
        }

        // ---------------- birds ----------------

        /// <summary>A few small chirps and trills at random gaps over near-silence, 12 s, every call inside the loop. Soft: a sine that glides, with a rounded envelope.</summary>
        public static float[] Birds()
        {
            const int seconds = 12;
            int n = SampleRate * seconds;
            var d = new float[n];
            var r = new Random(1701);
            float t = 0.5f;
            while (t < seconds - 1.2f)
            {
                bool trill = r.NextDouble() < 0.4;
                float baseHz = 2300f + (float)r.NextDouble() * 1700f;
                int notes = trill ? 3 + r.Next(4) : 1 + r.Next(2);
                float gain = 0.35f + 0.35f * (float)r.NextDouble();
                float at = t;
                for (int k = 0; k < notes; k++)
                {
                    float len = trill ? 0.05f + 0.02f * (float)r.NextDouble() : 0.09f + 0.07f * (float)r.NextDouble();
                    float up = r.NextDouble() < 0.6 ? 1f : -1f;
                    Chirp(d, at, len, baseHz * (trill ? 1f + 0.03f * k : 1f), up * (0.18f + 0.2f * (float)r.NextDouble()), gain);
                    at += len + (trill ? 0.03f : 0.12f);
                }
                t = at + 0.6f + (float)r.NextDouble() * 1.8f;
            }
            Normalise(d, 0.8f);
            return d;
        }

        static void Chirp(float[] d, float start, float len, float hz, float glide, float gain)
        {
            int s0 = (int)(start * SampleRate), n = (int)(len * SampleRate);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float u = i / (float)n;
                float f = hz * (1f + glide * u) + 35f * (float)Math.Sin(u * 40f);   // a little wobble
                phase += 2.0 * Math.PI * f / SampleRate;
                float env = (float)Math.Sin(Math.PI * u);
                env *= env;
                d[s0 + i] += (float)Math.Sin(phase) * env * gain * 0.6f + (float)Math.Sin(phase * 2.0) * env * gain * 0.06f;
            }
        }

        // ---------------- footsteps ----------------

        /// <summary>What a step on this ground is made of: a low thump (Hz, length, level), then a noise scuff (a low-pass in Hz for its brightness, length, level).</summary>
        public struct StepSpec
        {
            public float ThumpHz, ThumpLen, ThumpLevel, ScuffHz, ScuffLen, ScuffLevel;
        }

        public static StepSpec SpecFor(Surface s)
        {
            switch (s)
            {
                case Surface.Wood: return new StepSpec { ThumpHz = 150f, ThumpLen = 0.07f, ThumpLevel = 0.9f, ScuffHz = 2600f, ScuffLen = 0.03f, ScuffLevel = 0.5f };    // a light knock
                case Surface.Carpet: return new StepSpec { ThumpHz = 85f, ThumpLen = 0.09f, ThumpLevel = 1f, ScuffHz = 700f, ScuffLen = 0.07f, ScuffLevel = 0.35f };     // dull and soft
                case Surface.Grass: return new StepSpec { ThumpHz = 70f, ThumpLen = 0.08f, ThumpLevel = 0.6f, ScuffHz = 1400f, ScuffLen = 0.14f, ScuffLevel = 0.5f };    // a brushing rustle
                case Surface.Hard: return new StepSpec { ThumpHz = 125f, ThumpLen = 0.06f, ThumpLevel = 0.8f, ScuffHz = 4200f, ScuffLen = 0.04f, ScuffLevel = 0.7f };     // a bright tap
                default: return new StepSpec { ThumpHz = 105f, ThumpLen = 0.07f, ThumpLevel = 0.85f, ScuffHz = 3200f, ScuffLen = 0.045f, ScuffLevel = 0.6f };            // pavement
            }
        }

        /// <summary>One footstep (0.25 s). Four variations per ground: the noise and the pitch of the thump change a little.</summary>
        public static float[] Step(Surface surface, int variant)
        {
            StepSpec sp = SpecFor(surface);
            int n = (int)(0.25f * SampleRate);
            var d = new float[n];
            var r = new Random(900 + variant * 31 + (int)surface * 7);
            float thump = sp.ThumpHz * (0.93f + 0.14f * (float)r.NextDouble());
            var lp1 = new Lp();
            var lp2 = new Lp();
            float k = K(sp.ScuffHz);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float tEnv = (float)Math.Exp(-t / (sp.ThumpLen * 0.45f)) * Math.Min(1f, t / 0.004f);
                float th = (float)Math.Sin(2.0 * Math.PI * thump * t * (1f - 0.25f * Math.Min(1f, t / 0.1f))) * tEnv * sp.ThumpLevel;
                float nz = lp2.Run(lp1.Run(White(r), k), k);
                float nEnv = (float)Math.Exp(-t / (sp.ScuffLen * 0.45f)) * Math.Min(1f, t / 0.002f);
                d[i] = th + nz * nEnv * sp.ScuffLevel * 3.2f;
            }
            Normalise(d, 0.85f);
            return d;
        }

        // ---------------- applause ----------------

        /// <summary>About 2.4 s of applause: a loose wash of band-passed noise and many tiny claps (each a 10 to 20 ms noise burst), thickening then thinning. It fades itself in and out.</summary>
        public static float[] Applause()
        {
            const float seconds = 2.4f;
            int n = (int)(seconds * SampleRate);
            var d = new float[n];
            var r = new Random(777);
            var a = new Lp();
            var b = new Lp();
            var c = new Lp();
            float ka = K(3800f), kc = K(500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float x = White(r);
                float band = b.Run(a.Run(x, ka), ka) - c.Run(x, kc);   // low-passed at 3.8 kHz, minus what is below 500 Hz: a band
                d[i] = band * 0.16f * (float)Math.Sin(Math.PI * t);
            }
            const int claps = 170;
            float kHi = K(900f);
            for (int j = 0; j < claps; j++)
            {
                float pos = 0.04f + 0.92f * (float)((r.NextDouble() + r.NextDouble()) * 0.5);   // dense in the middle
                int s0 = (int)(pos * n);
                float kk = K(1800f + 2600f * (float)r.NextDouble());
                var l1 = new Lp();
                var l2 = new Lp();
                var hp = new Lp();
                float level = 0.35f + 0.65f * (float)r.NextDouble();
                int len = (int)((0.008f + 0.012f * (float)r.NextDouble()) * SampleRate);
                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    float x = White(r);
                    float y = l2.Run(l1.Run(x, kk), kk) - hp.Run(x, kHi);
                    float env = (float)Math.Exp(-i / (len * 0.3f));
                    d[s0 + i] += y * env * level * 1.8f;
                }
            }
            Normalise(d, 0.8f);
            int edge = (int)(0.08f * SampleRate);
            for (int i = 0; i < edge; i++) { float w = i / (float)edge; d[i] *= w; d[n - 1 - i] *= w; }
            return d;
        }

        // ---------------- crowd murmur ----------------

        /// <summary>
        /// A hall full of people a little way off, 14 s as a loop: noise through a low band (voices without words), under three slow swells of different speeds,
        /// plus a mid band that comes and goes about as fast as speech does.
        /// </summary>
        public static float[] Murmur()
        {
            const int seconds = 14, fade = SampleRate;
            int n = SampleRate * seconds;
            var raw = new float[n + fade];
            var r = new Random(2468);
            var lo1 = new Lp();
            var lo2 = new Lp();
            var hi = new Lp();
            var m1 = new Lp();
            var m2 = new Lp();
            var sm = new Lp();
            float kLow = K(650f), kHigh = K(140f), kMid = K(1500f), kOut = K(2200f);
            float syl = 0.5f, sylNow = 0.5f;
            double w1 = 2 * Math.PI / 7.0, w2 = 2 * Math.PI / 4.6, w3 = 2 * Math.PI / 2.8;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SampleRate;
                float x = White(r);
                float low = lo2.Run(lo1.Run(x, kLow), kLow) - hi.Run(x, kHigh);
                float mid = m2.Run(m1.Run(White(r), kMid), kMid);
                if (i % (SampleRate / 8) == 0) syl = (float)r.NextDouble();   // a new "syllable" level eight times a second
                sylNow += (syl - sylNow) * 0.0015f;                            // glided into, not stepped
                float gate = 0.35f + 0.65f * sylNow;
                float swell = 0.62f + 0.18f * (float)Math.Sin(w1 * t) + 0.12f * (float)Math.Sin(w2 * t + 1.3) + 0.08f * (float)Math.Sin(w3 * t + 2.1);
                raw[i] = sm.Run((low * 2.4f + mid * 3.0f * gate) * swell, kOut);
            }
            float[] d = Loopify(raw, n);
            Normalise(d, 0.8f);
            return d;
        }
    }
}
