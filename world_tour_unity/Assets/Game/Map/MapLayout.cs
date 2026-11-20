using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Map
{
    /// <summary>One doodle in the empty spaces of the map (art/map.js <c>decor</c>: [sticker id, x, y, tilt]).</summary>
    public struct MapDecor
    {
        public string Id;
        public Vector2 At;
        /// <summary>CSS degrees, clockwise.</summary>
        public float Rot;
    }

    /// <summary>Where the web svg box lies inside an exported sprite (manifest <c>w</c>, <c>h</c>, <c>box</c>), in sprite px, y down.</summary>
    public struct SpriteBox
    {
        public float W, H, BoxX, BoxY, BoxW, BoxH;

        /// <summary>
        /// The sprite's rect for an element whose web box is <paramref name="boxSize"/> wide: offset of the sprite's
        /// top-left from the element's top-left (y down) and the sprite's size. The art may overflow the element,
        /// as in the web.
        /// </summary>
        public void Fit(float boxSize, out Vector2 topLeft, out Vector2 size)
        {
            float k = BoxW > 0 ? boxSize / BoxW : 0f;
            topLeft = new Vector2(-BoxX * k, -BoxY * k);
            size = new Vector2(W * k, H * k);
        }

        /// <summary>Height of the element for that width (the box's aspect).</summary>
        public float BoxHeightFor(float boxWidth) { return BoxW > 0 ? boxWidth * BoxH / BoxW : boxWidth; }
    }

    /// <summary>
    /// The landscape tour map (art/map.js <c>LAYOUTS.landscape</c>) as exported in the sprite manifest's "map" section,
    /// plus the sizing rules of screens.css for a map of a given width. Layout units are 1000 x 610 with y down; the
    /// map is drawn at the layout's aspect, so one scale factor serves both axes (the web stretches with
    /// preserveAspectRatio none, which is the same thing at that aspect).
    /// </summary>
    public sealed class MapLayout
    {
        public float W, H;
        public Vector2[] Pts;
        public Dictionary<int, Vector2> Via = new Dictionary<int, Vector2>();
        public List<MapDecor> Decor = new List<MapDecor>();
        public string CurveAll;
        /// <summary>curveDone[k]: the route walked as far as venue k ("" for k = 0).</summary>
        public string[] CurveDone;
        /// <summary>Venue ids in route order, as exported.</summary>
        public string[] VenueOrder;

        /// <summary>map.js:82 the tilt of each venue sticker, CSS degrees (clockwise).</summary>
        public static readonly float[] VenueTilt = { -2f, 2f, -1.5f, 2.5f, -2.5f, 1.5f, -1f, 2f };

        /// <summary>screens.css:2 #app max-width 1180 with the page gutter on each side.</summary>
        public const float MaxContent = 1180f;
        /// <summary>screens.css:61 a landscape venue is 19% of the map wide, at most 190 (screens.css:57).</summary>
        public const float VenueShare = 0.19f, VenueMax = 190f;
        /// <summary>screens.css:53-54 a landscape doodle is 7% of the map wide, at most 84.</summary>
        public const float DecorShare = 0.07f, DecorMax = 84f;

        /// <summary>Reads the manifest's "map" object (made by Tools/export_sprites.mjs from art/map.js).</summary>
        public static MapLayout FromManifest(JsonValue map)
        {
            if (map == null || map.Kind != JsonKind.Object) throw new FormatException("sprite manifest has no map section");
            JsonValue L = Need(map, "layout");
            var o = new MapLayout { W = (float)Need(L, "w").Number, H = (float)Need(L, "h").Number };
            var pts = new List<Vector2>();
            foreach (JsonValue p in Need(L, "pts").Items) pts.Add(Pt(p));
            o.Pts = pts.ToArray();
            JsonValue via = L["via"];
            if (via != null && via.Kind == JsonKind.Object)
                foreach (string k in via.Keys) o.Via[int.Parse(k, System.Globalization.CultureInfo.InvariantCulture)] = Pt(via[k]);
            JsonValue decor = L["decor"];
            if (decor != null)
                foreach (JsonValue d in decor.Items)
                    o.Decor.Add(new MapDecor { Id = d[0].String, At = new Vector2((float)d[1].Number, (float)d[2].Number), Rot = (float)d[3].Number });
            o.CurveAll = Need(map, "curveAll").String;
            var done = new List<string>();
            foreach (JsonValue c in Need(map, "curveDone").Items) done.Add(c.String);
            o.CurveDone = done.ToArray();
            var order = new List<string>();
            JsonValue vo = map["venueOrder"];
            if (vo != null) foreach (JsonValue v in vo.Items) order.Add(v["id"].String);
            o.VenueOrder = order.ToArray();
            return o;
        }

        static JsonValue Need(JsonValue o, string key)
        {
            JsonValue v = o[key];
            if (v == null) throw new FormatException("sprite manifest map section has no \"" + key + "\"");
            return v;
        }

        static Vector2 Pt(JsonValue p) { return new Vector2((float)p[0].Number, (float)p[1].Number); }

        /// <summary>The sprite boxes of the manifest's "sprites" list, by sprite name.</summary>
        public static Dictionary<string, SpriteBox> Boxes(JsonValue manifest)
        {
            var o = new Dictionary<string, SpriteBox>();
            JsonValue list = manifest != null ? manifest["sprites"] : null;
            if (list == null) return o;
            foreach (JsonValue s in list.Items)
            {
                var b = new SpriteBox { W = (float)s["w"].Number, H = (float)s["h"].Number };
                JsonValue box = s["box"];
                if (box != null && box.Count == 4) { b.BoxX = (float)box[0].Number; b.BoxY = (float)box[1].Number; b.BoxW = (float)box[2].Number; b.BoxH = (float)box[3].Number; }
                else { b.BoxW = b.W; b.BoxH = b.H; }
                o[s["name"].String] = b;
            }
            return o;
        }

        /// <summary>art/map.js:34 <c>routePoints(L, upto)</c>: venue points with the swing-wide waypoints, cut after venue <paramref name="upto"/>.</summary>
        public List<Vector2> RoutePoints(int upto = -1)
        {
            if (upto < 0) upto = Pts.Length - 1;
            var o = new List<Vector2>();
            for (int i = 0; i < Pts.Length; i++)
            {
                if (i > upto) continue;
                o.Add(Pts[i]);
                Vector2 v;
                if (Via.TryGetValue(i, out v) && i < upto) o.Add(v);
            }
            return o;
        }

        /// <summary>The route walked as far as venue <paramref name="reached"/> (art/map.js:42: nothing at 0).</summary>
        public string DoneCurve(int reached)
        {
            if (reached <= 0 || CurveDone == null || CurveDone.Length == 0) return "";
            return CurveDone[Mathf.Min(reached, CurveDone.Length - 1)];
        }

        // ------------------------------------------------------------------ sizing

        /// <summary>The map's width in a viewport this wide: the page width less a gutter each side, at most 1180 less the gutters.</summary>
        public static float MapWidth(float viewportWidth, float gutter)
        {
            return Mathf.Max(0f, Mathf.Min(viewportWidth, MaxContent) - 2f * gutter);
        }

        public float MapHeight(float mapWidth) { return W > 0 ? mapWidth * H / W : 0f; }

        /// <summary>Layout units to map px.</summary>
        public float Scale(float mapWidth) { return W > 0 ? mapWidth / W : 0f; }

        /// <summary>A layout point as an anchored position from the map's top-left corner (Unity y up).</summary>
        public Vector2 ToLocal(Vector2 p, float mapWidth)
        {
            float s = Scale(mapWidth);
            return new Vector2(p.x * s, -p.y * s);
        }

        public static float VenueWidth(float mapWidth) { return Mathf.Min(mapWidth * VenueShare, VenueMax); }

        public static float DecorWidth(float mapWidth) { return Mathf.Min(mapWidth * DecorShare, DecorMax); }

        /// <summary>A CSS rotation (degrees clockwise) as a Unity z rotation (counter-clockwise).</summary>
        public static float UnityTilt(float cssDegrees) { return -cssDegrees; }

        /// <summary>Index of a venue id in the route, or -1.</summary>
        public static int IndexOf(string id)
        {
            for (int i = 0; i < Venues.All.Count; i++) if (Venues.All[i].Id == id) return i;
            return -1;
        }
    }
}
