using UnityEngine;
using static AU.EditorTools.TextureGen;

namespace AU.EditorTools
{
    /// <summary>Procedural textures for the bay city: terrain, water, roofing, stone, league banners and posters.</summary>
    public static class WorldTextures
    {
        // league palette from the concept art: navy, red, off-white
        public static readonly Color Navy = new(0.07f, 0.1f, 0.2f);
        public static readonly Color Red = new(0.72f, 0.12f, 0.14f);
        public static readonly Color Cream = new(0.93f, 0.91f, 0.86f);

        // ------------------------------------------------------------------ terrain
        public enum GroundKind { Grass, Rock, Sand, Urban, Dirt }

        public static (Texture2D albedo, Texture2D normal) Ground(GroundKind kind)
        {
            const int S = 512;
            var px = new Color[S * S];
            var hgt = new float[S * S];
            int seed = 500 + (int)kind * 13;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float big = Fbm(u, v, 4, 4, seed), fine = Fbm(u, v, 32, 3, seed + 1), grit = Hash(x, y, seed + 2);
                    Color c; float h;
                    switch (kind)
                    {
                        case GroundKind.Grass:
                        {
                            float blade = Fbm(u * 3, v * 3, 48, 2, seed + 4);
                            c = Color.Lerp(new Color(0.16f, 0.24f, 0.08f), new Color(0.3f, 0.36f, 0.13f), big * 0.7f + blade * 0.5f);
                            c = Color.Lerp(c, new Color(0.36f, 0.32f, 0.18f), Mathf.SmoothStep(0.62f, 0.8f, Fbm(u, v, 6, 3, seed + 5)) * 0.5f);
                            h = blade * 0.7f + fine * 0.3f;
                            break;
                        }
                        case GroundKind.Rock:
                        {
                            float strata = Mathf.Abs(Mathf.Sin((v * 9 + big * 1.4f) * Mathf.PI));
                            float ridge = 1 - Mathf.Abs(Fbm(u, v, 8, 5, seed + 6) * 2 - 1);
                            float l = 0.24f + big * 0.16f + ridge * 0.12f - strata * 0.05f;
                            c = new Color(l * 1.02f, l, l * 0.95f);
                            h = ridge * 0.6f + big * 0.3f + fine * 0.1f;
                            break;
                        }
                        case GroundKind.Sand:
                        {
                            float rip = Mathf.Sin((u * 40 + big * 4) * Mathf.PI) * 0.5f + 0.5f;
                            c = Color.Lerp(new Color(0.55f, 0.48f, 0.36f), new Color(0.72f, 0.65f, 0.5f), big * 0.6f + grit * 0.25f);
                            h = rip * 0.3f + fine * 0.5f + grit * 0.2f;
                            break;
                        }
                        case GroundKind.Dirt:
                        {
                            c = Color.Lerp(new Color(0.24f, 0.19f, 0.14f), new Color(0.38f, 0.31f, 0.23f), big * 0.7f + grit * 0.2f);
                            h = fine * 0.6f + grit * 0.4f;
                            break;
                        }
                        default: // urban paving: worn concrete slabs
                        {
                            int tile = 128;
                            float edge = Mathf.Min(Mathf.Min(x % tile, tile - x % tile), Mathf.Min(y % tile, tile - y % tile));
                            float groove = Mathf.Clamp01(edge / 2f);
                            float tint = Hash(x / tile, y / tile, seed) * 0.05f;
                            float l = (0.34f + tint + big * 0.08f + fine * 0.05f) * Mathf.Lerp(0.6f, 1f, groove);
                            c = new Color(l, l * 0.99f, l * 0.96f);
                            h = groove * 0.6f + fine * 0.4f;
                            break;
                        }
                    }
                    px[y * S + x] = new Color(c.r, c.g, c.b, 1);
                    hgt[y * S + x] = h;
                }
            string n = "Ground_" + kind;
            return (Save(n + "_Albedo", px, S, S), Save(n + "_Normal", HeightToNormal(hgt, S, S, kind == GroundKind.Rock ? 4f : 2f), S, S, normal: true));
        }

        /// <summary>Tileable wind-wave height field as a normal map.</summary>
        public static Texture2D WaterNormal()
        {
            const int S = 512;
            var hgt = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float a = Fbm(u, v, 6, 5, 811);
                    float b = Mathf.Sin((u * 14 + a * 3) * Mathf.PI * 2) * 0.5f + 0.5f;
                    hgt[y * S + x] = a * 0.7f + b * 0.1f + Fbm(u, v, 24, 3, 812) * 0.2f;
                }
            return Save("Water_Normal", HeightToNormal(hgt, S, S, 6f), S, S, normal: true);
        }

        // ------------------------------------------------------------------ building materials
        public static (Texture2D albedo, Texture2D normal) RoofTiles()
        {
            const int S = 512;
            var px = new Color[S * S];
            var hgt = new float[S * S];
            int row = 32, col = 24;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int r = y / row;
                    int xo = (x + (r % 2) * col / 2) % S;
                    float fy = (y % row) / (float)row, fx = (xo % col) / (float)col;
                    float bump = Mathf.Sin(fx * Mathf.PI) * 0.6f + fy * 0.4f;
                    float tint = Hash(xo / col, r, 61);
                    float n = Fbm(x / (float)S, y / (float)S, 8, 3, 62);
                    Color c = Color.Lerp(new Color(0.5f, 0.2f, 0.12f), new Color(0.68f, 0.33f, 0.2f), tint * 0.6f + n * 0.4f);
                    c *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(fy * 4f)) * (0.85f + bump * 0.15f);
                    px[y * S + x] = new Color(c.r, c.g, c.b, 1);
                    hgt[y * S + x] = bump * 0.8f + fy * 0.2f;
                }
            return (Save("RoofTiles_Albedo", px, S, S), Save("RoofTiles_Normal", HeightToNormal(hgt, S, S, 4f), S, S, normal: true));
        }

        public static (Texture2D albedo, Texture2D normal) Corrugated(string name, Color baseCol, int seed)
        {
            const int S = 512;
            var px = new Color[S * S];
            var hgt = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float rib = Mathf.Sin(u * Mathf.PI * 2 * 24) * 0.5f + 0.5f;
                    float n = Fbm(u, v, 6, 4, seed);
                    float streak = Fbm(u * 8, v * 0.5f, 8, 3, seed + 1);
                    float seam = (y % 128) < 2 ? 0.6f : 1f;
                    Color c = baseCol * (0.78f + n * 0.3f + rib * 0.08f - streak * 0.12f) * seam;
                    px[y * S + x] = new Color(c.r, c.g, c.b, 1);
                    hgt[y * S + x] = rib;
                }
            return (Save(name + "_Albedo", px, S, S), Save(name + "_Normal", HeightToNormal(hgt, S, S, 3f), S, S, normal: true));
        }

        /// <summary>Dressed stone blocks (bridge towers, capitol, arena).</summary>
        public static (Texture2D albedo, Texture2D normal) Stone(string name, Color baseCol, int seed)
        {
            const int S = 512;
            var px = new Color[S * S];
            var hgt = new float[S * S];
            int bh = 42, bw = 96;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int r = y / bh;
                    int xo = (x + (r % 2) * bw / 2) % S;
                    float ex = Mathf.Min(xo % bw, bw - xo % bw), ey = Mathf.Min(y % bh, bh - y % bh);
                    float joint = Mathf.Clamp01(Mathf.Min(ex, ey) / 2.2f);
                    float tint = Hash(xo / bw, r, seed) * 0.08f;
                    float n = Fbm(x / (float)S, y / (float)S, 16, 4, seed + 1);
                    float dirt = Fbm(x / (float)S, y / (float)S, 3, 3, seed + 2);
                    Color c = baseCol * (0.86f + tint + n * 0.12f - dirt * 0.1f) * Mathf.Lerp(0.62f, 1f, joint);
                    px[y * S + x] = new Color(c.r, c.g, c.b, 1);
                    hgt[y * S + x] = joint * 0.7f + n * 0.3f;
                }
            return (Save(name + "_Albedo", px, S, S), Save(name + "_Normal", HeightToNormal(hgt, S, S, 3f), S, S, normal: true));
        }

        /// <summary>Colosseum facade tile: two tiers of arches over a stone wall (alpha unused).</summary>
        public static (Texture2D albedo, Texture2D emission) ArchFacade()
        {
            const int W = 512, H = 512;
            var px = new Color[W * H];
            var emi = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Fbm(x / (float)W, y / (float)H, 16, 3, 71);
                    Color c = Cream * (0.82f + n * 0.14f);
                    bool cornice = (y % 256) > 236 || (y % 256) < 10;
                    if (cornice) c *= 1.08f;
                    // arches: 2 per tile width, 2 tiers
                    int tier = y / 256;
                    float ly = (y % 256) / 256f, lx = (x % 256) / 256f;
                    float ax = (lx - 0.5f) / 0.32f;
                    float top = 0.62f + Mathf.Sqrt(Mathf.Max(0, 1 - ax * ax)) * 0.2f;
                    bool open = Mathf.Abs(ax) < 1 && ly > 0.1f && ly < top;
                    if (open)
                    {
                        float depth = Mathf.Clamp01((ly - 0.1f) / 0.6f);
                        c = new Color(0.1f, 0.09f, 0.08f) * (0.6f + depth * 0.4f);
                        emi[y * W + x] = new Color(1f, 0.75f, 0.45f) * (0.25f + 0.25f * depth) * (tier == 0 ? 1f : 0.6f);
                    }
                    else emi[y * W + x] = Color.black;
                    px[y * W + x] = new Color(c.r, c.g, c.b, 1);
                }
            return (Save("ArchFacade_Albedo", px, W, H), Save("ArchFacade_Emission", emi, W, H));
        }

        /// <summary>Stands seen from the field: rows of seats packed with a colorful crowd.</summary>
        public static Texture2D Crowd()
        {
            const int W = 1024, H = 256;
            var px = new Color[W * H];
            var rnd = new System.Random(77);
            Color[] shirts = { Navy, Red, Cream, new(0.2f, 0.35f, 0.7f), new(0.15f, 0.15f, 0.17f), new(0.8f, 0.7f, 0.25f), new(0.3f, 0.5f, 0.35f) };
            for (int y = 0; y < H; y++)
            {
                int rowI = y / 16;
                for (int x = 0; x < W; x++)
                {
                    int seat = x / 10;
                    float fy = (y % 16) / 16f, fx = (x % 10) / 10f;
                    Color c = new(0.22f, 0.24f, 0.28f);
                    if (fy < 0.2f) c = new Color(0.4f, 0.4f, 0.42f);
                    float occ = Hash(seat, rowI, 78);
                    if (occ < 0.86f && fy > 0.25f)
                    {
                        Color s = shirts[(int)(Hash(seat, rowI, 79) * shirts.Length) % shirts.Length];
                        bool head = fy > 0.7f && Mathf.Abs(fx - 0.5f) < 0.22f;
                        bool body = fy <= 0.7f && Mathf.Abs(fx - 0.5f) < 0.38f;
                        if (head) c = new Color(0.55f, 0.4f, 0.3f) * (0.6f + Hash(seat, rowI, 80) * 0.6f);
                        else if (body) c = s * (0.75f + Hash(seat, rowI, 81) * 0.4f);
                    }
                    px[y * W + x] = c;
                }
            }
            return Save("Crowd", px, W, H);
        }

        /// <summary>Red/white/blue racing pylon stripes, tiling vertically.</summary>
        public static Texture2D PylonStripes()
        {
            const int W = 64, H = 512;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)H;
                    Color c = v > 0.82f ? Red : v > 0.62f ? Cream : v > 0.42f ? new Color(0.12f, 0.22f, 0.55f) : v > 0.22f ? Cream : Red;
                    px[y * W + x] = c * (0.92f + Hash(x, y, 9) * 0.08f);
                }
            return Save("PylonStripes", px, W, H);
        }

        // ------------------------------------------------------------------ league graphics
        static void FillPoly(Color[] px, int W, int H, Vector2[] poly, Color c, float alpha = 1)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in poly) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            for (int y = Mathf.Max(0, (int)minY); y <= Mathf.Min(H - 1, (int)maxY); y++)
                for (int x = Mathf.Max(0, (int)minX); x <= Mathf.Min(W - 1, (int)maxX); x++)
                {
                    bool inside = false;
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                        if (((poly[i].y > y) != (poly[j].y > y)) && (x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) inside = !inside;
                    if (inside) px[y * W + x] = Color.Lerp(px[y * W + x], c, alpha);
                }
        }

        static Vector2[] Map(Rect r, params float[] uv)
        {
            var p = new Vector2[uv.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(r.x + uv[i * 2] * r.width, r.y + uv[i * 2 + 1] * r.height);
            return p;
        }

        /// <summary>League wing-chevron emblem.</summary>
        public static void Emblem(Color[] px, int W, int H, Rect r, Color c)
        {
            FillPoly(px, W, H, Map(r, 0.0f, 1.0f, 0.42f, 0.0f, 0.5f, 0.12f, 0.2f, 1.0f), c);
            FillPoly(px, W, H, Map(r, 1.0f, 1.0f, 0.58f, 0.0f, 0.5f, 0.12f, 0.8f, 1.0f), c);
            FillPoly(px, W, H, Map(r, 0.3f, 0.95f, 0.5f, 0.35f, 0.7f, 0.95f, 0.5f, 0.7f), c);
        }

        /// <summary>Front-on mech silhouette with tall shoulder fins (after the "03" hero frame).</summary>
        public static void MechSilhouette(Color[] px, int W, int H, Rect r, Color c)
        {
            void P(params float[] uv) => FillPoly(px, W, H, Map(r, uv), c);
            P(0.36f, 0.0f, 0.47f, 0.0f, 0.46f, 0.38f, 0.38f, 0.38f);          // left leg
            P(0.53f, 0.0f, 0.64f, 0.0f, 0.62f, 0.38f, 0.54f, 0.38f);          // right leg
            P(0.31f, 0.0f, 0.49f, 0.0f, 0.48f, 0.05f, 0.33f, 0.05f);          // feet
            P(0.51f, 0.0f, 0.69f, 0.0f, 0.67f, 0.05f, 0.52f, 0.05f);
            P(0.37f, 0.36f, 0.63f, 0.36f, 0.66f, 0.48f, 0.34f, 0.48f);        // hips
            P(0.32f, 0.46f, 0.68f, 0.46f, 0.74f, 0.7f, 0.26f, 0.7f);          // torso
            P(0.45f, 0.69f, 0.55f, 0.69f, 0.56f, 0.78f, 0.44f, 0.78f);        // head
            P(0.18f, 0.62f, 0.3f, 0.7f, 0.28f, 0.45f, 0.19f, 0.36f, 0.14f, 0.5f); // left shoulder + upper arm
            P(0.82f, 0.62f, 0.7f, 0.7f, 0.72f, 0.45f, 0.81f, 0.36f, 0.86f, 0.5f);
            P(0.15f, 0.38f, 0.22f, 0.38f, 0.23f, 0.18f, 0.14f, 0.18f);        // forearms
            P(0.85f, 0.38f, 0.78f, 0.38f, 0.77f, 0.18f, 0.86f, 0.18f);
            P(0.2f, 0.66f, 0.27f, 0.68f, 0.33f, 1.0f, 0.27f, 0.96f);          // shoulder fins
            P(0.8f, 0.66f, 0.73f, 0.68f, 0.67f, 1.0f, 0.73f, 0.96f);
        }

        /// <summary>Vertical street-pole banner: navy field, red slash, emblem and a mech.</summary>
        public static Texture2D Banner()
        {
            const int W = 256, H = 768;
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = Navy;
            FillPoly(px, W, H, new[] { new Vector2(0, 230), new Vector2(W, 470), new Vector2(W, 560), new Vector2(0, 320) }, Red);
            FillPoly(px, W, H, new[] { new Vector2(0, 330), new Vector2(W, 570), new Vector2(W, 590), new Vector2(0, 350) }, Cream);
            Emblem(px, W, H, new Rect(70, 600, 116, 110), Cream);
            MechSilhouette(px, W, H, new Rect(20, 40, 216, 520), new Color(0.03f, 0.04f, 0.08f));
            return Save("LeagueBanner", px, W, H);
        }

        /// <summary>Building-sized championship poster.</summary>
        public static Texture2D Poster(string name, string line1, string line2, int seed)
        {
            const int W = 512, H = 1024;
            var px = new Color[W * H];
            var rnd = new System.Random(seed);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = Color.Lerp(Cream, new Color(0.85f, 0.83f, 0.8f), y / (float)H);
            FillPoly(px, W, H, new[] { new Vector2(0, 520), new Vector2(W, 900), new Vector2(W, 1024), new Vector2(380, 1024), new Vector2(0, 700) }, Red);
            FillPoly(px, W, H, new[] { new Vector2(0, 380), new Vector2(W, 760), new Vector2(W, 800), new Vector2(0, 420) }, Navy);
            MechSilhouette(px, W, H, new Rect(30, 150, 450, 760), new Color(0.06f, 0.07f, 0.12f));
            FillPoly(px, W, H, new[] { new Vector2(0, 0), new Vector2(W, 0), new Vector2(W, 150), new Vector2(0, 150) }, Navy);
            StrokeFont.Paint(px, W, H, line1, new Rect(24, 82, W - 48, 56), Cream * 1.1f, 4f, 2f);
            StrokeFont.Paint(px, W, H, line2, new Rect(24, 20, W - 48, 44), Red * 1.4f, 3f, 2f);
            Emblem(px, W, H, new Rect(390, 920, 90, 80), Navy);
            return Save(name, px, W, H);
        }

        /// <summary>Emissive arena screen: wide graphic for the Grand Finals.</summary>
        public static Texture2D Screen(string name, string line1, string line2)
        {
            const int W = 1024, H = 512;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = Color.Lerp(Navy * 1.4f, new Color(0.1f, 0.18f, 0.4f), y / (float)H);
            FillPoly(px, W, H, new[] { new Vector2(0, 0), new Vector2(380, 0), new Vector2(560, H), new Vector2(180, H) }, Red * 1.1f);
            FillPoly(px, W, H, new[] { new Vector2(390, 0), new Vector2(420, 0), new Vector2(600, H), new Vector2(570, H) }, Cream);
            MechSilhouette(px, W, H, new Rect(60, 20, 380, 470), new Color(0.95f, 0.95f, 0.98f));
            StrokeFont.Paint(px, W, H, line1, new Rect(560, 250, 430, 150), Cream * 1.2f, 6f, 4f);
            StrokeFont.Paint(px, W, H, line2, new Rect(560, 110, 430, 100), Cream * 1.0f, 5f, 3f);
            return Save(name, px, W, H);
        }

        /// <summary>Big hangar roller door with a painted bay number.</summary>
        public static Texture2D HangarDoor(string number)
        {
            const int W = 512, H = 512;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float rib = (y % 16) < 3 ? 0.75f : 1f;
                    float n = Fbm(x / (float)W, y / (float)H, 8, 3, 33);
                    Color c = new Color(0.46f, 0.47f, 0.47f) * (0.85f + n * 0.25f) * rib;
                    if (y < 24) c = new Color(0.8f, 0.62f, 0.1f) * ((x / 32) % 2 == 0 ? 1f : 0.15f);
                    px[y * W + x] = c;
                }
            StrokeFont.Paint(px, W, H, number, new Rect(140, 120, 232, 280), Cream * 1.05f, 14f, 1f);
            return Save("HangarDoor_" + number, px, W, H);
        }
    }
}
