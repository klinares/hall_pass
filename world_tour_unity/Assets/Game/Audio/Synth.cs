using System;

namespace WorldTour.Game.Audio
{
    /// <summary>One note for <see cref="Synth.Beat"/>: Len is a fraction of a whole note, D its denominator (2, 3, 4, 6, 8, 12).</summary>
    public struct BeatNote
    {
        public double Len;
        public int D;
        public BeatNote(double len, int d) { Len = len; D = d; }
    }

    /// <summary>
    /// Every sound in the game, rendered to mono float samples in code: world_tour/js/sound.js, node for node.
    /// sound.js builds Web Audio graphs (oscillators with gain and frequency ramps, filtered noise, a Karplus-Strong
    /// pluck, a compressor on the master bus); this renders the same graphs offline into buffers that the Game layer
    /// turns into AudioClips. Plain C#, deterministic for a given Random, so edit-mode tests can check it.
    /// Differences from the browser: oscillators are not band-limited (the only bright one, peel's sawtooth, is very
    /// quiet), and the master compressor is a simple per-sound model of DynamicsCompressorNode's defaults.
    /// </summary>
    public static class Synth
    {
        /// <summary>The rate the clips are made at. Web Audio resamples to the device rate.</summary>
        public const int Rate = 44100;

        public enum Wave { Sine, Triangle, Sawtooth, Square }

        // ------------------------------------------------------------------ the sounds

        /// <summary>tap: osc(1250, sine, .04 s, vol .05). UI clicks, picks, undo.</summary>
        public static float[] Tap(int sr = Rate) { return Single(sr, 1250f, Wave.Sine, 0.04f, 0.05f, 0f); }

        /// <summary>pop: osc(420 to 980, triangle, .11 s, vol .12). Equip, sticker stuck.</summary>
        public static float[] Pop(int sr = Rate) { return Single(sr, 420f, Wave.Triangle, 0.11f, 0.12f, 980f); }

        /// <summary>peel: osc(1800 to 600, sawtooth, .06 s, vol .02). A sticker peeling on.</summary>
        public static float[] Peel(int sr = Rate) { return Single(sr, 1800f, Wave.Sawtooth, 0.06f, 0.02f, 600f); }

        /// <summary>tick: osc(2100, square, .02 s, vol .015). Soundcheck's bonus beep.</summary>
        public static float[] Tick(int sr = Rate) { return Single(sr, 2100f, Wave.Square, 0.02f, 0.015f, 0f); }

        /// <summary>thud: a soft low knock for a wrong answer. osc(110 to 48, sine, .22 s, vol .32) plus .12 s of noise through a 380 Hz low-pass at gain .5.</summary>
        public static float[] Thud(Random rng, int sr = Rate)
        {
            float[] buf = new float[Samples(sr, 0.24f)];
            Osc(buf, sr, 110f, Wave.Sine, 0.22f, 0.32f, 0f, 48f);
            float[] n = Noise(rng, sr, 0.12f);
            var lp = Biquad.LowPass(sr, 380f, 1f);
            for (int i = 0; i < n.Length && i < buf.Length; i++) buf[i] += lp.Process(n[i]) * 0.5f;
            Master(buf, sr);
            return buf;
        }

        /// <summary>sound.js CHORDS.win: G major strummed down (G2 B2 D3 G3 B3 G4). A correct answer.</summary>
        public static readonly float[] ChordWin = { 98f, 123.5f, 146.8f, 196f, 246.9f, 392f };
        /// <summary>sound.js CHORDS.big: C major, for bigger moments (purchases, the end of a show).</summary>
        public static readonly float[] ChordBig = { 130.8f, 164.8f, 196f, 261.6f, 329.6f, 523.3f };

        /// <summary>strum(kind): the chord plucked once into a buffer (sound.js pluckBuffer), played at volume 1.</summary>
        public static float[] Strum(float[] chord, Random rng, int sr = Rate)
        {
            float[] buf = Pluck(chord, rng, sr);
            Master(buf, sr);
            return buf;
        }

        /// <summary>
        /// crowd(size): the end-of-show swell, size 0..1 by venue. Band-passed noise rising and falling over
        /// 2.6 + 1.4*size seconds, 12 + 40*size claps, and the big strum 350 ms in.
        /// </summary>
        public static float[] Crowd(float size, Random rng, int sr = Rate, int phrase = 0)
        {
            double dur = 2.6 + size * 1.4;
            float[] buf = new float[Samples(sr, (float)dur)];
            float[] noise = Noise(rng, sr, 4.2f);

            // the swell
            var bp = Biquad.BandPass(sr, 900f + size * 500f, 0.6f);
            double peak = 0.18 + size * 0.32;
            for (int i = 0; i < buf.Length; i++)
            {
                double t = (double)i / sr;
                double g;
                if (t < 0.9) g = peak * (t / 0.9);
                else if (t < dur * 0.6) g = peak + (peak * 0.7 - peak) * ((t - 0.9) / (dur * 0.6 - 0.9));
                else g = peak * 0.7 * (1.0 - (t - dur * 0.6) / (dur - dur * 0.6));
                float x = i < noise.Length ? noise[i] : 0f;
                buf[i] += (float)(bp.Process(x) * g);
            }

            // the claps: each one a slice of the same noise, high-passed, with a 4 ms attack and a 60 ms decay
            int claps = (int)Math.Round(12 + size * 40);
            for (int c = 0; c < claps; c++)
            {
                double at = 0.25 + rng.NextDouble() * (dur - 0.6);
                var hp = Biquad.HighPass(sr, (float)(1400 + rng.NextDouble() * 1200), 1f);
                double top = 0.12 + rng.NextDouble() * 0.1;
                int from = (int)(rng.NextDouble() * 3 * sr);
                int start = (int)(at * sr), len = Samples(sr, 0.08f);
                for (int k = 0; k < len; k++)
                {
                    int o = start + k, src = from + k;
                    if (o >= buf.Length || src >= noise.Length) break;
                    double t = (double)k / sr, g;
                    if (t < 0.004) g = top * (t / 0.004);
                    else if (t < 0.06) g = top * Math.Pow(0.001 / top, (t - 0.004) / 0.056);
                    else g = 0.001;
                    buf[o] += (float)(hp.Process(noise[src]) * g);
                }
            }

            // setTimeout(() => strum('big'), 350)
            float[] big = Pluck(BigPhrases[((phrase % PhraseCount) + PhraseCount) % PhraseCount], rng, sr);
            int off = (int)(0.35 * sr);
            for (int i = 0; i < big.Length && off + i < buf.Length; i++) buf[off + i] += big[i];

            Master(buf, sr);
            return buf;
        }

        /// <summary>sound.js NOTE_HZ: a pitch per note size for beat maker.</summary>
        public static float NoteHz(int d)
        {
            switch (d)
            {
                case 2: return 196f;
                case 3: return 233.1f;
                case 4: return 293.7f;
                case 6: return 349.2f;
                case 8: return 392f;
                case 12: return 466.2f;
                default: return 0f;
            }
        }

        /// <summary>
        /// beat(notes, whole): one measure for beat maker (phase 4). A whole note lasts <paramref name="whole"/> seconds;
        /// each note gets a pitch by size, and a kick lands on each quarter (louder on the downbeat).
        /// <paramref name="seconds"/> is the measure length, what sound.js returns.
        /// </summary>
        public static float[] Beat(BeatNote[] notes, out double seconds, double whole = 1.6, int sr = Rate)
        {
            double t = 0;
            if (notes != null) foreach (BeatNote n in notes) t += n.Len * whole;
            seconds = t;
            float[] buf = new float[Samples(sr, (float)(t + 0.45))];
            double at = 0;
            if (notes != null)
            {
                foreach (BeatNote n in notes)
                {
                    float hz = NoteHz(n.D);
                    Osc(buf, sr, hz > 0f ? hz : 330f, Wave.Triangle, (float)Math.Min(0.4, n.Len * whole * 0.9), 0.16f, (float)at, 0f);
                    Osc(buf, sr, hz > 0f ? hz * 2f : 660f, Wave.Sine, 0.05f, 0.04f, (float)at, 0f);
                    at += n.Len * whole;
                }
            }
            for (double k = 0; k < t - 1e-6; k += whole / 4)
            {
                bool down = k % whole < 1e-6;
                Osc(buf, sr, down ? 70f : 55f, Wave.Sine, 0.14f, down ? 0.35f : 0.15f, (float)k, 38f);
            }
            Master(buf, sr);
            return buf;
        }

        // ------------------------------------------------------------------ guitar phrases (phase 10A)

        /// <summary>
        /// Phase 10A: the guitar for a right answer, five different phrases so it never plays the same one twice in a row (G, D, E minor, C and A minor, as strums and arpeggios). Low string first.
        /// </summary>
        public static readonly float[][] WinPhrases =
        {
            ChordWin,
            new[] { 73.4f, 110f, 146.8f, 185f, 220f, 293.7f },     // D
            new[] { 82.4f, 123.5f, 164.8f, 196f, 246.9f, 329.6f }, // Em
            new[] { 130.8f, 164.8f, 196f, 261.6f, 329.6f },        // C
            new[] { 110f, 164.8f, 220f, 261.6f, 329.6f },          // Am
        };

        /// <summary>The bigger moments (a purchase, the end of a show): five phrases too.</summary>
        public static readonly float[][] BigPhrases =
        {
            ChordBig,
            new[] { 87.3f, 130.8f, 174.6f, 220f, 261.6f, 349.2f }, // F
            new[] { 110f, 164.8f, 220f, 261.6f, 329.6f, 440f },    // Am
            new[] { 98f, 146.8f, 196f, 246.9f, 392f },             // G
            new[] { 130.8f, 196f, 261.6f, 329.6f, 392f, 523.3f },  // C, wide
        };

        public const int PhraseCount = 5;

        /// <summary>
        /// One guitar phrase, soft and short: a plucked chord (0, 1 and 4 strummed down at different speeds; 2 and 3 rolled up and down as an arpeggio), a bit under a second,
        /// faded out so nothing rings into the next question, and scaled to a peak of exactly 1 so the caller sets how loud it is with one number.
        /// </summary>
        public static float[] Phrase(bool big, int index, Random rng, int sr = Rate)
        {
            float[][] set = big ? BigPhrases : WinPhrases;
            int i = ((index % PhraseCount) + PhraseCount) % PhraseCount;
            float[] chord = (float[])set[i].Clone();
            double stagger = i == 2 ? 0.1 : (i == 3 ? 0.085 : (i == 1 ? 0.045 : 0.03));
            if (i == 3) Array.Reverse(chord);                      // a roll down
            float[] buf = Pluck(chord, rng, sr, stagger, 0.75, 0.994);
            Master(buf, sr);
            int fade = Math.Min(buf.Length, (int)(0.3 * sr));
            for (int k = 0; k < fade; k++) buf[buf.Length - 1 - k] *= k / (float)fade;
            float peak = 1e-4f;
            for (int k = 0; k < buf.Length; k++) peak = Math.Max(peak, Math.Abs(buf[k]));
            for (int k = 0; k < buf.Length; k++) buf[k] /= peak;
            return buf;
        }

        // ------------------------------------------------------------------ building blocks

        static float[] Single(int sr, float freq, Wave w, float dur, float vol, float to)
        {
            float[] buf = new float[Samples(sr, dur + 0.02f)];
            Osc(buf, sr, freq, w, dur, vol, 0f, to);
            Master(buf, sr);
            return buf;
        }

        /// <summary>Samples for <paramref name="seconds"/>, rounded up.</summary>
        public static int Samples(int sr, float seconds) { return Math.Max(1, (int)Math.Ceiling(seconds * sr)); }

        /// <summary>
        /// sound.js osc(): adds one oscillator note into <paramref name="buf"/>. Gain 0 to <paramref name="vol"/> in 6 ms,
        /// then an exponential fall to .0008 at <paramref name="dur"/>; it stops 20 ms later. With <paramref name="to"/> &gt; 0
        /// the pitch glides exponentially from <paramref name="freq"/> to it over the note.
        /// </summary>
        public static void Osc(float[] buf, int sr, float freq, Wave type, float dur, float vol, float delay, float to)
        {
            int start = (int)Math.Round(delay * sr);
            int len = (int)Math.Round((dur + 0.02f) * sr);
            double phase = 0;
            for (int k = 0; k < len; k++)
            {
                int i = start + k;
                if (i >= buf.Length) break;
                double t = (double)k / sr;
                double f = to > 0f && t < dur ? freq * Math.Pow(to / freq, t / dur) : (to > 0f ? to : freq);
                double g;
                if (t < 0.006) g = vol * (t / 0.006);
                else if (t < dur) g = vol * Math.Pow(0.0008 / vol, (t - 0.006) / (dur - 0.006));
                else g = 0.0008;
                buf[i] += (float)(Shape(type, phase) * g);
                phase += f / sr;
                phase -= Math.Floor(phase);
            }
        }

        static double Shape(Wave w, double phase)
        {
            switch (w)
            {
                case Wave.Triangle: return phase < 0.25 ? 4 * phase : (phase < 0.75 ? 2 - 4 * phase : 4 * phase - 4);
                case Wave.Sawtooth: return 2 * phase - 1;
                case Wave.Square: return phase < 0.5 ? 1 : -1;
                default: return Math.Sin(2 * Math.PI * phase);
            }
        }

        /// <summary>sound.js noise(): white noise mixed with a leaky-integrated (brown-ish) copy.</summary>
        public static float[] Noise(Random rng, int sr, float dur)
        {
            int n = (int)Math.Floor(sr * dur);
            float[] d = new float[n];
            double last = 0;
            for (int i = 0; i < n; i++)
            {
                double w = rng.NextDouble() * 2 - 1;
                last = (last + 0.02 * w) / 1.02;
                d[i] = (float)(last * 3.5 + w * 0.25);
            }
            return d;
        }

        /// <summary>sound.js pluckBuffer(): Karplus-Strong strings, one per frequency, each 28 ms after the last.</summary>
        public static float[] Pluck(float[] freqs, Random rng, int sr = Rate, double stagger = 0.028, double dur = 1.6, double bright = 0.996)
        {
            int len = (int)Math.Floor(sr * (dur + stagger * freqs.Length));
            float[] o = new float[len];
            for (int n = 0; n < freqs.Length; n++)
            {
                int N = Math.Max(2, (int)Math.Round(sr / freqs[n]));
                double[] ring = new double[N];
                for (int i = 0; i < N; i++) ring[i] = rng.NextDouble() * 2 - 1;
                int start = (int)Math.Floor(n * stagger * sr);
                double gain = 0.22 * (1 - n * 0.06);
                int idx = 0;
                double prev = 0;
                for (int i = start; i < len; i++)
                {
                    double cur = ring[idx];
                    double next = bright * 0.5 * (cur + prev);
                    prev = cur; ring[idx] = next; idx = (idx + 1) % N;
                    o[i] += (float)(cur * gain);
                }
            }
            return o;
        }

        // ------------------------------------------------------------------ master bus

        const double Threshold = -24, Knee = 30, Ratio = 12, Attack = 0.003, Release = 0.25;

        /// <summary>Static curve of the compressor in dB: linear below the threshold, a soft knee 30 dB wide, then 12:1.</summary>
        public static double CurveDb(double x)
        {
            double over = x - Threshold;
            if (over <= 0) return x;
            if (over < Knee) return x + (1.0 / Ratio - 1.0) * over * over / (2 * Knee);
            return Threshold + Knee + (1.0 / Ratio - 1.0) * Knee / 2 + (over - Knee) / Ratio;
        }

        /// <summary>The automatic make-up gain Chrome applies: (1 / gain at full scale) ^ 0.6, in dB.</summary>
        public static double MakeupDb { get { return 0.6 * (0 - CurveDb(0)); } }

        /// <summary>
        /// The master bus of sound.js (gain, DynamicsCompressorNode at its defaults, destination), applied to one
        /// sound: threshold -24 dB, knee 30, ratio 12, attack 3 ms, release 250 ms, make-up gain, then a hard limit at 1.
        /// </summary>
        public static void Master(float[] buf, int sr)
        {
            double atk = Math.Exp(-1.0 / (Attack * sr)), rel = Math.Exp(-1.0 / (Release * sr));
            double gainDb = 0, makeup = MakeupDb;
            for (int i = 0; i < buf.Length; i++)
            {
                double x = buf[i];
                double ax = Math.Abs(x);
                double xdb = ax > 1e-6 ? 20 * Math.Log10(ax) : -120;
                double target = CurveDb(xdb) - xdb;
                double coef = target < gainDb ? atk : rel;
                gainDb = coef * gainDb + (1 - coef) * target;
                double y = x * Math.Pow(10, (gainDb + makeup) / 20);
                buf[i] = (float)(y > 1 ? 1 : (y < -1 ? -1 : y));
            }
        }
    }

    /// <summary>A Web Audio BiquadFilterNode (Audio EQ Cookbook). Low- and high-pass Q is in dB, as the spec says; band-pass Q is linear.</summary>
    public sealed class Biquad
    {
        readonly double _b0, _b1, _b2, _a1, _a2;
        double _x1, _x2, _y1, _y2;

        Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
        }

        public static Biquad LowPass(int sr, float freq, float qDb)
        {
            double w0 = 2 * Math.PI * freq / sr, c = Math.Cos(w0), alpha = Math.Sin(w0) / (2 * Math.Pow(10, qDb / 20.0));
            return new Biquad((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + alpha, -2 * c, 1 - alpha);
        }

        public static Biquad HighPass(int sr, float freq, float qDb)
        {
            double w0 = 2 * Math.PI * freq / sr, c = Math.Cos(w0), alpha = Math.Sin(w0) / (2 * Math.Pow(10, qDb / 20.0));
            return new Biquad((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + alpha, -2 * c, 1 - alpha);
        }

        public static Biquad BandPass(int sr, float freq, float q)
        {
            double w0 = 2 * Math.PI * freq / sr, c = Math.Cos(w0), alpha = Math.Sin(w0) / (2 * q);
            return new Biquad(alpha, 0, -alpha, 1 + alpha, -2 * c, 1 - alpha);
        }

        public float Process(float x)
        {
            double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
            return (float)y;
        }
    }
}
