using System.Collections.Generic;

namespace WorldTour.Core.Data
{
    /// <summary>One stop on the tour (world_tour/data/venues.js). All venues and names are fictional.</summary>
    public sealed class Venue
    {
        public readonly string Id;
        public readonly string Art;
        public readonly string Name;
        public readonly string Short;
        /// <summary>Lifetime fans that unlock it.</summary>
        public readonly long Fans;
        /// <summary>0..1, scales the crowd swell at the end of a show.</summary>
        public readonly double Crowd;
        public readonly string Blurb;

        public Venue(string id, string art, string name, string shortName, long fans, double crowd, string blurb)
        {
            Id = id; Art = art; Name = name; Short = shortName; Fans = fans; Crowd = crowd; Blurb = blurb;
        }
    }

    /// <summary>The tour route, ported from world_tour/data/venues.js. Lifetime fans unlock the next venue.</summary>
    public static class Venues
    {
        /// <summary>U+2019, the right single quote the JS copy uses. Built from its code so the source stays ASCII.</summary>
        private static readonly string Q = ((char)0x2019).ToString();

        /// <summary>venues.js VENUES, in tour order.</summary>
        public static readonly IReadOnlyList<Venue> All = new[]
        {
            new Venue("coffee", "coffee", "the corner coffee shop", "coffee shop", 0, .15, "open mic night. twelve people and a barista. everyone starts here."),
            new Venue("records", "records", "record store basement", "record store", 20000, .25, "low ceiling, loud amps, a cat asleep on the bass case."),
            new Venue("rec", "rec", "rec center battle of the bands", "rec center", 60000, .35, "folding chairs, a scoreboard, and very serious judges."),
            new Venue("rooftop", "rooftop", "rooftop party", "rooftop", 125000, .45, "string lights, city skyline, someone" + Q + "s aunt filming everything."),
            new Venue("club", "club", "the fuzz, downtown", "the club", 225000, .6, "a real stage, a real sound guy, a line around the block."),
            new Venue("theater", "theater", "the old movie palace", "theater", 375000, .72, "velvet seats, gold balconies, your name on the marquee."),
            new Venue("festival", "festival", "summer festival side stage", "festival", 600000, .86, "mud, glitter, and thousands of people who just found you."),
            new Venue("arena", "arena", "the arena", "the arena", 900000, 1, "the big one. lights, screens, and a whole city singing along."),
        };

        /// <summary>The venue everyone starts at; always unlocked.</summary>
        public const string First = "coffee";

        /// <summary>venues.js <c>venueById</c>: the venue, or the first one for an unknown id.</summary>
        public static Venue ById(string id)
        {
            foreach (Venue v in All) if (v.Id == id) return v;
            return All[0];
        }

        /// <summary>True for an id in the table.</summary>
        public static bool Exists(string id)
        {
            foreach (Venue v in All) if (v.Id == id) return true;
            return false;
        }
    }
}
