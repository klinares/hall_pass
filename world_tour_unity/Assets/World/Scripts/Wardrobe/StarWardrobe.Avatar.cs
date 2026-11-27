using System.Collections.Generic;
using UnityEngine;
using WorldTour.Core.Meta;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 10B.1: the Star dressed from an <see cref="AvatarLook"/>. Her body parts are swapped for the clean ones the AvatarArtBuilder made (no belt, buckle, pouch or earring, no painted-on
    /// face; the cloth has its own patch of the texture). The body is recoloured by painting the KayKit palette cells, and the cloth canvases with the colour and pattern, into three small
    /// runtime textures (one for the body, head and hands, one for the arms when a top has short sleeves or none, one for the legs); every other item is a set of procedural parts hung on
    /// bone frames (see <see cref="AvatarCtx"/>, <see cref="AvatarParts"/>), rebuilt only when that slot's pick changes. Patterns are never parts. Nothing here is saved; the caller decides what to apply.
    /// </summary>
    public sealed partial class StarWardrobe
    {
        // palette cells in texture space (column + 8 * row, row 0 at the bottom, as the model's UVs read them)
        const int CellJacket = 16, CellTrim = 17, CellStrap = 29, CellBelt = 23, CellHands = 13, CellSkin = 24, CellHair = 25, CellBoots = 11;
        const float U = 0.85f;

        struct FrameSpec { public string Anchor; public Vector3 Joint; public FrameSpec(string a, Vector3 j) { Anchor = a; Joint = j; } }

        static readonly Dictionary<string, FrameSpec> FrameSpecs = new Dictionary<string, FrameSpec>
        {
            { "head", new FrameSpec("StarHead", new Vector3(0f, 1.24f, 0f)) },
            { "chest", new FrameSpec("StarChest", new Vector3(0f, 0.97f, 0f)) },
            { "hips", new FrameSpec("StarHips", new Vector3(0f, 0.41f, 0f)) },
            { "armL", new FrameSpec("StarArmL", new Vector3(-0.21f, 1.11f, 0f)) },
            { "armR", new FrameSpec("StarArmR", new Vector3(0.21f, 1.11f, 0f)) },
            { "legL", new FrameSpec("StarLegL", new Vector3(-0.17f, 0.52f, 0f)) },
            { "legR", new FrameSpec("StarLegR", new Vector3(0.17f, 0.52f, 0f)) },
            { "footL", new FrameSpec("StarFootL", new Vector3(-0.17f, 0.15f, -0.02f)) },
            { "footR", new FrameSpec("StarFootR", new Vector3(0.17f, 0.15f, -0.02f)) },
        };

        public static readonly string[] FrameNames = { "head", "chest", "hips", "armL", "armR", "legL", "legR", "footL", "footR" };
        static readonly string[] PartSlots = { AvatarCatalogue.Top, AvatarCatalogue.Bottom, AvatarCatalogue.Shoes, AvatarCatalogue.Hair, AvatarCatalogue.Hat, AvatarCatalogue.Glasses, AvatarCatalogue.Bag, AvatarCatalogue.Face };

        readonly Dictionary<string, Transform> _frames = new Dictionary<string, Transform>();
        readonly Dictionary<string, List<GameObject>> _slotParts = new Dictionary<string, List<GameObject>>();
        readonly Dictionary<string, string> _slotSig = new Dictionary<string, string>();
        Texture2D _texA, _texB, _texC;
        Material _matA, _matB, _matC;
        string _paintSig, _guitarSig;
        AvatarLook _avatar;
        bool _meshesSwapped;

        static Color32[] s_Base;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAvatarStatics() { s_Base = null; }

        /// <summary>The avatar look last applied (null until <see cref="Apply(AvatarLook)"/> runs).</summary>
        public AvatarLook AppliedAvatar { get { return _avatar; } }

        /// <summary>The parts of a slot that are on her now (for tests).</summary>
        public IReadOnlyList<GameObject> PartsOf(string slot) { List<GameObject> l; return _slotParts.TryGetValue(slot, out l) ? l : new List<GameObject>(); }

        public Transform FrameOf(string name)
        {
            Transform t;
            if (_frames.TryGetValue(name, out t) && t != null) return t;
            FrameSpec spec;
            if (!FrameSpecs.TryGetValue(name, out spec)) return null;
            Transform anchor = WardrobeAnchors.Ensure(gameObject, spec.Anchor);
            if (anchor == null) return null;
            var g = new GameObject("Model");
            g.transform.SetParent(anchor, false);
            g.transform.localScale = Vector3.one * U;
            g.transform.localPosition = -spec.Joint * U;
            _frames[name] = g.transform;
            return g.transform;
        }

        /// <summary>Swaps her body parts for the clean ones (once). Without them (the builder has not run) she keeps the original parts and the painting falls back to the palette only.</summary>
        void SwapMeshes(WardrobeAssets a)
        {
            if (_meshesSwapped) return;
            _meshesSwapped = true;
            foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh m = a.BodyMesh(r.name);
                if (m != null) r.sharedMesh = m;
            }
        }

        /// <summary>Dresses her from a look: skin, hair colour, clothes colours, and every slot's parts. Only what changed is rebuilt.</summary>
        public void Apply(AvatarLook look)
        {
            Find();
            WardrobeAssets a = WardrobeAssets.Load();
            if (a == null || look == null) return;
            _avatar = look.Clone();
            SwapMeshes(a);
            if (a.palette != null) Paint(a, look);
            foreach (string slot in PartSlots)
            {
                AvatarPick p = look.Get(slot);
                if (p == null) continue;
                string sig = p.Id + "/" + p.Color + "/" + p.Pattern + (slot == AvatarCatalogue.Hair ? "/" + look.HairColor + "/" + look.Hat.Id : "");
                string old;
                if (_slotSig.TryGetValue(slot, out old) && old == sig) continue;
                _slotSig[slot] = sig;
                BuildSlot(a, slot, p, look);
            }
            string g = look.Guitar.Id + "/" + look.Guitar.Color;
            if (g != _guitarSig)
            {
                _guitarSig = g;
                if (_guitar != null) { if (Application.isPlaying) Destroy(_guitar); else DestroyImmediate(_guitar); _guitar = null; _guitarBody = null; }
                if (_guitarOn) { BuildGuitar(); if (_guitar != null) _guitar.SetActive(true); }
            }
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void BuildSlot(WardrobeAssets a, string slot, AvatarPick p, AvatarLook look)
        {
            List<GameObject> old;
            if (_slotParts.TryGetValue(slot, out old))
                foreach (GameObject o in old)
                {
                    if (o == null) continue;
                    var mf = o.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "avf_part") Kill(mf.sharedMesh);   // a baked cloth part owns its mesh
                    Kill(o);
                }
            var c = new AvatarCtx { A = a, Frame = FrameOf, Pattern = p.Pattern, Skin = AvatarColors.Skin(look.Skin), Hair = AvatarColors.Palette(look.HairColor), HatKey = AvatarCatalogue.Key(look.Hat.Id) };
            c.Colors(AvatarColors.Palette(p.Color));
            string key = AvatarCatalogue.Key(p.Id);
            switch (slot)
            {
                case AvatarCatalogue.Top: AvatarParts.Top(c, key); break;
                case AvatarCatalogue.Bottom: AvatarParts.Bottom(c, key); break;
                case AvatarCatalogue.Shoes: AvatarParts.Shoes(c, key); break;
                case AvatarCatalogue.Hair: AvatarParts.Hair(c, key); break;
                case AvatarCatalogue.Hat: AvatarParts.Hat(c, key); break;
                case AvatarCatalogue.Glasses: AvatarParts.Glasses(c, key); break;
                case AvatarCatalogue.Bag: AvatarParts.Bag(c, key); break;
                case AvatarCatalogue.Face: AvatarParts.Face(c, key); break;
            }
            _slotParts[slot] = c.Made;
        }

        // ---------- painting the palette ----------

        static Color32[] BasePixels(WardrobeAssets a)
        {
            if (s_Base == null) s_Base = a.palette.GetPixels32();
            return s_Base;
        }

        /// <summary>Paints one palette cell (in the lower left quarter of the doubled texture): a vertical gradient from <paramref name="top"/> to <paramref name="bottom"/>.</summary>
        static void Cell(Color32[] px, int stride, int w, int h, int cell, Color top, Color bottom)
        {
            int cw = w / 8, ch = h / 4, col = cell % 8, row = cell / 8;
            for (int iy = 0; iy < ch; iy++)
            {
                Color32 c = Color.Lerp(bottom, top, iy / (float)(ch - 1));
                int y = row * ch + iy;
                for (int ix = 0; ix < cw; ix++) px[y * stride + col * cw + ix] = c;
            }
        }

        // the three painted textures live in buffers made once (a doubled 256 x 128 palette is half a megabyte each): a new look repaints into them, nothing is allocated
        Color32[] _pa, _pb, _pc;
        PaintKey _key;
        bool _hasKey;

        /// <summary>What the paint depends on, kept apart for the body (and arms) and for the legs so a change repaints only what it touches.</summary>
        struct PaintKey
        {
            public string TopId, BotId, ShoeId;
            public int TopColor, TopPattern, BotColor, BotPattern, ShoeColor, Skin, Hair;
            public bool BodySame(PaintKey o) { return TopId == o.TopId && TopColor == o.TopColor && TopPattern == o.TopPattern && BotColor == o.BotColor && Skin == o.Skin && Hair == o.Hair; }
            public bool LegsSame(PaintKey o) { return BotId == o.BotId && BotColor == o.BotColor && BotPattern == o.BotPattern && ShoeId == o.ShoeId && ShoeColor == o.ShoeColor && Skin == o.Skin; }
        }

        /// <summary>Copies the palette into the lower left quarter of a doubled-size buffer (the rest is painted as cloth).</summary>
        static void CopyPalette(Color32[] src, Color32[] dst, int w, int h)
        {
            for (int y = 0; y < h; y++) System.Array.Copy(src, y * w, dst, y * 2 * w, w);
        }

        /// <summary>
        /// Paints a cloth canvas (one quarter of the doubled texture): <paramref name="baseC"/> shaded a little darker toward the bottom (as the palette cells are), with the pattern in
        /// a contrasting mark color. A pattern of 0 is plain. The pattern is sampled in the same cloth space the mesh UVs came from, so it lies in the cloth.
        /// </summary>
        static void PaintCanvas(Color32[] px, int w, int h, Fabric.Mode mode, Color baseC, int pattern, float vLo, float vHi, float darken)
        {
            int stride = 2 * w;
            Vector2Int o = Fabric.CanvasOrigin(mode, w, h);
            Color markC = AvatarColors.Contrast(baseC);
            float soft = 1f / Fabric.Ppu;
            for (int y = 0; y < h; y++)
            {
                float u0, v;
                Fabric.CanvasPoint(mode, 0f, y + 0.5f, out u0, out v);
                float t = vHi > vLo ? Mathf.Clamp01((v - vLo) / (vHi - vLo)) : 1f;
                Color b = Color.Lerp(AvatarColors.Shade(baseC, darken), baseC, t), m = Color.Lerp(AvatarColors.Shade(markC, darken), markC, t);
                int row = (o.y + y) * stride + o.x;
                if (pattern <= 0) { Color32 flat = b; for (int x = 0; x < w; x++) px[row + x] = flat; continue; }
                for (int x = 0; x < w; x++)
                {
                    float u, vv; Fabric.CanvasPoint(mode, x + 0.5f, y + 0.5f, out u, out vv);
                    px[row + x] = Color.Lerp(b, m, Fabric.Sample(pattern, u, vv, soft));
                }
            }
        }

        void Paint(WardrobeAssets a, AvatarLook look)
        {
            var key = new PaintKey
            {
                TopId = look.Top.Id, TopColor = look.Top.Color, TopPattern = look.Top.Pattern, BotId = look.Bottom.Id, BotColor = look.Bottom.Color, BotPattern = look.Bottom.Pattern,
                ShoeId = look.Shoes.Id, ShoeColor = look.Shoes.Color, Skin = look.Skin, Hair = look.HairColor,
            };
            bool first = _matA == null;
            bool body = first || !_hasKey || !key.BodySame(_key), legs = first || !_hasKey || !key.LegsSame(_key);
            if (!body && !legs) return;
            _key = key; _hasKey = true;
            Color32[] src = BasePixels(a);
            int w = a.palette.width, h = a.palette.height, W = 2 * w, H = 2 * h;
            if (_pa == null) { _pa = new Color32[W * H]; _pb = new Color32[W * H]; _pc = new Color32[W * H]; CopyPalette(src, _pa, w, h); CopyPalette(src, _pb, w, h); CopyPalette(src, _pc, w, h); }
            Color skin = AvatarColors.Skin(look.Skin), hair = AvatarColors.Palette(look.HairColor);
            string topKey = AvatarCatalogue.Key(look.Top.Id), botKey = AvatarCatalogue.Key(look.Bottom.Id), shoeKey = AvatarCatalogue.Key(look.Shoes.Id);
            Color bot = AvatarColors.Palette(look.Bottom.Color), shoe = AvatarColors.Palette(look.Shoes.Color);

            if (body)
            {
                // body, head and hands (the trunk's cloth and, for long sleeves, the sleeves' cloth are painted canvases)
                Color top = AvatarParts.TopBase(topKey, AvatarColors.Palette(look.Top.Color));
                int topPattern = topKey == "overalls" ? 0 : look.Top.Pattern;   // the overalls' bib wears the pattern, the shirt under it is plain
                Color32[] pa = _pa;
                Cell(pa, W, w, h, CellJacket, top, AvatarColors.Shade(top, 0.15f));
                Cell(pa, W, w, h, CellTrim, AvatarColors.Tint(top, 0.12f), top);
                Cell(pa, W, w, h, CellStrap, AvatarColors.Shade(top, 0.15f), AvatarColors.Shade(top, 0.35f));
                Cell(pa, W, w, h, CellBelt, bot, AvatarColors.Shade(bot, 0.35f));
                Cell(pa, W, w, h, CellHands, skin, AvatarColors.Shade(skin, 0.12f));
                Cell(pa, W, w, h, CellSkin, skin, AvatarColors.Shade(skin, 0.12f));
                Cell(pa, W, w, h, CellHair, AvatarColors.Tint(hair, 0.22f), AvatarColors.Shade(hair, 0.3f));
                PaintCanvas(pa, w, h, Fabric.Mode.Body, top, topPattern, 0.43f, 1.13f, 0.15f);
                System.Array.Copy(pa, _pb, pa.Length);
                PaintCanvas(pa, w, h, Fabric.Mode.Arm, top, topPattern, 0f, 0f, 0f);
                // arms with bare skin (short sleeves and none): the sleeves and cuffs are skin, the shoulder strap stays the top's colour
                Cell(_pb, W, w, h, CellTrim, skin, AvatarColors.Shade(skin, 0.12f));
                PaintCanvas(_pb, w, h, Fabric.Mode.Arm, skin, 0, 0f, 0f, 0f);
            }
            if (legs)
            {
                // legs: trousers (or skin under shorts and skirts), the ankle band, the boots
                Color32[] pc = _pc;
                Cell(pc, W, w, h, CellSkin, skin, AvatarColors.Shade(skin, 0.12f));
                if (AvatarParts.LegsAreSkin(botKey)) PaintCanvas(pc, w, h, Fabric.Mode.Leg, skin, 0, 0.26f, 0.53f, 0.12f);
                else PaintCanvas(pc, w, h, Fabric.Mode.Leg, bot, look.Bottom.Pattern, 0.26f, 0.53f, 0.4f);
                if (AvatarParts.FeetAreSkin(shoeKey)) Cell(pc, W, w, h, CellBoots, skin, AvatarColors.Shade(skin, 0.12f)); else Cell(pc, W, w, h, CellBoots, shoe, AvatarColors.Shade(shoe, 0.3f));
            }

            Material proto = _top.Count > 0 ? _top[0].sharedMaterial : (_head != null ? _head.sharedMaterial : null);
            if (proto == null && _matA == null) { _hasKey = false; return; }
            if (body) { _texA = Upload(_texA, _pa, W, H, "avatar_body"); _texB = Upload(_texB, _pb, W, H, "avatar_arms"); }
            if (legs) _texC = Upload(_texC, _pc, W, H, "avatar_legs");
            if (first)
            {
                _matA = Mat(_matA, proto, _texA, "avatar_body"); _matB = Mat(_matB, proto, _texB, "avatar_arms"); _matC = Mat(_matC, proto, _texC, "avatar_legs");
                foreach (Renderer r in _body) r.sharedMaterial = _matA;
                if (_head != null) _head.sharedMaterial = _matA;
                foreach (Renderer r in _legs) r.sharedMaterial = _matC;
            }
            bool longSleeves = AvatarParts.SleeveOf(topKey) == AvatarParts.Sleeve.Long;
            foreach (Renderer r in _arms) r.sharedMaterial = longSleeves ? _matA : _matB;
        }

        static Texture2D Upload(Texture2D t, Color32[] px, int w, int h, string name)
        {
            if (t == null) { t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; }
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        static Material Mat(Material m, Material proto, Texture2D tex, string name)
        {
            if (m == null) { m = new Material(proto) { name = name }; }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            return m;
        }

        void OnDestroy()
        {
            DropGuitarMats(); if (_texA != null) Destroy(_texA); if (_texB != null) Destroy(_texB); if (_texC != null) Destroy(_texC);
            if (_matA != null) Destroy(_matA); if (_matB != null) Destroy(_matB); if (_matC != null) Destroy(_matC);
        }

        // ---------- the guitar she plays ----------

        readonly List<Material> _guitarMats = new List<Material>();

        /// <summary>A guitar material that is destroyed with the guitar (a new guitar pick must not leave materials behind).</summary>
        Material GM(WardrobeAssets a, string name, Color c, float smooth) { Material m = a.Make(name, c, smooth); if (m != null) _guitarMats.Add(m); return m; }

        void DropGuitarMats() { foreach (Material m in _guitarMats) Kill(m); _guitarMats.Clear(); }

        void BuildGuitarAvatar(WardrobeAssets a)
        {
            string kind = AvatarCatalogue.Key(_avatar.Guitar.Id);
            Color body = AvatarColors.Palette(_avatar.Guitar.Color);
            _guitar = new GameObject("Guitar");
            _guitar.transform.SetParent(_front, false);
            _guitar.transform.localPosition = new Vector3(0.1f, -0.2f, 0.42f);
            _guitar.transform.localRotation = Quaternion.Euler(-12f, 0f, 48f);
            _guitar.transform.localScale = Vector3.one * (kind == "ukulele" ? 0.7f : 0.95f);
            DropGuitarMats(); _guitarBody = GM(a, "guitar_body", body, 0.45f);
            Material wood = GM(a, "guitar_neck", new Color(0.36f, 0.2f, 0.1f), 0.3f), dark = GM(a, "guitar_dark", new Color(0.09f, 0.08f, 0.12f), 0.3f), metal = GM(a, "guitar_metal", new Color(0.85f, 0.85f, 0.9f), 0.7f);
            float neckLen = kind == "bass" ? 0.66f : (kind == "ukulele" ? 0.36f : 0.5f);
            float neckY = 0.02f + 0.26f + neckLen * 0.5f;
            switch (kind)
            {
                case "electric": case "bass":
                    Part(a.sphere, "LowerBout", _guitarBody, new Vector3(0f, -0.04f, 0f), new Vector3(0.27f, 0.3f, 0.05f));
                    Part(a.sphere, "UpperBout", _guitarBody, new Vector3(0f, 0.14f, 0f), new Vector3(0.19f, 0.2f, 0.05f));
                    Part(a.sphere, "HornL", _guitarBody, new Vector3(-0.1f, 0.24f, 0f), new Vector3(0.07f, 0.17f, 0.05f), Quaternion.Euler(0f, 0f, 15f));
                    Part(a.sphere, "HornR", _guitarBody, new Vector3(0.1f, 0.24f, 0f), new Vector3(0.07f, 0.17f, 0.05f), Quaternion.Euler(0f, 0f, -15f));
                    Part(a.cube, "Pickup", dark, new Vector3(0f, 0.06f, 0.03f), new Vector3(0.13f, 0.03f, 0.014f));
                    Part(a.cube, "Pickup", dark, new Vector3(0f, -0.04f, 0.03f), new Vector3(0.13f, 0.03f, 0.014f));
                    Part(a.cube, "Bridge", metal, new Vector3(0f, -0.13f, 0.03f), new Vector3(0.12f, 0.025f, 0.014f));
                    break;
                case "starguitar":
                    Part(MeshKit.Star(0.5f, 0.24f, 0.1f), "Star", _guitarBody, new Vector3(0f, 0.05f, 0f), new Vector3(0.62f, 0.62f, 0.5f));
                    Part(a.sphere, "Hole", dark, new Vector3(0f, 0.05f, 0.05f), new Vector3(0.09f, 0.09f, 0.03f));
                    neckY = 0.02f + 0.3f + neckLen * 0.5f;
                    break;
                default:   // acoustic and ukulele: two round bouts with a waist, a sound hole and a pickguard
                    Part(a.sphere, "LowerBout", _guitarBody, new Vector3(0f, -0.02f, 0f), new Vector3(0.28f, 0.22f, 0.07f));
                    Part(a.sphere, "UpperBout", _guitarBody, new Vector3(0f, 0.17f, 0f), new Vector3(0.19f, 0.15f, 0.065f));
                    Part(a.sphere, "SoundHole", dark, new Vector3(0f, 0.05f, 0.04f), new Vector3(0.09f, 0.09f, 0.04f));
                    Part(a.cube, "Pickguard", dark, new Vector3(0.075f, -0.05f, 0.034f), new Vector3(0.085f, 0.065f, 0.012f), Quaternion.Euler(0f, 0f, 20f));
                    Part(a.cube, "Bridge", wood, new Vector3(0f, -0.09f, 0.036f), new Vector3(0.12f, 0.025f, 0.02f));
                    neckY = 0.26f + neckLen * 0.5f;
                    break;
            }
            Part(a.cube, "Neck", wood, new Vector3(0f, neckY, 0f), new Vector3(0.06f, neckLen, 0.04f));
            Part(a.cube, "Fretboard", dark, new Vector3(0f, neckY, 0.02f), new Vector3(0.048f, neckLen - 0.02f, 0.012f));
            float headY = neckY + neckLen * 0.5f + 0.07f;
            Part(a.cube, "Headstock", kind == "electric" || kind == "bass" || kind == "starguitar" ? _guitarBody : wood, new Vector3(0f, headY, -0.005f), new Vector3(0.095f, 0.17f, 0.035f));
            int pegs = kind == "bass" ? 2 : (kind == "ukulele" ? 2 : 3);
            for (int i = 0; i < pegs; i++)
            {
                Part(a.sphere, "PegL" + i, metal, new Vector3(-0.058f, headY - 0.04f + i * 0.05f, -0.005f), new Vector3(0.035f, 0.035f, 0.035f));
                Part(a.sphere, "PegR" + i, metal, new Vector3(0.058f, headY - 0.04f + i * 0.05f, -0.005f), new Vector3(0.035f, 0.035f, 0.035f));
            }
            _guitar.SetActive(_guitarOn);
        }
    }
}
