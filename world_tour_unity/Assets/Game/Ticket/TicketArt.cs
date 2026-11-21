using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTour.Core.Data;

namespace WorldTour.Game.Ticket
{
    /// <summary>
    /// The sprites and text materials the ticket screen uses (task 2.1's exports). Made and filled by the
    /// TicketScreenBuilder Editor script (Assets/Art/Ticket/TicketArt.asset), never by hand. Everything is optional:
    /// a missing sprite falls back to a plain panel, a missing material to plain text.
    /// </summary>
    [CreateAssetMenu(menuName = "World Tour/Ticket Art")]
    public sealed class TicketArt : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public Sprite sprite;
        }

        [Header("decor (Assets/Art/Sprites/decor)")]
        public Sprite ticketStub;
        public Sprite stamp;
        public Sprite paperLined;
        public Sprite tape;
        public Sprite bubble;
        public Sprite bubbleTail;

        [Header("glyphs neither font has (none exported yet; null draws them in code)")]
        public Sprite star6;
        public Sprite heart;

        [Header("TMP material presets on the display font (keep their shader variants in the build)")]
        [Tooltip(".shout: white face, 2px ink outline, 5px ink offset shadow")]
        public Material shout;
        [Tooltip("-webkit-text-stroke: a thin ink outline (.tk-top, the missed tag)")]
        public Material stroke;

        [Header("stickers/<id> and map/venue_<id>")]
        public List<Entry> sprites = new List<Entry>();

        Dictionary<string, Sprite> _byName;

        /// <summary>The sprite exported under this manifest name ("star", "venue_coffee"), or null. A plain guitar sticker wears the equipped skin (LookArt).</summary>
        public Sprite Get(string name)
        {
            if (name != null) name = WorldTour.Game.Ui.LookArt.Skinned(name);
            if (_byName == null)
            {
                _byName = new Dictionary<string, Sprite>();
                foreach (Entry e in sprites) if (!string.IsNullOrEmpty(e.name) && e.sprite != null) _byName[e.name] = e.sprite;
            }
            Sprite s;
            return name != null && _byName.TryGetValue(name, out s) ? WorldTour.Game.Ui.ThemeSkin.For(s) : null;   // the live theme's twin, if it has one
        }

        /// <summary>sticker.js journalArt: "venue:&lt;id&gt;" is a venue drawing, anything else a sticker.</summary>
        public Sprite Journal(string art)
        {
            if (string.IsNullOrEmpty(art)) return null;
            return art.StartsWith("venue:", StringComparison.Ordinal) ? Get("venue_" + art.Substring(6)) : Get(art);
        }

        /// <summary>sticker.js venueArt.</summary>
        public Sprite VenueArt(Venue v) { return v != null ? Get("venue_" + v.Art) : null; }

        void OnValidate() { _byName = null; }
    }
}
