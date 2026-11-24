using System;

namespace WorldTour.World.Audio
{
    public enum Surface { Stone, Wood, Carpet, Grass, Hard }
    public enum Bed { Birds, City, Hall }

    /// <summary>
    /// Phase 8.3: what each place sounds like, as plain data. The footsteps are synthesised per ground (<see cref="WorldSynth.SpecFor"/>: pavement and the hard floors bright, wood a light knock, carpet dull and soft,
    /// grass a brushing rustle) and played at the volume here.
    /// The bed under the place is birds (outdoors in the morning), a city hum (the streets and the rooftop) or a hall murmur (rooms with people in them).
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
                default: return Surface.Stone;
            }
        }

        public static Bed BedFor(string placeId)
        {
            switch (placeId)
            {
                case "coffee": return Bed.Birds;
                case "festival": return Bed.Birds;
                case "records": return Bed.City;
                case "club": return Bed.City;
                case "rooftop": return Bed.City;
                case "rec": return Bed.Hall;
                case "theater": return Bed.Hall;
                case "arena": return Bed.Hall;
                default: return Bed.Birds;
            }
        }

        public static float StepVolume(Surface s)
        {
            switch (s)
            {
                case Surface.Wood: return 0.5f;
                case Surface.Carpet: return 0.3f;
                case Surface.Grass: return 0.34f;
                case Surface.Hard: return 0.5f;
                default: return 0.46f;
            }
        }

        /// <summary>Source volume of the bed (0 to 1). Gentle: well under the synthesised answer sounds.</summary>
        public static float BedVolume(Bed b)
        {
            switch (b)
            {
                case Bed.Birds: return 0.3f;
                case Bed.City: return 0.3f;
                default: return 0.2f;
            }
        }

        /// <summary>Metres of walking per footstep (the run cycle is about three steps a second at 5 m/s).</summary>
        public const float Stride = 1.6f;

        /// <summary>Volume of the crowd murmur: louder with a fuller crowd, loudest close to it, steady during a show. 0 to 0.4.</summary>
        public static float CrowdVolume(float fill, float distance, bool show)
        {
            float near = show ? 1f : 1f - Smooth((distance - 3.5f) / 11f);
            return 0.4f * Math.Max(0f, Math.Min(1f, fill)) * Math.Max(0f, near) * (show ? 0.8f : 1f);
        }

        static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
