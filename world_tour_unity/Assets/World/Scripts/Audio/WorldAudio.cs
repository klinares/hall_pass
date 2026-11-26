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
    /// The sound of the world. Footsteps that follow her stride and suit the ground, a rare soft bird chirp in the daytime outdoor places, applause on right answers in a show,
    /// a pop and a click as cards open and close, a short jingle for a gig done and a new stop.
    /// The cue clips (pop, click, whoosh, two jingles) are the CC0 Kenney ones, kept out of Resources in Assets/World/Audio/Clips and reached through the WorldAudioBank scene
    /// that only the 3D build contains, so the 2D tour build never carries them (see ASSETS.md). Everything else (birds, footsteps, applause) is synthesised in code by <see cref="WorldSynth"/>.
    /// Nothing plays until the first click or key (<see cref="Sfx.Unlocked"/>), and everything obeys the remembered sound settings: <see cref="Sfx.AmbienceVolume"/> (the birds) and
    /// <see cref="Sfx.EffectsVolume"/> (everything else here).
    /// Phase 10A, the "loud static" fix: there is no noise bed any more (the city hum and the hall and crowd murmur are gone), every sound has a peak ceiling (see <see cref="AudioPlan"/>),
    /// and the whole mix is held 1.5 dB down (<see cref="Headroom"/>) so the sources can never add up to clipping.
    /// </summary>
    public sealed class WorldAudio : MonoBehaviour
    {
        public static WorldAudio Instance { get; private set; }
        /// <summary>The scene (3D build only) that holds the cue clips; loaded once, additively, then unloaded.</summary>
        public const string BankScene = "WorldAudioBank";
        public const string ClipDir = "Assets/World/Audio/Clips/";
        /// <summary>The log line each sound writes ("WT_AUDIO event source"), so a run shows what fired. Footsteps log once per ground.</summary>
        public const string LogTag = "WT_AUDIO ";
        /// <summary>Master gain on everything Unity plays (AudioListener.volume): 0.85, about -1.4 dB. The loudest each source can be is capped, and these caps add up to under 1 with this on top.</summary>
        public const float Headroom = 0.85f;

        AudioSource _step, _bird, _cheer, _ui;
        readonly Dictionary<Surface, AudioClip[]> _stepSets = new Dictionary<Surface, AudioClip[]>();
        AudioClip[] _steps;
        AudioClip[] _chirps;
        readonly Dictionary<AudioClip, float> _peaks = new Dictionary<AudioClip, float>();
        Surface _loggedSurface = (Surface)(-1);
        AudioClip _applause, _open, _close, _whoosh, _done, _unlock;
        string _placeId = "";
        Surface _surface = Surface.Stone;
        Bed _bedKind = Bed.None;
        PlayerController _player;
        float _stride, _cheerAt = -10f, _lastCheer = -10f, _birdWait = 30f;
        int _lastStep = -1, _lastChirp = -1;
        bool _started;

        /// <summary>0 to 1: how full the crowd is (set by the gameplay); the applause is louder with a fuller crowd.</summary>
        public float CrowdFill;
        /// <summary>A gig or a show panel is open: no birds.</summary>
        public bool PanelOpen;
        /// <summary>The stage show is on.</summary>
        public bool ShowOn;
        /// <summary>Test only (the audio probe): the crowd counts as full, so the applause is as loud as it ever gets.</summary>
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
            AudioListener.volume = Headroom;
            _step = Source("Steps");
            _bird = Source("Birds");
            _cheer = Source("Cheer");
            _ui = Source("Ui");
            _applause = Clip("wt_applause", WorldSynth.Applause());
            _cheer.clip = _applause;
            _chirps = new AudioClip[WorldSynth.ChirpCount];
            for (int i = 0; i < _chirps.Length; i++) _chirps[i] = Clip("wt_chirp" + i, WorldSynth.BirdChirp(i));
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

        AudioSource Source(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        /// <summary>A new place has loaded: its ground and whether it has birds. The first chirp comes 20 to 40 seconds in.</summary>
        public void Bind(string placeId, PlayerController player, Transform crowdCenter)
        {
            _placeId = placeId;
            _player = player;
            _surface = AudioPlan.SurfaceFor(placeId);
            _bedKind = AudioPlan.BedFor(placeId);
            _stride = AudioPlan.Stride * 0.5f;
            _birdWait = AudioPlan.BirdGap(UnityEngine.Random.value);
            if (!_stepSets.TryGetValue(_surface, out _steps))
            {
                _steps = new AudioClip[4];
                for (int i = 0; i < 4; i++) _steps[i] = Clip("wt_step_" + _surface + i, WorldSynth.Step(_surface, i));
                _stepSets[_surface] = _steps;
            }
            Debug.Log(LogTag + "place " + placeId + " bed=" + _bedKind + (_bedKind == Bed.Birds ? " source=synth:birds" : " (silent)"));
        }

        void Update()
        {
            bool on = Sfx.Enabled;
            AudioSource[] all = { _step, _bird, _cheer, _ui };
            for (int i = 0; i < all.Length; i++) all[i].mute = !on;
            if (!Sfx.Unlocked) return;

            float dt = Time.unscaledDeltaTime;
            UpdateBirds(dt);
            UpdateSteps(dt);
            UpdateCheer();
            if (AudioMeter.Active) MeterFrame(dt);
        }

        /// <summary>
        /// One short soft chirp every 20 to 40 seconds, only in a daytime outdoor place (<see cref="AudioPlan.BedFor"/>), never while a gig or show panel is open, never with the ambience slider off.
        /// The wait only runs down while a chirp would be allowed, so two chirps are always at least 20 seconds apart. Never the same call twice running.
        /// </summary>
        void UpdateBirds(float dt)
        {
            if (_bedKind != Bed.Birds || PanelOpen || _player == null || Sfx.AmbienceVolume <= 0f) return;
            _birdWait -= dt;
            if (_birdWait > 0f) return;
            _birdWait = AudioPlan.BirdGap(UnityEngine.Random.value);
            int pick = UnityEngine.Random.Range(0, _chirps.Length);
            if (pick == _lastChirp) pick = (pick + 1) % _chirps.Length;
            _lastChirp = pick;
            float v = AudioPlan.BirdPeak * Sfx.AmbienceVolume;
            _bird.PlayOneShot(_chirps[pick], v);
            Debug.Log(LogTag + "bird chirp=" + pick + " source=synth:birds place=" + _placeId);
            if (AudioMeter.Active) AudioMeter.OneShot("birds", _chirps[pick], v * (Sfx.Enabled ? AudioListener.volume : 0f));
        }

        /// <summary>Test only (AudioMeter): what the looping source is putting out this frame (the applause is the only one that is driven frame by frame).</summary>
        void MeterFrame(float dt)
        {
            float on = Sfx.Enabled ? AudioListener.volume : 0f;
            if (_cheer.isPlaying) AudioMeter.Loop("cheer", _cheer.clip, _cheer.timeSamples, dt, _cheer.volume * on);
            AudioMeter.Frame(dt);
        }

        /// <summary>Test only: reads the cue clips into memory now, so the meter can see their samples.</summary>
        public void WarmCueClips()
        {
            foreach (AudioClip c in new[] { _open, _close, _whoosh, _done, _unlock }) if (c != null) c.LoadAudioData();
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
            float stepVol = AudioPlan.StepVolume(_surface) * UnityEngine.Random.Range(0.85f, 1f) * Sfx.EffectsVolume;
            if (stepVol <= 0f) return;
            _step.PlayOneShot(c, stepVol);
            if (AudioMeter.Active) AudioMeter.OneShot("steps", c, stepVol * (Sfx.Enabled ? AudioListener.volume : 0f), _step.pitch);
        }

        void PlayCue(Cue cue)
        {
            if (!Sfx.Unlocked || !Sfx.Enabled) return;
            Debug.Log(LogTag + "cue " + cue + " source=" + (cue == Cue.Cheer ? "synth:applause" : "clip:" + CueName(cue) + (CueClip(cue) == null ? " (MISSING)" : "")));
            switch (cue)
            {
                case Cue.CardOpen: One(_open, AudioPlan.CueCard); break;
                case Cue.CardClose: One(_close, AudioPlan.CueCard); break;
                case Cue.Whoosh: One(_whoosh, AudioPlan.CueWhoosh); break;
                case Cue.GigDone: One(_done, AudioPlan.CueJingle); break;
                case Cue.Unlock: One(_unlock, AudioPlan.CueJingle); break;
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

        /// <summary>The cue's volume that brings the clip's own peak to <paramref name="peak"/> (a clip that can't be read is treated as full scale, so it is only ever quieter than asked).</summary>
        float CueVolume(AudioClip c, float peak)
        {
            float p;
            if (!_peaks.TryGetValue(c, out p) || p <= 0f)
            {
                p = AudioMeter.PeakOf(c);
                if (p > 0f) _peaks[c] = p;
            }
            return Mathf.Min(1f, peak / Mathf.Max(0.05f, p > 0f ? p : 1f)) * Sfx.EffectsVolume;
        }

        void One(AudioClip c, float peak)
        {
            if (c == null) return;
            float volume = CueVolume(c, peak);
            if (volume <= 0f) return;
            _ui.PlayOneShot(c, volume);
            if (AudioMeter.Active) AudioMeter.OneShot("ui:" + c.name, c, volume * (Sfx.Enabled ? AudioListener.volume : 0f));
        }

        /// <summary>Applause for a right answer: about 2 s, as loud as the crowd is full. One at a time, and not again within 1.2 s.</summary>
        void StartCheer()
        {
            if (_applause == null || Time.unscaledTime - _lastCheer < 1.2f || Sfx.EffectsVolume <= 0f) return;
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
            float peak = AudioPlan.CheerPeak(ProbeLoudest ? 1f : CrowdFill) / 0.8f * Sfx.EffectsVolume;   // the clip itself peaks at 0.8
            float env = t < 0.12f ? t / 0.12f : Mathf.Clamp01(1f - (t - 1.3f) / 0.9f);
            _cheer.volume = peak * env;
            if (t > 2.3f) _cheer.Stop();
        }
    }
}
