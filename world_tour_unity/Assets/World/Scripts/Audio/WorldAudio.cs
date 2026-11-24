using System;
using UnityEngine;
using WorldTour.Game.Audio;

namespace WorldTour.World.Audio
{
    /// <summary>What <see cref="WorldAudio.Play"/> can play.</summary>
    public enum Cue { CardOpen, CardClose, Whoosh, GigDone, Unlock, Cheer }

    /// <summary>
    /// Phase 8.3: the sound of the world. Footsteps that follow her stride and suit the ground, a bed under each place (birds, a city hum, a hall murmur),
    /// the crowd murmur near the stage, applause on right answers in a show, a pop and a click as cards open and close, a short jingle for a gig done and a new stop.
    /// The clips are the CC0 ones in Resources/WorldAudio (see ASSETS.md); the city hum is synthesised here, like the 2D game sounds.
    /// Nothing plays until the first click or key (<see cref="Sfx.Unlocked"/>), and everything obeys the remembered sound toggle (<see cref="Sfx.Enabled"/>).
    /// The beds stay quiet and drop while a gig or show panel is open, so the teaching sounds (<see cref="Sfx"/>) are always on top.
    /// </summary>
    public sealed class WorldAudio : MonoBehaviour
    {
        public static WorldAudio Instance { get; private set; }
        const string Dir = "WorldAudio/";

        AudioSource _step, _bed, _crowd, _cheer, _ui;
        AudioClip[] _steps;
        AudioClip _birds, _murmur, _city, _applause, _open, _close, _whoosh, _done, _unlock;
        string _placeId = "";
        Surface _surface = Surface.Stone;
        Bed _bedKind = Bed.Birds;
        PlayerController _player;
        Transform _crowdCenter;
        float _stride, _cheerAt = -10f, _lastCheer = -10f, _bedGain = 1f;
        int _lastStep = -1;
        bool _started;

        /// <summary>0 to 1: how full the crowd is (set by the gameplay).</summary>
        public float CrowdFill;
        /// <summary>A gig or a show panel is open: the bed drops.</summary>
        public bool PanelOpen;
        /// <summary>The stage show is on: the crowd murmur stays up.</summary>
        public bool ShowOn;

        /// <summary>The place the sound is bound to (tests and the browser log).</summary>
        public string PlaceId { get { return _placeId; } }
        public Surface CurrentSurface { get { return _surface; } }
        public Bed CurrentBed { get { return _bedKind; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        public static WorldAudio Attach(GameObject host)
        {
            var a = host.GetComponent<WorldAudio>();
            return a != null ? a : host.AddComponent<WorldAudio>();
        }

        /// <summary>A one-off sound (silent until the first click or key, and when sound is off).</summary>
        public static void Play(Cue cue)
        {
            if (Instance != null) Instance.PlayCue(cue);
        }

        void Awake()
        {
            Instance = this;
            _step = Source("Steps", false);
            _bed = Source("Bed", true);
            _crowd = Source("Crowd", true);
            _cheer = Source("Cheer", false);
            _ui = Source("Ui", false);
            _steps = new AudioClip[4];
            for (int i = 0; i < 4; i++) _steps[i] = Resources.Load<AudioClip>(Dir + "step_" + (i + 1));
            _birds = Resources.Load<AudioClip>(Dir + "amb_birds");
            _murmur = Resources.Load<AudioClip>(Dir + "amb_murmur");
            _applause = Resources.Load<AudioClip>(Dir + "cheer_applause");
            _open = Resources.Load<AudioClip>(Dir + "ui_open");
            _close = Resources.Load<AudioClip>(Dir + "ui_close");
            _whoosh = Resources.Load<AudioClip>(Dir + "ui_whoosh");
            _done = Resources.Load<AudioClip>(Dir + "jingle_done");
            _unlock = Resources.Load<AudioClip>(Dir + "jingle_unlock");
            _crowd.clip = _murmur;
            _cheer.clip = _applause;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        /// <summary>A new place has loaded: its ground, its bed, its crowd.</summary>
        public void Bind(string placeId, PlayerController player, Transform crowdCenter)
        {
            _placeId = placeId;
            _player = player;
            _crowdCenter = crowdCenter;
            _surface = AudioPlan.SurfaceFor(placeId);
            _bedKind = AudioPlan.BedFor(placeId);
            _stride = AudioPlan.Stride * 0.5f;
            _bed.Stop();
            _bed.clip = ClipFor(_bedKind);
            _crowd.Stop();
            if (_started) StartLoops();
        }

        AudioClip ClipFor(Bed b)
        {
            switch (b)
            {
                case Bed.Birds: return _birds;
                case Bed.City: if (_city == null) _city = MakeCityHum(); return _city;
                default: return _murmur;
            }
        }

        void StartLoops()
        {
            if (_bed.clip != null && !_bed.isPlaying) _bed.Play();
            if (_crowd.clip != null && !_crowd.isPlaying)
            {
                _crowd.Play();
                _crowd.time = Mathf.Min(_crowd.clip.length * 0.37f, Mathf.Max(0f, _crowd.clip.length - 0.1f));   // not in step with the hall bed, which is the same clip
            }
        }

        void Update()
        {
            bool on = Sfx.Enabled;
            AudioSource[] all = { _step, _bed, _crowd, _cheer, _ui };
            for (int i = 0; i < all.Length; i++) all[i].mute = !on;
            if (!Sfx.Unlocked) return;
            if (!_started) { _started = true; StartLoops(); }

            float dt = Time.unscaledDeltaTime;
            _bedGain = Mathf.MoveTowards(_bedGain, PanelOpen ? 0.55f : 1f, dt * 1.5f);
            _bed.volume = AudioPlan.BedVolume(_bedKind) * _bedGain;
            float dist = _player != null && _crowdCenter != null
                ? Vector3.Distance(new Vector3(_player.transform.position.x, 0, _player.transform.position.z), new Vector3(_crowdCenter.position.x, 0, _crowdCenter.position.z))
                : 99f;
            float want = AudioPlan.CrowdVolume(CrowdFill, dist, ShowOn);
            _crowd.volume = Mathf.MoveTowards(_crowd.volume, want, dt * 0.4f);

            UpdateSteps(dt);
            UpdateCheer();
        }

        /// <summary>One step per stride of walking (about 1.6 m): the clip, its pitch and its volume vary a little, and the ground sets the base.</summary>
        void UpdateSteps(float dt)
        {
            if (_player == null) return;
            float v = _player.planarSpeed;
            if (v < 0.8f) { _stride = AudioPlan.Stride * 0.5f; return; }
            _stride -= v * dt;
            if (_stride > 0f) return;
            _stride += AudioPlan.Stride;
            int pick = UnityEngine.Random.Range(0, _steps.Length);
            if (pick == _lastStep) pick = (pick + 1) % _steps.Length;
            _lastStep = pick;
            AudioClip c = _steps[pick];
            if (c == null) return;
            _step.pitch = AudioPlan.StepPitch(_surface) * UnityEngine.Random.Range(0.94f, 1.06f);
            _step.PlayOneShot(c, AudioPlan.StepVolume(_surface) * UnityEngine.Random.Range(0.85f, 1f));
        }

        void PlayCue(Cue cue)
        {
            if (!Sfx.Unlocked || !Sfx.Enabled) return;
            switch (cue)
            {
                case Cue.CardOpen: One(_open, 0.5f); break;
                case Cue.CardClose: One(_close, 0.4f); break;
                case Cue.Whoosh: One(_whoosh, 0.45f); break;
                case Cue.GigDone: One(_done, 0.5f); break;
                case Cue.Unlock: One(_unlock, 0.55f); break;
                case Cue.Cheer: StartCheer(); break;
            }
        }

        void One(AudioClip c, float volume) { if (c != null) _ui.PlayOneShot(c, volume); }

        /// <summary>Applause for a right answer: about 2 s, as loud as the crowd is full. One at a time, and not again within 1.2 s.</summary>
        void StartCheer()
        {
            if (_applause == null || Time.unscaledTime - _lastCheer < 1.2f) return;
            _lastCheer = _cheerAt = Time.unscaledTime;
            _cheer.Stop();
            _cheer.time = 0f;
            _cheer.volume = 0f;
            _cheer.Play();
        }

        void UpdateCheer()
        {
            if (!_cheer.isPlaying) return;
            float t = Time.unscaledTime - _cheerAt;
            float peak = 0.22f + 0.4f * Mathf.Clamp01(CrowdFill);
            float env = t < 0.12f ? t / 0.12f : Mathf.Clamp01(1f - (t - 1.3f) / 0.9f);
            _cheer.volume = peak * env;
            if (t > 2.3f) _cheer.Stop();
        }

        /// <summary>
        /// A soft city hum, 8 s that loops without a click: low-passed noise, two low tones (55 Hz and 82.5 Hz, whole cycles in the loop) and a slow swell.
        /// The end is cross-faded into the start. Made once, the first time a place needs it.
        /// </summary>
        static AudioClip MakeCityHum()
        {
            const int sr = 22050, seconds = 8, fade = sr / 2;
            int n = sr * seconds;
            var rng = new System.Random(4242);
            var raw = new float[n + fade];
            float a = 0f, b = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                a += (white - a) * 0.035f;   // two one-pole low-passes: a rumble, no hiss
                b += (a - b) * 0.12f;
                raw[i] = b;
            }
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float tone = 0.05f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.03f * Mathf.Sin(2f * Mathf.PI * 82.5f * t);
                float swell = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                data[i] = (raw[i] * 3.2f + tone) * swell;
            }
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                data[i] = data[i] * w + raw[n + i] * 3.2f * (1f - w);   // the tail that follows the loop end, blended in at the start
            }
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
            AudioClip clip = AudioClip.Create("wt_city_hum", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
