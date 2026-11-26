using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldTour.Game.Audio
{
    /// <summary>The fixed sounds, made once at start-up. Names follow world_tour/js/sound.js.</summary>
    public enum SfxId
    {
        /// <summary>tap: ordinary clicks, picking a tile, undo, a hint.</summary>
        Tap,
        /// <summary>pop: equipping, a sticker stuck down.</summary>
        Pop,
        /// <summary>peel: a sticker peeling on.</summary>
        Peel,
        /// <summary>thud: a wrong answer (soft, never harsh).</summary>
        Thud,
        /// <summary>strum('win'): a correct answer.</summary>
        Strum,
        /// <summary>strum('big'): a purchase, bigger moments.</summary>
        StrumBig,
        /// <summary>tick: soundcheck's bonus beep.</summary>
        Tick,
    }

    /// <summary>
    /// Plays the synthesised sounds (<see cref="Synth"/>). One persistent AudioSource with PlayOneShot, which the Web
    /// player supports, and clips from AudioClip.Create(stream: false) + SetData, the path the 0.2 smoke build proved.
    /// Nothing plays until the first click or key press (browsers block audio before a gesture, and the brief says so);
    /// <see cref="Enabled"/> mirrors the remembered mute toggle (settings.sound), set by GameSettings.
    /// Logs "WT_SOUND_READY clips=7 rate=44100 mode=setdata" once the clips exist and one of them reads back with
    /// non-zero samples (AudioClip.GetData), else a warning "WT_SOUND_UNVERIFIED ..."; and "WT_SOUND_UNLOCKED" at the
    /// first gesture. <see cref="UsePcmCallback"/> switches clip creation to a PCMReaderCallback, in case SetData turns
    /// out silent on the Web (the manual only promises SetData for imported Decompress-on-Load clips).
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        public const string ReadyLog = "WT_SOUND_READY";
        public const string UnlockedLog = "WT_SOUND_UNLOCKED";
        public const string UnverifiedLog = "WT_SOUND_UNVERIFIED";

        /// <summary>
        /// Off (default): AudioClip.Create(stream: false) then SetData, the path the 0.2 smoke build ran. On: the samples are
        /// handed over through a PCMReaderCallback at creation instead. Set it before <see cref="Prepare"/>.
        /// </summary>
        public static bool UsePcmCallback;

        /// <summary>Phase 10A: the slider defaults. Ambience (the music and ambience slider: birds and the guitar) starts at 40 percent, effects at full.</summary>
        public const float DefaultAmbience = 0.4f, DefaultEffects = 1f;
        /// <summary>Phase 10A: how loud each sound may be at full slider: its peak sample, linear. The tap is the reference and plays as made (-21 dBFS);
        /// nothing else peaks over -12 dBFS (0.25) except the end-of-show crowd, which is applause.</summary>
        public const float GuitarPeak = 0.25f, EffectPeak = 0.2f, BeatPeak = 0.2f, CrowdPeak = 0.4f;

        static Sfx s_Instance;
        static bool s_Enabled = true;
        static bool s_Unlocked;
        static float s_Ambience = DefaultAmbience, s_Effects = DefaultEffects;
        static readonly GuitarPlanner s_Guitar = new GuitarPlanner();
        static readonly System.Random s_Rng = new System.Random();
        static int s_CrowdPhrase = -1;

        AudioSource _src;
        readonly AudioClip[] _clips = new AudioClip[7];
        readonly float[] _gain = new float[7];
        readonly AudioClip[] _win = new AudioClip[Synth.PhraseCount];
        readonly AudioClip[] _big = new AudioClip[Synth.PhraseCount];
        readonly Dictionary<int, AudioClip> _crowd = new Dictionary<int, AudioClip>();
        AudioClip _beat;
        bool _building;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Instance = null; s_Enabled = true; s_Unlocked = false; UsePcmCallback = false; Ready = false;
            s_Ambience = DefaultAmbience; s_Effects = DefaultEffects; s_Guitar.Reset(); s_CrowdPhrase = -1;
        }

        /// <summary>The music and ambience slider, 0 to 1 (the birds and the guitar). Set by GameSettings.</summary>
        public static float AmbienceVolume { get { return s_Ambience; } set { s_Ambience = Mathf.Clamp01(value); } }

        /// <summary>The effects slider, 0 to 1 (footsteps, taps, thuds, cues, applause, the beat). Set by GameSettings.</summary>
        public static float EffectsVolume { get { return s_Effects; } set { s_Effects = Mathf.Clamp01(value); } }

        /// <summary>True after the first click or key press of the session.</summary>
        public static bool Unlocked { get { return s_Unlocked; } }

        /// <summary>True when every fixed clip has been made.</summary>
        public static bool Ready { get; private set; }

        /// <summary>Sound on or off (settings.sound). Off also silences whatever is playing, as sound.js setEnabled does.</summary>
        public static bool Enabled
        {
            get { return s_Enabled; }
            set
            {
                s_Enabled = value;
                if (s_Instance != null && s_Instance._src != null) s_Instance._src.mute = !value;
            }
        }

        static Sfx Instance
        {
            get
            {
                if (s_Instance != null) return s_Instance;
                if (!Application.isPlaying) return null;
                var go = new GameObject("[Sfx]");
                DontDestroyOnLoad(go);
                s_Instance = go.AddComponent<Sfx>();
                return s_Instance;
            }
        }

        void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _src.volume = 1f;
            _src.mute = !s_Enabled;
        }

        /// <summary>Makes the clips over the next few frames (one per frame, so nothing hitches). Safe to call more than once.</summary>
        public static void Prepare()
        {
            Sfx i = Instance;
            if (i == null || i._building || Ready) return;
            i._building = true;
            i.StartCoroutine(i.Build());
        }

        IEnumerator Build()
        {
            int sr = Synth.Rate;
            for (int k = 0; k < _clips.Length; k++)
            {
                float[] data;
                try { data = Render((SfxId)k, sr); }
                catch (Exception e) { Debug.LogWarning("[sound] could not make " + (SfxId)k + ": " + e.Message); data = null; }
                if (data != null)
                {
                    _clips[k] = MakeClip("wt_" + ((SfxId)k).ToString().ToLowerInvariant(), data, sr);
                    _gain[k] = GainFor((SfxId)k, data);
                }
                yield return null;
            }
            // the guitar phrases (the two strums above are only the first of each set, kept for the tests and the 2D-era callers)
            for (int p = 0; p < Synth.PhraseCount; p++)
            {
                try
                {
                    _win[p] = MakeClip("wt_win" + p, Synth.Phrase(false, p, new System.Random(300 + p), sr), sr);
                    _big[p] = MakeClip("wt_big" + p, Synth.Phrase(true, p, new System.Random(400 + p), sr), sr);
                }
                catch (Exception e) { Debug.LogWarning("[sound] could not make guitar phrase " + p + ": " + e.Message); }
                yield return null;
            }
            Ready = true;
            _building = false;
            int made = 0;
            foreach (AudioClip c in _clips) if (c != null) made++;
            string tail = " clips=" + made + " rate=" + sr + " mode=" + (UsePcmCallback ? "pcmcallback" : "setdata");
            if (Verify(_clips[(int)SfxId.Tick]) || Verify(_clips[(int)SfxId.Tap])) Debug.Log(ReadyLog + tail);
            else Debug.LogWarning(UnverifiedLog + tail + " (the clips read back as silence or could not be read)");
        }

        /// <summary>The playback gain that brings a sound's peak to its allowed level (never above 1): the tap plays as made, the others are held to <see cref="EffectPeak"/>.</summary>
        public static float GainFor(SfxId id, float[] data)
        {
            if (id == SfxId.Tap) return 1f;
            float peak = 1e-4f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float cap = id == SfxId.Strum || id == SfxId.StrumBig ? GuitarPeak : EffectPeak;
            return Mathf.Min(1f, cap / peak);
        }

        /// <summary>True if the clip reads back (GetData) with at least one non-zero sample.</summary>
        static bool Verify(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0) return false;
            try
            {
                var back = new float[clip.samples * clip.channels];
                if (!clip.GetData(back, 0)) return false;
                foreach (float x in back) if (x > 1e-6f || x < -1e-6f) return true;
                return false;
            }
            catch (Exception) { return false; }
        }

        /// <summary>The samples for one fixed sound. Each noisy sound has its own fixed seed, so it sounds the same every session.</summary>
        public static float[] Render(SfxId id, int sr)
        {
            switch (id)
            {
                case SfxId.Tap: return Synth.Tap(sr);
                case SfxId.Pop: return Synth.Pop(sr);
                case SfxId.Peel: return Synth.Peel(sr);
                case SfxId.Thud: return Synth.Thud(new System.Random(101), sr);
                case SfxId.Strum: return Synth.Strum(Synth.ChordWin, new System.Random(102), sr);
                case SfxId.StrumBig: return Synth.Strum(Synth.ChordBig, new System.Random(103), sr);
                case SfxId.Tick: return Synth.Tick(sr);
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }

        static AudioClip MakeClip(string name, float[] data, int sr)
        {
            if (UsePcmCallback)
            {
                int pos = 0;
                return AudioClip.Create(name, data.Length, 1, sr, false,
                    buf =>
                    {
                        int n = Math.Min(buf.Length, data.Length - pos);
                        if (n > 0) Array.Copy(data, pos, buf, 0, n);
                        for (int i = Math.Max(n, 0); i < buf.Length; i++) buf[i] = 0f;
                        pos += Math.Max(n, 0);
                    },
                    p => { pos = Math.Max(0, Math.Min(p, data.Length)); });
            }
            AudioClip clip = AudioClip.Create(name, data.Length, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Lets sound play from now on. Called by the start card's click; the first press anywhere does it too.</summary>
        public static void Unlock()
        {
            if (s_Unlocked) return;
            s_Unlocked = true;
            Debug.Log(UnlockedLog);
        }

        /// <summary>Plays a fixed sound, unless sound is off, not yet unlocked, or the clip isn't made yet.</summary>
        public static void Play(SfxId id, float volume = 1f)
        {
            Sfx i = s_Instance;
            if (!s_Unlocked || !s_Enabled || i == null) return;
            if (id == SfxId.Strum || id == SfxId.StrumBig) { i.PlayGuitar(id == SfxId.StrumBig, volume); return; }
            AudioClip c = i._clips[(int)id];
            if (c == null) return;
            float v = volume * i._gain[(int)id] * s_Effects;
            if (v <= 0f) return;
            i._src.PlayOneShot(c, v);
            if (AudioMeter.Active) AudioMeter.OneShot("sfx:" + id, c, v * AudioListener.volume);
        }

        /// <summary>Phase 10A: a right answer or a big moment is a guitar phrase, one of five, never the same twice running, at most once every six seconds, quiet, and on the music slider. Too soon, or the slider off: silence.</summary>
        void PlayGuitar(bool big, float volume)
        {
            if (s_Ambience <= 0f || !_win[0]) return;
            int p = s_Guitar.Request(Time.unscaledTimeAsDouble, s_Rng);
            if (p < 0) return;
            AudioClip c = (big ? _big : _win)[p];
            if (c == null) return;
            float v = volume * GuitarPeak * s_Ambience;
            _src.PlayOneShot(c, v);
            Debug.Log("WT_AUDIO guitar phrase=" + p + (big ? " big" : " win"));
            if (AudioMeter.Active) AudioMeter.OneShot("sfx:" + (big ? "StrumBig" : "Strum"), c, v * AudioListener.volume);
        }

        /// <summary>Makes the crowd for a venue size ahead of time (call when a show starts), so the ticket screen doesn't wait.</summary>
        public static void PrepareCrowd(double size)
        {
            if (Instance == null) return;
            if (s_CrowdPhrase < 0) s_CrowdPhrase = NextCrowdPhrase();
            Instance.CrowdClip(size, s_CrowdPhrase);
        }

        static int s_LastCrowdPhrase = -1;

        /// <summary>The next end-of-show guitar phrase: any of the five except the last one used.</summary>
        static int NextCrowdPhrase()
        {
            int p = s_LastCrowdPhrase < 0 ? s_Rng.Next(Synth.PhraseCount) : s_Rng.Next(Synth.PhraseCount - 1);
            if (s_LastCrowdPhrase >= 0 && p >= s_LastCrowdPhrase) p++;
            return s_LastCrowdPhrase = p;
        }

        /// <summary>crowd(size): the end-of-show swell with claps and a guitar chord (a different phrase each show). Size 0..1 (Venue.Crowd). It is applause: on the effects slider, held to <see cref="CrowdPeak"/>.</summary>
        public static void Crowd(double size)
        {
            Sfx i = s_Instance;
            if (!s_Unlocked || !s_Enabled || i == null) return;
            int phrase = s_CrowdPhrase >= 0 ? s_CrowdPhrase : NextCrowdPhrase();
            s_CrowdPhrase = -1;
            AudioClip c = i.CrowdClip(size, phrase);
            if (c == null || s_Effects <= 0f) return;
            float v = i._crowdGain[CrowdKey(size, phrase)] * s_Effects;
            i._src.PlayOneShot(c, v);
            if (AudioMeter.Active) AudioMeter.OneShot("sfx:crowd", c, v * AudioListener.volume);
        }

        readonly Dictionary<int, float> _crowdGain = new Dictionary<int, float>();

        static int CrowdKey(double size, int phrase) { return (int)Math.Round(Math.Max(0, Math.Min(1, size)) * 20) * 10 + phrase; }

        AudioClip CrowdClip(double size, int phrase)
        {
            int key = CrowdKey(size, phrase);
            if (_crowd.TryGetValue(key, out AudioClip c)) return c;
            try
            {
                float[] data = Synth.Crowd(key / 10 / 20f, new System.Random(200 + key / 10), Synth.Rate, phrase);
                c = MakeClip("wt_crowd_" + key, data, Synth.Rate);
                float peak = 1e-4f;
                for (int k = 0; k < data.Length; k++) peak = Mathf.Max(peak, Mathf.Abs(data[k]));
                _crowdGain[key] = Mathf.Min(1f, CrowdPeak / peak);
            }
            catch (Exception e) { Debug.LogWarning("[sound] could not make the crowd: " + e.Message); c = null; }
            _crowd[key] = c;
            return c;
        }
        /// <summary>beat(notes): plays one beat-maker measure and returns its length in seconds (0 when silent), as sound.js does.</summary>
        public static double Beat(BeatNote[] notes, double whole = 1.6)
        {
            Sfx i = s_Instance;
            if (!s_Unlocked || !s_Enabled || i == null) return 0;
            float[] data = Synth.Beat(notes, out double seconds, whole, Synth.Rate);
            if (i._beat != null) Destroy(i._beat);
            i._beat = MakeClip("wt_beat", data, Synth.Rate);
            float peakB = 1e-4f;
            for (int k = 0; k < data.Length; k++) peakB = Mathf.Max(peakB, Mathf.Abs(data[k]));
            float vb = Mathf.Min(1f, BeatPeak / peakB) * s_Effects;
            if (vb > 0f) i._src.PlayOneShot(i._beat, vb);
            if (AudioMeter.Active && vb > 0f) AudioMeter.OneShot("sfx:beat", i._beat, vb * AudioListener.volume);
            return seconds;
        }

        void Update()
        {
            if (s_Unlocked) return;
            // main.js: addEventListener('pointerdown', unlock, { once: true }). Keyboard counts too: she can play from the keys alone.
            Mouse m = Mouse.current;
            Keyboard k = Keyboard.current;
            if ((m != null && m.leftButton.wasPressedThisFrame) || (k != null && k.anyKey.wasPressedThisFrame)) Unlock();
        }
    }
}
