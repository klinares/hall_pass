using System;
using System.Collections.Generic;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tour
{
    /// <summary>Where she is in the 3D world, and which crew she has met. Lives in one added save key, "world".</summary>
    public sealed class WorldState
    {
        /// <summary>The place id (a venue id: "coffee"), or null for none saved.</summary>
        public string Place;
        public bool HasPosition;
        public float X, Z;
        /// <summary>"place/Crew_1" for every crew member she has talked to.</summary>
        public List<string> MetCrew = new List<string>();

        public bool Met(string place, string crew) { return MetCrew.Contains(place + "/" + crew); }
    }

    /// <summary>
    /// Reads and writes the world game's own data inside the shared save (phase 8.1). It is one added top-level key,
    /// <c>world</c>, held in <see cref="SaveBlob.Extra"/>, which both games already keep untouched: the old 2D game
    /// loads a save written here and writes it back with the key intact, and this game reads a save with no key.
    /// Fans, stickers, levels and the rest are never touched here.
    /// </summary>
    public static class WorldSave
    {
        public const string Key = "world";

        public static WorldState Read(SaveBlob blob)
        {
            var s = new WorldState();
            JsonValue w = blob != null && blob.Extra != null ? blob.Extra[Key] : null;
            if (w == null || w.Kind != JsonKind.Object) return s;
            JsonValue place = w["place"];
            if (place != null && place.Kind == JsonKind.String && place.String.Length > 0) s.Place = place.String;
            JsonValue pos = w["position"];
            if (pos != null && pos.Kind == JsonKind.Object && IsNum(pos["x"]) && IsNum(pos["z"]))
            {
                s.HasPosition = true;
                s.X = (float)pos["x"].Number;
                s.Z = (float)pos["z"].Number;
            }
            JsonValue met = w["metCrew"];
            if (met != null && met.Kind == JsonKind.Array)
                foreach (JsonValue m in met.Items) if (m != null && m.Kind == JsonKind.String && !s.MetCrew.Contains(m.String)) s.MetCrew.Add(m.String);
            return s;
        }

        public static void Write(SaveBlob blob, WorldState state)
        {
            if (blob == null) throw new ArgumentNullException(nameof(blob));
            if (state == null) throw new ArgumentNullException(nameof(state));
            JsonValue o = JsonValue.Obj();
            if (!string.IsNullOrEmpty(state.Place)) o.Set("place", state.Place);
            if (state.HasPosition)
                o.Set("position", JsonValue.Obj().Set("x", JsonValue.Num(Math.Round(state.X, 2))).Set("z", JsonValue.Num(Math.Round(state.Z, 2))));
            var met = new List<JsonValue>();
            foreach (string m in state.MetCrew) met.Add(JsonValue.Str(m));
            o.Set("metCrew", JsonValue.Arr(met));
            if (blob.Extra == null || blob.Extra.Kind != JsonKind.Object) blob.Extra = JsonValue.Obj();
            blob.Extra.Set(Key, o);
        }

        static bool IsNum(JsonValue v) { return v != null && v.Kind == JsonKind.Number; }
    }

    /// <summary>What an exit's tour map card shows (see <see cref="Journey.Offer"/>).</summary>
    public sealed class ExitOffer
    {
        public Venue Next;
        /// <summary>True when the next stop is unlocked and has a scene: the card then has a "go" button.</summary>
        public bool NextOpen;
        public string Line, Detail;
        /// <summary>Stops she can go back to, nearest first.</summary>
        public List<Venue> Earlier = new List<Venue>();
    }

    /// <summary>The tour route as the world sees it: the next place, and whether fans have unlocked it (the venue thresholds).</summary>
    public static class Journey
    {
        /// <summary>The venue after <paramref name="placeId"/>, or null at the last stop.</summary>
        public static Venue Next(string placeId)
        {
            int i = GigPlan.PlaceIndex(placeId);
            return i + 1 < Venues.All.Count ? Venues.All[i + 1] : null;
        }

        /// <summary>"next stop: record store".</summary>
        public static string NextStopLine(Venue next)
        {
            return next == null ? "that was the last stop. you made it." : "next stop: " + next.Short;
        }

        /// <summary>"back to: coffee shop".</summary>
        public static string BackLine(Venue earlier)
        {
            return earlier == null ? "" : "back to: " + earlier.Short;
        }

        /// <summary>
        /// The earlier stops she can go back to from <paramref name="placeId"/>, nearest first: every venue before this one that she has
        /// unlocked and that has a scene in the build. Nothing she has earned is taken away, so an unlocked stop is always open.
        /// </summary>
        public static List<Venue> Earlier(string placeId, Func<string, bool> unlocked, Func<string, bool> built)
        {
            var list = new List<Venue>();
            for (int i = GigPlan.PlaceIndex(placeId) - 1; i >= 0; i--)
            {
                Venue v = Venues.All[i];
                if ((unlocked == null || unlocked(v.Id)) && (built == null || built(v.Id))) list.Add(v);
            }
            return list;
        }

        /// <summary>
        /// What the tour map card at an exit offers: the next stop (open only when unlocked and built, with the reason when not),
        /// the earlier stops she can go back to, and that she can stay.
        /// </summary>
        public static ExitOffer Offer(string placeId, long lifetimeFans, Func<string, bool> unlocked, Func<string, bool> built)
        {
            Venue next = Next(placeId);
            bool isUnlocked = next != null && (unlocked == null || unlocked(next.Id));
            bool isBuilt = next != null && (built == null || built(next.Id));
            return new ExitOffer
            {
                Next = next,
                NextOpen = next != null && isUnlocked && isBuilt,
                Line = NextStopLine(next),
                Detail = DetailLine(next, lifetimeFans, isUnlocked, isBuilt),
                Earlier = Earlier(placeId, unlocked, built),
            };
        }

        /// <summary>A second line for the card: fans still needed while it is locked, "coming soon" when it is open but not built.</summary>
        public static string DetailLine(Venue next, long lifetimeFans, bool unlocked, bool built)
        {
            if (next == null) return "";
            if (!unlocked)
            {
                long left = Math.Max(0, next.Fans - lifetimeFans);
                return left.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture) + " more fans to open it";
            }
            return built ? "" : "coming soon";
        }
    }
}
