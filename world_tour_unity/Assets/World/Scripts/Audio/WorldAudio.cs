using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldTour.Game.Audio;

namespace WorldTour.World.Audio
{
    /// <summary>What <see cref="WorldAudio.Play"/> can play.</summary>
    public enum Cue { CardOpen, CardClose, Whoosh, GigDone, Unlock, Cheer }

    /// <summary>
    /// Phase 8.3: the sound of the world. Footsteps that follow her stride and suit the ground, a bed under each place (birds, a city hum, a hall murmur),
    /// the crowd murmur near the stage, applause on right answers in a show, a pop and a click as cards open and close, a short jingle for a gig done and a new stop.
    /// The cue clips (pop, click, whoosh, two jingles) are the CC0 Kenney ones, kept out of Resources in Assets/World/Audio/Clips and reached through the WorldAudioBank scene
    /// that only the 3D build contains, so the 2D tour build never carries them (see ASSETS.md). Everything else (birds, footsteps, applause, crowd murmur, city hum) is
    /// synthesised in code by <see cref="WorldSynth"/> and <see cref="MakeCityHum"/>, like the 2D game sounds.
    /// Nothing plays until the first click or key (<see cref="Sfx.Unlocked"/>), and everything obeys the remembered sound toggle (<see cref="Sfx.Enabled"/>).
    /// The beds stay quiet and drop while a gig or show panel is open, so the teaching sounds (<see cref="Sfx"/>) are always on top.
    /// </summary>
    public sealed class WorldAudio : MonoBehaviour
    {
        public static WorldAudio Instance { get; private set; }
        /// <summary>The scene (3D build only) that holds the cue clips; loaded once, additively, then unloaded.</summary>
        public const string BankScene = "WorldAudioBank";
        public const string ClipDir = "Assets/World/Audio/Clips/";
        /// <summary>The log line each sound writes ("WT_AUDIO event source"), so a run shows what fired. Footsteps log once per ground.</summary>
        public const string LogTag = "WT_AUDIO ";

        AudioSource _step, _bed, _crowd, _cheer, _ui;
        readonly Dictionary<Surface, AudioClip[]> _stepSets = new Dictionary<Surface, AudioClip[]>();
        AudioClip[] _steps;
        Surface _loggedSurface = (Surface)(-1);
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
        /// <summary>Test only (the audio probe): the crowd counts as full and close, so its level is the loudest it ever is.</summary>
        public bool ProbeLoudest;

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
            _murmur = Clip("wt_murmur", WorldSynth.Murmur());
            _applause = Clip("wt_applause", WorldSynth.Applause());
            _crowd.clip = _murmur;
            _cheer.clip = _applause;
            StartCoroutine(LoadCueClips());
        }

        static AudioClip Clip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, WorldSynth.SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>The five cue clips. In the Editor straight from the project; in the 3D build from the WorldAudioBank scene (no Resources folder, so the 2D build has none of it).</summary>
        IEnumerator LoadCueClips()
        {
#if UNITY_EDITOR
            _open = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(ClipDir + "ui_open.ogg");
            _close = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(ClipDir + "ui_close.ogg");
            _whoosh = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(ClipDir + "ui_whoosh.ogg");
            _done = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(ClipDir + "jingle_done.ogg");
            _unlock = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(ClipDir + "jingle_unlock.ogg");
            yield break;
#else
            if (!Application.CanStreamedLevelBeLoaded(BankScene)) yield break;
            AsyncOperation load = SceneManager.LoadSceneAsync(BankScene, LoadSceneMode.Additive);
            if (load == null) yield break;
            yield return load;
            WorldAudioClips bank = WorldAudioClips.Instance;
            if (bank != null)
            {
                _open = bank.open; _close = bank.close; _whoosh = bank.whoosh; _done = bank.done; _unlock = bank.unlock;
            }
            Scene s = SceneManager.GetSceneByName(BankScene);
            if (s.isLoaded) SceneManager.UnloadSceneAsync(s);
            Debug.Log(LogTag + "cue-clips loaded from scene " + BankScene + (bank != null ? "" : " (no bank found)"));
#endif
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
            if (!_stepSets.TryGetValue(_surface, out _steps))
            {
                _steps = new AudioClip[4];
                for (int i = 0; i < 4; i++) _steps[i] = Clip("wt_step_" + _surface + i, WorldSynth.Step(_surface, i));
                _stepSets[_surface] = _steps;
            }
            _bed.Stop();
            _bed.clip = ClipFor(_bedKind);
            _crowd.Stop();
            if (_started) StartLoops();
        }

        AudioClip ClipFor(Bed b)
        {
            switch (b)
            {
                case Bed.Birds: if (_birds == null) _birds = Clip("wt_birds", WorldSynth.Birds()); return _birds;
                case Bed.City: if (_city == null) _city = MakeCityHum(); return _city;
                default: return _murmur;
            }
        }

        void StartLoops()
        {
            if (_bed.clip != null && !_bed.isPlaying) { _bed.Play(); Debug.Log(LogTag + "bed " + _bedKind + " source=" + (_bedKind == Bed.City ? "synth:city-hum" : _bedKind == Bed.Birds ? "synth:birds" : "synth:murmur") + " place=" + _placeId); }
            if (_crowd.clip != null && !_crowd.isPlaying)
            {
                _crowd.Play();
                Debug.Log(LogTag + "crowd source=synth:murmur place=" + _placeId);
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
            float want = ProbeLoudest ? AudioPlan.CrowdVolume(1f, 0f, false) : AudioPlan.CrowdVolume(CrowdFill, dist, ShowOn);
            _crowd.volume = Mathf.MoveTowards(_crowd.volume, want, dt * 0.4f);

            UpdateSteps(dt);
            UpdateCheer();
            if (AudioMeter.Active) MeterFrame(dt);
        }

        /// <summary>Test only (AudioMeter): what each looping source is putting out this frame.</summary>
        void MeterFrame(float dt)
        {
            float on = Sfx.Enabled ? AudioListener.volume : 0f;
            if (_bed.isPlaying) AudioMeter.Loop("bed", _bed.clip, _bed.timeSamples, dt, _bed.volume * on);
            if (_crowd.isPlaying) AudioMeter.Loop("crowd", _crowd.clip, _crowd.timeSamples, dt, _crowd.volume * on);
            if (_cheer.isPlaying) AudioMeter.Loop("cheer", _cheer.clip, _cheer.timeSamples, dt, _cheer.volume * on);
            AudioMeter.Frame(dt);
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
            _step.pitch = UnityEngine.Random.Range(0.94f, 1.06f);   // the ground is in the synthesised step itself (WorldSynth.SpecFor)
            if (_loggedSurface != _surface) { _loggedSurface = _surface; Debug.Log(LogTag + "step synth:" + _surface + " place=" + _placeId); }
            float stepVol = AudioPlan.StepVolume(_surface) * UnityEngine.Random.Range(0.85f, 1f);
            _step.PlayOneShot(c, stepVol);
            if (AudioMeter.Active) AudioMeter.OneShot("steps", c, stepVol * (Sfx.Enabled ? AudioListener.volume : 0f), _step.pitch);
        }

        void PlayCue(Cue cue)
        {
            if (!Sfx.Unlocked || !Sfx.Enabled) return;
            Debug.Log(LogTag + "cue " + cue + " source=" + (cue == Cue.Cheer ? "synth:applause" : "clip:" + CueName(cue) + (CueClip(cue) == null ? " (MISSING)" : "")));
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

        AudioClip CueClip(Cue cue)
        {
            switch (cue)
            {
                case Cue.CardOpen: return _open;
                case Cue.CardClose: return _close;
                case Cue.Whoosh: return _whoosh;
                case Cue.GigDone: return _done;
                case Cue.Unlock: return _unlock;
                default: return null;
            }
        }

        static string CueName(Cue cue)
        {
            switch (cue)
            {
                case Cue.CardOpen: return "ui_open";
                case Cue.CardClose: return "ui_close";
                case Cue.Whoosh: return "ui_whoosh";
                case Cue.GigDone: return "jingle_done";
                default: return "jingle_unlock";
            }
        }

        void One(AudioClip c, float volume)
        {
            if (c == null) return;
            _ui.PlayOneShot(c, volume);
            if (AudioMeter.Active) AudioMeter.OneShot("ui:" + c.name, c, volume * (Sfx.Enabled ? AudioListener.volume : 0f));
        }

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
            float peak = 0.22f + 0.4f * Mathf.Clamp01(ProbeLoudest ? 1f : CrowdFill);
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
