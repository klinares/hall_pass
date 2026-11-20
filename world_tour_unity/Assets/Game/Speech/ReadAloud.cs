using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace WorldTour.Game.Speech
{
    /// <summary>
    /// Read-aloud (QUESTIONS 1). In the web player it is the browser's speechSynthesis through Plugins/WebGL/speech.jslib,
    /// with world_tour/js/speech.js's behaviour: cancel before every speak, rate 0.8 (0.55 for the syllable hint), an
    /// en-US voice preferred. It starts only after the start card's click (StartCard.Unlocked), which also warms it up
    /// with a silent utterance (speech.js warmUp). In the Editor, where there is no browser, every speak is only logged,
    /// so play-mode tests can assert on it. Every speak logs "WT_SPEAK word= rate=" in both.
    /// </summary>
    public static class ReadAloud
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int WT_SpeechAvailable();
        [DllImport("__Internal")] private static extern int WT_SpeechVoiceCount();
        [DllImport("__Internal")] private static extern void WT_SpeechWarmUp();
        [DllImport("__Internal")] private static extern int WT_SpeechSay(string text, float rate);
        [DllImport("__Internal")] private static extern void WT_SpeechHush();
        private static readonly bool Browser = true;
#else
        private static readonly bool Browser = false;
#endif

        /// <summary>Tests: behave as if the browser had no voice (studio shows the clue).</summary>
        public static bool ForceNoVoice;

        /// <summary>The start card has been clicked.</summary>
        public static bool Unlocked { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Unlocked = false; ForceNoVoice = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            StartCard.Unlocked -= OnUnlocked;
            StartCard.Unlocked += OnUnlocked;
        }

        private static void OnUnlocked()
        {
            Unlocked = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { if (WT_SpeechAvailable() == 1) WT_SpeechWarmUp(); } catch (Exception) { /* no speech */ }
#endif
            Debug.Log("WT_SPEECH_UNLOCKED browser=" + (Browser ? "true" : "false"));
        }

        private static bool ApiPresent
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { return WT_SpeechAvailable() == 1; } catch (Exception) { return false; }
#else
                return true;                                           // the Editor's logging voice
#endif
            }
        }

        private static int Voices
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { return WT_SpeechVoiceCount(); } catch (Exception) { return 0; }
#else
                return 1;
#endif
            }
        }

        /// <summary>Speak, show the clue, or wait (the song has been waiting <paramref name="waited"/> seconds).</summary>
        public static SpeechChoice Decide(float waited)
        {
            if (ForceNoVoice) return SpeechChoice.Clue;
            // the Editor's logging voice needs no click; the browser does
            return SpeechRules.Decide(ApiPresent, Unlocked || !Browser, Voices, waited);
        }

        /// <summary>speech.js say(text, rate): cancel whatever is playing, then speak.</summary>
        public static void Say(string text, bool slow = false)
        {
            float rate = SpeechRules.Rate(slow);
            Debug.Log(SpeechRules.Log(text, rate));
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!Unlocked || ForceNoVoice) return;
            try { WT_SpeechSay(text ?? "", rate); } catch (Exception) { /* ignore, as speech.js does */ }
#endif
        }

        /// <summary>speech.js hush(): stop speaking (a song ends or the screen goes).</summary>
        public static void Hush()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { WT_SpeechHush(); } catch (Exception) { /* ignore */ }
#endif
        }
    }
}
