using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTour.Core.Data;

namespace WorldTour.Game.Map
{
    /// <summary>
    /// The sprites and manifest the tour map and venue card use (task 2.1's exports). Made and filled by the
    /// MapBuilder Editor script from Assets/Art/Sprites/manifest.json, never by hand. The manifest supplies the route
    /// curves, the layout and each sprite's web box.
    /// </summary>
    [CreateAssetMenu(menuName = "World Tour/Map Art")]
    public sealed class MapArt : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public Sprite sprite;
        }

        /// <summary>Assets/Art/Sprites/manifest.json.</summary>
        public TextAsset manifest;
        public List<Entry> sprites = new List<Entry>();

        Dictionary<string, Sprite> _byName;
        MapLayout _layout;
        Dictionary<string, SpriteBox> _boxes;
        string _error;

        /// <summary>
        /// The sprite exported under this manifest name ("venue_coffee", "star_sm"), or null. A plain guitar sticker comes
        /// back in the equipped skin (LookArt); <see cref="GetExact"/> skips that.
        /// </summary>
        public Sprite Get(string name) { return GetExact(name != null ? WorldTour.Game.Ui.LookArt.Skinned(name) : null); }

        /// <summary>The sprite exported under exactly this name, or null.</summary>
        public Sprite GetExact(string name)
        {
            if (_byName == null)
            {
                _byName = new Dictionary<string, Sprite>();
                foreach (Entry e in sprites) if (!string.IsNullOrEmpty(e.name) && e.sprite != null) _byName[e.name] = e.sprite;
            }
            Sprite s;
            return name != null && _byName.TryGetValue(name, out s) ? WorldTour.Game.Ui.ThemeSkin.For(s) : null;   // the live theme's twin, if it has one
        }

        public Sprite Venue(Venue v) { return Get("venue_" + v.Art); }

        /// <summary>The landscape layout from the manifest, or null (with <see cref="Error"/>) if it can't be read.</summary>
        public MapLayout Layout { get { Load(); return _layout; } }

        public string Error { get { Load(); return _error; } }

        /// <summary>A sprite's web box; a square box the size of the sprite when the manifest doesn't list it.</summary>
        public SpriteBox Box(string name)
        {
            Load();
            SpriteBox b;
            if (_boxes != null && name != null && _boxes.TryGetValue(name, out b)) return b;
            Sprite s = Get(name);
            float w = s != null ? s.rect.width : 1f, h = s != null ? s.rect.height : 1f;
            return new SpriteBox { W = w, H = h, BoxW = w, BoxH = h };
        }

        void Load()
        {
            if (_layout != null || _error != null) return;
            if (manifest == null) { _error = "map art has no sprite manifest"; return; }
            try
            {
                var root = StrictJson.Parse(manifest.text);
                _layout = MapLayout.FromManifest(root["map"]);
                _boxes = MapLayout.Boxes(root);
            }
            catch (Exception e) { _error = "sprite manifest unreadable: " + e.Message; }
        }

        void OnValidate() { _byName = null; _layout = null; _boxes = null; _error = null; }
    }
}
