using System.IO;
using UnityEditor;
using UnityEngine;

namespace AU.EditorTools
{
    /// <summary>Procedural texture authoring. Every texture is written to disk as PNG
    /// and imported with the right settings so the scene references stable assets.</summary>
    public static class TextureGen
    {
        public const string Dir = "Assets/AU/Generated/Textures";

        // ------------------------------------------------------------------ noise
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        public static float Value(float x, float y, int seed, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int Wr(int v) => ((v % period) + period) % period;
            float a = Hash(Wr(x0), Wr(y0), seed), b = Hash(Wr(x0 + 1), Wr(y0), seed);
            float c = Hash(Wr(x0), Wr(y0 + 1), seed), d = Hash(Wr(x0 + 1), Wr(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Tileable fBm in [0,1]. u,v in [0,1).</summary>
        public static float Fbm(float u, float v, int baseFreq, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int f = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(u * f, v * f, seed + o * 17, f) * amp;
                norm += amp; amp *= 0.5f; f *= 2;
            }
            return sum / norm;
        }

        // ------------------------------------------------------------------ io
        public static Texture2D Save(string name, Color[] px, int w, int h, bool linear = false, bool normal = false, bool mips = true, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            Directory.CreateDirectory(Dir);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !linear && !normal;
            imp.mipmapEnabled = mips;
            imp.wrapMode = wrap;
            imp.anisoLevel = 8;
            imp.filterMode = FilterMode.Trilinear;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.maxTextureSize = Mathf.Max(w, h);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Derive a tangent-space normal map from a height field (wraps).</summary>
        public static Color[] HeightToNormal(float[] hgt, int w, int h, float strength)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float l = hgt[y * w + (x - 1 + w) % w], r = hgt[y * w + (x + 1) % w];
                    float d = hgt[((y - 1 + h) % h) * w + x], u = hgt[((y + 1) % h) * w + x];
                    Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    px[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
                }
            return px;
        }

        // ------------------------------------------------------------------ asphalt
        public static (Texture2D albedo, Texture2D normal) Asphalt()
        {
            const int S = 1024;
            var px = new Color[S * S];
            var hgt = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float big = Fbm(u, v, 4, 5, 3);
                    float grit = Hash(x, y, 9);
                    float stones = Fbm(u, v, 64, 2, 11);
                    float h = stones * 0.6f + grit * 0.25f + big * 0.15f;
                    // hairline cracks from a ridged noise band
                    float crack = Mathf.Abs(Fbm(u, v, 6, 4, 21) - 0.5f);
                    float c = Mathf.SmoothStep(0.012f, 0.0f, crack);
                    h -= c * 0.5f;
                    hgt[y * S + x] = h;
                    float lum = 0.055f + big * 0.05f + (grit > 0.93f ? 0.06f : 0f) + stones * 0.03f - c * 0.03f;
                    px[y * S + x] = new Color(lum, lum * 1.01f, lum * 1.05f, 1);
                }
            var albedo = Save("Asphalt_Albedo", px, S, S);
            var normal = Save("Asphalt_Normal", HeightToNormal(hgt, S, S, 3.5f), S, S, normal: true);
            return (albedo, normal);
        }

        /// <summary>R: puddles (large blotches), G: grime variation.</summary>
        public static Texture2D PuddleMask()
        {
            const int S = 512;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float p = Fbm(u, v, 3, 5, 41);
                    float g = Fbm(u, v, 8, 4, 57);
                    px[y * S + x] = new Color(Mathf.SmoothStep(0.45f, 0.62f, p), g, 0, 1);
                }
            return Save("Puddle_Mask", px, S, S, linear: true);
        }

        /// <summary>Tileable field of rain ripple rings as a normal map.</summary>
        public static Texture2D Ripples()
        {
            const int S = 256;
            var hgt = new float[S * S];
            var rnd = new System.Random(7);
            for (int k = 0; k < 70; k++)
            {
                float cx = (float)rnd.NextDouble() * S, cy = (float)rnd.NextDouble() * S;
                float r0 = 4 + (float)rnd.NextDouble() * 14;
                float amp = 0.4f + (float)rnd.NextDouble() * 0.6f;
                int reach = (int)(r0 + 6);
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float ring = Mathf.Cos((d - r0) * 1.4f) * Mathf.Exp(-Mathf.Pow((d - r0) / 3f, 2));
                        int x = ((int)cx + dx + S) % S, y = ((int)cy + dy + S) % S;
                        hgt[y * S + x] += ring * amp * Mathf.Clamp01(1 - d / (r0 + 6));
                    }
            }
            return Save("Rain_Ripples", HeightToNormal(hgt, S, S, 0.9f), S, S, normal: true);
        }

        public static (Texture2D albedo, Texture2D normal) Sidewalk()
        {
            const int S = 512;
            var px = new Color[S * S];
            var hgt = new float[S * S];
            int tile = 64;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    int tx = x / tile, ty = y / tile;
                    float edge = Mathf.Min(Mathf.Min(x % tile, tile - x % tile), Mathf.Min(y % tile, tile - y % tile));
                    float groove = Mathf.Clamp01(edge / 2.5f);
                    float tint = Hash(tx, ty, 5) * 0.04f;
                    float n = Fbm(u, v, 16, 4, 77);
                    float lum = (0.16f + tint + n * 0.06f) * Mathf.Lerp(0.45f, 1f, groove);
                    px[y * S + x] = new Color(lum, lum, lum * 1.03f, 1);
                    hgt[y * S + x] = groove * 0.6f + n * 0.4f;
                }
            return (Save("Sidewalk_Albedo", px, S, S), Save("Sidewalk_Normal", HeightToNormal(hgt, S, S, 2.5f), S, S, normal: true));
        }

        public static (Texture2D albedo, Texture2D normal) Concrete(int seed)
        {
            const int S = 512;
            var px = new Color[S * S];
            var hgt = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u, v, 8, 5, seed);
                    float streak = Fbm(u * 0.25f, v * 4f, 4, 3, seed + 3) * 0.5f;
                    float panel = (x % 128 < 2 || y % 128 < 2) ? 0.5f : 1f;
                    float lum = (0.12f + n * 0.07f - streak * 0.05f) * panel;
                    px[y * S + x] = new Color(lum * 1.02f, lum, lum * 0.98f, 1);
                    hgt[y * S + x] = n * 0.5f + panel * 0.5f;
                }
            return (Save($"Concrete{seed}_Albedo", px, S, S), Save($"Concrete{seed}_Normal", HeightToNormal(hgt, S, S, 2f), S, S, normal: true));
        }

        /// <summary>
        /// Building facade: one texture tile = floors x bays. Albedo has frames/mullions,
        /// emission has lit windows (warm/cool/dark, some with blinds), alpha unused.
        /// </summary>
        public static (Texture2D albedo, Texture2D emission) Facade(string name, int seed, int floors, int bays, float glassFrac, Color frameColor, float litChance, Color coolTint, bool curtainWall)
        {
            const int S = 512;
            var alb = new Color[S * S];
            var emi = new Color[S * S];
            var rnd = new System.Random(seed);
            int fh = S / floors, bw = S / bays;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Fbm(x / (float)S, y / (float)S, 8, 3, seed);
                    alb[y * S + x] = frameColor * (0.8f + n * 0.4f);
                    emi[y * S + x] = Color.black;
                }
            for (int f = 0; f < floors; f++)
            {
                bool floorLit = rnd.NextDouble() < 0.55;
                for (int b = 0; b < bays; b++)
                {
                    int mx = curtainWall ? 3 : (int)(bw * (1 - glassFrac) * 0.5f);
                    int my = curtainWall ? 4 : (int)(fh * 0.22f);
                    int x0 = b * bw + mx, x1 = (b + 1) * bw - mx;
                    int y0 = f * fh + my, y1 = (f + 1) * fh - (curtainWall ? 2 : (int)(fh * 0.12f));
                    bool lit = rnd.NextDouble() < (floorLit ? litChance * 1.4 : litChance * 0.4);
                    double kind = rnd.NextDouble();
                    Color lc = kind < 0.62 ? new Color(1f, 0.72f, 0.42f) : kind < 0.9 ? coolTint : new Color(0.95f, 0.95f, 1f);
                    float bright = 0.25f + (float)rnd.NextDouble() * 0.55f;
                    bool blinds = rnd.NextDouble() < 0.35;
                    float tv = rnd.NextDouble() < 0.06 ? 1f : 0f;
                    int midX = (x0 + x1) / 2;
                    int transomY = y0 + (int)((y1 - y0) * 0.78f);
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            float gy = (y - y0) / (float)(y1 - y0), gx = (x - x0) / (float)(x1 - x0);
                            bool mullion = !curtainWall && (Mathf.Abs(x - midX) < 2 || Mathf.Abs(y - transomY) < 2);
                            bool edge = x - x0 < 2 || x1 - x < 3 || y - y0 < 2 || y1 - y < 2;
                            if (mullion || edge)
                            {
                                alb[y * S + x] = frameColor * 0.55f;
                                continue;
                            }
                            // glass: dark reflective with a faint sky gradient
                            Color g = new Color(0.025f, 0.03f, 0.042f) * (0.7f + gy * 0.6f);
                            alb[y * S + x] = g;
                            if (lit)
                            {
                                float k = bright * (0.7f + 0.3f * gy);
                                if (blinds && ((y - y0) % 5 < 2)) k *= 0.3f;
                                // interior: ceiling light falloff + furniture silhouettes low in the window
                                k *= 0.55f + 0.45f * Mathf.Exp(-Mathf.Pow((gx - 0.5f) * 2.0f, 2));
                                if (gy < 0.28f && Hash(x / 6, f * 31 + b, seed) > 0.45f) k *= 0.25f;
                                Color c = tv > 0 ? new Color(0.3f, 0.5f, 1f) : lc;
                                emi[y * S + x] = c * k;
                            }
                        }
                    // sill
                    for (int x = x0 - 2; x < x1 + 2; x++)
                        for (int y = y0 - 4; y < y0 - 1; y++)
                            if (x >= 0 && x < S && y >= 0) alb[y * S + x] = frameColor * 1.3f;
                }
            }
            return (Save(name + "_Albedo", alb, S, S), Save(name + "_Emission", emi, S, S));
        }

        /// <summary>Street-level shopfronts: bright display windows, shutters, small signs.</summary>
        public static (Texture2D albedo, Texture2D emission) Storefront(int seed)
        {
            const int W = 1024, H = 256;
            var alb = new Color[W * H];
            var emi = new Color[W * H];
            var rnd = new System.Random(seed);
            for (int i = 0; i < alb.Length; i++) { alb[i] = new Color(0.06f, 0.06f, 0.07f); emi[i] = Color.black; }
            int x = 0;
            Color[] neon = { new Color(1f, 0.15f, 0.6f), new Color(0f, 0.85f, 1f), new Color(1f, 0.65f, 0.1f), new Color(0.5f, 0.3f, 1f), new Color(0.2f, 1f, 0.4f) };
            while (x < W)
            {
                int w = 140 + rnd.Next(120);
                int x1 = Mathf.Min(W, x + w);
                double kind = rnd.NextDouble();
                Color c = kind < 0.5 ? new Color(1f, 0.85f, 0.6f) : neon[rnd.Next(neon.Length)];
                bool shutter = kind > 0.85;
                for (int yy = 18; yy < 190; yy++)
                    for (int xx = x + 8; xx < x1 - 8; xx++)
                    {
                        int i = yy * W + xx;
                        if (shutter)
                        {
                            float s = (yy % 6 < 2) ? 0.07f : 0.13f;
                            alb[i] = new Color(s, s, s * 1.05f);
                        }
                        else
                        {
                            float gy = (yy - 18) / 172f;
                            alb[i] = new Color(0.03f, 0.03f, 0.04f);
                            float k = 0.5f + 0.5f * gy;
                            // counter + shelf rows and the odd customer silhouette
                            if (yy < 60) k *= 0.3f;
                            else if ((yy - 60) % 34 < 4) k *= 0.45f;
                            float person = Mathf.Abs(xx - (x + w * 0.6f));
                            if (person < 9 && yy < 130 && yy > 40 && seed % 2 == 0) k *= 0.15f;
                            // pendant lamps
                            if (yy > 172 && ((xx - x) % 40 > 15 && (xx - x) % 40 < 25)) k *= 2.2f;
                            emi[i] = c * k * 1.2f;
                        }
                    }
                // sign band above
                Color sc = neon[rnd.Next(neon.Length)];
                for (int yy = 200; yy < 246; yy++)
                    for (int xx = x + 10; xx < x1 - 10; xx++)
                    {
                        alb[yy * W + xx] = new Color(0.02f, 0.02f, 0.025f);
                    }
                string[] words = { "RAMEN", "BAR", "PARTS", "24H", "NOODLE", "ARCADE", "DINER", "TECH", "KARAOKE", "PAWN", "GARAGE", "SUSHI", "CAFE", "OPEN" };
                StrokeFont.Paint(emi, W, H, words[rnd.Next(words.Length)], new Rect(x + 16, 204, x1 - x - 32, 38), sc * 1.4f, 1.6f, 4f);
                x = x1;
            }
            return (Save($"Storefront{seed}_Albedo", alb, W, H), Save($"Storefront{seed}_Emission", emi, W, H));
        }

        /// <summary>Rooftop billboard ad: graphic shapes + a slogan.</summary>
        public static Texture2D Billboard(string name, string line1, string line2, Color a, Color b, int seed)
        {
            const int W = 1024, H = 512;
            var px = new Color[W * H];
            var rnd = new System.Random(seed);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    Color bg = Color.Lerp(a * 0.25f, b * 0.35f, v + Mathf.Sin(u * 3 + seed) * 0.15f);
                    px[y * W + x] = bg;
                }
            // diagonal stripes + big circle motif
            float cx = 760 + rnd.Next(-60, 60), cy = 260, r = 170;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d < r) px[y * W + x] = Color.Lerp(px[y * W + x], b, 0.75f * Mathf.Clamp01((r - d) / 6f));
                    if (((x + y) / 22) % 5 == 0 && x < 520) px[y * W + x] = Color.Lerp(px[y * W + x], a, 0.35f);
                }
            StrokeFont.Paint(px, W, H, line1, new Rect(40, 230, 600, 200), Color.white * 1.2f, 7f, 10f);
            StrokeFont.Paint(px, W, H, line2, new Rect(40, 90, 560, 90), a * 1.3f, 4f, 6f);
            return Save(name, px, W, H);
        }

        public static Texture2D RoadLines()
        {
            // 1x64 dash pattern used by lane marking meshes (white with wear)
            const int W = 64, H = 256;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float wear = Fbm(x / (float)W, y / (float)H, 8, 3, 91);
                    float k = Mathf.Clamp01(wear * 1.6f - 0.25f);
                    px[y * W + x] = new Color(0.8f, 0.78f, 0.7f, k);
                }
            return Save("RoadLine", px, W, H);
        }

        public static Texture2D SoftDot()
        {
            const int S = 64;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    float k = Mathf.Pow(Mathf.Clamp01(1 - d), 2f);
                    px[y * S + x] = new Color(1, 1, 1, k);
                }
            return Save("SoftDot", px, S, S, wrap: TextureWrapMode.Clamp);
        }

        public static Texture2D RainStreak()
        {
            const int W = 16, H = 128;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - W / 2f) / (W / 2f);
                    float k = Mathf.Pow(Mathf.Clamp01(1 - dx), 3f) * Mathf.Sin(y / (float)H * Mathf.PI);
                    px[y * W + x] = new Color(1, 1, 1, k);
                }
            return Save("RainStreak", px, W, H, wrap: TextureWrapMode.Clamp);
        }
    }
}
