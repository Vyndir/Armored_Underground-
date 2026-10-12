using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static AU.CityLayout;

namespace AU.EditorTools
{
    /// <summary>
    /// Generates the Kessel Ward district: wet streets, sidewalks, dense buildings with
    /// lit facades and storefronts, neon signage, rooftop clutter, streetlights,
    /// parked cars and street props. Geometry is merged per block and material.
    /// </summary>
    public class CityBuilder
    {
        public const string MeshAssetPath = "Assets/AU/Generated/CityMeshes.asset";

        readonly System.Random rnd = new(20261012);
        float R() => (float)rnd.NextDouble();
        float R(float a, float b) => a + (b - a) * R();
        T Pick<T>(IList<T> l) => l[rnd.Next(l.Count)];

        Transform root;
        Mesh meshContainer;
        int meshCount;

        // materials
        Material asphalt, sidewalk, curb, concreteA, concreteB, roof, metal, darkMetal, glass, lampHead, shaft, laneLine, rubber, awningRed, awningTeal, signBack;
        Material[] facades, storefronts, neons, billboards, carPaints;
        Material tail, head;
        Color[] neonColors;

        // merged geometry per material for the current group
        Dictionary<Material, MeshBuilder> builders = new();
        MeshBuilder B(Material m) { if (!builders.TryGetValue(m, out var b)) builders[m] = b = new MeshBuilder(); return b; }

        public static void Build(Transform parent)
        {
            new CityBuilder().Run(parent);
        }

        void Run(Transform parent)
        {
            root = new GameObject("City").transform;
            root.SetParent(parent, false);
            if (File.Exists(MeshAssetPath)) AssetDatabase.DeleteAsset(MeshAssetPath);
            meshContainer = null; meshCount = 0;
            CreateMaterials();

            BuildGround();
            for (int i = 0; i < AvenueX.Length - 1; i++)
                for (int j = 0; j < StreetZ.Length - 1; j++)
                    BuildBlock(i, j);
            BuildBorder();
            BuildStreets();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ materials
        void CreateMaterials()
        {
            var (aAlb, aNrm) = TextureGen.Asphalt();
            var mask = TextureGen.PuddleMask();
            var ripples = TextureGen.Ripples();
            asphalt = Mats.Custom("Asphalt_Wet", Shader.Find("AU/WetAsphalt"));
            asphalt.SetTexture("_BaseMap", aAlb);
            asphalt.SetTexture("_BumpMap", aNrm);
            asphalt.SetTexture("_MaskMap", mask);
            asphalt.SetTexture("_RippleMap", ripples);
            asphalt.SetFloat("_Tiling", 0.11f);
            asphalt.SetFloat("_PuddleTiling", 0.012f);
            asphalt.SetFloat("_BumpScale", 1.2f);

            var (sAlb, sNrm) = TextureGen.Sidewalk();
            sidewalk = Mats.LitMat("Sidewalk", new Color(0.55f, 0.55f, 0.58f), 0.58f, 0f, sAlb, sNrm);
            var (c1a, c1n) = TextureGen.Concrete(3);
            var (c2a, c2n) = TextureGen.Concrete(8);
            concreteA = Mats.LitMat("ConcreteA", new Color(0.9f, 0.88f, 0.86f), 0.25f, 0, c1a, c1n);
            concreteB = Mats.LitMat("ConcreteB", new Color(0.7f, 0.72f, 0.78f), 0.3f, 0, c2a, c2n);
            curb = Mats.LitMat("Curb", new Color(0.35f, 0.35f, 0.37f), 0.5f, 0, c1a, c1n);
            roof = Mats.LitMat("Roof", new Color(0.09f, 0.09f, 0.1f), 0.2f, 0);
            metal = Mats.LitMat("Metal", new Color(0.32f, 0.33f, 0.36f), 0.55f, 0.85f);
            darkMetal = Mats.LitMat("MetalDark", new Color(0.06f, 0.06f, 0.07f), 0.45f, 0.8f);
            glass = Mats.LitMat("GlassDark", new Color(0.02f, 0.025f, 0.03f), 0.95f, 0.3f);
            rubber = Mats.LitMat("Rubber", new Color(0.02f, 0.02f, 0.02f), 0.3f, 0f);
            laneLine = Mats.LitMat("LaneLine", new Color(0.75f, 0.73f, 0.66f), 0.55f, 0f, null, null, null, new Color(0.75f, 0.73f, 0.66f) * 0.18f);
            awningRed = Mats.LitMat("AwningRed", new Color(0.45f, 0.05f, 0.06f), 0.35f, 0f);
            awningTeal = Mats.LitMat("AwningTeal", new Color(0.03f, 0.3f, 0.32f), 0.35f, 0f);
            signBack = Mats.LitMat("SignBack", new Color(0.03f, 0.03f, 0.035f), 0.5f, 0.5f);
            lampHead = Mats.Neon("LampHead", new Color(1f, 0.78f, 0.5f) * 6f);
            shaft = Mats.Glow("LampShaft", new Color(1f, 0.62f, 0.32f) * 0.05f, 1.6f, 0.75f);
            tail = Mats.Neon("TailLight", new Color(1f, 0.05f, 0.03f) * 5f);
            head = Mats.Neon("HeadLight", new Color(1f, 0.95f, 0.85f) * 3f);

            neonColors = new[]
            {
                new Color(1f, 0.1f, 0.55f), new Color(0f, 0.85f, 1f), new Color(1f, 0.55f, 0.05f),
                new Color(0.55f, 0.25f, 1f), new Color(0.25f, 1f, 0.35f), new Color(1f, 0.15f, 0.1f), new Color(1f, 0.95f, 0.85f),
            };
            neons = new Material[neonColors.Length];
            for (int i = 0; i < neonColors.Length; i++) neons[i] = Mats.Neon($"Neon_{i}", neonColors[i] * 7f, null, i % 3 == 0 ? 0.25f : 0f, i * 13.1f);

            var styles = new (string name, int floors, int bays, float glass, Color frame, float lit, Color cool, bool curtain)[]
            {
                ("FacadeOffice", 4, 4, 0.85f, new Color(0.16f, 0.17f, 0.19f), 0.45f, new Color(0.65f, 0.85f, 1f), false),
                ("FacadeApart", 4, 4, 0.55f, new Color(0.22f, 0.19f, 0.17f), 0.5f, new Color(1f, 0.6f, 0.85f), false),
                ("FacadeGlass", 4, 6, 1f, new Color(0.08f, 0.09f, 0.11f), 0.35f, new Color(0.55f, 0.8f, 1f), true),
                ("FacadeBrick", 4, 3, 0.5f, new Color(0.24f, 0.12f, 0.09f), 0.55f, new Color(1f, 0.75f, 0.5f), false),
                ("FacadeConcrete", 4, 4, 0.6f, new Color(0.3f, 0.3f, 0.31f), 0.4f, new Color(0.7f, 1f, 0.85f), false),
            };
            facades = new Material[styles.Length];
            for (int i = 0; i < styles.Length; i++)
            {
                var s = styles[i];
                var (alb, emi) = TextureGen.Facade(s.name, 100 + i, s.floors, s.bays, s.glass, s.frame, s.lit, s.cool, s.curtain);
                facades[i] = Mats.LitMat(s.name, Color.white, s.curtain ? 0.85f : 0.55f, s.curtain ? 0.5f : 0.15f, alb, null, emi, Color.white * 1.15f);
            }
            storefronts = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                var (alb, emi) = TextureGen.Storefront(200 + i);
                storefronts[i] = Mats.LitMat($"Storefront{i}", Color.white, 0.75f, 0.1f, alb, null, emi, Color.white * 1.5f);
            }
            var ads = new[]
            {
                ("VOLTLINE", "ENERGY FOR MACHINES", new Color(0f, 0.8f, 1f), new Color(1f, 0.2f, 0.6f)),
                ("KAIJU", "RAMEN  OPEN 24H", new Color(1f, 0.5f, 0.05f), new Color(1f, 0.1f, 0.1f)),
                ("HEXA", "FRAMES BUILT TO FIGHT", new Color(0.5f, 0.3f, 1f), new Color(0f, 0.9f, 0.7f)),
                ("NIGHTSHIFT", "STAY UP. GET PAID.", new Color(1f, 0.15f, 0.55f), new Color(0.2f, 0.3f, 1f)),
                ("ZERO-G", "SKYHOOK AIRLINES", new Color(0.3f, 1f, 0.5f), new Color(0f, 0.5f, 1f)),
                ("OKAMI", "BOOSTERS & PARTS", new Color(1f, 0.85f, 0.2f), new Color(1f, 0.35f, 0f)),
            };
            billboards = new Material[ads.Length];
            for (int i = 0; i < ads.Length; i++)
            {
                var tex = TextureGen.Billboard($"Billboard{i}", ads[i].Item1, ads[i].Item2, ads[i].Item3, ads[i].Item4, 300 + i);
                billboards[i] = Mats.Neon($"Billboard{i}", Color.white * 2.4f, tex);
            }
            carPaints = new[]
            {
                Mats.LitMat("Car_Black", new Color(0.02f, 0.02f, 0.025f), 0.9f, 0.6f),
                Mats.LitMat("Car_Silver", new Color(0.45f, 0.46f, 0.5f), 0.85f, 0.9f),
                Mats.LitMat("Car_Red", new Color(0.35f, 0.02f, 0.03f), 0.9f, 0.4f),
                Mats.LitMat("Car_White", new Color(0.7f, 0.7f, 0.72f), 0.85f, 0.3f),
                Mats.LitMat("Car_Blue", new Color(0.03f, 0.08f, 0.25f), 0.9f, 0.5f),
            };
        }

        // ------------------------------------------------------------------ helpers
        void Flush(string groupName, Transform parent, bool isStatic = true)
        {
            var g = new GameObject(groupName).transform;
            g.SetParent(parent, false);
            foreach (var kv in builders)
            {
                if (kv.Value.Count == 0) continue;
                var mesh = kv.Value.ToMesh($"{groupName}_{kv.Key.name}");
                SaveMesh(mesh);
                var go = new GameObject(kv.Key.name);
                go.transform.SetParent(g, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key;
                bool transparent = kv.Key.shader.name == "AU/Glow";
                mr.shadowCastingMode = transparent || kv.Key.shader.name == "AU/Neon" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            builders = new Dictionary<Material, MeshBuilder>();
        }

        void SaveMesh(Mesh m)
        {
            if (meshContainer == null)
            {
                meshContainer = m;
                AssetDatabase.CreateAsset(m, MeshAssetPath);
            }
            else AssetDatabase.AddObjectToAsset(m, meshContainer);
            meshCount++;
        }

        static void AddBoxCollider(Transform parent, Vector3 min, Vector3 max, string name = "Col")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = (min + max) * 0.5f;
            bc.size = max - min;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        static Vector2 Tile(float x, float y) => new(x, y);

        // ------------------------------------------------------------------ ground
        void BuildGround()
        {
            float x0 = MinX - 200, x1 = MaxX + 200, z0 = MinZ - 200, z1 = MaxZ + 200;
            // road surface: one big quad grid (subdivided so fog/vertex precision behave)
            var b = B(asphalt);
            int n = 16;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float ax = Mathf.Lerp(x0, x1, i / (float)n), bx = Mathf.Lerp(x0, x1, (i + 1) / (float)n);
                    float az = Mathf.Lerp(z0, z1, j / (float)n), bz = Mathf.Lerp(z0, z1, (j + 1) / (float)n);
                    b.Quad(new Vector3(ax, 0, az), new Vector3(bx, 0, az), new Vector3(bx, 0, bz), new Vector3(ax, 0, bz), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
            Flush("Ground", root);
            var groundCol = new GameObject("GroundCollider");
            groundCol.transform.SetParent(root, false);
            var bc = groundCol.AddComponent<BoxCollider>();
            bc.center = new Vector3((x0 + x1) / 2, -0.5f, (z0 + z1) / 2);
            bc.size = new Vector3(x1 - x0, 1, z1 - z0);
            groundCol.layer = 0;
        }

        // ------------------------------------------------------------------ blocks
        public static Rect BlockRect(int i, int j)
        {
            float bx0 = AvenueX[i] + AvenueW[i] * 0.5f, bx1 = AvenueX[i + 1] - AvenueW[i + 1] * 0.5f;
            float bz0 = StreetZ[j] + StreetW[j] * 0.5f, bz1 = StreetZ[j + 1] - StreetW[j + 1] * 0.5f;
            return Rect.MinMaxRect(bx0, bz0, bx1, bz1);
        }

        void BuildBlock(int i, int j)
        {
            var blockRoot = new GameObject($"Block_{i}_{j}").transform;
            blockRoot.SetParent(root, false);
            Rect r = BlockRect(i, j);
            // sidewalk slab with curb
            BuildSidewalk(r);
            Rect lot = Rect.MinMaxRect(r.xMin + Sidewalk, r.yMin + Sidewalk, r.xMax - Sidewalk, r.yMax - Sidewalk);

            bool garage = i == 2 && j == 3;
            if (garage) BuildGarage(lot, blockRoot);
            else
            {
                // split into lots
                float depth = lot.height;
                int rows = depth > 60 ? 2 : 1;
                for (int row = 0; row < rows; row++)
                {
                    float z0 = rows == 1 ? lot.yMin : (row == 0 ? lot.yMin : lot.yMin + depth * 0.5f);
                    float z1 = rows == 1 ? lot.yMax : (row == 0 ? lot.yMin + depth * 0.5f : lot.yMax);
                    float x = lot.xMin;
                    while (x < lot.xMax - 1)
                    {
                        float w = R(16, 34);
                        if (lot.xMax - (x + w) < 14) w = lot.xMax - x;
                        var faces = new bool[4]; // W, E, S, N touch street
                        faces[0] = Mathf.Abs(x - lot.xMin) < 0.1f;
                        faces[1] = Mathf.Abs(x + w - lot.xMax) < 0.1f;
                        faces[2] = row == 0;
                        faces[3] = row == rows - 1;
                        BuildBuilding(Rect.MinMaxRect(x, z0, x + w, z1), faces, blockRoot);
                        x += w;
                    }
                }
            }
            Flush($"Block_{i}_{j}_Geo", blockRoot);
        }

        void BuildSidewalk(Rect r)
        {
            var b = B(sidewalk);
            b.Box(new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0.22f, r.yMax), Tile(4, 4), 4); // top only
            var c = B(curb);
            // curb faces
            c.Box(new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0.22f, r.yMax), Tile(2, 0.3f), 1 | 2 | 16 | 32);
        }

        void BuildBuilding(Rect f, bool[] street, Transform parent)
        {
            float cx = f.center.x, cz = f.center.y;
            float dist = new Vector2(cx, cz).magnitude;
            float t = Mathf.Clamp01(dist / 520f);
            float h = Mathf.Lerp(120f, 26f, t) * R(0.45f, 1.35f);
            if (R() < 0.08f && t < 0.5f) h *= 1.7f; // occasional landmark tower
            h = Mathf.Max(16f, Mathf.Round((h - 6.6f) / 3.6f) * 3.6f + 6.6f);
            int fi = rnd.Next(facades.Length);
            var facade = facades[fi];
            var store = Pick(storefronts);
            // shift texture per building so neighbours sharing a facade don't line up
            B(facade).uvShift = new Vector2(rnd.Next(0, 4) * 0.25f, rnd.Next(0, 4) * 0.25f);
            B(store).uvShift = new Vector2(R(), 0);
            var concrete = R() < 0.5f ? concreteA : concreteB;

            // podium: storefronts on street faces, concrete elsewhere
            int storeFaces = (street[0] ? 2 : 0) | (street[1] ? 1 : 0) | (street[2] ? 32 : 0) | (street[3] ? 16 : 0);
            Vector3 pmin = new Vector3(f.xMin, 0, f.yMin), pmax = new Vector3(f.xMax, 6f, f.yMax);
            B(store).Box(pmin, pmax, Tile(24, 6), storeFaces);
            B(concrete).Box(pmin, pmax, Tile(8, 8), 63 & ~storeFaces & ~4 & ~8);
            // ledge
            B(concrete).Box(new Vector3(f.xMin - 0.4f, 6f, f.yMin - 0.4f), new Vector3(f.xMax + 0.4f, 6.6f, f.yMax + 0.4f), Tile(8, 1));

            float bodyTop = h;
            bool setback = h > 55 && R() < 0.65f;
            float sb = setback ? Mathf.Round((6.6f + (h - 6.6f) * R(0.45f, 0.7f)) / 3.6f) * 3.6f : h;
            B(facade).Box(new Vector3(f.xMin, 6.6f, f.yMin), new Vector3(f.xMax, setback ? sb : bodyTop, f.yMax), Tile(12, 14.4f), 63 & ~8 & ~4);
            AddBoxCollider(parent, new Vector3(f.xMin, 0, f.yMin), new Vector3(f.xMax, setback ? sb : bodyTop, f.yMax));
            Rect top = f;
            float topY = setback ? sb : bodyTop;
            Cap(f, topY, concrete);
            if (setback)
            {
                float inset = R(3f, 6f);
                Rect tw = Rect.MinMaxRect(f.xMin + inset, f.yMin + inset, f.xMax - inset, f.yMax - inset);
                if (tw.width > 8 && tw.height > 8)
                {
                    var tf = R() < 0.5f ? facade : Pick(facades);
                    B(tf).Box(new Vector3(tw.xMin, sb, tw.yMin), new Vector3(tw.xMax, h, tw.yMax), Tile(12, 14.4f), 63 & ~8 & ~4);
                    AddBoxCollider(parent, new Vector3(tw.xMin, sb, tw.yMin), new Vector3(tw.xMax, h, tw.yMax));
                    Cap(tw, h, concrete);
                    top = tw; topY = h;
                    Rooftop(Rect.MinMaxRect(f.xMin + 1, f.yMin + 1, f.xMin + inset - 0.5f, f.yMax - 1), sb, false);
                }
            }
            Rooftop(top, topY, true);
            FloorBands(f, 6.6f, setback ? sb : bodyTop, fi, concrete);

            // street dressing on each street-facing side
            for (int s = 0; s < 4; s++)
            {
                if (!street[s]) continue;
                FaceInfo(f, s, out Vector3 a, out Vector3 dir, out Vector3 outN, out float len);
                if (R() < 0.45f) Awning(a, dir, outN, len);
                if (R() < 0.38f && h > 20) VerticalSign(a, dir, outN, len, h);
                if (R() < 0.3f && len > 14) StoreSign(a, dir, outN, len);
                FacadeACs(a, dir, outN, len, h);
            }
        }

        /// <summary>Face s of rect: 0=W,1=E,2=S,3=N. a = start corner at ground, dir along face, outN outward.</summary>
        static void FaceInfo(Rect f, int s, out Vector3 a, out Vector3 dir, out Vector3 outN, out float len)
        {
            switch (s)
            {
                case 0: a = new Vector3(f.xMin, 0, f.yMax); dir = Vector3.back; outN = Vector3.left; len = f.height; break;
                case 1: a = new Vector3(f.xMax, 0, f.yMin); dir = Vector3.forward; outN = Vector3.right; len = f.height; break;
                case 2: a = new Vector3(f.xMin, 0, f.yMin); dir = Vector3.right; outN = Vector3.back; len = f.width; break;
                default: a = new Vector3(f.xMax, 0, f.yMax); dir = Vector3.left; outN = Vector3.forward; len = f.width; break;
            }
        }

        /// <summary>Thin protruding slab edges between floors: breaks up flat facades.</summary>
        void FloorBands(Rect f, float y0, float y1, int style, Material m)
        {
            if (style == 2) return; // curtain wall stays smooth
            int every = style == 0 ? 4 : 1;
            float depth = style == 3 ? 0.18f : 0.12f;
            int floor = 0;
            for (float y = y0 + 3.6f; y < y1 - 1f; y += 3.6f, floor++)
            {
                if (floor % every != every - 1) continue;
                B(m).Box(new Vector3(f.xMin - depth, y - 0.12f, f.yMin - depth), new Vector3(f.xMax + depth, y + 0.12f, f.yMax + depth), Tile(8, 1), 63 & ~8);
            }
            // corner pilasters
            if (style == 3 || style == 4)
                foreach (var cx in new[] { f.xMin, f.xMax })
                    foreach (var cz in new[] { f.yMin, f.yMax })
                        B(m).Box(new Vector3(cx - 0.35f, y0, cz - 0.35f), new Vector3(cx + 0.35f, y1, cz + 0.35f), Tile(2, 8));
        }

        void Cap(Rect f, float y, Material wall)
        {
            B(roof).Box(new Vector3(f.xMin, y - 0.1f, f.yMin), new Vector3(f.xMax, y, f.yMax), Tile(6, 6), 4);
            // parapet
            float p = 0.35f, ph = 1.1f;
            B(wall).Box(new Vector3(f.xMin - 0.2f, y, f.yMin - 0.2f), new Vector3(f.xMax + 0.2f, y + ph, f.yMin + p), Tile(8, 2));
            B(wall).Box(new Vector3(f.xMin - 0.2f, y, f.yMax - p), new Vector3(f.xMax + 0.2f, y + ph, f.yMax + 0.2f), Tile(8, 2));
            B(wall).Box(new Vector3(f.xMin - 0.2f, y, f.yMin), new Vector3(f.xMin + p, y + ph, f.yMax), Tile(8, 2));
            B(wall).Box(new Vector3(f.xMax - p, y, f.yMin), new Vector3(f.xMax + 0.2f, y + ph, f.yMax), Tile(8, 2));
        }

        void Rooftop(Rect f, float y, bool allowBig)
        {
            if (f.width < 4 || f.height < 4) return;
            int ac = rnd.Next(1, 5);
            for (int k = 0; k < ac; k++)
            {
                Vector3 c = new Vector3(R(f.xMin + 2, f.xMax - 2), y, R(f.yMin + 2, f.yMax - 2));
                Vector3 s = new Vector3(R(1.5f, 3.5f), R(1f, 2.2f), R(1.5f, 3f));
                B(metal).Box(c - new Vector3(s.x / 2, 0, s.z / 2), c + new Vector3(s.x / 2, s.y, s.z / 2), Tile(2, 2));
                B(darkMetal).Cylinder(c + new Vector3(0, s.y, 0), c + new Vector3(0, s.y + 0.15f, 0), Mathf.Min(s.x, s.z) * 0.35f, 10);
            }
            if (!allowBig) return;
            if (R() < 0.3f)
            {
                Vector3 c = new Vector3(R(f.xMin + 3, f.xMax - 3), y, R(f.yMin + 3, f.yMax - 3));
                for (int k = 0; k < 4; k++)
                {
                    Vector3 lp = c + new Vector3(k % 2 == 0 ? -1.5f : 1.5f, 0, k < 2 ? -1.5f : 1.5f);
                    B(darkMetal).Box(lp - new Vector3(0.15f, 0, 0.15f), lp + new Vector3(0.15f, 3, 0.15f), Tile(1, 1));
                }
                B(concreteB).Cylinder(c + Vector3.up * 3, c + Vector3.up * 7, 2.4f, 14);
                B(darkMetal).Cylinder(c + Vector3.up * 7, c + Vector3.up * 8.2f, 2.5f, 14, null, true, 0.2f);
            }
            if (R() < 0.3f && y > 40)
            {
                Vector3 c = new Vector3(R(f.xMin + 2, f.xMax - 2), y, R(f.yMin + 2, f.yMax - 2));
                float mh = R(8, 22);
                B(metal).Cylinder(c, c + Vector3.up * mh, 0.25f, 6, null, true, 0.08f);
                B(Pick(new[] { neons[5] })).BoxCentered(c + Vector3.up * mh, Vector3.one * 0.5f, Tile(1, 1));
            }
            if (R() < 0.28f && y < 60 && f.width > 12)
            {
                // billboard facing south or north depending on side
                bool south = R() < 0.5f;
                float bw = Mathf.Min(f.width - 2, R(14, 22)), bh = bw * 0.5f;
                float bz = south ? f.yMin + 1.5f : f.yMax - 1.5f;
                float bx = f.center.x;
                float baseY = y + 3;
                for (int k = -1; k <= 1; k += 2)
                    B(darkMetal).Box(new Vector3(bx + k * bw * 0.35f - 0.2f, y, bz - 0.2f), new Vector3(bx + k * bw * 0.35f + 0.2f, baseY, bz + 0.2f), Tile(1, 1));
                B(darkMetal).Box(new Vector3(bx - bw / 2 - 0.3f, baseY - 0.3f, bz - 0.3f), new Vector3(bx + bw / 2 + 0.3f, baseY + bh + 0.3f, bz + 0.3f), Tile(2, 2));
                var mat = Pick(billboards);
                float zf = south ? bz - 0.32f : bz + 0.32f;
                Vector3 p0 = new Vector3(bx - bw / 2, baseY, zf), p1 = new Vector3(bx + bw / 2, baseY, zf);
                if (south) B(mat).Quad(p0, p1, p1 + Vector3.up * bh, p0 + Vector3.up * bh, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                else B(mat).Quad(p1, p0, p0 + Vector3.up * bh, p1 + Vector3.up * bh, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                // floodlights
                for (int k = -1; k <= 1; k += 2)
                    B(lampHead).BoxCentered(new Vector3(bx + k * bw * 0.3f, baseY - 0.6f, zf + (south ? -1.2f : 1.2f)), new Vector3(0.8f, 0.3f, 0.4f), Tile(1, 1));
            }
        }

        void Awning(Vector3 a, Vector3 dir, Vector3 outN, float len)
        {
            float start = R(1, len * 0.4f), w = Mathf.Min(len - start - 1, R(5, 12));
            if (w < 3) return;
            Vector3 p = a + dir * start + Vector3.up * 4.4f;
            var m = R() < 0.5f ? awningRed : awningTeal;
            // slanted awning: top edge at wall, bottom edge 1.8m out and 0.8m lower
            Vector3 t0 = p, t1 = p + dir * w;
            Vector3 b0 = t0 + outN * 1.8f - Vector3.up * 0.8f, b1 = t1 + outN * 1.8f - Vector3.up * 0.8f;
            B(m).Quad(b0, b1, t1, t0, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            B(m).Quad(t0, t1, b1, b0, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            // under-awning light strip
            B(Pick(neons)).BoxCentered((b0 + b1) * 0.5f - Vector3.up * 0.05f, Abs(dir) * w + new Vector3(0.08f, 0.06f, 0.08f), Tile(1, 1));
        }

        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static readonly string[] VWords = { "HOTEL", "BAR", "RAMEN", "CLUB", "KAI", "NEON", "OKAMI", "VOLT", "24H", "PARTS", "ZERO", "DINER", "MECH", "SUSHI", "PACHI" };
        static readonly string[] HWords = { "KAIJU RAMEN", "NIGHT OWL", "BLUE LOTUS", "OVERCLOCK", "RUST BELT", "KOI CLUB", "GRIDLOCK", "SHOGUN BAR", "ION WASH", "MIDNIGHT OIL", "AFTERBURN", "SKYHOOK" };

        void VerticalSign(Vector3 a, Vector3 dir, Vector3 outN, float len, float h)
        {
            string word = Pick(VWords);
            int ci = rnd.Next(neonColors.Length);
            float glyph = 1.5f, step = 2.1f;
            float panelH = word.Length * step + 1.2f;
            float y0 = Mathf.Min(R(8f, 16f), h - panelH - 2);
            if (y0 < 7) return;
            float along = R(1.5f, Mathf.Max(1.6f, len - 1.5f));
            Vector3 basePos = a + dir * along + outN * 1.3f + Vector3.up * y0;
            // backing panel (perpendicular to wall)
            Vector3 size = Abs(outN) * 2.2f + Abs(dir) * 0.5f + Vector3.up * panelH;
            B(signBack).BoxCentered(basePos + Vector3.up * panelH * 0.5f, size, Tile(2, 2));
            B(neons[ci]).BoxCentered(basePos + outN * 1.12f + Vector3.up * panelH * 0.5f, Abs(outN) * 0.12f + Abs(dir) * 0.58f + Vector3.up * (panelH + 0.1f), Tile(1, 1));
            // letters on both faces of the panel, stacked top to bottom
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 faceN = dir * side; // panel faces along the street
                Quaternion rot = Quaternion.LookRotation(-faceN, Vector3.up);
                for (int k = 0; k < word.Length; k++)
                {
                    var lm = StrokeFont.TubeMesh(word[k].ToString(), glyph, 0.07f);
                    float lw = lm.bounds.size.x;
                    Vector3 pos = basePos + faceN * 0.32f + Vector3.up * (panelH - 1.0f - (k + 1) * step + 0.3f);
                    // center letter on panel width (panel width is along outN)
                    Vector3 right = rot * Vector3.right;
                    pos -= right * lw * 0.5f;
                    B(neons[ci]).Append(lm, Matrix4x4.TRS(pos, rot, Vector3.one));
                    Object.DestroyImmediate(lm);
                }
            }
            AddLight(basePos + outN * 1.5f + Vector3.up * panelH * 0.5f, neonColors[ci], 3.2f, 16f);
        }

        void StoreSign(Vector3 a, Vector3 dir, Vector3 outN, float len)
        {
            string word = Pick(HWords);
            int ci = rnd.Next(neonColors.Length);
            var lm = StrokeFont.TubeMesh(word, 1.0f, 0.06f);
            float w = lm.bounds.size.x;
            if (w > len - 2) { Object.DestroyImmediate(lm); return; }
            Quaternion rot = Quaternion.LookRotation(-outN, Vector3.up);
            Vector3 right = rot * Vector3.right;
            Vector3 center = a + dir * (len * 0.5f) + outN * 0.45f + Vector3.up * 6.9f;
            B(neons[ci]).Append(lm, Matrix4x4.TRS(center - right * w * 0.5f, rot, Vector3.one));
            Object.DestroyImmediate(lm);
            AddLight(center + outN * 2f, neonColors[ci], 2.2f, 12f);
        }

        void FacadeACs(Vector3 a, Vector3 dir, Vector3 outN, float len, float h)
        {
            int n = rnd.Next(0, 6);
            for (int k = 0; k < n; k++)
            {
                float y = 6.6f + Mathf.Floor(R(1, Mathf.Max(1, (h - 8) / 3.6f))) * 3.6f + 0.5f;
                if (y > h - 2) continue;
                Vector3 p = a + dir * R(1.5f, len - 1.5f) + Vector3.up * y + outN * 0.45f;
                B(metal).BoxCentered(p, Abs(dir) * 1.1f + Abs(outN) * 0.8f + Vector3.up * 0.8f, Tile(1, 1));
            }
        }

        void BuildGarage(Rect f, Transform parent)
        {
            float h = 16f;
            B(concreteB).Box(new Vector3(f.xMin, 0, f.yMin), new Vector3(f.xMax, h, f.yMax), Tile(8, 8), 63 & ~8);
            AddBoxCollider(parent, new Vector3(f.xMin, 0, f.yMin), new Vector3(f.xMax, h, f.yMax));
            Cap(f, h, concreteA);
            // roller door on the south face, lit from inside
            float cx = GarageDoor.x;
            var doorMat = Mats.Neon("GarageDoor", new Color(1f, 0.55f, 0.15f) * 1.2f);
            Vector3 d0 = new Vector3(cx - 11, 0.25f, f.yMin - 0.05f), d1 = new Vector3(cx + 11, 0.25f, f.yMin - 0.05f);
            B(doorMat).Quad(d0, d1, d1 + Vector3.up * 11, d0 + Vector3.up * 11, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            // hazard frame
            B(neons[2]).BoxCentered(new Vector3(cx, 11.4f, f.yMin - 0.3f), new Vector3(23, 0.4f, 0.4f), Tile(1, 1));
            B(neons[2]).BoxCentered(new Vector3(cx - 11.4f, 5.7f, f.yMin - 0.3f), new Vector3(0.4f, 11.4f, 0.4f), Tile(1, 1));
            B(neons[2]).BoxCentered(new Vector3(cx + 11.4f, 5.7f, f.yMin - 0.3f), new Vector3(0.4f, 11.4f, 0.4f), Tile(1, 1));
            var lm = StrokeFont.TubeMesh("RIG SHOP", 2.2f, 0.12f);
            float w = lm.bounds.size.x;
            B(neons[0]).Append(lm, Matrix4x4.TRS(new Vector3(cx - w * 0.5f, 12.6f, f.yMin - 0.4f), Quaternion.identity, Vector3.one));
            Object.DestroyImmediate(lm);
            AddLight(new Vector3(cx, 8, f.yMin - 6), new Color(1f, 0.6f, 0.25f), 6f, 26f);
            Rooftop(f, h, true);
        }

        // ------------------------------------------------------------------ border ring
        void BuildBorder()
        {
            var g = new GameObject("Border").transform;
            g.SetParent(root, false);
            float pad = 4;
            float w0 = AvenueX[0] - AvenueW[0] / 2 - Sidewalk, w1 = AvenueX[^1] + AvenueW[^1] / 2 + Sidewalk;
            float s0 = StreetZ[0] - StreetW[0] / 2 - Sidewalk, s1 = StreetZ[^1] + StreetW[^1] / 2 + Sidewalk;
            // sidewalks outside the outer roads
            BuildSidewalk(Rect.MinMaxRect(w0 - 60, s0 - 60, w0 + Sidewalk, s1 + 60));
            BuildSidewalk(Rect.MinMaxRect(w1 - Sidewalk, s0 - 60, w1 + 60, s1 + 60));
            BuildSidewalk(Rect.MinMaxRect(w0, s0 - 60, w1, s0 + Sidewalk));
            BuildSidewalk(Rect.MinMaxRect(w0, s1 - Sidewalk, w1, s1 + 60));
            // building walls facing inward
            void Row(Rect area, bool alongX, bool[] faces)
            {
                float start = alongX ? area.xMin : area.yMin, end = alongX ? area.xMax : area.yMax;
                float p = start;
                while (p < end - 1)
                {
                    float w = Mathf.Min(R(18, 36), end - p);
                    Rect f = alongX ? Rect.MinMaxRect(p, area.yMin, p + w, area.yMax) : Rect.MinMaxRect(area.xMin, p, area.xMax, p + w);
                    BuildBuilding(f, faces, g);
                    p += w;
                }
            }
            Row(Rect.MinMaxRect(w0 - 40, s0, w0, s1), false, new[] { false, true, false, false });
            Row(Rect.MinMaxRect(w1, s0, w1 + 40, s1), false, new[] { true, false, false, false });
            Row(Rect.MinMaxRect(w0 - 40, s0 - 40, w1 + 40, s0), true, new[] { false, false, false, true });
            Row(Rect.MinMaxRect(w0 - 40, s1, w1 + 40, s1 + 40), true, new[] { false, false, true, false });
            Flush("Border_Geo", g);
            // invisible limits
            AddBoxCollider(g, new Vector3(w0 - pad - 2, 0, s0 - 50), new Vector3(w0 - pad, 300, s1 + 50), "Limit");
            AddBoxCollider(g, new Vector3(w1 + pad, 0, s0 - 50), new Vector3(w1 + pad + 2, 300, s1 + 50), "Limit");
            AddBoxCollider(g, new Vector3(w0 - 50, 0, s0 - pad - 2), new Vector3(w1 + 50, 300, s0 - pad), "Limit");
            AddBoxCollider(g, new Vector3(w0 - 50, 0, s1 + pad), new Vector3(w1 + 50, 300, s1 + pad + 2), "Limit");
        }

        // ------------------------------------------------------------------ streets
        void BuildStreets()
        {
            var g = new GameObject("Streets").transform;
            g.SetParent(root, false);
            var lights = new GameObject("StreetLights").transform;
            lights.SetParent(root, false);
            lightParent = lights;

            // avenues (N-S)
            for (int i = 0; i < AvenueX.Length; i++)
            {
                float x = AvenueX[i], hw = AvenueW[i] * 0.5f;
                for (int j = 0; j < StreetZ.Length - 1; j++)
                {
                    float z0 = StreetZ[j] + StreetW[j] * 0.5f, z1 = StreetZ[j + 1] - StreetW[j + 1] * 0.5f;
                    Segment(new Vector3(x, 0, z0), new Vector3(x, 0, z1), hw, AvenueW[i] >= 22);
                }
            }
            for (int j = 0; j < StreetZ.Length; j++)
            {
                float z = StreetZ[j], hw = StreetW[j] * 0.5f;
                for (int i = 0; i < AvenueX.Length - 1; i++)
                {
                    float x0 = AvenueX[i] + AvenueW[i] * 0.5f, x1 = AvenueX[i + 1] - AvenueW[i + 1] * 0.5f;
                    Segment(new Vector3(x0, 0, z), new Vector3(x1, 0, z), hw, StreetW[j] >= 22);
                }
            }
            // intersections: crosswalks + traffic lights
            for (int i = 0; i < AvenueX.Length; i++)
                for (int j = 0; j < StreetZ.Length; j++)
                    Intersection(i, j);
            Flush("Streets_Geo", g);
        }

        Transform lightParent;
        int lampCount;

        void Segment(Vector3 a, Vector3 b, float hw, bool twoLanesEachWay)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir); // points to the right of travel
            float len = Vector3.Distance(a, b);
            // center line: double solid yellow on wide roads, dashed white on narrow
            if (twoLanesEachWay)
            {
                LineStrip(a, dir, len, right * 0.18f, 0.12f, 0);
                LineStrip(a, dir, len, -right * 0.18f, 0.12f, 0);
                LineStrip(a, dir, len, right * hw * 0.5f, 0.12f, 3f);
                LineStrip(a, dir, len, -right * hw * 0.5f, 0.12f, 3f);
            }
            else LineStrip(a, dir, len, Vector3.zero, 0.12f, 3f);
            // edge lines
            LineStrip(a, dir, len, right * (hw - 0.6f), 0.12f, 0);
            LineStrip(a, dir, len, -right * (hw - 0.6f), 0.12f, 0);

            // streetlights on both sidewalks, staggered
            float spacing = 34f;
            for (int side = -1; side <= 1; side += 2)
            {
                float offset = side < 0 ? spacing * 0.5f : spacing;
                for (float d = offset; d < len - 6; d += spacing)
                {
                    Vector3 basePos = a + dir * d + right * side * (hw + 1.2f);
                    Streetlight(basePos, -right * side);
                }
            }
            // parked cars along curbs
            for (int side = -1; side <= 1; side += 2)
            {
                float d = R(8, 20);
                while (d < len - 10)
                {
                    if (R() < 0.35f)
                    {
                        Vector3 p = a + dir * d + right * side * (hw - 1.4f);
                        Car(p, dir * (R() < 0.5f ? 1 : -1));
                    }
                    d += R(7, 16);
                }
            }
            // steam vents
            if (R() < 0.3f)
            {
                var sv = new GameObject("SteamVent");
                sv.transform.SetParent(lightParent, false);
                sv.transform.position = a + dir * R(10, len - 10) + right * R(-hw * 0.5f, hw * 0.5f);
                sv.AddComponent<SteamVentMarker>();
                B(darkMetal).Cylinder(sv.transform.position, sv.transform.position + Vector3.up * 0.03f, 0.6f, 12);
            }
            // occasional string lights crossing narrow streets
            if (hw < 10 && R() < 0.5f)
            {
                int n = rnd.Next(1, 3);
                for (int k = 0; k < n; k++)
                {
                    Vector3 c = a + dir * R(10, len - 10);
                    StringLights(c - right * (hw + Sidewalk), c + right * (hw + Sidewalk), R(7, 10));
                }
            }
        }

        void LineStrip(Vector3 a, Vector3 dir, float len, Vector3 off, float width, float dash)
        {
            Vector3 right = Vector3.Cross(Vector3.up, dir) * width * 0.5f;
            var b = B(laneLine);
            float y = 0.015f;
            if (dash <= 0)
            {
                Vector3 p0 = a + off + Vector3.up * y, p1 = a + off + dir * len + Vector3.up * y;
                b.Quad(p0 - right, p0 + right, p1 + right, p1 - right, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                return;
            }
            for (float d = 2; d < len - 2; d += dash * 2.5f)
            {
                Vector3 p0 = a + off + dir * d + Vector3.up * y, p1 = p0 + dir * dash;
                b.Quad(p0 - right, p0 + right, p1 + right, p1 - right, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }

        void Streetlight(Vector3 p, Vector3 towardRoad)
        {
            float hgt = 9f;
            B(darkMetal).Cylinder(p, p + Vector3.up * hgt, 0.16f, 8, null, true, 0.1f);
            B(darkMetal).Cylinder(p + Vector3.up * 0.0f, p + Vector3.up * 0.8f, 0.3f, 8);
            Vector3 armEnd = p + Vector3.up * hgt + towardRoad * 2.8f;
            B(darkMetal).Cylinder(p + Vector3.up * (hgt - 0.1f), armEnd, 0.08f, 6);
            Vector3 headPos = armEnd - Vector3.up * 0.15f;
            B(darkMetal).BoxCentered(headPos + Vector3.up * 0.1f, Abs(towardRoad) * 1.4f + Abs(Vector3.Cross(towardRoad, Vector3.up)) * 0.6f + Vector3.up * 0.25f, Tile(1, 1));
            B(lampHead).BoxCentered(headPos - Vector3.up * 0.06f, Abs(towardRoad) * 1.2f + Abs(Vector3.Cross(towardRoad, Vector3.up)) * 0.45f + Vector3.up * 0.06f, Tile(1, 1));
            // light shaft cone
            ConeShaft(headPos - Vector3.up * 0.1f, 0.5f, 4.2f, hgt - 0.3f);
            if (lampCount++ % 2 == 0) AddLight(headPos - Vector3.up * 0.6f, new Color(1f, 0.72f, 0.45f), 4.5f, 22f);
        }

        void ConeShaft(Vector3 top, float r0, float r1, float h)
        {
            var b = B(shaft);
            int sides = 14;
            int start = b.Count;
            for (int k = 0; k <= sides; k++)
            {
                float ang = k / (float)sides * Mathf.PI * 2;
                Vector3 o = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                b.v.Add(top + o * r0); b.n.Add((o + Vector3.up * 0.3f).normalized); b.uv.Add(new Vector2(k / (float)sides, 0)); b.col.Add(Color.white);
                b.v.Add(top + o * r1 - Vector3.up * h); b.n.Add((o + Vector3.up * 0.3f).normalized); b.uv.Add(new Vector2(k / (float)sides, 1)); b.col.Add(Color.white);
            }
            for (int k = 0; k < sides; k++)
            {
                int i = start + k * 2;
                b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 1);
                b.t.Add(i + 1); b.t.Add(i + 2); b.t.Add(i + 3);
            }
        }

        void AddLight(Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(lightParent ? lightParent : root, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
        }

        void Car(Vector3 p, Vector3 fwd)
        {
            var paint = Pick(carPaints);
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
            Matrix4x4 m = Matrix4x4.TRS(p, rot, Vector3.one);
            Vector3 L(Vector3 v) => m.MultiplyPoint3x4(v);
            // body
            B(paint).Box(new Vector3(-0.92f, 0.32f, -2.25f), new Vector3(0.92f, 0.95f, 2.25f), Tile(1, 1), 63, null, m);
            // cabin (slightly narrower, tapered via two boxes)
            B(glass).Box(new Vector3(-0.8f, 0.95f, -1.1f), new Vector3(0.8f, 1.45f, 0.9f), Tile(1, 1), 63, null, m);
            B(paint).Box(new Vector3(-0.82f, 1.45f, -0.95f), new Vector3(0.82f, 1.5f, 0.75f), Tile(1, 1), 63, null, m);
            // wheels
            foreach (var wx in new[] { -0.85f, 0.85f })
                foreach (var wz in new[] { -1.45f, 1.4f })
                    B(rubber).Cylinder(L(new Vector3(wx - 0.12f * Mathf.Sign(wx), 0.34f, wz)), L(new Vector3(wx + 0.12f * Mathf.Sign(wx), 0.34f, wz)), 0.34f, 10);
            // lights
            B(tail).Box(new Vector3(-0.85f, 0.7f, -2.27f), new Vector3(-0.45f, 0.82f, -2.24f), Tile(1, 1), 63, null, m);
            B(tail).Box(new Vector3(0.45f, 0.7f, -2.27f), new Vector3(0.85f, 0.82f, -2.24f), Tile(1, 1), 63, null, m);
            B(head).Box(new Vector3(-0.85f, 0.68f, 2.24f), new Vector3(-0.5f, 0.78f, 2.27f), Tile(1, 1), 63, null, m);
            B(head).Box(new Vector3(0.5f, 0.68f, 2.24f), new Vector3(0.85f, 0.78f, 2.27f), Tile(1, 1), 63, null, m);
            var col = new GameObject("CarCol");
            col.transform.SetParent(lightParent, false);
            col.transform.SetPositionAndRotation(p, rot);
            var bc = col.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.75f, 0);
            bc.size = new Vector3(1.9f, 1.5f, 4.6f);
            col.AddComponent<Prop>();
        }

        void StringLights(Vector3 a, Vector3 b, float y)
        {
            a.y = b.y = y;
            int segs = 16;
            Vector3 prev = a;
            var mat = Pick(new[] { neons[2], neons[6], neons[0] });
            for (int k = 1; k <= segs; k++)
            {
                float t = k / (float)segs;
                Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.4f;
                B(darkMetal).Cylinder(prev, p, 0.025f, 4, null, false);
                if (k < segs) B(mat).BoxCentered(p - Vector3.up * 0.15f, Vector3.one * 0.2f, Tile(1, 1));
                prev = p;
            }
        }

        void Intersection(int i, int j)
        {
            float x = AvenueX[i], z = StreetZ[j];
            float hw = AvenueW[i] * 0.5f, hs = StreetW[j] * 0.5f;
            var b = B(laneLine);
            // zebra crossings on all four arms
            for (int s = 0; s < 4; s++)
            {
                bool ns = s < 2; // crossing the avenue (north/south arms)
                float sign = s % 2 == 0 ? 1 : -1;
                if (ns)
                {
                    float zc = z + sign * (hs + 2.2f);
                    for (float xx = x - hw + 1; xx < x + hw - 1; xx += 1.6f)
                        b.Quad(new Vector3(xx, 0.016f, zc - 1.5f), new Vector3(xx + 0.8f, 0.016f, zc - 1.5f), new Vector3(xx + 0.8f, 0.016f, zc + 1.5f), new Vector3(xx, 0.016f, zc + 1.5f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
                else
                {
                    float xc = x + sign * (hw + 2.2f);
                    for (float zz = z - hs + 1; zz < z + hs - 1; zz += 1.6f)
                        b.Quad(new Vector3(xc - 1.5f, 0.016f, zz + 0.8f), new Vector3(xc + 1.5f, 0.016f, zz + 0.8f), new Vector3(xc + 1.5f, 0.016f, zz), new Vector3(xc - 1.5f, 0.016f, zz), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
            }
            // traffic signal on one corner
            Vector3 pole = new Vector3(x + hw + 1.5f, 0, z - hs - 1.5f);
            B(darkMetal).Cylinder(pole, pole + Vector3.up * 7.5f, 0.18f, 8);
            Vector3 armEnd = pole + Vector3.up * 7.2f + Vector3.left * (hw * 0.8f);
            B(darkMetal).Cylinder(pole + Vector3.up * 7.2f, armEnd, 0.1f, 6);
            B(darkMetal).BoxCentered(armEnd + Vector3.down * 0.9f, new Vector3(0.6f, 1.8f, 0.5f), Tile(1, 1));
            bool green = (i + j) % 2 == 0;
            B(green ? neons[4] : neons[5]).BoxCentered(armEnd + Vector3.down * (green ? 1.45f : 0.35f) + Vector3.back * 0.27f, new Vector3(0.35f, 0.35f, 0.05f), Tile(1, 1));
            B(neons[2]).BoxCentered(armEnd + Vector3.down * 0.9f + Vector3.back * 0.27f, new Vector3(0.35f, 0.35f, 0.05f) * 0.6f, Tile(1, 1));
        }
    }
}
