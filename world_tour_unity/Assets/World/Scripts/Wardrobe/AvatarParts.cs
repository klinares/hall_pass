using System;
using UnityEngine;
using WorldTour.Core.Meta;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 10B.1: the procedural parts that draw each avatar item on the Star (tops, bottoms, shoes, hair, hats, glasses, bags). Everything is built from the built-in
    /// cube, sphere and cylinder and the MeshKit shapes, in the Star's own model space (see <see cref="AvatarCtx"/>), so a new colour or pattern is a material change and a new
    /// style is a handful of parts. The body itself is recoloured by <see cref="StarWardrobe"/> (palette cells); these are the extras on top of it.
    /// Model-space landmarks (the Star dump): torso half width 0.40 and front z 0.345 from y 0.42 to 1.15, neck y 1.2, head joint 1.24, head top 2.19, eyes y 1.53 at z 0.52,
    /// legs at x +-0.17 from the hip y 0.52, boots to y 0.22 with the toe at z 0.26, arms out along x at y 1.11 (shoulder x +-0.21).
    /// </summary>
    public static class AvatarParts
    {
        public enum Sleeve { Long, Short, None }

        const float TX = 0.335f, TZ = 0.35f, TB = -0.34f, RZ = 0.275f;

        /// <summary>How a top treats her arms.</summary>
        public static Sleeve SleeveOf(string key)
        {
            switch (key)
            {
                case "tee": case "polo": case "overalls": return Sleeve.Short;
                case "tank": case "vest": case "sequin": return Sleeve.None;
            }
            return Sleeve.Long;
        }

        /// <summary>The colour the torso and sleeves are painted for a top (the style's parts use the pick colour; overalls wear a light shirt under the bib).</summary>
        public static Color TopBase(string key, Color c)
        {
            return key == "overalls" ? AvatarColors.Tint(c, 0.7f) : c;
        }

        /// <summary>Whether her legs show skin under a bottom (the leg texture paints the trousers cell as skin).</summary>
        public static bool LegsAreSkin(string key)
        {
            switch (key) { case "shorts": case "skirt": case "pleated": case "sparkleskirt": case "capris": return true; }
            return false;
        }

        /// <summary>Whether shoes leave the foot bare (the boot cell is painted as skin and a low shoe is added on top).</summary>
        public static bool FeetAreSkin(string key) { return key == "flats" || key == "sandals"; }

        static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

        // ====================================================================== tops

        public static void Top(AvatarCtx c, string key)
        {
            Material m = c.F(c.C), d = c.M(c.Ds), l = c.M(c.Ls), p = c.M(c.Pc);
            if (key == "overalls") l = c.M(AvatarColors.Tint(c.C, 0.7f));
            switch (key)
            {
                case "sweater":
                    c.Ring("chest", "Hem", d, V(0, 0.46f, 0), TX, RZ, 0.09f);
                    c.Ring("chest", "Neck", d, V(0, 1.2f, 0), 0.27f, 0.27f, 0.1f);
                    Cuffs(c, d, 0.09f);
                    break;
                case "zipjacket":
                    c.Ring("chest", "Collar", d, V(0, 1.2f, 0), 0.29f, 0.28f, 0.12f);
                    c.Cube("chest", "Zip", p, V(0, 0.8f, TZ - 0.005f), V(0.035f, 0.74f, 0.03f));
                    c.Ring("chest", "Hem", d, V(0, 0.46f, 0), TX, RZ, 0.07f);
                    break;
                case "hoodie":
                    c.Ball("chest", "Hood", m, V(0, 1.1f, -0.36f), V(0.62f, 0.4f, 0.28f));
                    c.Ring("chest", "HoodRim", l, V(0, 1.2f, 0), 0.33f, 0.3f, 0.12f);
                    c.Cube("chest", "Pocket", d, V(0, 0.6f, TZ), V(0.42f, 0.2f, 0.04f));
                    foreach (float s in new[] { -1f, 1f }) c.Cube("chest", "String", l, V(0.08f * s, 1.0f, TZ), V(0.025f, 0.24f, 0.025f));
                    c.Ring("chest", "Hem", d, V(0, 0.45f, 0), TX, RZ, 0.06f);
                    Cuffs(c, d, 0.07f);
                    break;
                case "polo":
                    foreach (float s in new[] { -1f, 1f }) c.Cube("chest", "Collar", l, V(0.13f * s, 1.19f, 0.3f), V(0.2f, 0.16f, 0.04f), V(0, 0, -28f * s));
                    c.Cube("chest", "Placket", d, V(0, 1.05f, TZ), V(0.06f, 0.3f, 0.025f));
                    c.Ball("chest", "Button", p, V(0, 1.1f, TZ + 0.01f), 0.045f); c.Ball("chest", "Button", p, V(0, 0.98f, TZ + 0.01f), 0.045f);
                    c.Ring("chest", "Neck", d, V(0, 1.2f, 0), 0.27f, 0.27f, 0.07f);
                    break;
                case "turtleneck":
                    c.Ring("chest", "Neck", m, V(0, 1.27f, 0), 0.25f, 0.25f, 0.26f);
                    c.Ring("chest", "Hem", d, V(0, 0.45f, 0), TX, RZ, 0.05f);
                    break;
                case "cardigan":
                    foreach (float s in new[] { -1f, 1f }) c.Cube("chest", "Edge", d, V(0.04f + 0.0f, 0.8f, TZ), V(0.05f, 0.74f, 0.03f));
                    for (int i = 0; i < 4; i++) c.Ball("chest", "Button", p, V(0.0f, 1.05f - i * 0.15f, TZ + 0.012f), 0.05f);
                    c.Ring("chest", "Hem", d, V(0, 0.46f, 0), TX, RZ, 0.07f);
                    c.Ring("chest", "Neck", d, V(0, 1.2f, 0), 0.28f, 0.27f, 0.08f);
                    Cuffs(c, d, 0.07f);
                    break;
                case "vest":
                    c.Cube("chest", "Edge", d, V(0.0f, 0.8f, TZ), V(0.05f, 0.74f, 0.03f));
                    foreach (float s in new[] { -1f, 1f }) { c.Cube("chest", "Pocket", d, V(0.2f * s, 0.58f, TZ), V(0.17f, 0.1f, 0.03f)); c.Ring("arm" + (s < 0 ? "L" : "R"), "ArmHole", d, V(0.24f * s, 1.11f, 0), 0.14f, 0.15f, 0.05f, V(0, 0, 90f)); }
                    c.Ring("chest", "Hem", d, V(0, 0.46f, 0), TX, RZ, 0.07f);
                    break;
                case "denimjacket":
                    c.Ring("chest", "Collar", d, V(0, 1.2f, 0), 0.3f, 0.29f, 0.12f);
                    foreach (float s in new[] { -1f, 1f }) { c.Cube("chest", "Pocket", d, V(0.2f * s, 0.95f, TZ), V(0.18f, 0.16f, 0.03f)); c.Cube("chest", "Stitch", l, V(0.2f * s, 0.9f, TZ + 0.012f), V(0.18f, 0.015f, 0.01f)); }
                    c.Cube("chest", "Seam", l, V(0, 0.78f, TZ), V(0.02f, 0.7f, 0.03f));
                    c.Ring("chest", "Hem", d, V(0, 0.46f, 0), TX, RZ, 0.08f);
                    Cuffs(c, d, 0.08f);
                    break;
                case "bomber":
                    c.Ring("chest", "Collar", d, V(0, 1.2f, 0), 0.3f, 0.29f, 0.15f);
                    c.Ring("chest", "Hem", d, V(0, 0.47f, 0), TX, RZ, 0.11f);
                    c.Cube("chest", "Zip", p, V(0, 0.8f, TZ), V(0.035f, 0.7f, 0.03f));
                    Cuffs(c, d, 0.11f);
                    break;
                case "raincoat":
                    c.Ball("chest", "Hood", l, V(0, 1.1f, -0.36f), V(0.66f, 0.44f, 0.3f));
                    c.Ring("chest", "HoodRim", l, V(0, 1.2f, 0), 0.34f, 0.31f, 0.13f);
                    c.Cube("chest", "Flap", d, V(0, 0.8f, TZ), V(0.12f, 0.72f, 0.03f));
                    for (int i = 0; i < 4; i++) c.Ball("chest", "Button", p, V(0, 1.05f - i * 0.17f, TZ + 0.02f), 0.05f);
                    foreach (float s in new[] { -1f, 1f }) c.Cube("chest", "Pocket", d, V(0.22f * s, 0.6f, TZ), V(0.18f, 0.2f, 0.04f));
                    c.Ring("chest", "Hem", d, V(0, 0.45f, 0), TX + 0.01f, RZ + 0.01f, 0.08f);
                    break;
                case "overalls":
                    c.Cube("chest", "Bib", m, V(0, 0.76f, TZ), V(0.48f, 0.42f, 0.04f));
                    c.Cube("chest", "BibPocket", d, V(0, 0.7f, TZ + 0.02f), V(0.22f, 0.14f, 0.025f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        c.Cube("chest", "StrapFront", m, V(0.16f * s, 1.08f, TZ), V(0.08f, 0.34f, 0.04f));
                        c.Cube("chest", "StrapTop", m, V(0.16f * s, 1.25f, 0f), V(0.08f, 0.04f, 0.7f));
                        c.Cube("chest", "StrapBack", m, V(0.16f * s, 0.95f, TB), V(0.08f, 0.6f, 0.04f));
                        c.Ball("chest", "Button", p, V(0.16f * s, 0.96f, TZ + 0.03f), 0.06f);
                    }
                    c.Ring("chest", "Waist", m, V(0, 0.46f, 0), TX + 0.005f, RZ + 0.005f, 0.07f);
                    break;
                case "poncho":
                    c.Skirt("chest", "Poncho", m, 0.62f, 0.62f, 0.3f, 0.66f, 0.85f, false, V(0, 0, 0.0f));
                    c.Ring("chest", "Trim", p, V(0, 0.64f, 0), 0.64f, 0.55f, 0.07f);
                    c.Ring("chest", "Neck", d, V(0, 1.25f, 0), 0.3f, 0.29f, 0.09f);
                    break;
                case "sailor":
                    c.Cube("chest", "BackCollar", p, V(0, 1.0f, TB - 0.01f), V(0.86f, 0.5f, 0.04f));
                    foreach (float s in new[] { -1f, 1f }) c.Cube("chest", "Lapel", p, V(0.14f * s, 1.04f, TZ), V(0.12f, 0.46f, 0.03f), V(0, 0, 30f * s));
                    c.Cube("chest", "Knot", d, V(0, 0.84f, TZ + 0.01f), V(0.13f, 0.13f, 0.03f), V(0, 0, 45f));
                    c.Ring("chest", "Neck", d, V(0, 1.2f, 0), 0.27f, 0.27f, 0.06f);
                    Cuffs(c, p, 0.07f);
                    break;
                case "puffer":
                    for (int i = 0; i < 5; i++) c.Ring("chest", "Puff", d, V(0, 0.52f + i * 0.14f, 0), TX + 0.01f, RZ + 0.01f, 0.05f);
                    c.Ring("chest", "Collar", m, V(0, 1.22f, 0), 0.33f, 0.31f, 0.18f);
                    c.Cube("chest", "Zip", p, V(0, 0.8f, TZ + 0.012f), V(0.03f, 0.7f, 0.03f));
                    Cuffs(c, d, 0.1f);
                    break;
                case "rockerjacket":
                    foreach (float s in new[] { -1f, 1f })
                    {
                        c.Cube("chest", "Collar", d, V(0.16f * s, 1.2f, 0.3f), V(0.2f, 0.22f, 0.05f), V(0, 0, -30f * s));
                        c.Ball("chest", "Stud", c.Metal(Color.white), V(0.33f * s, 1.17f, 0.1f), 0.05f); c.Ball("chest", "Stud", c.Metal(Color.white), V(0.33f * s, 1.17f, 0.0f), 0.05f); c.Ball("chest", "Stud", c.Metal(Color.white), V(0.33f * s, 1.17f, -0.1f), 0.05f);
                    }
                    c.Cube("chest", "Zip", c.Metal(AvatarColors.Hex("#d9dbe8")), V(0f, 0.8f, TZ + 0.005f), V(0.035f, 0.74f, 0.03f));
                    Cuffs(c, d, 0.08f);
                    break;
                case "varsity":
                    c.Ring("chest", "Collar", p, V(0, 1.2f, 0), 0.29f, 0.28f, 0.1f);
                    c.Ring("chest", "Hem", p, V(0, 0.46f, 0), TX, RZ, 0.09f);
                    c.Star("chest", "Patch", p, V(0.2f, 0.95f, TZ + 0.012f), 0.22f);
                    foreach (float s in new[] { -1f, 1f }) { string f = s < 0 ? "armL" : "armR"; c.Ring(f, "Stripe", p, V(0.34f * s, 1.11f, 0), 0.14f, 0.14f, 0.04f, V(0, 0, 90f)); c.Ring(f, "Stripe", p, V(0.42f * s, 1.11f, 0), 0.14f, 0.14f, 0.04f, V(0, 0, 90f)); }
                    Cuffs(c, p, 0.09f);
                    break;
                case "stagecoat":
                    c.Skirt("hips", "Tails", m, 0.2f, 0.52f, 0.43f, 0.28f, 0.78f);
                    c.Ring("chest", "Collar", d, V(0, 1.2f, 0), 0.3f, 0.29f, 0.12f);
                    Material gold = c.Metal(AvatarColors.Hex("#ffc533"));
                    for (int i = 0; i < 3; i++) c.Ball("chest", "Button", gold, V(0.1f, 0.95f - i * 0.17f, TZ + 0.02f), 0.06f);
                    foreach (float s in new[] { -1f, 1f }) c.Cube("chest", "Epaulette", gold, V(0.3f * s, 1.17f, 0f), V(0.2f, 0.04f, 0.22f));
                    c.Cube("chest", "Edge", gold, V(0.0f, 0.8f, TZ), V(0.03f, 0.74f, 0.03f));
                    Cuffs(c, gold, 0.07f);
                    break;
                case "sequin":
                    {
                        Material s1 = c.M(c.Ls, 0.85f, 0.25f), s2 = c.M(c.Pc, 0.85f, 0.2f);
                        for (int r = 0; r < 5; r++)
                            for (int k = 0; k < 4; k++)
                                c.Ball("chest", "Sequin", (r + k) % 2 == 0 ? s1 : s2, V(-0.24f + k * 0.16f + (r % 2) * 0.04f, 0.58f + r * 0.13f, TZ + 0.005f), V(0.055f, 0.055f, 0.025f));
                        for (int k = 0; k < 6; k++) c.Ball("chest", "Sequin", k % 2 == 0 ? s1 : s2, V(-0.2f + k * 0.08f, 0.7f + (k % 3) * 0.14f, TB - 0.005f), V(0.055f, 0.055f, 0.025f));
                        c.Ring("chest", "Neck", s1, V(0, 1.2f, 0), 0.27f, 0.27f, 0.05f);
                    }
                    break;
            }
            // short sleeves are a ring round the top of each arm
            if (SleeveOf(key) == Sleeve.Short)
                foreach (float s in new[] { -1f, 1f })
                    c.Ring(s < 0 ? "armL" : "armR", "Sleeve", key == "overalls" ? l : m, V(0.37f * s, 1.11f, 0), 0.116f, 0.116f, 0.3f, V(0, 0, 90f));

        }

        static void Cuffs(AvatarCtx c, Material m, float len)
        {
            foreach (float s in new[] { -1f, 1f }) c.Ring(s < 0 ? "armL" : "armR", "Cuff", m, V(0.6f * s, 1.11f, 0), 0.108f, 0.108f, len, V(0, 0, 90f));
        }

        // ====================================================================== bottoms

        public static void Bottom(AvatarCtx c, string key)
        {
            Material m = c.F(c.C), d = c.M(c.Ds), l = c.M(c.Ls), p = c.M(c.Pc);
            switch (key)
            {
                case "shorts":
                    foreach (float s in new[] { -1f, 1f }) c.Cyl(s < 0 ? "legL" : "legR", "Short", m, V(0.17f * s, 0.46f, 0), 0.33f, 0.17f);
                    c.Ring("hips", "Waist", d, V(0, 0.47f, 0), TX - 0.01f, RZ - 0.02f, 0.08f);
                    break;
                case "capris":
                    foreach (float s in new[] { -1f, 1f }) { string f = s < 0 ? "legL" : "legR"; c.Cyl(f, "Capri", m, V(0.17f * s, 0.42f, 0), 0.3f, 0.25f); c.Ring(f, "Cuff", d, V(0.17f * s, 0.31f, 0), 0.16f, 0.16f, 0.05f); }
                    c.Ring("hips", "Waist", d, V(0, 0.47f, 0), TX - 0.01f, RZ - 0.02f, 0.08f);
                    break;
                case "skirt":
                    c.Skirt("hips", "Skirt", m, 0.3f, 0.5f, 0.37f, 0.24f, 0.88f);
                    c.Ring("hips", "Waist", d, V(0, 0.47f, 0), TX - 0.01f, RZ - 0.02f, 0.08f);
                    break;
                case "pleated":
                    c.Skirt("hips", "Pleats", m, 0.28f, 0.58f, 0.38f, 0.26f, 0.88f, true, null, 16);
                    c.Ring("hips", "Waist", d, V(0, 0.48f, 0), TX - 0.01f, RZ - 0.02f, 0.08f);
                    break;
                case "joggers":
                    foreach (float s in new[] { -1f, 1f }) { string f = s < 0 ? "legL" : "legR"; c.Ring(f, "Cuff", d, V(0.17f * s, 0.31f, 0), 0.15f, 0.145f, 0.07f); c.Cube(f, "Stripe", p, V(0.285f * s, 0.43f, 0), V(0.02f, 0.22f, 0.1f)); }
                    c.Ring("hips", "Waist", d, V(0, 0.46f, 0), TX - 0.01f, RZ - 0.02f, 0.06f);
                    break;
                case "cargo":
                    foreach (float s in new[] { -1f, 1f }) { string f = s < 0 ? "legL" : "legR"; c.Cube(f, "Pocket", d, V(0.29f * s, 0.42f, 0.02f), V(0.05f, 0.14f, 0.16f)); c.Cube(f, "Flap", m, V(0.295f * s, 0.5f, 0.02f), V(0.055f, 0.05f, 0.17f)); }
                    c.Ring("hips", "Belt", d, V(0, 0.46f, 0), TX - 0.01f, RZ - 0.02f, 0.06f);
                    break;
                case "flares":
                    foreach (float s in new[] { -1f, 1f }) c.Skirt(s < 0 ? "legL" : "legR", "Bell", m, 0.12f, 0.2f, 0.13f, 0.26f, 1f, false, V(0.17f * s, 0, 0), 14);
                    break;
                case "tutu":
                    c.Skirt("hips", "TutuLow", m, 0.38f, 0.68f, 0.4f, 0.14f, 0.9f);
                    c.Skirt("hips", "TutuHigh", l, 0.43f, 0.58f, 0.4f, 0.13f, 0.9f);
                    c.Ring("hips", "Waist", d, V(0, 0.5f, 0), TX - 0.01f, RZ - 0.02f, 0.07f);
                    break;
                case "stagepants":
                    {
                        Material gold = c.Metal(AvatarColors.Hex("#ffc533"));
                        foreach (float s in new[] { -1f, 1f }) c.Cube(s < 0 ? "legL" : "legR", "Stripe", gold, V(0.285f * s, 0.4f, 0), V(0.03f, 0.3f, 0.1f));
                        c.Ring("hips", "Belt", gold, V(0, 0.45f, 0), TX - 0.01f, RZ - 0.02f, 0.05f);
                    }
                    break;
                case "sparkleskirt":
                    {
                        c.Skirt("hips", "Skirt", m, 0.28f, 0.54f, 0.38f, 0.26f, 0.88f);
                        c.Ring("hips", "Waist", d, V(0, 0.48f, 0), TX - 0.01f, RZ - 0.02f, 0.08f);
                        Material s1 = c.M(c.Ls, 0.85f, 0.25f), s2 = c.M(c.Pc, 0.85f, 0.2f);
                        for (int i = 0; i < 14; i++) { float a = i / 14f * Mathf.PI * 2f; c.Ball("hips", "Sparkle", i % 2 == 0 ? s1 : s2, V(Mathf.Sin(a) * 0.5f, 0.3f + (i % 3) * 0.08f, Mathf.Cos(a) * 0.5f * 0.88f), 0.06f); }
                    }
                    break;
            }
        }

        // ====================================================================== shoes

        public static void Shoes(AvatarCtx c, string key)
        {
            Material m = c.M(c.C), d = c.M(c.Ds), l = c.M(c.Ls), p = c.M(c.Pc);
            Material white = c.M(AvatarColors.Hex("#fafafa"), 0.4f);
            foreach (float s in new[] { -1f, 1f })
            {
                string f = s < 0 ? "footL" : "footR";
                float x = 0.17f * s;
                switch (key)
                {
                    case "sneakers":
                        c.Cube(f, "Sole", white, V(x, 0.025f, 0.07f), V(0.27f, 0.055f, 0.47f));
                        c.Ball(f, "Cap", white, V(x, 0.075f, 0.2f), V(0.25f, 0.13f, 0.2f));
                        c.Cube(f, "Tongue", white, V(x, 0.22f, 0.08f), V(0.12f, 0.04f, 0.12f));
                        for (int i = 0; i < 3; i++) c.Cube(f, "Lace", white, V(x, 0.14f + i * 0.04f, 0.15f - i * 0.03f), V(0.14f, 0.015f, 0.02f));
                        break;
                    case "flats":
                        c.Ball(f, "Flat", m, V(x, 0.065f, 0.07f), V(0.29f, 0.13f, 0.5f));
                        c.Ball(f, "Bow", l, V(x, 0.13f, 0.27f), V(0.08f, 0.05f, 0.05f));
                        break;
                    case "sandals":
                        c.Cube(f, "Sole", m, V(x, 0.02f, 0.07f), V(0.27f, 0.045f, 0.47f));
                        c.Cube(f, "Strap", p, V(x, 0.065f, 0.0f), V(0.27f, 0.03f, 0.05f));
                        c.Cube(f, "Strap", p, V(x, 0.065f, 0.15f), V(0.27f, 0.03f, 0.05f));
                        c.Ring(f, "Ankle", p, V(x, 0.25f, 0.0f), 0.14f, 0.13f, 0.03f);
                        break;
                    case "hightops":
                        c.Cube(f, "Sole", white, V(x, 0.025f, 0.07f), V(0.27f, 0.055f, 0.47f));
                        c.Ball(f, "Cap", white, V(x, 0.075f, 0.2f), V(0.25f, 0.13f, 0.2f));
                        c.Ring(f, "Collar", white, V(x, 0.24f, 0.0f), 0.145f, 0.135f, 0.07f);
                        c.Ball(f, "Patch", white, V(x + 0.125f * s, 0.15f, 0.0f), V(0.03f, 0.08f, 0.08f));
                        for (int i = 0; i < 3; i++) c.Cube(f, "Lace", white, V(x, 0.14f + i * 0.04f, 0.15f - i * 0.03f), V(0.14f, 0.015f, 0.02f));
                        break;
                    case "rainboots":
                        c.Cyl(f, "Shaft", c.M(c.C, 0.8f), V(x, 0.2f, 0.0f), 0.285f, 0.3f);
                        c.Ring(f, "Rim", l, V(x, 0.34f, 0.0f), 0.145f, 0.145f, 0.04f);
                        c.Cube(f, "Sole", d, V(x, 0.02f, 0.07f), V(0.28f, 0.045f, 0.48f));
                        break;
                    case "platforms":
                        c.Cube(f, "Platform", d, V(x, 0.05f, 0.06f), V(0.31f, 0.11f, 0.52f));
                        c.Cube(f, "Stripe", p, V(x, 0.05f, 0.06f), V(0.315f, 0.025f, 0.525f));
                        break;
                    case "glitterboots":
                        {
                            Material gold = c.Metal(AvatarColors.Hex("#ffc533")), sp = c.M(c.Ls, 0.9f, 0.3f);
                            c.Cube(f, "Sole", gold, V(x, 0.02f, 0.07f), V(0.27f, 0.045f, 0.47f));
                            for (int i = 0; i < 7; i++) c.Ball(f, "Glitter", sp, V(x + ((i % 3) - 1) * 0.07f, 0.06f + i * 0.026f, 0.16f - (i % 4) * 0.07f), 0.04f);
                            c.Ring(f, "Rim", gold, V(x, 0.24f, 0.0f), 0.145f, 0.135f, 0.035f);
                        }
                        break;
                }
            }
        }

        // ====================================================================== hair

        /// <summary>A hat that sits on the crown of her head. Hair that stands up there (buns, pom poms, puffs, spikes, curls, bangs) would poke through it, so under one of these hats it
        /// is tucked: the volume drops to the back or the sides, or goes. A headband sits low enough to leave the hair as it is.</summary>
        public static bool CrownHat(string hat) { return hat != null && hat != "none" && hat != "headband"; }

        public static void Hair(AvatarCtx c, string key)
        {
            Material h = c.M(c.Hair, 0.2f), tie = c.M(AvatarColors.Contrast(c.Hair), 0.3f);
            bool hat = CrownHat(c.HatKey);
            switch (key)
            {
                case "ponytail":
                    c.Ball("head", "Tie", tie, V(0, hat ? 1.84f : 1.98f, -0.5f), 0.15f);
                    c.Ball("head", "Tail", h, V(0, hat ? 1.8f : 1.95f, -0.64f), V(0.36f, 0.4f, 0.34f));
                    c.Ball("head", "Tail", h, V(0, 1.7f, -0.72f), V(0.34f, 0.46f, 0.3f));
                    c.Ball("head", "Tail", h, V(0, 1.46f, -0.72f), V(0.28f, 0.42f, 0.26f));
                    c.Ball("head", "Tail", h, V(0, 1.26f, -0.7f), V(0.2f, 0.3f, 0.2f));
                    break;
                case "pigtails":
                    foreach (float s in new[] { -1f, 1f })
                    {
                        c.Ball("head", "Tie", tie, V(0.56f * s, 1.78f, -0.1f), 0.14f);
                        c.Ball("head", "Tail", h, V(0.64f * s, 1.55f, -0.1f), V(0.28f, 0.42f, 0.28f));
                        c.Ball("head", "Tail", h, V(0.66f * s, 1.3f, -0.1f), V(0.26f, 0.4f, 0.26f));
                        c.Ball("head", "Tail", h, V(0.65f * s, 1.1f, -0.1f), V(0.18f, 0.28f, 0.18f));
                    }
                    break;
                case "bun":
                    if (hat) c.Ball("head", "BunLow", h, V(0, 1.66f, -0.62f), 0.42f);          // the bun slips down behind the hat
                    else { c.Ball("head", "Bun", h, V(0, 2.26f, -0.12f), 0.48f); c.Ring("head", "Tie", tie, V(0, 2.1f, -0.12f), 0.22f, 0.22f, 0.06f); }
                    break;
                case "braid":
                    {
                        float[] ys = { 1.92f, 1.74f, 1.56f, 1.38f, 1.2f, 1.02f }; float[] xs = { 0f, 0.05f, -0.05f, 0.05f, -0.05f, 0f };
                        for (int i = 0; i < ys.Length; i++) c.Ball("head", "Braid", h, V(xs[i], ys[i], -0.58f - (i < 2 ? i * 0.03f : 0.04f)), V(0.24f, 0.22f, 0.22f));
                        c.Ball("head", "Tie", tie, V(0, 0.9f, -0.56f), 0.12f);
                    }
                    break;
                case "fringe":
                    if (!hat) c.Ball("head", "Bangs", h, V(0, 1.99f, 0.37f), V(0.72f, 0.22f, 0.16f), V(-28f, 0, 0));
                    foreach (float s in new[] { -1f, 1f }) c.Ball("head", "Lock", h, V(0.5f * s, 1.7f, 0.32f), V(0.14f, 0.5f, 0.2f));
                    break;
                case "spacebuns":
                    foreach (float s in new[] { -1f, 1f })
                    {
                        if (hat) { c.Ball("head", "BunLow", h, V(0.62f * s, 1.66f, -0.22f), 0.36f); continue; }
                        c.Ball("head", "Bun", h, V(0.38f * s, 2.12f, 0f), 0.46f);
                    }
                    break;
                case "long":
                    LongHair(c, h, 0f, false);
                    break;
                case "wavy":
                    LongHair(c, h, 0.1f, true);
                    break;
                case "pompoms":
                    foreach (float s in new[] { -1f, 1f })
                    {
                        if (hat) { c.Ball("head", "PomLow", h, V(0.64f * s, 1.62f, -0.22f), 0.38f); continue; }
                        c.Ball("head", "Pom", h, V(0.4f * s, 2.14f, 0f), 0.5f);
                    }
                    break;
                case "curls":
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f, x = Mathf.Sin(a) * 0.58f, z = Mathf.Cos(a) * 0.58f;
                        if (z > 0.2f && Mathf.Abs(x) < 0.4f) continue;
                        if (!hat) c.Ball("head", "Curl", h, V(x, 1.9f + (i % 3) * 0.1f, z - 0.05f), 0.34f);
                        c.Ball("head", "Curl", h, V(x * 1.05f, 1.55f + (i % 2) * 0.05f, z * 0.9f - 0.05f), 0.28f);
                    }
                    if (!hat) for (int i = 0; i < 5; i++) { float a = i / 5f * Mathf.PI * 2f; c.Ball("head", "CurlTop", h, V(Mathf.Sin(a) * 0.22f, 2.22f, Mathf.Cos(a) * 0.22f - 0.05f), 0.32f); }
                    break;
                case "afro":
                    if (hat)
                    {
                        foreach (float s in new[] { -1f, 1f }) c.Ball("head", "PuffSide", h, V(0.66f * s, 1.72f, -0.12f), V(0.5f, 0.6f, 0.7f));
                        c.Ball("head", "PuffBack", h, V(0, 1.7f, -0.62f), V(0.9f, 0.66f, 0.46f));
                    }
                    else c.Ball("head", "Puff", h, V(0, 2.0f, -0.22f), V(1.5f, 1.4f, 1.4f));
                    break;
                case "mohawk":
                    if (!hat) for (int i = 0; i < 8; i++) c.Cone("head", "Spike", h, V(0, 2.12f + 0.02f * Mathf.Sin(i), -0.42f + i * 0.1f), 0.1f, 0.34f, V(-10f + i * 3f, 0, 0));
                    else for (int i = 0; i < 3; i++) c.Cone("head", "SpikeLow", h, V(0, 1.78f - i * 0.12f, -0.6f - i * 0.04f), 0.1f, 0.28f, V(-110f, 0, 0));
                    break;
                case "spikes":
                    if (hat) break;
                    c.Cone("head", "Spike", h, V(0, 2.18f, -0.02f), 0.13f, 0.38f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * 360f, ar = a * Mathf.Deg2Rad;
                        if (Mathf.Cos(ar) > 0.5f) continue;
                        c.Cone("head", "Spike", h, V(Mathf.Sin(ar) * 0.38f, 2.1f, Mathf.Cos(ar) * 0.38f - 0.02f), 0.11f, 0.34f, Quaternion.Euler(0, a, 0).eulerAngles + V(32f, 0, 0));
                    }
                    break;
            }
        }

        /// <summary>
        /// Long hair: a curtain behind her that falls past the shoulders (no wider than they are: the torso is 0.8 across), kept off the top of the head so the crown of the skull is still
        /// the head's own hairline, and two thin locks beside her face. Wavy adds scallops down the sides.
        /// </summary>
        static void LongHair(AvatarCtx c, Material h, float extra, bool waves)
        {
            c.Ball("head", "Back", h, V(0, 1.74f, -0.4f), V(0.96f, 0.72f, 0.4f));
            c.Ball("head", "Back", h, V(0, 1.38f, -0.47f), V(0.84f, 0.8f, 0.3f));
            c.Ball("head", "Back", h, V(0, 1.07f - extra, -0.5f), V(0.72f, 0.66f + extra, 0.26f));
            foreach (float s in new[] { -1f, 1f })
            {
                c.Ball("head", "Lock", h, V(0.54f * s, 1.55f, -0.1f), V(0.16f, 0.78f, 0.3f));
                if (waves) for (int i = 0; i < 3; i++) c.Ball("head", "Wave", h, V((0.46f - (i % 2) * 0.04f) * s, 1.34f - i * 0.2f, -0.34f), V(0.26f, 0.3f, 0.26f));
            }
        }

        // ====================================================================== hats

        public static void Hat(AvatarCtx c, string key)
        {
            Material m = c.M(c.C), d = c.M(c.Ds), l = c.M(c.Ls), p = c.M(c.Pc);
            if (key == "headphones") { Color lc = AvatarColors.Lift(c.C, 0.34f); m = c.M(lc); d = c.M(AvatarColors.Shade(lc, 0.2f)); l = c.M(AvatarColors.Tint(lc, 0.4f)); }   // black phones keep a visible rim
            switch (key)
            {
                case "beanie":
                    c.Dome("head", "Cap", m, V(0, 1.8f, -0.02f), V(1.12f, 1.12f, 1.12f), default(Vector3), 0.8f);
                    c.Ring("head", "Cuff", l, V(0, 1.88f, -0.02f), 0.59f, 0.59f, 0.17f);
                    c.Ball("head", "Pom", l, V(0, 2.28f, -0.02f), 0.22f);
                    break;
                case "cap":
                    c.Dome("head", "Crown", m, V(0, 1.86f, -0.02f), V(1.18f, 1.18f, 1.18f), default(Vector3), 0.8f);
                    c.Ball("head", "Brim", d, V(0, 1.9f, 0.55f), V(0.84f, 0.06f, 0.6f), V(8f, 0, 0));
                    c.Ball("head", "Button", d, V(0, 2.32f, -0.02f), 0.1f);
                    break;
                case "headband":
                    c.Ring("head", "Band", m, V(0, 2.02f, -0.02f), 0.56f, 0.55f, 0.14f);
                    break;
                case "bucket":
                    c.Add("head", "Crown", MeshKit.Frustum(1f, 0.82f, 0.38f, 22, false, true), m, V(0, 1.9f, -0.02f), V(0.62f, 1f, 0.62f));
                    c.Ring("head", "Brim", d, V(0, 1.88f, -0.02f), 0.95f, 0.95f, 0.05f, V(4f, 0, 0), 0.5f);
                    c.Ring("head", "Band", l, V(0, 1.96f, -0.02f), 0.63f, 0.63f, 0.09f);
                    break;
                case "bow":
                    c.Ball("head", "WingL", m, V(-0.48f, 2.3f, 0.1f), V(0.34f, 0.26f, 0.12f), V(0, 0, 24f));
                    c.Ball("head", "WingR", m, V(-0.12f, 2.28f, 0.1f), V(0.34f, 0.26f, 0.12f), V(0, 0, -24f));
                    c.Ball("head", "Knot", d, V(-0.3f, 2.29f, 0.12f), 0.15f);
                    break;
                case "partyhat":
                    c.Cone("head", "Cone", m, V(0, 2.08f, -0.02f), 0.32f, 0.72f, V(-6f, 0, 0));
                    c.Ring("head", "Brim", l, V(0, 2.1f, -0.02f), 0.34f, 0.34f, 0.07f);
                    c.Ball("head", "Pom", l, V(-0.03f, 2.82f, -0.06f), 0.16f);
                    for (int i = 0; i < 4; i++) c.Ball("head", "Dot", l, V(Mathf.Sin(i * 1.6f) * 0.16f, 2.3f + i * 0.1f, Mathf.Cos(i * 1.6f) * 0.16f - 0.04f), 0.06f);
                    break;
                case "flowers":
                    {
                        Material yel = c.M(AvatarColors.Hex("#ffd34e")), leaf = c.M(AvatarColors.Hex("#4fb870"));
                        for (int i = 0; i < 9; i++)
                        {
                            float a = i / 9f * Mathf.PI * 2f, x = Mathf.Sin(a) * 0.55f, z = Mathf.Cos(a) * 0.55f - 0.02f;
                            c.Ball("head", "Petals", m, V(x, 2.04f, z), V(0.2f, 0.17f, 0.2f));
                            c.Ball("head", "Heart", yel, V(x * 1.04f, 2.06f, z * 1.04f), 0.09f);
                            float b = a + Mathf.PI / 9f;
                            c.Ball("head", "Leaf", leaf, V(Mathf.Sin(b) * 0.55f, 2.02f, Mathf.Cos(b) * 0.55f - 0.02f), V(0.13f, 0.07f, 0.13f));
                        }
                    }
                    break;
                case "beret":
                    c.Ball("head", "Beret", m, V(0.06f, 2.12f, -0.02f), V(1.12f, 0.36f, 1.12f), V(0, 0, 8f));
                    c.Ring("head", "Band", d, V(0, 1.98f, -0.02f), 0.58f, 0.57f, 0.07f);
                    c.Ball("head", "Nub", d, V(0.1f, 2.33f, -0.02f), 0.09f);
                    break;
                case "catears":
                    c.Ring("head", "Band", m, V(0, 2.04f, -0.02f), 0.56f, 0.55f, 0.1f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        c.Cone("head", "Ear", m, V(0.33f * s, 2.08f, 0.0f), 0.17f, 0.34f, V(0, 0, -16f * s));
                        c.Cone("head", "Inner", l, V(0.33f * s, 2.1f, 0.05f), 0.1f, 0.22f, V(0, 0, -16f * s));
                    }
                    break;
                case "sunhat":
                    c.Ring("head", "Brim", m, V(0, 1.9f, -0.02f), 1.08f, 1.08f, 0.04f, V(3f, 0, 0), 0.6f);
                    c.Dome("head", "Crown", m, V(0, 1.9f, -0.02f), V(1.0f, 1.0f, 1.0f), default(Vector3), 0.7f);
                    c.Ring("head", "Band", l, V(0, 1.95f, -0.02f), 0.54f, 0.54f, 0.1f);
                    break;
                case "headphones":
                    for (int i = 0; i < 13; i++)
                    {
                        float a = (-90f + i * 15f) * Mathf.Deg2Rad;
                        c.Ball("head", "Band", d, V(Mathf.Sin(a) * 0.57f, 1.63f + Mathf.Cos(a) * 0.55f, -0.02f), 0.09f);
                    }
                    foreach (float s in new[] { -1f, 1f })
                    {
                        c.Cyl("head", "Cup", m, V(0.58f * s, 1.68f, -0.02f), 0.3f, 0.15f, V(0, 0, 90f));
                        c.Cyl("head", "Pad", d, V(0.52f * s, 1.68f, -0.02f), 0.24f, 0.07f, V(0, 0, 90f));
                    }
                    break;
                case "tophat":
                    c.Cyl("head", "Crown", m, V(0, 2.34f, -0.02f), 0.68f, 0.62f);
                    c.Cyl("head", "Brim", m, V(0, 2.03f, -0.02f), 1.05f, 0.05f);
                    c.Cyl("head", "Band", l, V(0, 2.12f, -0.02f), 0.7f, 0.12f);
                    break;
                case "crown":
                    {
                        Material gem = c.M(c.Pc, 0.9f, 0.2f);
                        c.Ring("head", "Band", m, V(0, 2.08f, -0.02f), 0.52f, 0.5f, 0.2f);
                        for (int i = 0; i < 7; i++)
                        {
                            float a = i / 7f * Mathf.PI * 2f;
                            c.Cone("head", "Point", m, V(Mathf.Sin(a) * 0.5f, 2.18f, Mathf.Cos(a) * 0.48f - 0.02f), 0.09f, 0.26f);
                            c.Ball("head", "Gem", gem, V(Mathf.Sin(a) * 0.54f, 2.08f, Mathf.Cos(a) * 0.52f - 0.02f), 0.07f);
                        }
                    }
                    break;
            }
        }

        // ====================================================================== glasses

        public static void Glasses(AvatarCtx c, string key)
        {
            Color lc = AvatarColors.Lift(c.C, 0.3f);
            Material m = c.M(lc, 0.4f), dark = c.M(AvatarColors.Hex("#14121c"), 0.85f);
            const float y = 1.6f, z = 0.575f;
            Action<float> arms = zFront =>
            {
                foreach (float s in new[] { -1f, 1f }) c.Cube("head", "Arm", m, V(0.5f * s, y + 0.02f, 0.27f), V(0.025f, 0.025f, 0.62f));
            };
            switch (key)
            {
                case "round":
                    foreach (float s in new[] { -1f, 1f }) c.Add("head", "Frame", MeshKit.Torus(0.16f, 0.02f, 24, 8), m, V(0.2f * s, y, z), Vector3.one, V(90f, 0, 0));
                    c.Cube("head", "Bridge", m, V(0, y + 0.04f, z), V(0.1f, 0.025f, 0.025f)); arms(z);
                    break;
                case "square":
                    foreach (float s in new[] { -1f, 1f })
                    {
                        float x = 0.2f * s;
                        c.Cube("head", "Top", m, V(x, y + 0.13f, z), V(0.34f, 0.03f, 0.03f)); c.Cube("head", "Bottom", m, V(x, y - 0.13f, z), V(0.34f, 0.03f, 0.03f));
                        c.Cube("head", "In", m, V(x - 0.17f, y, z), V(0.03f, 0.28f, 0.03f)); c.Cube("head", "Out", m, V(x + 0.17f, y, z), V(0.03f, 0.28f, 0.03f));
                    }
                    c.Cube("head", "Bridge", m, V(0, y + 0.08f, z), V(0.1f, 0.025f, 0.025f)); arms(z);
                    break;
                case "shades":
                    {
                        // lenses just big enough for the eyes, each in a lighter rim (black shades keep a visible rim), a thin bridge between them
                        Material lens = c.M(AvatarColors.Shade(lc, 0.72f), 0.85f);
                        foreach (float s in new[] { -1f, 1f })
                        {
                            c.Ball("head", "Lens", lens, V(0.18f * s, y, z), V(0.27f, 0.19f, 0.05f));
                            c.Add("head", "Rim", MeshKit.Torus(0.1f, 0.016f, 24, 8), m, V(0.18f * s, y, z + 0.005f), V(1.3f, 1f, 1f), V(90f, 0, 0));
                        }
                        c.Cube("head", "Bridge", m, V(0, y + 0.03f, z), V(0.1f, 0.03f, 0.03f)); arms(z);
                    }
                    break;
                case "cateye":
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 e = (Quaternion.Euler(0, 0, 14f * s) * Quaternion.Euler(90f, 0, 0)).eulerAngles;
                        c.Add("head", "Frame", MeshKit.Torus(0.15f, 0.02f, 24, 8), m, V(0.21f * s, y + 0.02f, z), V(1.3f, 1f, 0.9f), e);
                        c.Ball("head", "Wing", m, V(0.4f * s, y + 0.12f, z), V(0.12f, 0.05f, 0.04f), V(0, 0, 25f * s));
                    }
                    c.Cube("head", "Bridge", m, V(0, y + 0.05f, z), V(0.1f, 0.025f, 0.025f)); arms(z);
                    break;
                case "star":
                    foreach (float s in new[] { -1f, 1f }) c.Star("head", "Lens", m, V(0.21f * s, y, z + 0.01f), 0.42f);
                    c.Cube("head", "Bridge", m, V(0, y + 0.02f, z), V(0.1f, 0.025f, 0.025f)); arms(z);
                    break;
                case "goggles":
                    foreach (float s in new[] { -1f, 1f })
                    {
                        c.Add("head", "Frame", MeshKit.Torus(0.13f, 0.035f, 24, 8), m, V(0.2f * s, y, z), Vector3.one, V(90f, 0, 0));
                    }
                    c.Cube("head", "Bridge", m, V(0, y, z), V(0.1f, 0.05f, 0.04f));
                    c.Ring("head", "Strap", c.M(c.Ds), V(0, 1.55f, -0.02f), 0.56f, 0.54f, 0.09f);
                    break;
                case "visor":
                    c.Ring("head", "Band", m, V(0, 1.9f, -0.02f), 0.56f, 0.55f, 0.1f);
                    c.Ball("head", "Brim", m, V(0, 1.9f, 0.66f), V(1.0f, 0.05f, 0.5f), V(8f, 0, 0));
                    break;
            }
        }

        // ====================================================================== bags

        public static void Bag(AvatarCtx c, string key)
        {
            Material m = c.M(c.C), d = c.M(c.Ds), l = c.M(c.Ls), p = c.M(c.Pc);
            switch (key)
            {
                case "backpack":
                    c.Cube("chest", "Pack", m, V(0, 0.85f, -0.47f), V(0.5f, 0.62f, 0.22f));
                    c.Cube("chest", "Flap", d, V(0, 1.17f, -0.47f), V(0.52f, 0.1f, 0.24f));
                    c.Cube("chest", "Pocket", d, V(0, 0.68f, -0.6f), V(0.34f, 0.22f, 0.08f));
                    Straps(c, d);
                    break;
                case "tote":
                    c.Cube("chest", "Tote", m, V(0, 0.62f, -0.5f), V(0.52f, 0.46f, 0.14f));
                    c.Add("chest", "Handle", MeshKit.Torus(0.16f, 0.018f, 20, 6, 180f), d, V(0, 0.84f, -0.5f), Vector3.one, V(90f, 0, 0));
                    c.Cube("chest", "Band", l, V(0, 0.7f, -0.58f), V(0.53f, 0.07f, 0.02f));
                    break;
                case "beltbag":
                    c.Ring("hips", "Belt", d, V(0, 0.47f, 0), TX + 0.005f, RZ + 0.005f, 0.05f);
                    c.Cube("hips", "Pouch", m, V(0.12f, 0.46f, TZ + 0.07f), V(0.3f, 0.18f, 0.13f));
                    c.Cube("hips", "Zip", p, V(0.12f, 0.53f, TZ + 0.07f), V(0.3f, 0.015f, 0.14f));
                    break;
                case "satchel":
                    c.Cube("chest", "Strap", d, V(0, 0.82f, TZ + 0.005f), V(0.05f, 1.0f, 0.03f), V(0, 0, -34f));
                    c.Cube("chest", "StrapBack", d, V(0, 0.82f, TB - 0.005f), V(0.05f, 1.0f, 0.03f), V(0, 0, 34f));
                    c.Cube("chest", "Satchel", m, V(-0.28f, 0.5f, -0.44f), V(0.4f, 0.3f, 0.14f));
                    c.Cube("chest", "Flap", d, V(-0.28f, 0.58f, -0.44f), V(0.42f, 0.12f, 0.15f));
                    c.Ball("chest", "Clasp", p, V(-0.28f, 0.52f, -0.53f), 0.06f);
                    break;
                case "heartbag":
                    c.Ball("chest", "LobeL", m, V(-0.12f, 0.76f, -0.5f), V(0.26f, 0.26f, 0.12f));
                    c.Ball("chest", "LobeR", m, V(0.12f, 0.76f, -0.5f), V(0.26f, 0.26f, 0.12f));
                    c.Cube("chest", "Point", m, V(0, 0.64f, -0.5f), V(0.26f, 0.26f, 0.12f), V(0, 0, 45f));
                    Straps(c, l);
                    break;
                case "bigpack":
                    c.Cube("chest", "Pack", m, V(0, 0.88f, -0.5f), V(0.64f, 0.8f, 0.3f));
                    c.Cube("chest", "Pocket", d, V(0, 0.62f, -0.67f), V(0.46f, 0.3f, 0.1f));
                    c.Cyl("chest", "Roll", l, V(0, 1.36f, -0.5f), 0.22f, 0.7f, V(0, 0, 90f));
                    Straps(c, d);
                    break;
                case "starbag":
                    c.Star("chest", "Bag", m, V(0, 0.86f, -0.44f), 0.8f, V(0, 180f, 0));
                    Straps(c, d);
                    break;
            }
        }

        // ====================================================================== faces

        /// <summary>Her face: a transparent decal on the front of the head (eyes, brows, mouth, freckles), a texture per face. The head itself has no painted-on face.</summary>
        public static void Face(AvatarCtx c, string key)
        {
            Material m = c.FaceMat(key);
            if (m == null || c.A.faceMesh == null) return;
            c.Add("head", "Face", c.A.faceMesh, m, Vector3.zero, Vector3.one);
        }

        static void Straps(AvatarCtx c, Material m)
        {
            foreach (float s in new[] { -1f, 1f }) { c.Cube("chest", "Strap", m, V(0.16f * s, 0.9f, TZ + 0.004f), V(0.07f, 0.62f, 0.03f)); c.Cube("chest", "StrapTop", m, V(0.16f * s, 1.2f, TZ - 0.1f), V(0.07f, 0.03f, 0.28f), V(-30f, 0, 0)); }
        }
    }
}
