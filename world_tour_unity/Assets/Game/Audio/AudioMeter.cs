using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WorldTour.Game.Audio
{
    /// <summary>
    /// Phase 10A: a level meter for the game's own sounds, used only by the browser test (the page has ?wt_test). Every sound the game starts is told to it
    /// (looping sources by their play position, one-shots when they start), and each frame it rebuilds what that source handed to the audio engine, sample
    /// by sample, from the clip data times the volume: so the peak and RMS it reports are what each source puts into the mix, before the browser's own
    /// resampling. "mix" is the sum of all of them, which is what would clip. Nothing here runs unless <see cref="Active"/> (no cost in play).
    /// Report lines: "WT_AUDIO_LEVELS label=... source=... peak=-12.3 rms=-30.1 seconds=60.0" (dBFS, RMS over the whole window including silence).
    /// </summary>
    public static class AudioMeter
    {
        public const string LevelsLog = "WT_AUDIO_LEVELS";
        const int OutRate = 44100, MaxFrame = 8192;

        sealed class Shot
        {
            public string Source; public float[] Data; public int Channels, Rate; public float Gain, Pitch; public double Pos;
        }

        sealed class Track { public float Peak; public double SumSq; public long N; }

        static readonly Dictionary<AudioClip, float[]> s_Data = new Dictionary<AudioClip, float[]>();
        static readonly List<Shot> s_Shots = new List<Shot>();
        static readonly Dictionary<string, Track> s_Tracks = new Dictionary<string, Track>();
        static readonly Dictionary<string, float[]> s_Frame = new Dictionary<string, float[]>();
        static readonly float[] s_Mix = new float[MaxFrame];
        static string s_Label = "";
        static double s_Seconds;

        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; s_Data.Clear(); s_Shots.Clear(); s_Tracks.Clear(); s_Frame.Clear(); }

        /// <summary>Starts a new window under this label (a place id); anything measured before is dropped.</summary>
        public static void Begin(string label)
        {
            Active = true;
            s_Label = label ?? "";
            s_Seconds = 0;
            s_Shots.Clear();
            s_Tracks.Clear();
        }

        public static void Stop() { Active = false; }

        static float[] DataOf(AudioClip clip)
        {
            if (clip == null) return null;
            float[] d;
            if (s_Data.TryGetValue(clip, out d)) return d;
            if (clip.loadState == AudioDataLoadState.Unloaded) { clip.LoadAudioData(); return null; }   // read in, then try again next time
            if (clip.loadState == AudioDataLoadState.Loading) return null;
            try
            {
                d = new float[clip.samples * clip.channels];
                if (!clip.GetData(d, 0)) d = null;
            }
            catch (Exception) { d = null; }
            s_Data[clip] = d;
            return d;
        }

        /// <summary>The highest sample of a clip (0 when it can't be read).</summary>
        public static float PeakOf(AudioClip clip)
        {
            float[] d = DataOf(clip);
            if (d == null) return 0f;
            float p = 0f;
            for (int i = 0; i < d.Length; i++) { float a = Math.Abs(d[i]); if (a > p) p = a; }
            return p;
        }

        /// <summary>A one-shot started now at this effective volume (source volume, group volume and listener volume multiplied).</summary>
        public static void OneShot(string source, AudioClip clip, float gain, float pitch = 1f)
        {
            if (!Active || clip == null) return;
            float[] d = DataOf(clip);
            if (d == null) return;
            s_Shots.Add(new Shot { Source = source, Data = d, Channels = Math.Max(1, clip.channels), Rate = clip.frequency, Gain = gain, Pitch = pitch <= 0f ? 1f : pitch, Pos = 0 });
        }

        /// <summary>One frame of a looping source: it is at <paramref name="timeSamples"/> now and has just played <paramref name="dt"/> seconds.</summary>
        public static void Loop(string source, AudioClip clip, int timeSamples, float dt, float gain, float pitch = 1f)
        {
            if (!Active || clip == null || gain <= 0f) return;
            float[] d = DataOf(clip);
            if (d == null || d.Length == 0) return;
            int ch = Math.Max(1, clip.channels), len = d.Length / ch;
            int m = Math.Min(MaxFrame, (int)Math.Round(dt * OutRate));
            double step = clip.frequency * (double)pitch / OutRate;
            double start = timeSamples - m * step;
            float[] buf = Buffer(source, m);
            for (int k = 0; k < m; k++)
            {
                int i = (int)Math.Floor(start + k * step) % len;
                if (i < 0) i += len;
                buf[k] += d[i * ch] * gain;
            }
        }

        static float[] Buffer(string source, int m)
        {
            float[] buf;
            if (!s_Frame.TryGetValue(source, out buf)) { buf = new float[MaxFrame]; s_Frame[source] = buf; }
            return buf;
        }

        /// <summary>Closes the frame: adds the running one-shots, then folds every source into its track and into the mix. Call once per frame, after the Loop calls.</summary>
        public static void Frame(float dt)
        {
            if (!Active) return;
            int m = Math.Min(MaxFrame, Math.Max(1, (int)Math.Round(dt * OutRate)));
            for (int s = s_Shots.Count - 1; s >= 0; s--)
            {
                Shot sh = s_Shots[s];
                float[] buf = Buffer(sh.Source, m);
                int len = sh.Data.Length / sh.Channels;
                double step = sh.Rate * (double)sh.Pitch / OutRate;
                for (int k = 0; k < m; k++)
                {
                    int i = (int)(sh.Pos + k * step);
                    if (i >= len) break;
                    buf[k] += sh.Data[i * sh.Channels] * sh.Gain;
                }
                sh.Pos += m * step;
                if (sh.Pos >= len) s_Shots.RemoveAt(s);
            }
            Array.Clear(s_Mix, 0, m);
            foreach (KeyValuePair<string, float[]> kv in s_Frame)
            {
                Track t;
                if (!s_Tracks.TryGetValue(kv.Key, out t)) { t = new Track(); s_Tracks[kv.Key] = t; }
                Fold(t, kv.Value, m);
                for (int k = 0; k < m; k++) s_Mix[k] += kv.Value[k];
                Array.Clear(kv.Value, 0, m);
            }
            Track mix;
            if (!s_Tracks.TryGetValue("mix", out mix)) { mix = new Track(); s_Tracks["mix"] = mix; }
            Fold(mix, s_Mix, m);
            s_Seconds += dt;
        }

        static void Fold(Track t, float[] buf, int m)
        {
            float peak = t.Peak;
            double sq = 0;
            for (int k = 0; k < m; k++) { float a = Math.Abs(buf[k]); if (a > peak) peak = a; sq += (double)buf[k] * buf[k]; }
            t.Peak = peak; t.SumSq += sq; t.N += m;
        }

        static string Db(double lin) { return (20.0 * Math.Log10(Math.Max(lin, 1e-6))).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>One log line per source seen in this window, plus "mix".</summary>
        public static string[] Report()
        {
            var lines = new List<string>();
            var names = new List<string>(s_Tracks.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (string n in names)
            {
                Track t = s_Tracks[n];
                // the window is the same length for every source, so a source that only spoke now and then is averaged over the whole window
                double rms = Math.Sqrt(t.SumSq / Math.Max(1.0, s_Seconds * OutRate));
                var sb = new StringBuilder(LevelsLog);
                sb.Append(" label=").Append(s_Label).Append(" source=").Append(n).Append(" peak=").Append(Db(t.Peak)).Append(" rms=").Append(Db(rms))
                  .Append(" seconds=").Append(s_Seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                lines.Add(sb.ToString());
            }
            if (lines.Count == 0) lines.Add(LevelsLog + " label=" + s_Label + " source=none peak=-120.0 rms=-120.0 seconds=" + s_Seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            return lines.ToArray();
        }
    }
}
