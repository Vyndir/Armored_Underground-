using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static AU.CityLayout;

namespace AU.EditorTools
{
    /// <summary>
    /// Generates the bay city from CityLayout: terrain with roads cut in, the bay,
    /// districts of buildings, the Central Bridge, causeway, docks, ships, the two
    /// arenas, the flight circuit, lighthouses, trees and a distant mountain ring.
    /// </summary>
    public partial class CityBuilder
    {
        public const string TerrainPath = "Assets/AU/Generated/Terrain.asset";
        const int HeightRes = 1025;
        const float MinY = SeaFloor - 4f, MaxY = 740f;

        float[,] hgt;     // final ground height per heightmap sample [z, x]
        float[,] roadW;   // how strongly a sample was flattened for a road
        float[] nodeH;    // ground height at every road node
        NodeInfo[] ninfo;
        Transform worldRoot;
        readonly List<TreeInstance> trees = new();
        readonly List<Vector3> treeWorld = new();
        readonly List<int> treeProto = new();
        GameObject treeBroad, treeConifer;

        Material water, backdrop, stone, stoneDark, domeMat, roofTile, hangarWall, hangarRoof, warehouseWall, girder, cableMat, crowdMat, archFacade,
            fieldSand, fieldFloor, bannerMat, pylonMat, runwayLight, beacon, ledBlue, quayMat, craneMat, hullDark, hullRed, shipWhite, lighthouseRed,
            lantern, laneYellow, paintWhite, interiorGlow, bark, leaves, leavesDark, shipGlass;
        Material[] posters, screens, containerMats, dayFacades;
        int[] dayKinds;
        readonly Dictionary<string, Material> hangarDoors = new();

        struct NodeInfo { public int e, w, n, s; public float hwX, hwZ; public bool cross; }

        class BStyle
        {
            public Material[] facades; public int[] kinds;
            public System.Func<float, float, float, float> height; // (x, z, rand01) -> meters
            public float store, signs, awnings, pitched, setback, acs = 1f, lotMin = 16, lotMax = 34, gap;
            public Material trim;
        }

        // ------------------------------------------------------------------ entry
        void BuildWorld()
        {
            worldRoot = root;
            var lights = new GameObject("StreetLights").transform;
            lights.SetParent(root, false);
            lightParent = lights;
            CreateWorldMaterials();
            EditorUtility.DisplayProgressBar("AU", "Terrain heights", 0.05f);
            ComputeHeights();
            AnalyzeNodes();
            EditorUtility.DisplayProgressBar("AU", "Roads", 0.2f);
            BuildRoads();
            BuildElevated();
            EditorUtility.DisplayProgressBar("AU", "Districts", 0.35f);
            BuildDistricts();
            EditorUtility.DisplayProgressBar("AU", "Landmarks", 0.65f);
            CentralBridge();
            Waterfront();
            GroundArena(Venues[3].pos);
            ChampionshipArena(Venues[5].pos);
            FlightCircuit();
            Lighthouse(new Vector2(60, -640), 24);
            Lighthouse(new Vector2(2080, -820), 26);
            Ships();
            NatureTrees();
            EditorUtility.DisplayProgressBar("AU", "Terrain", 0.8f);
            BuildTerrain();
            BuildWater();
            Backdrop();
            Bounds();
            WorldMap();
            EditorUtility.ClearProgressBar();
            Debug.Log($"[AU] World: {Nodes.Count} nodes, {Edges.Count} roads, {trees.Count} trees, {meshCount} meshes");
        }

        // ------------------------------------------------------------------ materials
        void CreateWorldMaterials()
        {
            // the kit's materials were tuned for night; lift the plain surfaces for daylight
            concreteA.SetColor("_BaseColor", new Color(3.1f, 3.0f, 2.85f));
            concreteB.SetColor("_BaseColor", new Color(2.5f, 2.55f, 2.7f));
            curb.SetColor("_BaseColor", new Color(2.0f, 2.0f, 2.0f));
            sidewalk.SetColor("_BaseColor", new Color(2.3f, 2.25f, 2.2f));
            roof.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.21f));

            water = Mats.Custom("Water", Shader.Find("AU/Water"));
            water.SetTexture("_WaveMap", WorldTextures.WaterNormal());
            backdrop = Mats.LitMat("Backdrop", new Color(0.36f, 0.42f, 0.5f), 0.05f, 0);
            var (sa, sn) = WorldTextures.Stone("StoneLight", new Color(0.86f, 0.82f, 0.74f), 401);
            stone = Mats.LitMat("StoneLight", Color.white, 0.22f, 0, sa, sn);
            var (sda, sdn) = WorldTextures.Stone("StoneDark", new Color(0.46f, 0.45f, 0.43f), 402);
            stoneDark = Mats.LitMat("StoneDark", Color.white, 0.2f, 0, sda, sdn);
            domeMat = Mats.LitMat("Dome", new Color(0.32f, 0.47f, 0.58f), 0.6f, 0.65f);
            var (ra, rn) = WorldTextures.RoofTiles();
            roofTile = Mats.LitMat("RoofTiles", Color.white, 0.25f, 0, ra, rn);
            var (ha, hn) = WorldTextures.Corrugated("HangarWall", new Color(0.66f, 0.64f, 0.6f), 411);
            hangarWall = Mats.LitMat("HangarWall", Color.white, 0.38f, 0.35f, ha, hn);
            var (hra, hrn) = WorldTextures.Corrugated("HangarRoof", new Color(0.45f, 0.47f, 0.5f), 412);
            hangarRoof = Mats.LitMat("HangarRoof", Color.white, 0.5f, 0.6f, hra, hrn);
            var (wa, wnn) = WorldTextures.Corrugated("WarehouseWall", new Color(0.58f, 0.5f, 0.4f), 413);
            warehouseWall = Mats.LitMat("WarehouseWall", Color.white, 0.3f, 0.15f, wa, wnn);
            girder = Mats.LitMat("Girder", new Color(0.6f, 0.56f, 0.5f), 0.35f, 0.4f);
            cableMat = Mats.LitMat("Cable", new Color(0.32f, 0.3f, 0.28f), 0.45f, 0.7f);
            crowdMat = Mats.LitMat("Crowd", Color.white, 0.1f, 0, WorldTextures.Crowd());
            var (afa, afe) = WorldTextures.ArchFacade();
            archFacade = Mats.LitMat("ArchFacade", Color.white, 0.2f, 0, afa, null, afe, Color.white * 1.2f);
            var (da, dn) = WorldTextures.Ground(WorldTextures.GroundKind.Dirt);
            fieldSand = Mats.LitMat("ArenaSand", new Color(1.5f, 1.35f, 1.15f), 0.12f, 0, da, dn);
            var (fa, fn) = TextureGen.Concrete(5);
            fieldFloor = Mats.LitMat("ArenaFloor", new Color(2.6f, 2.65f, 2.8f), 0.45f, 0.1f, fa, fn);
            quayMat = Mats.LitMat("Quay", new Color(2.4f, 2.35f, 2.25f), 0.3f, 0, fa, fn);
            bannerMat = Mats.LitMat("LeagueBanner", Color.white, 0.25f, 0, WorldTextures.Banner());
            pylonMat = Mats.LitMat("Pylon", Color.white, 0.55f, 0, WorldTextures.PylonStripes());
            runwayLight = Mats.Neon("RunwayLight", new Color(1f, 0.85f, 0.5f) * 4f);
            beacon = Mats.Neon("BeaconRed", new Color(1f, 0.1f, 0.05f) * 6f, null, 0.6f, 3f);
            ledBlue = Mats.Neon("LedBand", new Color(0.25f, 0.55f, 1f) * 4f);
            craneMat = Mats.LitMat("CraneRed", new Color(0.72f, 0.22f, 0.1f), 0.4f, 0.3f);
            hullDark = Mats.LitMat("HullDark", new Color(0.12f, 0.15f, 0.2f), 0.45f, 0.3f);
            hullRed = Mats.LitMat("HullRed", new Color(0.42f, 0.09f, 0.07f), 0.45f, 0.3f);
            shipWhite = Mats.LitMat("ShipWhite", new Color(0.86f, 0.87f, 0.88f), 0.5f, 0.1f);
            shipGlass = Mats.LitMat("ShipGlass", new Color(0.05f, 0.07f, 0.1f), 0.9f, 0.4f, null, null, null, new Color(1f, 0.8f, 0.5f) * 0.6f);
            lighthouseRed = Mats.LitMat("LighthouseRed", new Color(0.62f, 0.08f, 0.06f), 0.45f, 0.1f);
            lantern = Mats.Neon("LampLantern", new Color(1f, 0.9f, 0.7f) * 9f);
            laneYellow = Mats.LitMat("LaneYellow", new Color(0.85f, 0.65f, 0.12f), 0.5f, 0f);
            laneLine.SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.84f));
            paintWhite = Mats.LitMat("PaintWhite", new Color(0.9f, 0.88f, 0.84f), 0.3f, 0f);
            interiorGlow = Mats.Neon("WindowHangarInterior", new Color(1f, 0.72f, 0.42f) * 1.6f);
            bark = Mats.LitMat("Bark", new Color(0.22f, 0.16f, 0.11f), 0.1f, 0);
            leaves = Mats.LitMat("Leaves", new Color(0.2f, 0.32f, 0.11f), 0.12f, 0);
            leavesDark = Mats.LitMat("LeavesConifer", new Color(0.1f, 0.19f, 0.11f), 0.1f, 0);
            posters = new[]
            {
                Mats.LitMat("Poster0", Color.white, 0.25f, 0, WorldTextures.Poster("Poster0", "CHAMPIONSHIP WEEK", "IRON ASCENDANT", 1)),
                Mats.LitMat("Poster1", Color.white, 0.25f, 0, WorldTextures.Poster("Poster1", "MECH GRAND FINALS", "CHAMPIONSHIP ARENA", 2)),
                Mats.LitMat("Poster2", Color.white, 0.25f, 0, WorldTextures.Poster("Poster2", "IRON ASCENDANT", "CHAMPIONSHIP", 3)),
            };
            screens = new[]
            {
                Mats.Neon("Screen0", Color.white * 1.6f, WorldTextures.Screen("Screen0", "MECH GRAND", "FINALS")),
                Mats.Neon("Screen1", Color.white * 1.6f, WorldTextures.Screen("Screen1", "IRON", "ASCENDANT")),
            };
            var cc = new[] { new Color(0.6f, 0.15f, 0.1f), new Color(0.1f, 0.3f, 0.55f), new Color(0.15f, 0.4f, 0.25f), new Color(0.75f, 0.55f, 0.15f), new Color(0.55f, 0.56f, 0.58f), new Color(0.25f, 0.2f, 0.3f) };
            containerMats = new Material[cc.Length];
            for (int i = 0; i < cc.Length; i++)
            {
                var (ca, cn) = WorldTextures.Corrugated($"Container{i}", cc[i] * 1.3f, 420 + i);
                containerMats[i] = Mats.LitMat($"Container{i}", Color.white, 0.35f, 0.3f, ca, cn);
            }

            // daylight facades: stone, brick, office, glass; windows light up at night
            var defs = new (string name, int floors, int bays, float glass, Color frame, float lit, Color cool, bool curtain, Color? tint, int kind)[]
            {
                ("FacadeDayStone", 4, 4, 0.55f, new Color(0.8f, 0.76f, 0.68f), 0.5f, new Color(0.7f, 0.85f, 1f), false, new Color(0.09f, 0.11f, 0.13f), 4),
                ("FacadeDayBrick", 4, 3, 0.5f, new Color(0.56f, 0.29f, 0.21f), 0.55f, new Color(1f, 0.75f, 0.5f), false, new Color(0.08f, 0.09f, 0.1f), 3),
                ("FacadeDayOffice", 4, 4, 0.8f, new Color(0.66f, 0.68f, 0.7f), 0.45f, new Color(0.65f, 0.85f, 1f), false, new Color(0.12f, 0.16f, 0.2f), 0),
                ("FacadeDayGlass", 4, 6, 1f, new Color(0.3f, 0.36f, 0.42f), 0.35f, new Color(0.55f, 0.8f, 1f), true, new Color(0.2f, 0.3f, 0.4f), 2),
                ("FacadeDayGold", 4, 6, 1f, new Color(0.38f, 0.34f, 0.28f), 0.35f, new Color(1f, 0.85f, 0.6f), true, new Color(0.3f, 0.27f, 0.2f), 2),
                ("FacadeDayApart", 4, 4, 0.45f, new Color(0.86f, 0.76f, 0.58f), 0.5f, new Color(1f, 0.6f, 0.85f), false, new Color(0.09f, 0.1f, 0.12f), 1),
                ("FacadeDayWhite", 4, 4, 0.4f, new Color(0.88f, 0.87f, 0.84f), 0.45f, new Color(1f, 0.85f, 0.6f), false, new Color(0.1f, 0.11f, 0.13f), 1),
            };
            dayFacades = new Material[defs.Length];
            dayKinds = new int[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                var (alb, emi) = TextureGen.Facade(d.name, 700 + i, d.floors, d.bays, d.glass, d.frame, d.lit, d.cool, d.curtain, d.tint);
                dayFacades[i] = Mats.LitMat(d.name, Color.white, d.curtain ? 0.88f : 0.4f, d.curtain ? 0.55f : 0.05f, alb, null, emi, Color.white * 2.0f);
                dayKinds[i] = d.kind;
            }
        }

        Material DayFacade(string n) { for (int i = 0; i < dayFacades.Length; i++) if (dayFacades[i].name == n) return dayFacades[i]; return dayFacades[0]; }
        int KindOf(Material m) { for (int i = 0; i < dayFacades.Length; i++) if (dayFacades[i] == m) return dayKinds[i]; return 4; }

        BStyle Style(params string[] names)
        {
            var s = new BStyle { facades = new Material[names.Length], kinds = new int[names.Length] };
            for (int i = 0; i < names.Length; i++) { s.facades[i] = DayFacade(names[i]); s.kinds[i] = KindOf(s.facades[i]); }
            return s;
        }

        // ------------------------------------------------------------------ heights
        static float GX(float x) => (x + Half) / (2 * Half) * (HeightRes - 1);
        static float WX(int i) => -Half + i * (2 * Half) / (HeightRes - 1);
        static float S01(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        float H(float x, float z)
        {
            float gx = Mathf.Clamp(GX(x), 0, HeightRes - 1.001f), gz = Mathf.Clamp(GX(z), 0, HeightRes - 1.001f);
            int i = (int)gx, j = (int)gz;
            float fx = gx - i, fz = gz - j;
            return Mathf.Lerp(Mathf.Lerp(hgt[j, i], hgt[j, i + 1], fx), Mathf.Lerp(hgt[j + 1, i], hgt[j + 1, i + 1], fx), fz);
        }

        float MinGround(Rect f)
        {
            float m = Mathf.Min(Mathf.Min(H(f.xMin, f.yMin), H(f.xMax, f.yMin)), Mathf.Min(H(f.xMin, f.yMax), H(f.xMax, f.yMax)));
            return Mathf.Min(m, H(f.center.x, f.center.y));
        }

        void ComputeHeights()
        {
            int n = HeightRes;
            hgt = new float[n, n];
            roadW = new float[n, n];
            var tgt = new float[n, n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    hgt[j, i] = RawHeight(WX(i), WX(j));

            nodeH = new float[Nodes.Count];
            for (int k = 0; k < Nodes.Count; k++)
            {
                var p = Nodes[k].p;
                float s = RawHeight(p.x, p.y) * 2 + RawHeight(p.x + 10, p.y) + RawHeight(p.x - 10, p.y) + RawHeight(p.x, p.y + 10) + RawHeight(p.x, p.y - 10);
                nodeH[k] = Nodes[k].Elevated ? Nodes[k].y : Mathf.Max(s / 6f, WaterLevel + 1.5f);
            }

            float cell = 2 * Half / (n - 1);
            foreach (var e in Edges)
            {
                if (IsElevated(e)) continue;
                Vector2 a = Nodes[e.a].p, b = Nodes[e.b].p, ab = b - a;
                float ya = nodeH[e.a], yb = nodeH[e.b];
                float core = e.w * 0.5f + Sidewalk + 3f, reach = core + 26f;
                int i0 = Mathf.Max(0, (int)GX(Mathf.Min(a.x, b.x) - reach)), i1 = Mathf.Min(n - 1, (int)GX(Mathf.Max(a.x, b.x) + reach) + 1);
                int j0 = Mathf.Max(0, (int)GX(Mathf.Min(a.y, b.y) - reach)), j1 = Mathf.Min(n - 1, (int)GX(Mathf.Max(a.y, b.y) + reach) + 1);
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        var p = new Vector2(WX(i), WX(j));
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                        float d = (a + ab * t - p).magnitude;
                        if (d > reach) continue;
                        float w = d <= core ? 1f : 1f - S01((d - core) / (reach - core));
                        float y = Mathf.Lerp(ya, yb, t);
                        if (w > roadW[j, i] + 0.001f) { roadW[j, i] = w; tgt[j, i] = y; }
                        else if (Mathf.Abs(w - roadW[j, i]) <= 0.001f) tgt[j, i] = (tgt[j, i] + y) * 0.5f;
                    }
            }
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    if (roadW[j, i] > 0) hgt[j, i] = Mathf.Lerp(hgt[j, i], tgt[j, i], roadW[j, i]);
            _ = cell;
        }

        // ------------------------------------------------------------------ road network
        static int DirOf(Vector2 d) => Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? 0 : 1) : (d.y > 0 ? 2 : 3); // E W N S
        static Vector3 V3(Vector2 p, float y) => new(p.x, y, p.y);

        void AnalyzeNodes()
        {
            ninfo = new NodeInfo[Nodes.Count];
            for (int k = 0; k < Nodes.Count; k++)
            {
                var ni = new NodeInfo { e = -1, w = -1, n = -1, s = -1 };
                foreach (int ei in Nodes[k].edges)
                {
                    var e = Edges[ei];
                    int o = e.a == k ? e.b : e.a;
                    int dir = DirOf(Nodes[o].p - Nodes[k].p);
                    if (dir == 0) ni.e = ei; else if (dir == 1) ni.w = ei; else if (dir == 2) ni.n = ei; else ni.s = ei;
                    if (dir < 2) ni.hwZ = Mathf.Max(ni.hwZ, e.w * 0.5f); else ni.hwX = Mathf.Max(ni.hwX, e.w * 0.5f);
                }
                ni.cross = (ni.e >= 0 || ni.w >= 0) && (ni.n >= 0 || ni.s >= 0);
                ninfo[k] = ni;
            }
        }

        float Trim(int node, int dir, bool walk)
        {
            var ni = ninfo[node];
            if (!ni.cross) return 0;
            float hw = dir < 2 ? ni.hwX : ni.hwZ;
            return walk ? hw + Sidewalk : hw;
        }

        float EdgeY(Edge e, float t) => IsElevated(e) ? ElevatedY(e, t, nodeH[e.a], nodeH[e.b]) : Mathf.Lerp(nodeH[e.a], nodeH[e.b], t);

        /// <summary>Quad that faces 'outward' whatever order the corners come in; UVs from its size.</summary>
        void QuadAuto(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, float tileU = 4f, float tileV = 4f, Vector2 uv0 = default)
        {
            if (Vector3.Dot(Vector3.Cross(d - a, b - a), outward) < 0) { (a, b) = (b, a); (c, d) = (d, c); }
            float w = Vector3.Distance(a, b) / tileU, h = Vector3.Distance(a, d) / tileV;
            B(m).Quad(a, b, c, d, uv0, uv0 + new Vector2(w, 0), uv0 + new Vector2(w, h), uv0 + new Vector2(0, h));
        }

        void TriAuto(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, float tile = 4f)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) (b, c) = (c, b);
            B(m).Tri(a, b, c, new Vector2(a.x + a.z, a.y) / tile, new Vector2(b.x + b.z, b.y) / tile, new Vector2(c.x + c.z, c.y) / tile);
        }

        /// <summary>A strip along an edge between lateral offsets o0..o1 (meters right of the centerline).</summary>
        void Ribbon(Material m, Edge e, float s0, float s1, float o0, float o1, float lift, float step, float tile = 4f)
        {
            if (s1 - s0 < 0.05f) return;
            Vector2 a = Nodes[e.a].p, b = Nodes[e.b].p;
            float len = Vector2.Distance(a, b);
            Vector2 d = (b - a) / len;
            Vector3 right = new(d.y, 0, -d.x);
            int segs = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / step));
            for (int k = 0; k < segs; k++)
            {
                float sa = Mathf.Lerp(s0, s1, k / (float)segs), sb = Mathf.Lerp(s0, s1, (k + 1) / (float)segs);
                Vector3 pa = V3(a + d * sa, EdgeY(e, sa / len) + lift), pb = V3(a + d * sb, EdgeY(e, sb / len) + lift);
                var uv0 = new Vector2(o0 / tile, sa / tile);
                QuadAuto(m, pa + right * o0, pa + right * o1, pb + right * o1, pb + right * o0, Vector3.up, tile, tile, uv0);
            }
        }

        /// <summary>A vertical strip along an edge at offset o, from lift y0 to y1, facing 'side' (+1 right, -1 left).</summary>
        void Wall(Material m, Edge e, float s0, float s1, float o, float y0, float y1, float side, float step, float tile = 4f)
        {
            if (s1 - s0 < 0.05f) return;
            Vector2 a = Nodes[e.a].p, b = Nodes[e.b].p;
            float len = Vector2.Distance(a, b);
            Vector2 d = (b - a) / len;
            Vector3 right = new(d.y, 0, -d.x);
            int segs = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / step));
            for (int k = 0; k < segs; k++)
            {
                float sa = Mathf.Lerp(s0, s1, k / (float)segs), sb = Mathf.Lerp(s0, s1, (k + 1) / (float)segs);
                Vector3 pa = V3(a + d * sa, EdgeY(e, sa / len)) + right * o, pb = V3(a + d * sb, EdgeY(e, sb / len)) + right * o;
                QuadAuto(m, pa + Vector3.up * y0, pb + Vector3.up * y0, pb + Vector3.up * y1, pa + Vector3.up * y1, right * side, tile, tile, new Vector2(sa / tile, y0 / tile));
            }
        }

        void Dashes(Material m, Edge e, float s0, float s1, float o, float width, float dash, float gap)
        {
            if (dash <= 0) { Ribbon(m, e, s0, s1, o - width / 2, o + width / 2, 0.16f, 20f); return; }
            for (float s = s0 + 2; s < s1 - 2; s += dash + gap)
                Ribbon(m, e, s, Mathf.Min(s + dash, s1 - 2), o - width / 2, o + width / 2, 0.16f, 20f);
        }

        void BuildRoads()
        {
            // chunk roads into tiles so they cull in pieces
            var tiles = new Dictionary<Vector2Int, List<int>>();
            for (int i = 0; i < Edges.Count; i++)
            {
                if (IsElevated(Edges[i])) continue;
                var mid = (Nodes[Edges[i].a].p + Nodes[Edges[i].b].p) * 0.5f;
                var key = new Vector2Int(Mathf.FloorToInt(mid.x / 700f), Mathf.FloorToInt(mid.y / 700f));
                if (!tiles.TryGetValue(key, out var l)) tiles[key] = l = new List<int>();
                l.Add(i);
            }
            var roads = new GameObject("Roads").transform;
            roads.SetParent(root, false);
            var doneNodes = new HashSet<int>();
            foreach (var kv in tiles)
            {
                foreach (int ei in kv.Value)
                {
                    RoadEdge(Edges[ei]);
                    foreach (int nk in new[] { Edges[ei].a, Edges[ei].b })
                        if (doneNodes.Add(nk)) Junction(nk);
                }
                Flush($"Roads_{kv.Key.x}_{kv.Key.y}", roads);
            }
        }

        void RoadEdge(Edge e)
        {
            Vector2 a = Nodes[e.a].p, b = Nodes[e.b].p;
            float len = Vector2.Distance(a, b);
            Vector2 d = (b - a) / len;
            Vector3 right = new(d.y, 0, -d.x);
            int da = DirOf(b - a), db = DirOf(a - b);
            float ta = Trim(e.a, da, false), tb = Trim(e.b, db, false);
            float hw = e.w * 0.5f;
            Ribbon(asphalt, e, ta, len - tb, -hw, hw, 0.12f, 20f, 8f);
            if (e.kind == RoadKind.Runway) { Runway(e, len); return; }

            // markings
            bool wide = e.w >= 22;
            if (wide)
            {
                Dashes(laneYellow, e, ta, len - tb, 0.2f, 0.14f, 0, 0);
                Dashes(laneYellow, e, ta, len - tb, -0.2f, 0.14f, 0, 0);
                Dashes(laneLine, e, ta, len - tb, hw * 0.5f, 0.14f, 3f, 6f);
                Dashes(laneLine, e, ta, len - tb, -hw * 0.5f, 0.14f, 3f, 6f);
            }
            else Dashes(laneLine, e, ta, len - tb, 0, 0.14f, 3f, 6f);
            Dashes(laneLine, e, ta, len - tb, hw - 0.6f, 0.14f, 0, 0);
            Dashes(laneLine, e, ta, len - tb, -hw + 0.6f, 0.14f, 0, 0);
            // crosswalks where the road meets a crossing street
            for (int end = 0; end < 2; end++)
            {
                int node = end == 0 ? e.a : e.b;
                if (!ninfo[node].cross) continue;
                float s = end == 0 ? ta + 2.4f : len - tb - 2.4f;
                for (float o = -hw + 1f; o < hw - 1f; o += 1.6f)
                    Ribbon(laneLine, e, s - 1.5f, s + 1.5f, o, o + 0.8f, 0.16f, 5f);
            }

            // sidewalks with curbs
            float wa = Trim(e.a, da, true), wb = Trim(e.b, db, true);
            for (int side = -1; side <= 1; side += 2)
            {
                float o0 = side > 0 ? hw : -hw - Sidewalk, o1 = side > 0 ? hw + Sidewalk : -hw;
                Ribbon(sidewalk, e, wa, len - wb, o0, o1, 0.34f, 20f);
                Wall(curb, e, wa, len - wb, side * hw, 0.08f, 0.34f, -side, 20f, 2f);
                Wall(curb, e, wa, len - wb, side * (hw + Sidewalk), -0.8f, 0.34f, side, 20f, 2f);
            }

            // street furniture
            bool lamps = e.district != District.Airfield;
            bool banners = e.kind >= RoadKind.Avenue && e.district != District.Island;
            bool parked = e.kind == RoadKind.Street && e.district != District.Airfield;
            bool streetTrees = e.district == District.OldTown || e.district == District.Downtown || e.district == District.Island;
            for (int side = -1; side <= 1; side += 2)
            {
                if (lamps)
                {
                    float off = side < 0 ? 18f : 36f;
                    int k = 0;
                    for (float s = wa + off * 0.5f; s < len - wb - 4; s += 36f, k++)
                    {
                        float y = EdgeY(e, s / len) + 0.34f;
                        Vector3 bp = V3(a + d * s, y) + right * side * (hw + 1.2f);
                        Streetlight(bp, -right * side);
                        if (banners && k % 2 == 0) PoleBanner(bp, V3(d, 0), -right * side);
                    }
                }
                if (streetTrees)
                    for (float s = wa + 9f; s < len - wb - 6; s += 18f)
                        if (R() < 0.7f) AddTree(V3(a + d * s, 0) + right * side * (hw + Sidewalk * 0.62f), 0, R(0.8f, 1.15f));
                if (parked)
                {
                    float s = wa + R(8, 20);
                    while (s < len - wb - 10)
                    {
                        if (R() < 0.28f) Car(V3(a + d * s, EdgeY(e, s / len) + 0.12f) + right * side * (hw - 1.4f), V3(d, 0) * (R() < 0.5f ? 1 : -1));
                        s += R(7, 16);
                    }
                }
            }
        }

        void PoleBanner(Vector3 pole, Vector3 along, Vector3 towardRoad)
        {
            Vector3 c = pole + towardRoad * 0.75f + Vector3.up * 6.1f;
            Vector3 side = towardRoad * 0.62f;
            Vector3 up = Vector3.up * 1.8f;
            B(bannerMat).Quad(c - side - up, c + side - up, c + side + up, c - side + up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            B(bannerMat).Quad(c + side - up, c - side - up, c - side + up, c + side + up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            B(darkMetal).Cylinder(pole + Vector3.up * 7.95f, c + side + Vector3.up * 1.85f, 0.04f, 4, null, false);
            B(darkMetal).Cylinder(pole + Vector3.up * 4.25f, c + side - Vector3.up * 1.85f, 0.04f, 4, null, false);
            _ = along;
        }

        void Junction(int k)
        {
            var ni = ninfo[k];
            if (!ni.cross) return;
            float y = nodeH[k];
            Vector2 p = Nodes[k].p;
            float hx = ni.hwX, hz = ni.hwZ, S = Sidewalk;
            var c = V3(p, y);
            Vector3 X = Vector3.right, Z = Vector3.forward;
            // asphalt square
            QuadAuto(asphalt, c + new Vector3(-hx, 0.12f, -hz), c + new Vector3(hx, 0.12f, -hz), c + new Vector3(hx, 0.12f, hz), c + new Vector3(-hx, 0.12f, hz), Vector3.up, 8, 8);
            // corner sidewalks and fills for missing arms
            void Pad(float x0, float x1, float z0, float z1)
            {
                QuadAuto(sidewalk, c + new Vector3(x0, 0.34f, z0), c + new Vector3(x1, 0.34f, z0), c + new Vector3(x1, 0.34f, z1), c + new Vector3(x0, 0.34f, z1), Vector3.up, 4, 4, new Vector2((p.x + x0) / 4, (p.y + z0) / 4));
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Pad(sx > 0 ? hx : -hx - S, sx > 0 ? hx + S : -hx, sz > 0 ? hz : -hz - S, sz > 0 ? hz + S : -hz);
            if (ni.e < 0) Pad(hx, hx + S, -hz, hz);
            if (ni.w < 0) Pad(-hx - S, -hx, -hz, hz);
            if (ni.n < 0) Pad(-hx, hx, hz, hz + S);
            if (ni.s < 0) Pad(-hx, hx, -hz - S, -hz);
            // a traffic signal on one corner of busy crossings
            if (hx + hz > 22)
            {
                Vector3 pole = c + new Vector3(hx + 1.5f, 0.34f, -hz - 1.5f);
                B(darkMetal).Cylinder(pole, pole + Vector3.up * 7.5f, 0.18f, 8);
                Vector3 armEnd = pole + Vector3.up * 7.2f + Vector3.left * (hx * 0.8f);
                B(darkMetal).Cylinder(pole + Vector3.up * 7.2f, armEnd, 0.1f, 6);
                B(darkMetal).BoxCentered(armEnd + Vector3.down * 0.9f, new Vector3(0.6f, 1.8f, 0.5f), Tile(1, 1));
                B(R() < 0.5f ? neons[4] : neons[5]).BoxCentered(armEnd + Vector3.down * 1.2f + Vector3.back * 0.27f, new Vector3(0.35f, 0.35f, 0.05f), Tile(1, 1));
            }
            _ = X; _ = Z;
        }

        void Runway(Edge e, float len)
        {
            float hw = e.w * 0.5f;
            Dashes(paintWhite, e, 60, len - 60, 0, 0.9f, 30f, 20f);
            Dashes(paintWhite, e, 0, len, hw - 1.5f, 0.9f, 0, 0);
            Dashes(paintWhite, e, 0, len, -hw + 1.5f, 0.9f, 0, 0);
            foreach (float s0 in new[] { 8f, len - 38f })
                for (float o = -hw + 4; o < hw - 4; o += 3.2f)
                    Ribbon(paintWhite, e, s0, s0 + 30, o, o + 1.8f, 0.16f, 30f);
            Vector2 a = Nodes[e.a].p, b = Nodes[e.b].p;
            Vector2 d = (b - a) / len;
            Vector3 right = new(d.y, 0, -d.x);
            for (float s = 0; s <= len; s += 30f)
                for (int side = -1; side <= 1; side += 2)
                    B(runwayLight).BoxCentered(V3(a + d * s, EdgeY(e, s / len) + 0.3f) + right * side * (hw + 1.5f), new Vector3(0.5f, 0.35f, 0.5f), Tile(1, 1));
        }

        // ------------------------------------------------------------------ elevated roads
        void BuildElevated()
        {
            var g = new GameObject("Elevated").transform;
            g.SetParent(root, false);
            foreach (var e in Edges)
            {
                if (!IsElevated(e)) continue;
                ElevatedEdge(e, g);
                Flush("Elevated_" + e.kind + "_" + e.a, g);
            }
        }

        void ElevatedEdge(Edge e, Transform parent)
        {
            Vector2 a = Nodes[e.a].p, b = Nodes[e.b].p;
            float len = Vector2.Distance(a, b);
            Vector2 d = (b - a) / len;
            Vector3 right = new(d.y, 0, -d.x);
            int da = DirOf(b - a), db = DirOf(a - b);
            float ta = Trim(e.a, da, false), tb = Trim(e.b, db, false);
            float hw = e.w * 0.5f;
            float walk = e.kind == RoadKind.Causeway ? 3f : 4.5f;
            float edgeO = hw + walk;
            const float step = 8f;
            Ribbon(asphalt, e, ta, len - tb, -hw, hw, 0.02f, step, 8f);
            Dashes(laneYellow, e, ta, len - tb, 0.2f, 0.14f, 0, 0);
            Dashes(laneYellow, e, ta, len - tb, -0.2f, 0.14f, 0, 0);
            Dashes(laneLine, e, ta, len - tb, hw * 0.5f, 0.14f, 3f, 6f);
            Dashes(laneLine, e, ta, len - tb, -hw * 0.5f, 0.14f, 3f, 6f);
            for (int side = -1; side <= 1; side += 2)
            {
                float o0 = side > 0 ? hw : -edgeO, o1 = side > 0 ? edgeO : -hw;
                Ribbon(sidewalk, e, ta, len - tb, o0, o1, 0.3f, step);
                // traffic barrier between road and walkway, railing at the edge
                Wall(concreteA, e, ta, len - tb, side * (hw + 0.3f), 0.02f, 1.0f, -side, step, 2f);
                Wall(concreteA, e, ta, len - tb, side * (hw + 0.6f), 0.02f, 1.0f, side, step, 2f);
                Ribbon(concreteA, e, ta, len - tb, side * (hw + 0.3f) - 0.15f, side * (hw + 0.6f) + 0.15f, 1.0f, step);
                Wall(girder, e, ta, len - tb, side * edgeO, -2.6f, 0.3f, side, step, 4f);
                Wall(darkMetal, e, ta, len - tb, side * (edgeO - 0.1f), 0.3f, 1.4f, -side, step, 2f);
                Wall(darkMetal, e, ta, len - tb, side * (edgeO - 0.1f), 0.3f, 1.4f, side, step, 2f);
            }
            // underside
            {
                int segs = Mathf.Max(1, Mathf.CeilToInt((len - ta - tb) / step));
                for (int k = 0; k < segs; k++)
                {
                    float sa = Mathf.Lerp(ta, len - tb, k / (float)segs), sb = Mathf.Lerp(ta, len - tb, (k + 1) / (float)segs);
                    Vector3 pa = V3(a + d * sa, EdgeY(e, sa / len) - 2.6f), pb = V3(a + d * sb, EdgeY(e, sb / len) - 2.6f);
                    QuadAuto(girder, pa - right * edgeO, pa + right * edgeO, pb + right * edgeO, pb - right * edgeO, Vector3.down, 6, 6);
                }
            }
            // colliders: deck slabs and side rails in short pieces
            var col = new GameObject("DeckColliders").transform;
            col.SetParent(parent, false);
            for (float s = ta; s < len - tb - 0.01f; s += 16f)
            {
                float s2 = Mathf.Min(s + 16f, len - tb);
                Vector3 p0 = V3(a + d * s, EdgeY(e, s / len)), p1 = V3(a + d * s2, EdgeY(e, s2 / len));
                Vector3 dir = (p1 - p0).normalized;
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                Vector3 mid = (p0 + p1) * 0.5f;
                float l = Vector3.Distance(p0, p1) + 0.6f;
                OrientedBox(col, mid + rot * new Vector3(0, -1.3f, 0), rot, new Vector3(edgeO * 2, 2.6f, l));
                OrientedBox(col, mid + rot * new Vector3(edgeO - 0.2f, 1.0f, 0), rot, new Vector3(0.5f, 2.0f, l));
                OrientedBox(col, mid + rot * new Vector3(-edgeO + 0.2f, 1.0f, 0), rot, new Vector3(0.5f, 2.0f, l));
            }
            // piers, except under the suspended main span
            if (e.kind != RoadKind.Bridge)
            {
                float spacing = e.kind == RoadKind.Causeway ? 32f : 46f;
                for (float s = ta + spacing * 0.5f; s < len - tb - 6; s += spacing)
                {
                    float top = EdgeY(e, s / len) - 2.6f;
                    Vector2 p = a + d * s;
                    float ground = Mathf.Max(H(p.x, p.y), SeaFloor);
                    if (top - ground < 1.5f) continue;
                    bool arch = e.kind == RoadKind.Causeway;
                    var m = arch ? stone : concreteB;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 c = V3(p, ground) + right * side * (edgeO - 3.5f);
                        B(m).BoxCentered(c + Vector3.up * (top - ground) * 0.5f, new Vector3(Mathf.Abs(right.x) * 4f + Mathf.Abs(d.x) * 3f, top - ground, Mathf.Abs(right.z) * 4f + Mathf.Abs(d.y) * 3f), Tile(4, 4));
                        AddBoxCollider(col, c + new Vector3(-1.5f, 0, -1.5f), c + new Vector3(1.5f, top - ground, 1.5f), "Pier");
                    }
                    B(m).BoxCentered(V3(p, top - 0.8f), new Vector3(Mathf.Abs(right.x) * edgeO * 2 + Mathf.Abs(d.x) * 3, 1.6f, Mathf.Abs(right.z) * edgeO * 2 + Mathf.Abs(d.y) * 3), Tile(4, 4));
                }
            }
            // lamps with league banners along the walkways
            int k2 = 0;
            for (float s = ta + 20; s < len - tb - 6; s += 40f, k2++)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 bp = V3(a + d * s, EdgeY(e, s / len) + 0.3f) + right * side * (edgeO - 0.8f);
                    Streetlight(bp, -right * side);
                    if (e.kind != RoadKind.Causeway || k2 % 2 == 0) PoleBanner(bp, V3(d, 0), -right * side);
                }
        }

        void OrientedBox(Transform parent, Vector3 center, Quaternion rot, Vector3 size)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<BoxCollider>().size = size;
        }

        // ------------------------------------------------------------------ districts
        int FindNode(float x, float z)
        {
            for (int i = 0; i < Nodes.Count; i++) if ((Nodes[i].p - new Vector2(x, z)).sqrMagnitude < 1f) return i;
            return -1;
        }

        void BuildDistricts()
        {
            var hangarTown = Style("FacadeDayBrick", "FacadeDayStone", "FacadeDayBrick", "FacadeDayOffice");
            hangarTown.height = (x, z, r) => Mathf.Lerp(12, 30, r);
            hangarTown.store = 0.85f; hangarTown.signs = 0.4f; hangarTown.awnings = 0.45f; hangarTown.pitched = 0.1f;
            var oldTown = Style("FacadeDayBrick", "FacadeDayApart", "FacadeDayStone", "FacadeDayWhite", "FacadeDayBrick");
            oldTown.height = (x, z, r) => Mathf.Lerp(11, 27, r * r);
            oldTown.store = 0.55f; oldTown.signs = 0.07f; oldTown.awnings = 0.5f; oldTown.pitched = 0.6f; oldTown.lotMin = 14; oldTown.lotMax = 26;
            var downtown = Style("FacadeDayGlass", "FacadeDayOffice", "FacadeDayStone", "FacadeDayGold", "FacadeDayGlass");
            downtown.height = (x, z, r) =>
            {
                float t = Mathf.Clamp01(Vector2.Distance(new Vector2(x, z), new Vector2(1520, 520)) / 820f);
                float h = Mathf.Lerp(230f, 34f, Mathf.Pow(t, 0.8f)) * Mathf.Lerp(0.45f, 1.25f, r);
                if (r > 0.93f && t < 0.45f) h *= 1.45f;
                return h;
            };
            downtown.store = 0.7f; downtown.signs = 0.04f; downtown.awnings = 0.2f; downtown.setback = 0.7f; downtown.lotMin = 26; downtown.lotMax = 52; downtown.acs = 0.3f;
            var island = Style("FacadeDayWhite", "FacadeDayApart", "FacadeDayWhite", "FacadeDayStone");
            island.height = (x, z, r) => Mathf.Lerp(7.6f, 15f, r);
            island.store = 0.15f; island.signs = 0.02f; island.awnings = 0.3f; island.pitched = 0.85f; island.lotMin = 11; island.lotMax = 17; island.gap = 0.3f; island.acs = 0.2f;

            Blocks(WestX, WestZ, WestWidth, (x, z) => z < -225 ? hangarTown : oldTown, "West");
            Blocks(EastX, EastZ, EastWidth, (x, z) => downtown, "East");
            Blocks(IslandX, IslandZ, IslandWidth, (x, z) => island, "Island");
        }

        void Blocks(float[] xs, float[] zs, System.Func<bool, float, float> width, System.Func<float, float, BStyle> style, string name)
        {
            var g = new GameObject("District_" + name).transform;
            g.SetParent(root, false);
            for (int i = 0; i < xs.Length - 1; i++)
                for (int j = 0; j < zs.Length - 1; j++)
                {
                    int c00 = FindNode(xs[i], zs[j]), c10 = FindNode(xs[i + 1], zs[j]), c01 = FindNode(xs[i], zs[j + 1]), c11 = FindNode(xs[i + 1], zs[j + 1]);
                    if (c00 < 0 || c10 < 0 || c01 < 0 || c11 < 0) continue;
                    var r = Rect.MinMaxRect(xs[i] + width(true, xs[i]) * 0.5f + Sidewalk, zs[j] + width(false, zs[j]) * 0.5f + Sidewalk,
                                            xs[i + 1] - width(true, xs[i + 1]) * 0.5f - Sidewalk, zs[j + 1] - width(false, zs[j + 1]) * 0.5f - Sidewalk);
                    if (CoastDist(r.center.x, r.center.y) < 40) continue;
                    var blockRoot = new GameObject($"Block_{name}_{i}_{j}").transform;
                    blockRoot.SetParent(g, false);
                    Block(r, style(r.center.x, r.center.y), blockRoot);
                    Flush($"Block_{name}_{i}_{j}_Geo", blockRoot);
                }
        }

        void Block(Rect lot, BStyle st, Transform parent)
        {
            var dist = DistrictAt(lot.center.x, lot.center.y);
            var c = lot.center;
            // landmark blocks
            if (lot.Contains(RigShop + new Vector2(0, 40))) { RigShopHangar(lot, parent); return; }
            if (dist == District.Downtown && lot.Contains(new Vector2(1225, 275))) { Capitol(lot, parent); return; }
            if (dist == District.Downtown && c.x < 760) { Park(lot); return; }
            if (dist == District.Downtown && lot.Contains(new Vector2(1525, 425))) { Plaza(lot, parent); return; }
            if (dist == District.Hangar && c.x > -1100 && R() < 0.72f) { HangarBlock(lot, parent); return; }
            if (dist == District.Hangar && R() < 0.4f) { WarehouseBlock(lot, parent); return; }
            Lots(lot, st, parent);
        }

        void Lots(Rect lot, BStyle st, Transform parent)
        {
            float depth = lot.height;
            int rows = depth > 60 ? 2 : 1;
            for (int row = 0; row < rows; row++)
            {
                float z0 = rows == 1 ? lot.yMin : (row == 0 ? lot.yMin : lot.yMin + depth * 0.5f);
                float z1 = rows == 1 ? lot.yMax : (row == 0 ? lot.yMin + depth * 0.5f : lot.yMax);
                float x = lot.xMin;
                while (x < lot.xMax - 1)
                {
                    float w = R(st.lotMin, st.lotMax);
                    if (lot.xMax - (x + w) < st.lotMin * 0.8f) w = lot.xMax - x;
                    var faces = new bool[4];
                    faces[0] = Mathf.Abs(x - lot.xMin) < 0.1f;
                    faces[1] = Mathf.Abs(x + w - lot.xMax) < 0.1f;
                    faces[2] = row == 0;
                    faces[3] = row == rows - 1;
                    var f = Rect.MinMaxRect(x, z0, x + w, z1);
                    if (st.gap > 0 && R() < st.gap)
                    {
                        // garden: a tree or two
                        for (int k = 0; k < 2; k++) AddTree(new Vector3(R(f.xMin + 2, f.xMax - 2), 0, R(f.yMin + 2, f.yMax - 2)), 0, R(0.8f, 1.2f));
                    }
                    else if (st.gap > 0)
                    {
                        // houses sit back from the sidewalk with a little yard
                        var hf = Rect.MinMaxRect(f.xMin + 1.2f, f.yMin + (faces[2] ? 3f : 1f), f.xMax - 1.2f, f.yMax - (faces[3] ? 3f : 1f));
                        if (hf.width > 6 && hf.height > 6) BuildingW(hf, faces, st, parent);
                    }
                    else BuildingW(f, faces, st, parent);
                    x += w;
                }
            }
        }

        void BuildingW(Rect f, bool[] street, BStyle st, Transform parent, float hOverride = -1)
        {
            float baseY = MinGround(f) - 0.15f;
            shift = new Vector3(0, baseY, 0);
            float h = hOverride > 0 ? hOverride : st.height(f.center.x, f.center.y, R());
            h = Mathf.Max(7.6f, Mathf.Round((h - 6.6f) / 3.6f) * 3.6f + 6.6f);
            int fi = rnd.Next(st.facades.Length);
            var facade = st.facades[fi];
            int kind = st.kinds[fi];
            B(facade).uvShift = new Vector2(rnd.Next(0, 4) * 0.25f, rnd.Next(0, 4) * 0.25f);
            var trim = st.trim ? st.trim : (R() < 0.5f ? concreteA : concreteB);
            bool pitched = h < 32 && R() < st.pitched;

            // foundation reaching into the slope
            B(trim).Box(new Vector3(f.xMin, -5f, f.yMin), new Vector3(f.xMax, 0f, f.yMax), Tile(8, 4), 63 & ~4 & ~8);
            float podium = 0;
            int storeFaces = 0;
            if (R() < st.store)
            {
                var store = Pick(storefronts);
                B(store).uvShift = new Vector2(R(), 0);
                storeFaces = (street[0] ? 2 : 0) | (street[1] ? 1 : 0) | (street[2] ? 32 : 0) | (street[3] ? 16 : 0);
                B(store).Box(new Vector3(f.xMin, 0, f.yMin), new Vector3(f.xMax, 6f, f.yMax), Tile(24, 6), storeFaces);
                B(trim).Box(new Vector3(f.xMin, 0, f.yMin), new Vector3(f.xMax, 6f, f.yMax), Tile(8, 8), 63 & ~storeFaces & ~4 & ~8);
                B(trim).Box(new Vector3(f.xMin - 0.4f, 6f, f.yMin - 0.4f), new Vector3(f.xMax + 0.4f, 6.6f, f.yMax + 0.4f), Tile(8, 1));
                podium = 6.6f;
            }
            bool setback = h > 55 && R() < st.setback;
            float sb = setback ? Mathf.Round((6.6f + (h - 6.6f) * R(0.45f, 0.7f)) / 3.6f) * 3.6f : h;
            float bodyTop = setback ? sb : h;
            B(facade).Box(new Vector3(f.xMin, podium, f.yMin), new Vector3(f.xMax, bodyTop, f.yMax), Tile(12, 14.4f), 63 & ~8 & ~4);
            AddBoxCollider(parent, new Vector3(f.xMin, -5, f.yMin), new Vector3(f.xMax, bodyTop, f.yMax));
            Rect top = f; float topY = bodyTop;
            if (pitched) Gable(f, bodyTop, trim);
            else Cap(f, bodyTop, trim);
            if (setback)
            {
                float inset = R(3f, 7f);
                Rect tw = Rect.MinMaxRect(f.xMin + inset, f.yMin + inset, f.xMax - inset, f.yMax - inset);
                if (tw.width > 8 && tw.height > 8)
                {
                    var tf = R() < 0.6f ? facade : Pick(st.facades);
                    B(tf).Box(new Vector3(tw.xMin, sb, tw.yMin), new Vector3(tw.xMax, h, tw.yMax), Tile(12, 14.4f), 63 & ~8 & ~4);
                    AddBoxCollider(parent, new Vector3(tw.xMin, sb, tw.yMin), new Vector3(tw.xMax, h, tw.yMax));
                    Cap(tw, h, trim);
                    top = tw; topY = h;
                    if (h > 130 && R() < 0.75f) Crown(tw, h, tf);
                }
            }
            if (!pitched) Rooftop(top, topY, topY < 80);
            FloorBands(f, Mathf.Max(podium, 6.6f), bodyTop, kind, trim);
            for (int s = 0; s < 4; s++)
            {
                if (!street[s]) continue;
                FaceInfo(f, s, out Vector3 a, out Vector3 dir, out Vector3 outN, out float len);
                if (storeFaces != 0 && R() < st.awnings) Awning(a, dir, outN, len);
                if (R() < st.signs && h > 20) VerticalSign(a, dir, outN, len, h);
                if (storeFaces != 0 && R() < st.signs * 0.8f && len > 14) StoreSign(a, dir, outN, len);
                if (R() < st.acs) FacadeACs(a, dir, outN, len, Mathf.Min(h, 40));
            }
            shift = Vector3.zero;
        }

        /// <summary>Pitched tile roof over a rectangle, ridge along the long side.</summary>
        void Gable(Rect f, float y, Material wall)
        {
            bool alongX = f.width >= f.height;
            float span = alongX ? f.height : f.width;
            float rise = span * 0.5f * 0.62f, ov = 0.5f;
            float x0 = f.xMin - ov, x1 = f.xMax + ov, z0 = f.yMin - ov, z1 = f.yMax + ov;
            float ye = y - ov * 0.6f, yr = y + rise;
            if (alongX)
            {
                float zc = f.center.y;
                QuadAuto(roofTile, new Vector3(x0, ye, z0), new Vector3(x1, ye, z0), new Vector3(x1, yr, zc), new Vector3(x0, yr, zc), new Vector3(0, 1, -1), 3, 3);
                QuadAuto(roofTile, new Vector3(x0, ye, z1), new Vector3(x1, ye, z1), new Vector3(x1, yr, zc), new Vector3(x0, yr, zc), new Vector3(0, 1, 1), 3, 3);
                TriAuto(wall, new Vector3(f.xMin, y, f.yMin), new Vector3(f.xMin, y, f.yMax), new Vector3(f.xMin, yr, zc), Vector3.left);
                TriAuto(wall, new Vector3(f.xMax, y, f.yMin), new Vector3(f.xMax, y, f.yMax), new Vector3(f.xMax, yr, zc), Vector3.right);
            }
            else
            {
                float xc = f.center.x;
                QuadAuto(roofTile, new Vector3(x0, ye, z0), new Vector3(x0, ye, z1), new Vector3(xc, yr, z1), new Vector3(xc, yr, z0), new Vector3(-1, 1, 0), 3, 3);
                QuadAuto(roofTile, new Vector3(x1, ye, z0), new Vector3(x1, ye, z1), new Vector3(xc, yr, z1), new Vector3(xc, yr, z0), new Vector3(1, 1, 0), 3, 3);
                TriAuto(wall, new Vector3(f.xMin, y, f.yMin), new Vector3(f.xMax, y, f.yMin), new Vector3(xc, yr, f.yMin), Vector3.back);
                TriAuto(wall, new Vector3(f.xMin, y, f.yMax), new Vector3(f.xMax, y, f.yMax), new Vector3(xc, yr, f.yMax), Vector3.forward);
            }
            // chimney
            if (R() < 0.5f)
            {
                Vector3 c = new(R(f.xMin + 2, f.xMax - 2), y, R(f.yMin + 2, f.yMax - 2));
                B(wall).Box(c + new Vector3(-0.6f, 0, -0.6f), c + new Vector3(0.6f, rise + 1.2f, 0.6f), Tile(2, 2));
            }
        }

        /// <summary>Tower crown: stepped tiers and a spire.</summary>
        void Crown(Rect f, float y, Material facade)
        {
            Rect r = f;
            float yy = y;
            for (int k = 0; k < 3; k++)
            {
                float inset = Mathf.Min(r.width, r.height) * 0.14f;
                r = Rect.MinMaxRect(r.xMin + inset, r.yMin + inset, r.xMax - inset, r.yMax - inset);
                if (r.width < 4) break;
                float hh = 7.2f - k * 1.8f;
                B(k == 0 ? facade : metal).Box(new Vector3(r.xMin, yy, r.yMin), new Vector3(r.xMax, yy + hh, r.yMax), Tile(12, 14.4f), 63 & ~8);
                yy += hh;
            }
            Vector3 c = new(f.center.x, yy, f.center.y);
            B(metal).Cylinder(c, c + Vector3.up * R(18, 40), 0.9f, 8, null, true, 0.15f);
            B(beacon).BoxCentered(c + Vector3.up * 2f, Vector3.one * 1.2f, Tile(1, 1));
        }

        // ------------------------------------------------------------------ hangars + industry
        void HangarBlock(Rect lot, Transform parent)
        {
            // one big hall or two side by side, doors facing the wider street side
            bool two = lot.width > 110 && R() < 0.55f;
            if (two)
            {
                float mid = lot.center.x;
                Hangar(Rect.MinMaxRect(lot.xMin + 2, lot.yMin + 4, mid - 4, lot.yMax - 4), R() < 0.5f ? 2 : 3, $"{rnd.Next(1, 30):00}", parent);
                Hangar(Rect.MinMaxRect(mid + 4, lot.yMin + 4, lot.xMax - 2, lot.yMax - 4), R() < 0.5f ? 2 : 3, $"{rnd.Next(1, 30):00}", parent);
            }
            else Hangar(Rect.MinMaxRect(lot.xMin + 6, lot.yMin + 4, lot.xMax - 6, lot.yMax - 4), R() < 0.5f ? 2 : 3, $"{rnd.Next(1, 30):00}", parent);
            // apron + containers
            AddTree(new Vector3(lot.xMin + 3, 0, lot.yMin + 3), 0, 0.9f);
        }

        void RigShopHangar(Rect lot, Transform parent)
        {
            var f = Rect.MinMaxRect(lot.xMin + 8, lot.yMin + 2, lot.xMax - 8, lot.yMax - 6);
            Hangar(f, 2, "03", parent, true);
        }

        Material HangarDoorMat(string num)
        {
            if (!hangarDoors.TryGetValue(num, out var m))
                hangarDoors[num] = m = Mats.LitMat("HangarDoor_" + num, Color.white, 0.35f, 0.3f, WorldTextures.HangarDoor(num));
            return m;
        }

        /// <summary>Barrel-vault hall. doorFace: 2 = south, 3 = north. Vault runs north-south.</summary>
        void Hangar(Rect f, int doorFace, string number, Transform parent, bool rigShop = false)
        {
            float baseY = MinGround(f) - 0.1f;
            shift = new Vector3(0, baseY, 0);
            float span = f.width, length = f.height;
            float wallH = rigShop ? 16f : R(11f, 15f);
            float rise = span * 0.26f;
            float x0 = f.xMin, x1 = f.xMax, z0 = f.yMin, z1 = f.yMax, xc = f.center.x;
            B(concreteB).Box(new Vector3(x0, -5, z0), new Vector3(x1, 0.6f, z1), Tile(8, 4), 63 & ~4 & ~8);
            B(hangarWall).Box(new Vector3(x0, 0.6f, z0), new Vector3(x1, wallH, z1), Tile(10, 10), 1 | 2);
            int segs = 14;
            for (int k = 0; k < segs; k++)
            {
                float a0 = Mathf.PI * k / segs, a1 = Mathf.PI * (k + 1) / segs;
                float u0 = xc - Mathf.Cos(a0) * span * 0.5f, u1 = xc - Mathf.Cos(a1) * span * 0.5f;
                float y0 = wallH + Mathf.Sin(a0) * rise, y1 = wallH + Mathf.Sin(a1) * rise;
                Vector3 outN = new(-Mathf.Cos((a0 + a1) * 0.5f), Mathf.Sin((a0 + a1) * 0.5f) * 1.5f, 0);
                QuadAuto(hangarRoof, new Vector3(u0, y0, z0 - 0.6f), new Vector3(u1, y1, z0 - 0.6f), new Vector3(u1, y1, z1 + 0.6f), new Vector3(u0, y0, z1 + 0.6f), outN, 8, 8);
                // gable ends: vertical strips under the vault
                foreach (var (z, n) in new[] { (z0, Vector3.back), (z1, Vector3.forward) })
                    QuadAuto(hangarWall, new Vector3(u0, wallH, z), new Vector3(u1, wallH, z), new Vector3(u1, y1, z), new Vector3(u0, y0, z), n, 10, 10, new Vector2(u0 / 10, wallH / 10));
            }
            // end walls with the big door
            float doorW = span * 0.62f, doorH = Mathf.Min(wallH + rise * 0.55f, rigShop ? 20f : 19f);
            foreach (int face in new[] { 2, 3 })
            {
                float z = face == 2 ? z0 : z1;
                Vector3 n = face == 2 ? Vector3.back : Vector3.forward;
                float dl = xc - doorW / 2, dr = xc + doorW / 2;
                bool door = face == doorFace;
                // wall around the door (or solid)
                if (door)
                {
                    QuadAuto(hangarWall, new Vector3(x0, 0.6f, z), new Vector3(dl, 0.6f, z), new Vector3(dl, wallH, z), new Vector3(x0, wallH, z), n, 10, 10);
                    QuadAuto(hangarWall, new Vector3(dr, 0.6f, z), new Vector3(x1, 0.6f, z), new Vector3(x1, wallH, z), new Vector3(dr, wallH, z), n, 10, 10);
                    float zz = z + n.z * 0.15f;
                    if (rigShop)
                    {
                        // door rolled half up over a lit workshop
                        float split = doorH * 0.48f;
                        QuadAuto(HangarDoorMat(number), new Vector3(dl, split, zz), new Vector3(dr, split, zz), new Vector3(dr, doorH, zz), new Vector3(dl, doorH, zz), n, doorW, doorH - split);
                        QuadAuto(interiorGlow, new Vector3(dl, 0.6f, zz - n.z * 0.4f), new Vector3(dr, 0.6f, zz - n.z * 0.4f), new Vector3(dr, split, zz - n.z * 0.4f), new Vector3(dl, split, zz - n.z * 0.4f), n, doorW, split);
                        var lm = StrokeFont.TubeMesh("RIG SHOP", 2.4f, 0.13f);
                        float w = lm.bounds.size.x;
                        B(neons[2]).Append(lm, Matrix4x4.TRS(new Vector3(xc - w * 0.5f, doorH + 1.2f, z + n.z * 0.5f), face == 2 ? Quaternion.identity : Quaternion.Euler(0, 180, 0), Vector3.one));
                        Object.DestroyImmediate(lm);
                        AddLight(new Vector3(xc, 7, z + n.z * 8), new Color(1f, 0.65f, 0.3f), 8f, 30f);
                        AddLight(new Vector3(xc, 4, z - n.z * 6), new Color(1f, 0.75f, 0.45f), 6f, 25f);
                    }
                    else QuadAuto(HangarDoorMat(number), new Vector3(dl, 0.6f, zz), new Vector3(dr, 0.6f, zz), new Vector3(dr, doorH, zz), new Vector3(dl, doorH, zz), n, doorW, doorH - 0.6f);
                    if (doorH < wallH) QuadAuto(hangarWall, new Vector3(dl, doorH, z), new Vector3(dr, doorH, z), new Vector3(dr, wallH, z), new Vector3(dl, wallH, z), n, 10, 10);
                    // painted bay number high on the gable
                    var num = StrokeFont.TubeMesh(number, Mathf.Min(rise * 0.7f, 9f), 0.35f);
                    float nw = num.bounds.size.x;
                    float nx = face == 2 ? xc - nw * 0.5f : xc + nw * 0.5f;
                    B(paintWhite).Append(num, Matrix4x4.TRS(new Vector3(nx, wallH + rise * 0.18f, z + n.z * 0.4f), face == 2 ? Quaternion.identity : Quaternion.Euler(0, 180, 0), Vector3.one));
                    Object.DestroyImmediate(num);
                    // mech poster beside the door
                    float px = dr + (x1 - dr) * 0.5f, pw = Mathf.Min(x1 - dr - 1.2f, 9f);
                    if (pw > 4)
                    {
                        var pm = rigShop ? posters[0] : Pick(posters);
                        float ph = Mathf.Min(pw * 2f, wallH - 1.5f);
                        QuadAuto(pm, new Vector3(px - pw / 2, 1.2f, zz), new Vector3(px + pw / 2, 1.2f, zz), new Vector3(px + pw / 2, 1.2f + ph, zz), new Vector3(px - pw / 2, 1.2f + ph, zz), n, pw, ph);
                    }
                    // apron light bar
                    B(lampHead).BoxCentered(new Vector3(xc, doorH + 0.5f, z + n.z * 0.5f), new Vector3(doorW, 0.25f, 0.4f), Tile(1, 1));
                }
                else QuadAuto(hangarWall, new Vector3(x0, 0.6f, z), new Vector3(x1, 0.6f, z), new Vector3(x1, wallH, z), new Vector3(x0, wallH, z), n, 10, 10);
            }
            // roof vents
            for (float z = z0 + 10; z < z1 - 6; z += 18)
                B(metal).Cylinder(new Vector3(xc, wallH + rise - 0.2f, z), new Vector3(xc, wallH + rise + 1.6f, z), 1.0f, 10, null, true, 0.7f);
            AddBoxCollider(parent, new Vector3(x0, -5, z0), new Vector3(x1, wallH + rise * 0.8f, z1), "Hangar");
            shift = Vector3.zero;
        }

        void WarehouseBlock(Rect lot, Transform parent)
        {
            float x = lot.xMin;
            while (x < lot.xMax - 10)
            {
                float w = Mathf.Min(R(30, 60), lot.xMax - x);
                if (lot.xMax - (x + w) < 20) w = lot.xMax - x;
                var f = Rect.MinMaxRect(x + 1, lot.yMin + 1, x + w - 1, lot.yMax - 1);
                float baseY = MinGround(f) - 0.1f;
                shift = new Vector3(0, baseY, 0);
                float h = R(9, 16);
                B(concreteB).Box(new Vector3(f.xMin, -5, f.yMin), new Vector3(f.xMax, 0.6f, f.yMax), Tile(8, 4), 63 & ~4 & ~8);
                B(warehouseWall).Box(new Vector3(f.xMin, 0.6f, f.yMin), new Vector3(f.xMax, h, f.yMax), Tile(10, 10), 63 & ~4 & ~8);
                Cap(f, h, concreteA);
                Rooftop(f, h, true);
                // loading doors on the long faces
                for (float dx = f.xMin + 5; dx < f.xMax - 6; dx += 11)
                    foreach (var (z, n) in new[] { (f.yMin - 0.05f, Vector3.back), (f.yMax + 0.05f, Vector3.forward) })
                        QuadAuto(darkMetal, new Vector3(dx, 0.6f, z), new Vector3(dx + 5, 0.6f, z), new Vector3(dx + 5, 5.5f, z), new Vector3(dx, 5.5f, z), n, 5, 5);
                AddBoxCollider(parent, new Vector3(f.xMin, -5, f.yMin), new Vector3(f.xMax, h, f.yMax));
                FaceInfo(f, 2, out var a, out var dir, out var outN, out float len);
                if (R() < 0.35f) VerticalSign(a, dir, outN, len, h + 8);
                shift = Vector3.zero;
                x += w;
            }
        }

        // ------------------------------------------------------------------ downtown landmarks
        void Capitol(Rect lot, Transform parent)
        {
            float y = MinGround(lot) + 0.25f;
            // plaza
            QuadAuto(sidewalk, new Vector3(lot.xMin, y + 0.1f, lot.yMin), new Vector3(lot.xMax, y + 0.1f, lot.yMin), new Vector3(lot.xMax, y + 0.1f, lot.yMax), new Vector3(lot.xMin, y + 0.1f, lot.yMax), Vector3.up, 4, 4);
            Vector2 c = lot.center + new Vector2(0, 12);
            float w = 104, d = 46, hh = 17;
            Vector3 b0 = new(c.x - w / 2, y, c.y - d / 2), b1 = new(c.x + w / 2, y + hh, c.y + d / 2);
            B(stone).Box(b0 + new Vector3(-3, -3, -3), new Vector3(b1.x + 3, y + 2.2f, b1.z + 3), Tile(6, 3));
            B(stone).Box(b0 + Vector3.up * 2.2f, b1, Tile(8, 6));
            B(stone).Box(new Vector3(b0.x - 0.8f, b1.y, b0.z - 0.8f), new Vector3(b1.x + 0.8f, b1.y + 1.4f, b1.z + 0.8f), Tile(8, 2));
            // windows as dark insets along the facade
            for (float x = b0.x + 5; x < b1.x - 4; x += 6.5f)
                for (int fl = 0; fl < 2; fl++)
                    QuadAuto(shipGlass, new Vector3(x, y + 4 + fl * 6.5f, b0.z - 0.05f), new Vector3(x + 2.4f, y + 4 + fl * 6.5f, b0.z - 0.05f), new Vector3(x + 2.4f, y + 8.6f + fl * 6.5f, b0.z - 0.05f), new Vector3(x, y + 8.6f + fl * 6.5f, b0.z - 0.05f), Vector3.back, 2.4f, 4.6f);
            // portico with columns and pediment
            float pz = b0.z - 9;
            B(stone).Box(new Vector3(c.x - 22, y, pz - 3), new Vector3(c.x + 22, y + 2.2f, b0.z), Tile(6, 3));
            for (int k = 0; k < 8; k++)
            {
                float x = c.x - 18.9f + k * 5.4f;
                B(stone).Cylinder(new Vector3(x, y + 2.2f, pz), new Vector3(x, y + hh, pz), 1.05f, 12, null, true, 0.9f);
            }
            B(stone).Box(new Vector3(c.x - 22, y + hh, pz - 1.5f), new Vector3(c.x + 22, y + hh + 2, b0.z), Tile(6, 2));
            TriAuto(stone, new Vector3(c.x - 22, y + hh + 2, pz - 1.5f), new Vector3(c.x + 22, y + hh + 2, pz - 1.5f), new Vector3(c.x, y + hh + 9, pz - 1.5f), Vector3.back);
            QuadAuto(stone, new Vector3(c.x - 22, y + hh + 2, pz - 1.5f), new Vector3(c.x, y + hh + 9, pz - 1.5f), new Vector3(c.x, y + hh + 9, b0.z), new Vector3(c.x - 22, y + hh + 2, b0.z), new Vector3(-1, 2, 0));
            QuadAuto(stone, new Vector3(c.x + 22, y + hh + 2, pz - 1.5f), new Vector3(c.x, y + hh + 9, pz - 1.5f), new Vector3(c.x, y + hh + 9, b0.z), new Vector3(c.x + 22, y + hh + 2, b0.z), new Vector3(1, 2, 0));
            // drum, colonnade, dome, lantern
            Vector3 dc = new(c.x, b1.y + 1.4f, c.y);
            B(stone).Cylinder(dc, dc + Vector3.up * 13, 17, 32);
            for (int k = 0; k < 24; k++)
            {
                float a = k / 24f * Mathf.PI * 2;
                Vector3 p = dc + new Vector3(Mathf.Cos(a) * 17.6f, 0, Mathf.Sin(a) * 17.6f);
                B(stone).Cylinder(p + Vector3.up * 1.5f, p + Vector3.up * 12, 0.55f, 8);
            }
            B(stone).Cylinder(dc + Vector3.up * 13, dc + Vector3.up * 14.6f, 18.6f, 32);
            Dome(domeMat, dc + Vector3.up * 14.6f, 17.5f, 1.15f, 32, 10);
            Vector3 lt = dc + Vector3.up * (14.6f + 17.5f * 1.15f - 0.5f);
            B(stone).Cylinder(lt, lt + Vector3.up * 6, 3.2f, 12);
            B(domeMat).Cylinder(lt + Vector3.up * 6, lt + Vector3.up * 11, 3.6f, 12, null, true, 0.1f);
            B(lantern).BoxCentered(lt + Vector3.up * 3, new Vector3(4.6f, 2.5f, 4.6f), Tile(1, 1));
            AddBoxCollider(parent, b0 + new Vector3(-3, -3, -3), new Vector3(b1.x + 3, b1.y, b1.z + 3));
            AddBoxCollider(parent, dc + new Vector3(-17, 0, -17), dc + new Vector3(17, 30, 17));
            // flags and lawn trees
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 fp = new(c.x + s * 34, y, lot.yMin + 8);
                B(metal).Cylinder(fp, fp + Vector3.up * 22, 0.18f, 6);
                QuadAuto(bannerMat, fp + new Vector3(0, 15.5f, 0), fp + new Vector3(4.2f * s, 15.5f, 0), fp + new Vector3(4.2f * s, 21.5f, 0), fp + new Vector3(0, 21.5f, 0), Vector3.back, 4.2f, 6f);
                QuadAuto(bannerMat, fp + new Vector3(0, 15.5f, 0), fp + new Vector3(4.2f * s, 15.5f, 0), fp + new Vector3(4.2f * s, 21.5f, 0), fp + new Vector3(0, 21.5f, 0), Vector3.forward, 4.2f, 6f);
            }
            for (float x = lot.xMin + 6; x < lot.xMax - 4; x += 14)
            {
                AddTree(new Vector3(x, 0, lot.yMin + 3), 0, 0.9f);
                AddTree(new Vector3(x, 0, lot.yMax - 3), 0, 0.9f);
            }
        }

        void Dome(Material m, Vector3 c, float r, float hs, int seg, int rings)
        {
            for (int k = 0; k < rings; k++)
            {
                float p0 = k / (float)rings * Mathf.PI * 0.5f, p1 = (k + 1) / (float)rings * Mathf.PI * 0.5f;
                for (int s = 0; s < seg; s++)
                {
                    float t0 = s / (float)seg * Mathf.PI * 2, t1 = (s + 1) / (float)seg * Mathf.PI * 2;
                    Vector3 P(float ph, float th) => c + new Vector3(Mathf.Cos(ph) * Mathf.Cos(th) * r, Mathf.Sin(ph) * r * hs, Mathf.Cos(ph) * Mathf.Sin(th) * r);
                    Vector3 mid = (P(p0, t0) + P(p1, t1)) * 0.5f;
                    if (k == rings - 1) TriAuto(m, P(p0, t0), P(p0, t1), c + Vector3.up * r * hs, mid - c);
                    else QuadAuto(m, P(p0, t0), P(p0, t1), P(p1, t1), P(p1, t0), mid - c, 3, 3);
                }
            }
        }

        void Park(Rect lot)
        {
            float y = MinGround(lot);
            for (float x = lot.xMin + 6; x < lot.xMax - 4; x += R(12, 20))
                for (float z = lot.yMin + 6; z < lot.yMax - 4; z += R(12, 20))
                    if (R() < 0.55f) AddTree(new Vector3(x + R(-3, 3), 0, z + R(-3, 3)), 0, R(0.85f, 1.3f));
            // a path through the middle
            Vector3 a = new(lot.xMin, y + 0.2f, lot.center.y - 3), b = new(lot.xMax, y + 0.2f, lot.center.y + 3);
            QuadAuto(sidewalk, a, new Vector3(b.x, a.y, a.z), new Vector3(b.x, a.y, b.z), new Vector3(a.x, a.y, b.z), Vector3.up, 4, 4);
        }

        void Plaza(Rect lot, Transform parent)
        {
            // League HQ: the tallest tower, on a plaza with screens
            float y = MinGround(lot) + 0.25f;
            QuadAuto(sidewalk, new Vector3(lot.xMin, y + 0.1f, lot.yMin), new Vector3(lot.xMax, y + 0.1f, lot.yMin), new Vector3(lot.xMax, y + 0.1f, lot.yMax), new Vector3(lot.xMin, y + 0.1f, lot.yMax), Vector3.up, 4, 4);
            var f = Rect.MinMaxRect(lot.center.x - 24, lot.center.y - 18, lot.center.x + 24, lot.center.y + 30);
            var st = Style("FacadeDayGlass");
            st.store = 0; st.setback = 1f; st.acs = 0;
            BuildingW(f, new[] { true, true, true, true }, st, parent, 300f);
            // big screen facing the avenue
            Vector3 s0 = new(lot.center.x - 16, y + 12, lot.yMin + 8), s1 = new(lot.center.x + 16, y + 12, lot.yMin + 8);
            QuadAuto(screens[1], s0, s1, s1 + Vector3.up * 16, s0 + Vector3.up * 16, Vector3.back, 32, 16);
            B(darkMetal).Box(new Vector3(s0.x - 0.6f, y, s0.z + 0.05f), new Vector3(s1.x + 0.6f, y + 28.6f, s0.z + 1.2f), Tile(2, 2));
            AddBoxCollider(parent, new Vector3(s0.x - 0.6f, y, s0.z), new Vector3(s1.x + 0.6f, y + 28.6f, s0.z + 1.2f));
        }

        // ------------------------------------------------------------------ the Central Bridge
        void CentralBridge()
        {
            var g = new GameObject("CentralBridge").transform;
            g.SetParent(root, false);
            var span = Edges.Find(e => e.kind == RoadKind.Bridge);
            float DeckY(float x) => EdgeY(span, Mathf.InverseLerp(-450, 450, x));
            float zc = BridgeZ, cz = 21.5f;
            foreach (float tx in new[] { -BridgeTower, BridgeTower })
            {
                float top = 150f;
                // footing in the water
                B(stoneDark).Box(new Vector3(tx - 14, SeaFloor, zc - 36), new Vector3(tx + 14, 3.5f, zc + 36), Tile(4, 4));
                AddBoxCollider(g, new Vector3(tx - 14, SeaFloor, zc - 36), new Vector3(tx + 14, 3.5f, zc + 36));
                foreach (int s in new[] { -1, 1 })
                {
                    float lz = zc + s * 27f;
                    // tapering leg in three stages with buttress ribs
                    float[] ys = { 3.5f, 60f, 110f, top };
                    for (int k = 0; k < 3; k++)
                    {
                        float hx = 6.2f - k * 0.9f, hz = 5.4f - k * 0.8f;
                        B(stone).Box(new Vector3(tx - hx, ys[k], lz - hz), new Vector3(tx + hx, ys[k + 1], lz + hz), Tile(6, 6));
                        B(stone).Box(new Vector3(tx - hx - 0.7f, ys[k + 1] - 1.6f, lz - hz - 0.7f), new Vector3(tx + hx + 0.7f, ys[k + 1], lz + hz + 0.7f), Tile(6, 2));
                        for (int r = -1; r <= 1; r += 2)
                            B(stone).Box(new Vector3(tx + r * hx - 0.6f, ys[k], lz - hz * 0.4f), new Vector3(tx + r * hx + 0.6f, ys[k + 1] - 2f, lz + hz * 0.4f), Tile(2, 6));
                    }
                    AddBoxCollider(g, new Vector3(tx - 6.2f, 3.5f, lz - 5.4f), new Vector3(tx + 6.2f, top, lz + 5.4f), "Tower");
                    // gothic crown: pinnacles
                    B(stone).Box(new Vector3(tx - 3.6f, top, lz - 3f), new Vector3(tx + 3.6f, top + 9, lz + 3f), Tile(4, 4));
                    B(stone).Cylinder(new Vector3(tx, top + 9, lz), new Vector3(tx, top + 26, lz), 3.4f, 4, null, true, 0.15f);
                    for (int px = -1; px <= 1; px += 2)
                        for (int pz = -1; pz <= 1; pz += 2)
                        {
                            Vector3 pc = new(tx + px * 3.2f, top, lz + pz * 2.6f);
                            B(stone).Cylinder(pc, pc + Vector3.up * 16, 0.9f, 4, null, true, 0.05f);
                        }
                    B(beacon).BoxCentered(new Vector3(tx, top + 26.5f, lz), Vector3.one * 1.1f, Tile(1, 1));
                    // banners hanging on the tower faces toward the road
                    foreach (int bx in new[] { -1, 1 })
                    {
                        float fx = tx + bx * 6.3f;
                        Vector3 n = new(bx, 0, 0);
                        QuadAuto(bannerMat, new Vector3(fx, 64, lz - 3.8f), new Vector3(fx, 64, lz + 3.8f), new Vector3(fx, 86, lz + 3.8f), new Vector3(fx, 86, lz - 3.8f), n, 7.6f, 22f);
                    }
                }
                // portal beams with pointed arches between the legs
                foreach (float by in new[] { 92f, 134f })
                {
                    B(stone).Box(new Vector3(tx - 4.5f, by, zc - 22f), new Vector3(tx + 4.5f, by + 7, zc + 22f), Tile(6, 4));
                    for (int s = -1; s <= 1; s += 2)
                        TriAuto(stone, new Vector3(tx - 4.5f, by, zc + s * 22f), new Vector3(tx - 4.5f, by, zc + s * 8f), new Vector3(tx - 4.5f, by - 8, zc + s * 22f), Vector3.left);
                }
                float dy = DeckY(tx);
                B(stone).Box(new Vector3(tx - 4.5f, dy - 11f, zc - 22f), new Vector3(tx + 4.5f, dy - 2.6f, zc + 22f), Tile(6, 4));
            }
            // anchorages on both shores
            foreach (float ax in new[] { -575f, 575f })
            {
                float gy = Mathf.Max(H(ax, zc), 0);
                B(stoneDark).Box(new Vector3(ax - 18, gy - 6, zc - 34), new Vector3(ax + 18, gy + 12, zc + 34), Tile(4, 4));
                AddBoxCollider(g, new Vector3(ax - 18, gy - 6, zc - 34), new Vector3(ax + 18, gy + 12, zc + 34), "Anchor");
            }
            // main cables: parabola between the towers, sagging back spans to the anchorages
            float CableY(float x)
            {
                float ax = Mathf.Abs(x);
                if (ax <= BridgeTower) return DeckY(0) + 8 + (150 - 2 - DeckY(0) - 8) * (x / BridgeTower) * (x / BridgeTower);
                float u = Mathf.InverseLerp(BridgeTower, 575, ax);
                return Mathf.Lerp(148, 14, u) - Mathf.Sin(u * Mathf.PI) * 10;
            }
            foreach (int s in new[] { -1, 1 })
            {
                float z = zc + s * cz;
                for (float x = -575; x < 575; x += 15)
                {
                    float x2 = Mathf.Min(x + 15, 575);
                    B(cableMat).Cylinder(new Vector3(x, CableY(x), z), new Vector3(x2, CableY(x2), z), 0.75f, 8, null, false);
                }
                // hangers down to the deck
                for (float x = -560; x <= 560; x += 12)
                {
                    if (Mathf.Abs(Mathf.Abs(x) - BridgeTower) < 8) continue;
                    float dy = Mathf.Abs(x) <= 450 ? DeckY(x) : DeckY(Mathf.Sign(x) * 450) - (Mathf.Abs(x) - 450) * 0.085f;
                    float cy = CableY(x);
                    if (cy - dy < 2) continue;
                    B(cableMat).Cylinder(new Vector3(x, dy + 0.3f, z), new Vector3(x, cy, z), 0.13f, 4, null, false);
                }
            }
            Flush("CentralBridge_Geo", g);
        }

        // ------------------------------------------------------------------ waterfront
        void Waterfront()
        {
            var g = new GameObject("Waterfront").transform;
            g.SetParent(root, false);
            // quay walls along the city shores
            void QuayLine(Vector2 a, Vector2 b, Vector3 seaward)
            {
                Vector3 pa = new(a.x, 0.45f, a.y), pb = new(b.x, 0.45f, b.y);
                QuadAuto(quayMat, pa + Vector3.down * 9f, pb + Vector3.down * 9f, pb, pa, seaward, 6, 6);
                Vector3 land = -seaward * 6f;
                QuadAuto(quayMat, pa, pb, pb + land, pa + land, Vector3.up, 6, 6);
                float l = Vector2.Distance(a, b);
                Vector3 d = (pb - pa) / l;
                for (float s = 6; s < l; s += 14)
                    B(darkMetal).Cylinder(pa + d * s + seaward * 0.4f, pa + d * s + seaward * 0.4f + Vector3.up * 0.8f, 0.32f, 8);
            }
            QuayLine(new Vector2(-560, -1400), new Vector2(-560, 320), Vector3.right);
            QuayLine(new Vector2(-600, -1560), new Vector2(-560, -1400), new Vector3(1, 0, -0.25f).normalized);
            QuayLine(new Vector2(560, -480), new Vector2(560, 520), Vector3.left);
            QuayLine(new Vector2(560, -480), new Vector2(2600, -480), Vector3.back);
            // hangar district piers with cranes and container stacks
            float[] piers = { -1420, -1220, -1020, -820, -620 };
            for (int pi = 0; pi < piers.Length; pi++)
            {
                float pz = piers[pi];
                float x0 = -560, x1 = -330, hz = 18;
                B(quayMat).Box(new Vector3(x0, -1.2f, pz - hz), new Vector3(x1, 0.45f, pz + hz), Tile(6, 6));
                AddBoxCollider(g, new Vector3(x0, -9f, pz - hz), new Vector3(x1, 0.45f, pz + hz), "Pier");
                for (float x = x0 + 8; x < x1; x += 14)
                    foreach (int s in new[] { -1, 1 })
                        B(concreteB).Cylinder(new Vector3(x, SeaFloor + 6, pz + s * (hz - 1.5f)), new Vector3(x, -1.2f, pz + s * (hz - 1.5f)), 0.9f, 8);
                for (float x = x0 + 6; x < x1; x += 16)
                    foreach (int s in new[] { -1, 1 })
                        B(darkMetal).Cylinder(new Vector3(x, 0.45f, pz + s * (hz - 0.6f)), new Vector3(x, 1.2f, pz + s * (hz - 0.6f)), 0.3f, 8);
                if (pi % 2 == 1)
                    for (int k = 0; k < 2; k++) Crane(new Vector3(-490 + k * 80, 0.45f, pz), g);
                // containers
                for (float x = x0 + 30; x < x1 - 20; x += 14)
                {
                    if (R() < 0.35f) continue;
                    int stack = rnd.Next(1, 5);
                    for (int lvl = 0; lvl < stack; lvl++)
                    {
                        float zz = pz + (R() < 0.5f ? -6 : 6);
                        B(Pick(containerMats)).BoxCentered(new Vector3(x, 0.45f + 1.3f + lvl * 2.62f, zz), new Vector3(12.2f, 2.6f, 2.45f), Tile(6, 2.6f));
                    }
                    AddBoxCollider(g, new Vector3(x - 6.1f, 0.45f, pz - 8), new Vector3(x + 6.1f, 0.45f + stack * 2.62f, pz + 8), "Containers");
                }
            }
            Flush("Waterfront_Geo", g);
        }

        void Crane(Vector3 p, Transform parent)
        {
            float h = 30, w = 9, d = 7;
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                {
                    Vector3 lp = p + new Vector3(sx * d, 0, sz * w);
                    B(craneMat).Box(lp + new Vector3(-0.7f, 0, -0.7f), lp + new Vector3(0.7f, h, 0.7f), Tile(2, 4));
                    AddBoxCollider(parent, lp + new Vector3(-0.7f, 0, -0.7f), lp + new Vector3(0.7f, h, 0.7f), "Crane");
                }
            foreach (int sz in new[] { -1, 1 })
            {
                B(craneMat).Box(p + new Vector3(-d - 0.7f, h, sz * w - 0.8f), p + new Vector3(d + 0.7f, h + 2, sz * w + 0.8f), Tile(4, 2));
                B(craneMat).Box(p + new Vector3(-d - 0.5f, 8, sz * w - 0.5f), p + new Vector3(d + 0.5f, 9, sz * w + 0.5f), Tile(4, 1));
            }
            // boom out over the water and back over the pier
            B(craneMat).Box(p + new Vector3(-24, h + 2, -w - 0.8f), p + new Vector3(48, h + 4, -w + 0.8f), Tile(4, 2));
            B(craneMat).Box(p + new Vector3(-24, h + 2, w - 0.8f), p + new Vector3(48, h + 4, w + 0.8f), Tile(4, 2));
            B(craneMat).Box(p + new Vector3(-6, h + 4, -w - 0.8f), p + new Vector3(4, h + 12, w + 0.8f), Tile(4, 4));
            B(shipGlass).Box(p + new Vector3(10, h - 3, -2), p + new Vector3(15, h + 2, 2), Tile(2, 2));
            B(darkMetal).Cylinder(p + new Vector3(30, h + 2, 0), p + new Vector3(30, h - 14, 0), 0.08f, 4, null, false);
            B(beacon).BoxCentered(p + new Vector3(48, h + 4.5f, 0), Vector3.one * 0.6f, Tile(1, 1));
        }

        void Ships()
        {
            var g = new GameObject("Ships").transform;
            g.SetParent(root, false);
            Ship(new Vector3(-440, 0, -1270), 0, 150, 24, true, g);
            Ship(new Vector3(-430, 0, -870), 0, 120, 20, true, g);
            Ship(new Vector3(-440, 0, -1470), 0, 130, 22, false, g);
            Ship(new Vector3(-80, 0, -1150), 25, 170, 26, true, g);
            Ship(new Vector3(320, 0, 760), -60, 150, 24, true, g);
            Ferry(new Vector3(150, 0, -320), 70, g);
            Ferry(new Vector3(-250, 0, 420), -20, g);
            Ferry(new Vector3(720, 0, -780), 110, g);
            Ferry(new Vector3(1900, 0, -680), 30, g);
            Flush("Ships_Geo", g);
        }

        void Hull(Matrix4x4 m, float len, float beam, float lo, float hi, Material mat)
        {
            // outline: square stern, pointed bow
            var pts = new List<Vector3>
            {
                new(-beam / 2, 0, -len / 2), new(beam / 2, 0, -len / 2), new(beam / 2, 0, len * 0.28f), new(beam * 0.3f, 0, len * 0.44f),
                new(0, 0, len / 2), new(-beam * 0.3f, 0, len * 0.44f), new(-beam / 2, 0, len * 0.28f),
            };
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 a = m.MultiplyPoint3x4(pts[i] + Vector3.up * lo), b = m.MultiplyPoint3x4(pts[(i + 1) % pts.Count] + Vector3.up * lo);
                Vector3 a2 = m.MultiplyPoint3x4(pts[i] + Vector3.up * hi), b2 = m.MultiplyPoint3x4(pts[(i + 1) % pts.Count] + Vector3.up * hi);
                Vector3 mid = (pts[i] + pts[(i + 1) % pts.Count]) * 0.5f;
                QuadAuto(mat, a, b, b2, a2, m.MultiplyVector(mid), 6, 6);
            }
            Vector3 c = m.MultiplyPoint3x4(new Vector3(0, hi, 0));
            for (int i = 0; i < pts.Count; i++)
                TriAuto(girder, c, m.MultiplyPoint3x4(pts[i] + Vector3.up * hi), m.MultiplyPoint3x4(pts[(i + 1) % pts.Count] + Vector3.up * hi), Vector3.up, 6);
        }

        void Ship(Vector3 pos, float yaw, float len, float beam, bool loaded, Transform parent)
        {
            var m = Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, 0), Vector3.one);
            Hull(m, len, beam, -7, 7, R() < 0.5f ? hullDark : hullRed);
            var rot = Quaternion.Euler(0, yaw, 0);
            // bridge house at the stern
            B(shipWhite).Box(new Vector3(-beam / 2 + 1, 7, -len / 2 + 4), new Vector3(beam / 2 - 1, 22, -len / 2 + 18), Tile(4, 4), 63, null, m);
            B(shipGlass).Box(new Vector3(-beam / 2 + 0.9f, 19, -len / 2 + 17), new Vector3(beam / 2 - 0.9f, 21, -len / 2 + 18.1f), Tile(4, 2), 63, null, m);
            B(hullRed).Box(new Vector3(-2, 22, -len / 2 + 7), new Vector3(2, 30, -len / 2 + 11), Tile(2, 2), 63, null, m);
            if (loaded)
                for (float z = -len / 2 + 24; z < len * 0.3f; z += 13)
                    for (float x = -beam / 2 + 2.5f; x < beam / 2 - 2; x += 2.6f)
                    {
                        int stack = rnd.Next(1, 5);
                        for (int l = 0; l < stack; l++)
                            B(Pick(containerMats)).Box(new Vector3(x - 1.2f, 7 + l * 2.6f, z), new Vector3(x + 1.2f, 9.5f + l * 2.6f, z + 12.2f), Tile(6, 2.6f), 63, null, m);
                    }
            var col = new GameObject("ShipCol");
            col.transform.SetParent(parent, false);
            col.transform.SetPositionAndRotation(pos, rot);
            var bc = col.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 4, 0);
            bc.size = new Vector3(beam, 22, len);
        }

        void Ferry(Vector3 pos, float yaw, Transform parent)
        {
            var m = Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, 0), Vector3.one);
            Hull(m, 58, 14, -3, 3, shipWhite);
            B(shipWhite).Box(new Vector3(-6, 3, -24), new Vector3(6, 7, 18), Tile(4, 4), 63, null, m);
            B(shipGlass).Box(new Vector3(-6.1f, 4, -22), new Vector3(6.1f, 6, 16), Tile(4, 2), 63, null, m);
            B(shipWhite).Box(new Vector3(-4.5f, 7, -14), new Vector3(4.5f, 10, 8), Tile(4, 4), 63, null, m);
            B(navyMat).Box(new Vector3(-1.2f, 10, -8), new Vector3(1.2f, 14, -5), Tile(2, 2), 63, null, m);
        }

        Material _navy;
        Material navyMat => _navy ? _navy : (_navy = Mats.LitMat("Navy", WorldTextures.Navy, 0.4f, 0.2f));

        void Lighthouse(Vector2 p, float h)
        {
            var g = new GameObject("Lighthouse").transform;
            g.SetParent(root, false);
            float y = H(p.x, p.y) - 1;
            Vector3 b = new(p.x, y, p.y);
            B(paintWhite).Cylinder(b, b + Vector3.up * h, 3.4f, 16, null, true, 2.4f);
            B(lighthouseRed).Cylinder(b + Vector3.up * h, b + Vector3.up * (h + 1.2f), 3.4f, 16);
            B(lantern).Cylinder(b + Vector3.up * (h + 1.2f), b + Vector3.up * (h + 4.2f), 2.0f, 12);
            B(lighthouseRed).Cylinder(b + Vector3.up * (h + 4.2f), b + Vector3.up * (h + 7f), 2.6f, 12, null, true, 0.2f);
            B(paintWhite).Box(b + new Vector3(4, 0, -4), b + new Vector3(12, 5, 4), Tile(4, 4));
            B(lighthouseRed).Box(b + new Vector3(3.6f, 5, -4.4f), b + new Vector3(12.4f, 5.6f, 4.4f), Tile(4, 4));
            AddBoxCollider(g, b + new Vector3(-3, 0, -3), b + new Vector3(3, h + 7, 3));
            AddLight(b + Vector3.up * (h + 2.7f), new Color(1f, 0.9f, 0.7f), 6f, 60f);
            Flush("Lighthouse_Geo", g);
        }

        // ------------------------------------------------------------------ arenas
        static Vector3 EP(Vector2 c, float rx, float rz, float a, float y) => new(c.x + Mathf.Cos(a) * rx, y, c.y + Mathf.Sin(a) * rz);
        static bool InGap(float a, float gapDeg, float halfDeg) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, gapDeg)) < halfDeg;

        /// <summary>Oval stadium. gapDeg: bearing of the entrance tunnel (0 = +x, 90 = +z).</summary>
        void Stadium(Vector2 c, float y, float fieldX, float fieldZ, float outerX, float outerZ, float standTop, float facadeH, float gapDeg, Material facadeMat, Material floorMat, Transform parent)
        {
            int segs = 96;
            float gapHalf = 7.5f;
            var standsCol = new MeshBuilder();
            float wallH = 4.5f;
            float ix = fieldX + 3, iz = fieldZ + 3;
            for (int k = 0; k < segs; k++)
            {
                float a0 = k / (float)segs * Mathf.PI * 2, a1 = (k + 1) / (float)segs * Mathf.PI * 2;
                float am = (a0 + a1) * 0.5f;
                bool gap = InGap(am, gapDeg, gapHalf);
                Vector3 outward = new(Mathf.Cos(am) / outerX, 0, Mathf.Sin(am) / outerZ);
                outward = outward.normalized;
                // field
                TriAuto(floorMat, new Vector3(c.x, y + 0.08f, c.y), EP(c, fieldX, fieldZ, a0, y + 0.08f), EP(c, fieldX, fieldZ, a1, y + 0.08f), Vector3.up, 8);
                QuadAuto(floorMat, EP(c, fieldX, fieldZ, a0, y + 0.08f), EP(c, fieldX, fieldZ, a1, y + 0.08f), EP(c, ix, iz, a1, y + 0.08f), EP(c, ix, iz, a0, y + 0.08f), Vector3.up, 8, 8);
                if (gap)
                {
                    // tunnel floor out to the plaza
                    QuadAuto(floorMat, EP(c, ix, iz, a0, y + 0.08f), EP(c, ix, iz, a1, y + 0.08f), EP(c, outerX + 10, outerZ + 10, a1, y + 0.08f), EP(c, outerX + 10, outerZ + 10, a0, y + 0.08f), Vector3.up, 8, 8);
                    continue;
                }
                // inner wall
                QuadAuto(stoneDark, EP(c, ix, iz, a0, y), EP(c, ix, iz, a1, y), EP(c, ix, iz, a1, y + wallH), EP(c, ix, iz, a0, y + wallH), -outward, 4, 4);
                if (k % 6 == 0) B(ledBlue).BoxCentered(EP(c, ix - 0.2f, iz - 0.2f, am, y + wallH - 0.4f), new Vector3(1.4f, 0.3f, 1.4f), Tile(1, 1));
                // stands
                Vector3 s00 = EP(c, ix, iz, a0, y + wallH), s01 = EP(c, ix, iz, a1, y + wallH);
                Vector3 s10 = EP(c, outerX - 6, outerZ - 6, a0, y + standTop), s11 = EP(c, outerX - 6, outerZ - 6, a1, y + standTop);
                float arc = a0 * (outerX + ix) * 0.5f;
                QuadAuto(crowdMat, s00, s01, s11, s10, -outward + Vector3.up, 51, 13, new Vector2(arc / 51, 0));
                standsCol.Quad(s01, s00, s10, s11, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                // back parapet + roof canopy
                Vector3 b0 = EP(c, outerX - 6, outerZ - 6, a0, y + standTop), b1 = EP(c, outerX - 6, outerZ - 6, a1, y + standTop);
                QuadAuto(stone, b0, b1, b1 + Vector3.up * (facadeH - standTop), b0 + Vector3.up * (facadeH - standTop), -outward, 4, 4);
                Vector3 r0 = EP(c, outerX + 1, outerZ + 1, a0, y + facadeH), r1 = EP(c, outerX + 1, outerZ + 1, a1, y + facadeH);
                Vector3 r2 = EP(c, outerX - 22, outerZ - 22, a1, y + facadeH + 3), r3 = EP(c, outerX - 22, outerZ - 22, a0, y + facadeH + 3);
                QuadAuto(girder, r0, r1, r2, r3, Vector3.up, 6, 6);
                QuadAuto(girder, r0, r1, r2, r3, Vector3.down, 6, 6);
                // outer facade
                QuadAuto(facadeMat, EP(c, outerX, outerZ, a0, y - 2), EP(c, outerX, outerZ, a1, y - 2), EP(c, outerX, outerZ, a1, y + facadeH), EP(c, outerX, outerZ, a0, y + facadeH), outward, 32, 32, new Vector2(a0 * (outerX + outerZ) * 0.5f / 32, -2 / 32f));
                QuadAuto(stone, EP(c, outerX, outerZ, a0, y + facadeH), EP(c, outerX, outerZ, a1, y + facadeH), EP(c, outerX - 6, outerZ - 6, a1, y + facadeH), EP(c, outerX - 6, outerZ - 6, a0, y + facadeH), Vector3.up, 4, 4);
                // colliders: facade and inner wall
                foreach (var (rx, rz, hgtC) in new[] { (outerX, outerZ, facadeH), (ix, iz, wallH) })
                {
                    Vector3 p0 = EP(c, rx, rz, a0, y), p1 = EP(c, rx, rz, a1, y);
                    var rot = Quaternion.LookRotation(outward);
                    var go = new GameObject("Wall");
                    go.transform.SetParent(parent, false);
                    go.transform.SetPositionAndRotation((p0 + p1) * 0.5f + Vector3.up * hgtC * 0.5f, rot);
                    go.AddComponent<BoxCollider>().size = new Vector3(Vector3.Distance(p0, p1) + 0.8f, hgtC, 1.5f);
                }
            }
            // tunnel portal
            {
                float a = gapDeg * Mathf.Deg2Rad;
                Vector3 p = EP(c, outerX, outerZ, a, y);
                Vector3 outN = new Vector3(Mathf.Cos(a) / outerX, 0, Mathf.Sin(a) / outerZ).normalized;
                Vector3 tan = Vector3.Cross(Vector3.up, outN);
                float hw = Mathf.Sin(gapHalf * Mathf.Deg2Rad) * (outerX + outerZ) * 0.5f;
                QuadAuto(stone, p - tan * hw + Vector3.up * 14, p + tan * hw + Vector3.up * 14, p + tan * hw + Vector3.up * facadeH, p - tan * hw + Vector3.up * facadeH, outN, 6, 6);
                B(stone).BoxCentered(p + Vector3.up * 14.5f, new Vector3(Mathf.Abs(tan.x) * hw * 2 + Mathf.Abs(outN.x) * 3, 1.5f, Mathf.Abs(tan.z) * hw * 2 + Mathf.Abs(outN.z) * 3), Tile(4, 2));
                var lm = StrokeFont.TubeMesh("GATE A", 2.6f, 0.15f);
                float w = lm.bounds.size.x;
                B(lampHead).Append(lm, Matrix4x4.TRS(p + outN * 0.6f + Vector3.up * 16 - tan * w * 0.5f, Quaternion.LookRotation(-outN), Vector3.one));
                Object.DestroyImmediate(lm);
            }
            var mesh = standsCol.ToMesh("StandsCollider");
            SaveMesh(mesh);
            var sc = new GameObject("StandsCollider");
            sc.transform.SetParent(parent, false);
            sc.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void GroundArena(Vector3 at)
        {
            var g = new GameObject("GroundArena").transform;
            g.SetParent(root, false);
            var c = new Vector2(at.x, at.z);
            float y = H(c.x, c.y) + 0.05f;
            Stadium(c, y, 92, 70, 160, 132, 30, 38, -90, archFacade, fieldSand, g);
            // flags around the rim
            for (int k = 0; k < 28; k++)
            {
                float a = k / 28f * Mathf.PI * 2;
                if (InGap(a, -90, 10)) continue;
                Vector3 p = EP(c, 158, 130, a, y + 38);
                B(metal).Cylinder(p, p + Vector3.up * 9, 0.12f, 4);
                Vector3 t = new(-Mathf.Sin(a), 0, Mathf.Cos(a));
                var fm = k % 2 == 0 ? bannerMat : navyMat;
                QuadAuto(fm, p + Vector3.up * 5.5f, p + Vector3.up * 5.5f + t * 3.4f, p + Vector3.up * 8.8f + t * 3.4f, p + Vector3.up * 8.8f, Vector3.Cross(t, Vector3.up), 3.4f, 3.3f);
                QuadAuto(fm, p + Vector3.up * 5.5f, p + Vector3.up * 5.5f + t * 3.4f, p + Vector3.up * 8.8f + t * 3.4f, p + Vector3.up * 8.8f, -Vector3.Cross(t, Vector3.up), 3.4f, 3.3f);
            }
            // giant posters on the facade
            for (int k = 0; k < 6; k++)
            {
                float a = (30 + k * 60) * Mathf.Deg2Rad;
                if (InGap(a, -90, 14)) continue;
                Vector3 p = EP(c, 161, 133, a, y + 4);
                Vector3 outN = new Vector3(Mathf.Cos(a) / 160, 0, Mathf.Sin(a) / 132).normalized;
                Vector3 t = Vector3.Cross(Vector3.up, outN);
                QuadAuto(posters[k % 3], p - t * 9, p + t * 9, p + t * 9 + Vector3.up * 30, p - t * 9 + Vector3.up * 30, outN, 18, 30);
            }
            FloodMasts(c, y, 190, 160, g);
            Flush("GroundArena_Geo", g);
        }

        void FloodMasts(Vector2 c, float y, float rx, float rz, Transform parent)
        {
            foreach (float deg in new[] { 45f, 135f, 225f, 315f })
            {
                float a = deg * Mathf.Deg2Rad;
                Vector3 p = EP(c, rx, rz, a, y);
                B(metal).Cylinder(p, p + Vector3.up * 62, 1.1f, 8, null, true, 0.6f);
                Vector3 inward = (new Vector3(c.x, y, c.y) - p).normalized;
                Vector3 t = Vector3.Cross(Vector3.up, inward);
                B(darkMetal).BoxCentered(p + Vector3.up * 63, Abs(t) * 12 + Abs(inward) * 1.5f + Vector3.up * 6, Tile(2, 2));
                for (int i = -2; i <= 2; i++)
                    for (int j = 0; j < 2; j++)
                        B(lampHead).BoxCentered(p + Vector3.up * (61.5f + j * 3) + t * i * 2.3f + inward * 0.9f, Abs(t) * 1.8f + Abs(inward) * 0.3f + Vector3.up * 1.8f, Tile(1, 1));
                AddLight(p + Vector3.up * 58 + inward * 30, new Color(1f, 0.95f, 0.85f), 12f, 160f);
                AddBoxCollider(parent, p + new Vector3(-1, 0, -1), p + new Vector3(1, 62, 1), "Mast");
            }
        }

        void ChampionshipArena(Vector3 at)
        {
            var g = new GameObject("ChampionshipArena").transform;
            g.SetParent(root, false);
            var c = new Vector2(at.x, at.z);
            float y = H(c.x, c.y) + 0.05f;
            Stadium(c, y, 112, 92, 178, 152, 36, 44, 180, stone, fieldFloor, g);
            // arena floor markings
            for (int k = 0; k < 64; k++)
            {
                float a0 = k / 64f * Mathf.PI * 2, a1 = (k + 1) / 64f * Mathf.PI * 2;
                QuadAuto(ledBlue, EP(c, 40, 40, a0, y + 0.12f), EP(c, 40, 40, a1, y + 0.12f), EP(c, 41.5f, 41.5f, a1, y + 0.12f), EP(c, 41.5f, 41.5f, a0, y + 0.12f), Vector3.up, 4, 4);
            }
            // LED crown band and screens around the facade
            for (int k = 0; k < 96; k++)
            {
                float a0 = k / 96f * Mathf.PI * 2, a1 = (k + 1) / 96f * Mathf.PI * 2;
                if (InGap((a0 + a1) * 0.5f, 180, 7.5f)) continue;
                Vector3 n = new Vector3(Mathf.Cos(a0) / 178, 0, Mathf.Sin(a0) / 152).normalized;
                QuadAuto(ledBlue, EP(c, 178.3f, 152.3f, a0, y + 40), EP(c, 178.3f, 152.3f, a1, y + 40), EP(c, 178.3f, 152.3f, a1, y + 41.2f), EP(c, 178.3f, 152.3f, a0, y + 41.2f), n, 4, 4);
            }
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60 + 30) * Mathf.Deg2Rad;
                if (InGap(a, 180, 20)) continue;
                Vector3 p = EP(c, 179, 153, a, y + 8);
                Vector3 outN = new Vector3(Mathf.Cos(a) / 178, 0, Mathf.Sin(a) / 152).normalized;
                Vector3 t = Vector3.Cross(Vector3.up, outN);
                QuadAuto(k % 2 == 0 ? screens[0] : posters[1], p - t * 22, p + t * 22, p + t * 22 + Vector3.up * 24, p - t * 22 + Vector3.up * 24, outN, 44, 24);
            }
            // six sweeping fins rising over the bowl
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60 + 0) * Mathf.Deg2Rad;
                if (InGap(a, 180, 12)) a += 20 * Mathf.Deg2Rad;
                Fin(EP(c, 172, 147, a, y), new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), 118);
            }
            FloodMasts(c, y, 215, 188, g);
            // spectators' promenade toward the water with banners
            Flush("ChampionshipArena_Geo", g);
        }

        void Fin(Vector3 basePos, Vector3 outward, float height)
        {
            Vector3 tan = Vector3.Cross(Vector3.up, outward);
            int n = 10;
            var cs = new Vector3[n + 1];
            var fw = new Vector3[n + 1];
            var ws = new float[n + 1];
            var ts = new float[n + 1];
            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n;
                cs[k] = basePos + Vector3.up * height * t + outward * (-Mathf.Sin(t * Mathf.PI) * 16f + t * t * 26f);
                ws[k] = Mathf.Lerp(16f, 3.5f, t);
                ts[k] = Mathf.Lerp(5f, 1.5f, t);
            }
            for (int k = 0; k <= n; k++)
            {
                Vector3 d = k < n ? cs[k + 1] - cs[k] : cs[k] - cs[k - 1];
                fw[k] = Vector3.Cross(tan, d.normalized).normalized; // thickness axis
            }
            for (int k = 0; k < n; k++)
            {
                Vector3 A(int i, float sw, float st) => cs[i] + tan * ws[i] * 0.5f * sw + fw[i] * ts[i] * 0.5f * st;
                foreach (var (sw, st, nrm) in new[] { (1f, 0f, tan), (-1f, 0f, -tan), (0f, 1f, fw[k]), (0f, -1f, -fw[k]) })
                {
                    Vector3 a, b, c2, d2;
                    if (sw != 0) { a = A(k, sw, -1); b = A(k, sw, 1); c2 = A(k + 1, sw, 1); d2 = A(k + 1, sw, -1); }
                    else { a = A(k, -1, st); b = A(k, 1, st); c2 = A(k + 1, 1, st); d2 = A(k + 1, -1, st); }
                    QuadAuto(stone, a, b, c2, d2, nrm, 6, 6);
                }
            }
            B(beacon).BoxCentered(cs[n] + Vector3.up * 0.8f, Vector3.one * 1.4f, Tile(1, 1));
            B(ledBlue).Cylinder(cs[0] + fw[0] * 2.6f, cs[n / 2] + fw[n / 2] * 1.8f, 0.25f, 4, null, false);
        }

        // ------------------------------------------------------------------ flight circuit
        void FlightCircuit()
        {
            var g = new GameObject("FlightCircuit").transform;
            g.SetParent(root, false);
            float y = H(1950, 1520);
            // apron
            QuadAuto(asphalt, new Vector3(1450, y + 0.1f, 1380), new Vector3(2450, y + 0.1f, 1380), new Vector3(2450, y + 0.1f, 1505), new Vector3(1450, y + 0.1f, 1505), Vector3.up, 8, 8);
            // control tower
            Vector3 tp = new(1700, y, 1430);
            B(concreteA).Cylinder(tp, tp + Vector3.up * 44, 4.2f, 16, null, true, 3.2f);
            B(shipGlass).Cylinder(tp + Vector3.up * 44, tp + Vector3.up * 50, 7.5f, 12, null, true, 8.5f);
            B(metal).Cylinder(tp + Vector3.up * 50, tp + Vector3.up * 51.5f, 8.8f, 12);
            B(metal).Cylinder(tp + Vector3.up * 51.5f, tp + Vector3.up * 62f, 0.3f, 6);
            B(beacon).BoxCentered(tp + Vector3.up * 62.3f, Vector3.one * 0.8f, Tile(1, 1));
            AddBoxCollider(g, tp + new Vector3(-4, 0, -4), tp + new Vector3(4, 51, 4), "Tower");
            // hangars facing the runway
            for (int k = 0; k < 3; k++)
            {
                float x = 1880 + k * 150;
                Hangar(Rect.MinMaxRect(x - 45, 1385, x + 45, 1470), 3, $"F{k + 1}", g);
            }
            // grandstand on the north side, looking over the runway to the water
            var stand = Rect.MinMaxRect(1700, 1590, 2200, 1640);
            for (int r = 0; r < 12; r++)
            {
                float z = stand.yMin + r * 3.4f, h = 1.5f + r * 1.4f;
                B(concreteA).Box(new Vector3(stand.xMin, y, z), new Vector3(stand.xMax, y + h, z + 3.4f), Tile(8, 2), 63 & ~8);
                QuadAuto(crowdMat, new Vector3(stand.xMin, y + h + 0.02f, z), new Vector3(stand.xMax, y + h + 0.02f, z), new Vector3(stand.xMax, y + h + 1.4f, z + 3.4f), new Vector3(stand.xMin, y + h + 1.4f, z + 3.4f), new Vector3(0, 1, -0.5f), 51, 13, new Vector2(0, r * 0.25f));
            }
            AddBoxCollider(g, new Vector3(stand.xMin, y, stand.yMin), new Vector3(stand.xMax, y + 18, stand.yMax), "Grandstand");
            for (float x = stand.xMin + 20; x < stand.xMax; x += 40) PoleBanner(new Vector3(x, y, stand.yMin - 3), Vector3.right, Vector3.back);
            // racing pylons out over the water, a slalom from west to east
            for (int k = 0; k < 18; k++)
            {
                float x = 1420 + k * 70;
                float z = 1880 + (k % 2 == 0 ? -30 : 30) + Mathf.Sin(k * 0.6f) * 40;
                Vector3 p = new(x, WaterLevel - 0.5f, z);
                B(pylonMat).Cylinder(p, p + Vector3.up * 34, 2.3f, 16, null, true, 1.2f);
                B(Mats.Neon("BeaconRedSolid", new Color(1f, 0.12f, 0.05f) * 5f)).Cylinder(p + Vector3.up * 34, p + Vector3.up * 37, 1.2f, 12, null, true, 0.1f);
                B(concreteB).Cylinder(p + Vector3.down * 2, p + Vector3.up * 0.6f, 4f, 16);
                AddBoxCollider(g, p + new Vector3(-2, -2, -2), p + new Vector3(2, 37, 2), "Pylon");
            }
            Flush("FlightCircuit_Geo", g);
        }

        // ------------------------------------------------------------------ trees
        void AddTree(Vector3 p, int proto, float scale)
        {
            treeWorld.Add(p);
            treeProto.Add(proto);
            trees.Add(new TreeInstance
            {
                prototypeIndex = proto, widthScale = scale * R(0.85f, 1.15f), heightScale = scale * R(0.85f, 1.2f),
                color = Color.Lerp(new Color(0.85f, 0.9f, 0.8f), Color.white, R()), lightmapColor = Color.white, rotation = R(0, Mathf.PI * 2),
            });
        }

        void NatureTrees()
        {
            // hills and mountain foothills: clumps where it's green, not too steep, away from streets
            var rng = new System.Random(77);
            for (int k = 0; k < 60000 && trees.Count < 26000; k++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * (Half - 60), z = (float)(rng.NextDouble() * 2 - 1) * (Half - 60);
                float h = H(x, z);
                if (h < 2f || h > 380f) continue;
                if (CoastDist(x, z) < 25) continue;
                float gx = GX(x), gz = GX(z);
                if (roadW[(int)gz, (int)gx] > 0.05f) continue;
                if (InUrbanGrid(x, z)) continue;
                float slope = Slope(x, z);
                if (slope > 0.75f) continue;
                float clump = Mathf.PerlinNoise(x * 0.006f + 3, z * 0.006f + 7);
                if (clump < 0.45f) continue;
                int proto = h > 120 || (x < -1900) || (z > 1500 && x < 0) ? 1 : (rng.NextDouble() < 0.25 ? 1 : 0);
                AddTree(new Vector3(x, 0, z), proto, R(0.8f, 1.4f) * (proto == 1 ? 1.2f : 1f));
            }
        }

        float Slope(float x, float z)
        {
            float dx = (H(x + 5, z) - H(x - 5, z)) / 10f, dz = (H(x, z + 5) - H(x, z - 5)) / 10f;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static bool InUrbanGrid(float x, float z)
        {
            if (x > -1990 && x < -590 && z > -1530 && z < 980) return true;
            if (x > 590 && x < 2230 && z > -430 && z < 1130) return true;
            if (x > 870 && x < 1300 && z > -1580 && z < -900) return true;
            foreach (var p in Pads) if (Mathf.Abs(x - p.c.x) < p.half.x + 20 && Mathf.Abs(z - p.c.y) < p.half.y + 20) return true;
            return false;
        }

        GameObject TreePrefab(string name, bool conifer)
        {
            var bk = new MeshBuilder();
            var lf = new MeshBuilder();
            var rng = new System.Random(conifer ? 5 : 6);
            float Rr(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            if (conifer)
            {
                bk.Cylinder(Vector3.zero, Vector3.up * 4f, 0.32f, 6, null, false, 0.2f);
                lf.Cylinder(Vector3.up * 1.6f, Vector3.up * 8.5f, 3.0f, 8, null, true, 0.05f);
                lf.Cylinder(Vector3.up * 4.5f, Vector3.up * 11f, 2.3f, 8, null, true, 0.05f);
                lf.Cylinder(Vector3.up * 7.5f, Vector3.up * 13.5f, 1.5f, 7, null, true, 0.02f);
            }
            else
            {
                bk.Cylinder(Vector3.zero, Vector3.up * 4.8f, 0.3f, 6, null, false, 0.18f);
                bk.Cylinder(Vector3.up * 3.2f, new Vector3(1.4f, 5.6f, 0.4f), 0.13f, 4, null, false);
                bk.Cylinder(Vector3.up * 3.6f, new Vector3(-1.1f, 6f, -0.6f), 0.13f, 4, null, false);
                var blobs = new[] { (new Vector3(0, 6.2f, 0), 2.9f), (new Vector3(1.5f, 5.6f, 0.6f), 2.1f), (new Vector3(-1.3f, 5.8f, -0.8f), 2.2f), (new Vector3(0.2f, 7.6f, -0.3f), 1.9f) };
                foreach (var (bc, br) in blobs)
                {
                    int seg = 7, rings = 5;
                    var pts = new Vector3[rings + 1, seg + 1];
                    for (int i = 0; i <= rings; i++)
                        for (int j = 0; j <= seg; j++)
                        {
                            float ph = -Mathf.PI / 2 + Mathf.PI * i / rings, th = Mathf.PI * 2 * j / seg;
                            float jit = (i == 0 || i == rings || j == seg) ? 1f : Rr(0.85f, 1.12f);
                            pts[i, j] = bc + new Vector3(Mathf.Cos(ph) * Mathf.Cos(th), Mathf.Sin(ph) * 0.85f, Mathf.Cos(ph) * Mathf.Sin(th)) * br * jit;
                        }
                    for (int j = 0; j <= rings; j++) pts[j, seg] = pts[j, 0];
                    for (int i = 0; i < rings; i++)
                        for (int j = 0; j < seg; j++)
                        {
                            Vector3 a = pts[i, j], b = pts[i, j + 1], c = pts[i + 1, j + 1], d = pts[i + 1, j];
                            Vector3 outN = (a + c) * 0.5f - bc;
                            if (Vector3.Dot(Vector3.Cross(d - a, b - a), outN) < 0) { (a, b) = (b, a); (c, d) = (d, c); }
                            if ((a - b).sqrMagnitude < 1e-6f) lf.Tri(a, d, c, Vector2.zero, Vector2.up, Vector2.one);
                            else if ((c - d).sqrMagnitude < 1e-6f) lf.Tri(a, c, b, Vector2.zero, Vector2.one, Vector2.right);
                            else lf.Quad(a, b, c, d, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                        }
                }
            }
            var mesh = new Mesh { name = name };
            var verts = new List<Vector3>(bk.v); verts.AddRange(lf.v);
            var norms = new List<Vector3>(bk.n); norms.AddRange(lf.n);
            var uvs = new List<Vector2>(bk.uv); uvs.AddRange(lf.uv);
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(bk.t, 0);
            var lt = new List<int>(); foreach (int i in lf.t) lt.Add(i + bk.v.Count);
            mesh.SetTriangles(lt, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            SaveMesh(mesh);
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, conifer ? leavesDark : leaves };
            string path = $"Assets/AU/Generated/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------ terrain
        void BuildTerrain()
        {
            int n = HeightRes;
            var td = new TerrainData { heightmapResolution = n };
            td.size = new Vector3(2 * Half, MaxY - MinY, 2 * Half);
            var h01 = new float[n, n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    h01[j, i] = Mathf.Clamp01((hgt[j, i] - MinY) / (MaxY - MinY));
            td.SetHeights(0, 0, h01);

            // layers: grass, rock, sand, urban paving, dirt
            var kinds = new[] { WorldTextures.GroundKind.Grass, WorldTextures.GroundKind.Rock, WorldTextures.GroundKind.Sand, WorldTextures.GroundKind.Urban, WorldTextures.GroundKind.Dirt };
            var tiles = new[] { 9f, 22f, 10f, 12f, 8f };
            var layers = new TerrainLayer[kinds.Length];
            for (int k = 0; k < kinds.Length; k++)
            {
                var (alb, nrm) = WorldTextures.Ground(kinds[k]);
                string lp = $"Assets/AU/Generated/TL_{kinds[k]}.terrainlayer";
                if (File.Exists(lp)) AssetDatabase.DeleteAsset(lp);
                var l = new TerrainLayer { diffuseTexture = alb, normalMapTexture = nrm, tileSize = new Vector2(tiles[k], tiles[k]), normalScale = 0.8f, smoothness = k == 3 ? 0.25f : 0.05f };
                AssetDatabase.CreateAsset(l, lp);
                layers[k] = l;
            }
            td.terrainLayers = layers;
            int ar = 1024;
            td.alphamapResolution = ar;
            var alpha = new float[ar, ar, kinds.Length];
            for (int j = 0; j < ar; j++)
                for (int i = 0; i < ar; i++)
                {
                    float x = -Half + (i + 0.5f) / ar * 2 * Half, z = -Half + (j + 0.5f) / ar * 2 * Half;
                    float h = H(x, z);
                    float slope = Slope(x, z);
                    float gx = GX(x), gz = GX(z);
                    float rw = roadW[Mathf.Clamp((int)gz, 0, n - 1), Mathf.Clamp((int)gx, 0, n - 1)];
                    bool urban = InUrbanGrid(x, z);
                    float rock = Mathf.Max(S01((slope - 0.45f) / 0.35f), S01((h - 300f) / 140f));
                    float sand = h < 1.6f && !urban && CoastDist(x, z) < 30f ? 1f : 0f;
                    float pav = Mathf.Max(urban ? 0.85f : 0f, S01(rw * 1.5f) * 0.9f);
                    float dirt = Mathf.Clamp01(Mathf.PerlinNoise(x * 0.01f + 50, z * 0.01f) * 1.6f - 0.95f) * (1 - pav);
                    float grass = Mathf.Max(0, 1 - rock - sand - pav - dirt);
                    float sum = grass + rock + sand + pav + dirt + 1e-4f;
                    alpha[j, i, 0] = grass / sum; alpha[j, i, 1] = rock / sum; alpha[j, i, 2] = sand / sum; alpha[j, i, 3] = pav / sum; alpha[j, i, 4] = dirt / sum;
                }
            td.SetAlphamaps(0, 0, alpha);

            treeBroad = TreePrefab("TreeBroadleaf", false);
            treeConifer = TreePrefab("TreeConifer", true);
            td.treePrototypes = new[] { new TreePrototype { prefab = treeBroad }, new TreePrototype { prefab = treeConifer } };
            for (int k = 0; k < trees.Count; k++)
            {
                var t = trees[k];
                Vector3 w = treeWorld[k];
                t.position = new Vector3((w.x + Half) / (2 * Half), (H(w.x, w.z) - MinY) / (MaxY - MinY), (w.z + Half) / (2 * Half));
                trees[k] = t;
            }
            td.SetTreeInstances(trees.ToArray(), false);

            if (File.Exists(TerrainPath)) AssetDatabase.DeleteAsset(TerrainPath);
            AssetDatabase.CreateAsset(td, TerrainPath);
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(-Half, MinY, -Half);
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = Mats.Custom("TerrainLit", Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            terrain.heightmapPixelError = 3;
            terrain.basemapDistance = 900;
            terrain.treeDistance = 1400;
            terrain.treeBillboardDistance = 1400;
            terrain.treeMaximumFullLODCount = 30000;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        void BuildWater()
        {
            var g = new GameObject("Water").transform;
            g.SetParent(root, false);
            float ext = 12000f;
            int n = 48;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x0 = Mathf.Lerp(-ext, ext, i / (float)n), x1 = Mathf.Lerp(-ext, ext, (i + 1) / (float)n);
                    float z0 = Mathf.Lerp(-ext, ext, j / (float)n), z1 = Mathf.Lerp(-ext, ext, (j + 1) / (float)n);
                    B(water).Quad(new Vector3(x0, WaterLevel, z0), new Vector3(x1, WaterLevel, z0), new Vector3(x1, WaterLevel, z1), new Vector3(x0, WaterLevel, z1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
            Flush("Water_Geo", g);
            foreach (var mr in g.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // shallow bay floor: mechs wade instead of sinking
            var floor = new GameObject("BayFloor");
            floor.transform.SetParent(g, false);
            var bc = floor.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, WaterLevel - 2.6f - 20f, 0);
            bc.size = new Vector3(ext * 2, 40f, ext * 2);
        }

        void Backdrop()
        {
            var g = new GameObject("Backdrop").transform;
            g.SetParent(root, false);
            int segs = 160;
            float r0 = 6800, r1 = 8200;
            for (int k = 0; k < segs; k++)
            {
                float a0 = k / (float)segs * Mathf.PI * 2, a1 = (k + 1) / (float)segs * Mathf.PI * 2;
                float H0 = Peak(a0), H1 = Peak(a1);
                Vector3 p0 = new(Mathf.Cos(a0) * r0, WaterLevel, Mathf.Sin(a0) * r0), p1 = new(Mathf.Cos(a1) * r0, WaterLevel, Mathf.Sin(a1) * r0);
                Vector3 q0 = new(Mathf.Cos(a0) * r1, WaterLevel + H0, Mathf.Sin(a0) * r1), q1 = new(Mathf.Cos(a1) * r1, WaterLevel + H1, Mathf.Sin(a1) * r1);
                Vector3 inward = -new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                QuadAuto(backdrop, p0, p1, q1, q0, inward + Vector3.up * 0.5f, 400, 400);
            }
            Flush("Backdrop_Geo", g);
            foreach (var mr in g.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static float Peak(float a)
        {
            // bearing from north; mountains everywhere except the open sea to the south-east
            float x = Mathf.Cos(a), z = Mathf.Sin(a);
            float open = S01((-z - 0.2f) / 0.5f) * S01((x + 0.3f) / 0.6f);
            float n = Mathf.PerlinNoise(Mathf.Cos(a) * 3 + 10, Mathf.Sin(a) * 3 + 10) * 0.65f + Mathf.PerlinNoise(Mathf.Cos(a) * 9 + 3, Mathf.Sin(a) * 9 + 1) * 0.35f;
            return Mathf.Lerp(380f, 1500f, n) * (1 - open * 0.92f);
        }

        void Bounds()
        {
            var g = new GameObject("WorldBounds").transform;
            g.SetParent(root, false);
            float e = Half - 8;
            AddBoxCollider(g, new Vector3(-e - 4, -100, -e), new Vector3(-e, 1200, e), "Limit");
            AddBoxCollider(g, new Vector3(e, -100, -e), new Vector3(e + 4, 1200, e), "Limit");
            AddBoxCollider(g, new Vector3(-e, -100, -e - 4), new Vector3(e, 1200, -e), "Limit");
            AddBoxCollider(g, new Vector3(-e, -100, e), new Vector3(e, 1200, e + 4), "Limit");
        }

        // ------------------------------------------------------------------ map
        void WorldMap()
        {
            const int S = 1024;
            var px = new Color[S * S];
            for (int j = 0; j < S; j++)
                for (int i = 0; i < S; i++)
                {
                    float x = -Half + (i + 0.5f) / S * 2 * Half, z = -Half + (j + 0.5f) / S * 2 * Half;
                    float h = H(x, z);
                    Color c;
                    if (h < WaterLevel + 0.2f) c = new Color(0.05f, 0.1f, 0.19f, 1f);
                    else
                    {
                        float shade = Mathf.Clamp01(0.5f + (H(x - 6, z + 6) - H(x + 6, z - 6)) * 0.08f);
                        c = Color.Lerp(new Color(0.12f, 0.15f, 0.17f), new Color(0.22f, 0.25f, 0.27f), shade);
                        c = Color.Lerp(c, new Color(0.3f, 0.3f, 0.31f), Mathf.Clamp01(h / 500f));
                        if (InUrbanGrid(x, z)) c = Color.Lerp(c, new Color(0.17f, 0.19f, 0.23f), 0.6f);
                    }
                    px[j * S + i] = c;
                }
            foreach (var e in Edges)
            {
                Vector2 a = Nodes[e.a].p, b = Nodes[e.b].p;
                float wpx = Mathf.Max(1.6f, e.w / (2 * Half) * S * 0.75f);
                Color rc = IsElevated(e) ? new Color(0.95f, 0.85f, 0.6f) : new Color(0.62f, 0.68f, 0.78f);
                float len = Vector2.Distance(a, b);
                for (float s = 0; s <= len; s += 2f)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / len);
                    float ci = (p.x + Half) / (2 * Half) * S, cj = (p.y + Half) / (2 * Half) * S;
                    for (int dj = -3; dj <= 3; dj++)
                        for (int di = -3; di <= 3; di++)
                        {
                            int ii = (int)ci + di, jj = (int)cj + dj;
                            if (ii < 0 || jj < 0 || ii >= S || jj >= S) continue;
                            float dd = Mathf.Sqrt(di * di + dj * dj);
                            float k = Mathf.Clamp01(wpx * 0.5f + 0.5f - dd);
                            if (k > 0) px[jj * S + ii] = Color.Lerp(px[jj * S + ii], rc, k);
                        }
                }
            }
            var tex = TextureGen.Save("WorldMap", px, S, S, mips: false, wrap: TextureWrapMode.Clamp);
            var ga = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/AU/Resources/GameAssets.asset");
            if (ga) { ga.worldMap = tex; EditorUtility.SetDirty(ga); }
        }
    }
}
