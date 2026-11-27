using System;

namespace WorldTour.World.Audio
{
    public enum Surface { Stone, Wood, Carpet, Grass, Hard }
    /// <summary>Phase 10A: what plays under a place. Only birds are left: the city hum and the hall murmur were filtered noise (the "loud static") and are gone.</summary>
    public enum Bed { None, Birds }

    /// <summary>
    /// What each place sounds like, as plain data. The footsteps are synthesised per ground (<see cref="WorldSynth.SpecFor"/>) and played at the volume here.
    /// Phase 10A: the only bed is a rare, soft bird chirp, in the daytime places outdoors; every level here is a peak sample (linear, 1 = full scale) at full slider.
    /// </summary>
    public static class AudioPlan
    {
        public static Surface SurfaceFor(string placeId)
        {
            switch (placeId)
            {
                case "coffee": return Surface.Stone;     // the paved street
                case "records": return Surface.Stone;    // the basement's concrete
                case "rec": return Surface.Wood;         // the gym floor
                case "rooftop": return Surface.Stone;    // the deck
                case "club": return Surface.Hard;        // the dark painted floor
                case "theater": return Surface.Carpet;   // red carpet and velvet
                case "festival": return Surface.Grass;   // boardwalk and grass
                case "arena": return Surface.Hard;       // the arena floor
                case "home": return Surface.Carpet;      // phase 10B.2: her home, soft floors, no birds
                case "boutique": return Surface.Carpet;  // phase 10B.1: the boutique's soft rug floor, no birds indoors
                default: return Surface.Stone;
            }
        }

        /// <summary>
        /// Birds only where it is daytime and outdoors: the coffee shop street (morning) and the festival field (golden hour, in the open).
        /// None indoors or underground (records is a basement, rec is a gym), and none at dusk or night (the rooftop at sunset, the club street in the evening, the theater at dusk, the arena at night).
        /// </summary>
        public static Bed BedFor(string placeId)
        {
            switch (placeId)
            {
                case "coffee": return Bed.Birds;
                case "festival": return Bed.Birds;
                default: return Bed.None;
            }
        }

        /// <summary>The places with birds, for the tests and the report.</summary>
        public static readonly string[] BirdPlaces = { "coffee", "festival" };

        /// <summary>The peak of one chirp at the full ambience slider. About a quarter of the old bird bed (which peaked at 0.24 with the slider at its 40 percent default, so 0.06 now): 0.15 x 0.4.</summary>
        public const float BirdPeak = 0.15f;

        /// <summary>A bird calls once every 20 to 40 seconds, never more often.</summary>
        public const float BirdGapMin = 20f, BirdGapMax = 40f;

        /// <summary>The wait before the next chirp, from a number between 0 and 1.</summary>
        public static float BirdGap(float u) { return BirdGapMin + (BirdGapMax - BirdGapMin) * Math.Max(0f, Math.Min(1f, u)); }

        /// <summary>Peak of a footstep (the clip itself is made to 0.85): all of them under the tap's -21 dBFS at full slider, with the ground setting how hard.</summary>
        public static float StepVolume(Surface s)
        {
            switch (s)
            {
                case Surface.Wood: return 0.1f;
                case Surface.Carpet: return 0.06f;
                case Surface.Grass: return 0.07f;
                case Surface.Hard: return 0.1f;
                default: return 0.09f;
            }
        }

        /// <summary>Metres of walking per footstep (the run cycle is about three steps a second at 5 m/s).</summary>
        public const float Stride = 1.6f;

        /// <summary>Peak of each cue at full effects (-18 to -14 dBFS, well under the -12 ceiling).</summary>
        public const float CueCard = 0.12f, CueWhoosh = 0.15f, CueJingle = 0.2f;

        /// <summary>Applause on a right answer in a show: louder with a fuller crowd, 0.12 to 0.4 at its peak (-18 to -8 dBFS, the one sound allowed past -12).</summary>
        public static float CheerPeak(float fill) { return 0.12f + 0.28f * Math.Max(0f, Math.Min(1f, fill)); }
    }
}
