using System.Globalization;

namespace WorldTour.Game.Speech
{
    /// <summary>What studio session does about the voice for a song.</summary>
    public enum SpeechChoice
    {
        /// <summary>A voice is ready: the record button, no clue on screen.</summary>
        Speak,
        /// <summary>No speech: the clue on screen from the start (studio.js:18), and the clue hint skipped.</summary>
        Clue,
        /// <summary>The browser has speech but no voice yet: ask again shortly.</summary>
        Wait
    }

    /// <summary>
    /// The read-aloud rules (QUESTIONS 1, world_tour/js/speech.js), with no Unity in them so they can be tested: the two
    /// rates, and whether to speak, show the clue, or wait for the browser's voices (they often load late).
    /// </summary>
    public static class SpeechRules
    {
        /// <summary>speech.js:281 say(text, rate = .8).</summary>
        public const float NormalRate = .8f;
        /// <summary>studio.js:35 say(w, .55) for the syllable hint.</summary>
        public const float SlowRate = .55f;
        /// <summary>How long a song waits for the browser's voices before it shows the clue instead.</summary>
        public const float VoicesTimeout = 2f;
        /// <summary>How often it asks again while waiting.</summary>
        public const float PollEvery = .1f;
        /// <summary>studio.js:27 the word plays 380 ms after the song is drawn.</summary>
        public const float FirstPlayDelay = .38f;

        public static float Rate(bool slow) { return slow ? SlowRate : NormalRate; }

        /// <summary>
        /// Speak when the browser has speech, the player has clicked start (browsers gate audio and speech behind a
        /// gesture) and a voice is loaded. With no speech, or before the click, show the clue. With speech but no voice
        /// yet, wait, up to <paramref name="timeout"/> seconds, then show the clue.
        /// </summary>
        public static SpeechChoice Decide(bool apiPresent, bool unlocked, int voices, float waited, float timeout = VoicesTimeout)
        {
            if (!apiPresent || !unlocked) return SpeechChoice.Clue;
            if (voices > 0) return SpeechChoice.Speak;
            return waited >= timeout ? SpeechChoice.Clue : SpeechChoice.Wait;
        }

        /// <summary>The console line for every speak: "WT_SPEAK word=empire rate=0.80".</summary>
        public static string Log(string text, float rate)
        {
            return "WT_SPEAK word=" + (text ?? "").Replace(' ', '_') + " rate=" + rate.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
