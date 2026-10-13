using System.Collections.Generic;
using UnityEngine;

namespace AU
{
    /// <summary>
    /// The plan of the bay city, shared by the editor world generator and runtime
    /// systems (routes, minimap, spawn points). X runs east, Z runs north, meters.
    ///
    /// A bay splits the city. The west shore has the Hangar District and its docks,
    /// with the Old Town climbing the hills behind it to the Ground Arena. The
    /// Central Bridge crosses to Downtown on the east shore. The Flight Circuit sits
    /// on a peninsula to the northeast, and the Championship Arena on the southern
    /// island, joined to Downtown by a causeway. Mountains ring the west and north.
    /// </summary>
    public static class CityLayout
    {
        public const float Half = 2600f;        // terrain covers [-Half, Half] on both axes
        public const float WaterLevel = -3f;
        public const float SeaFloor = -24f;
        public const float Sidewalk = 5f;
        public const float BridgeDeck = 34f;    // deck height of the Central Bridge
        public const float BridgeZ = 50f;
        public const float BridgeTower = 300f;  // |x| of the two bridge towers

        // ------------------------------------------------------------------ coastlines
        public static readonly Vector2[] WestShore =
        {
            new(-2900, -1650), new(-760, -1650), new(-600, -1560), new(-560, -1400), new(-560, 320),
            new(-640, 660), new(-840, 1050), new(-990, 1420), new(-1040, 1800), new(-980, 2900), new(-2900, 2900),
        };
        public static readonly Vector2[] EastShore =
        {
            new(560, -480), new(560, 520), new(640, 820), new(820, 1080), new(1080, 1190), new(1300, 1270),
            new(1320, 1720), new(2900, 1760), new(2900, -480),
        };
        public static readonly Vector2[] Island =
        {
            new(850, -1000), new(1150, -880), new(1500, -900), new(1780, -1060), new(1880, -1330),
            new(1720, -1610), new(1360, -1700), new(1000, -1600), new(820, -1360),
        };
        public static readonly Vector2[] Islet = Ellipse(new Vector2(60, -640), 85, 70, 14, 7);
        public static readonly Vector2[] IsletEast = Ellipse(new Vector2(2080, -820), 110, 80, 14, 11);
        public static readonly Vector2[][] Land = { WestShore, EastShore, Island, Islet, IsletEast };

        static Vector2[] Ellipse(Vector2 c, float rx, float rz, int n, int seed)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2;
                float j = 0.8f + 0.35f * Mathf.PerlinNoise(seed * 3.1f + i * 0.7f, seed);
                p[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz) * j;
            }
            return p;
        }

        /// <summary>Signed distance to the nearest coastline: positive on land, negative over water.</summary>
        public static float CoastDist(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = -1e9f;
            foreach (var poly in Land)
            {
                bool inside = false;
                float d = 1e9f;
                for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                {
                    Vector2 a = poly[j], b = poly[i];
                    if (((a.y > z) != (b.y > z)) && (x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x)) inside = !inside;
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    d = Mathf.Min(d, (a + ab * t - p).sqrMagnitude);
                }
                d = Mathf.Sqrt(d);
                best = Mathf.Max(best, inside ? d : -d);
            }
            return best;
        }

        // ------------------------------------------------------------------ terrain shape
        public struct Pad { public Vector2 c, half; public float blend; }
        /// <summary>Flat building pads for the venues (rectangles, half extents).</summary>
        public static readonly Pad[] Pads =
        {
            new Pad { c = new Vector2(-1510, 1250), half = new Vector2(190, 170), blend = 90 },  // ground arena
            new Pad { c = new Vector2(1500, -1300), half = new Vector2(210, 200), blend = 70 },  // championship arena
            new Pad { c = new Vector2(1950, 1520), half = new Vector2(700, 170), blend = 90 },   // flight circuit
        };

        static float S01(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        static float Hill(float x, float z, float cx, float cz, float r, float h)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / r;
            return d >= 1 ? 0 : h * S01(1 - d);
        }

        static float Fbm(float x, float z, int oct)
        {
            float s = 0, a = 0.5f, f = 1, norm = 0;
            for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(x * f + 31.7f * i, z * f - 17.3f * i); norm += a; a *= 0.5f; f *= 2.03f; }
            return s / norm;
        }

        /// <summary>Natural ground height before roads are cut in (same in editor and game).</summary>
        public static float RawHeight(float x, float z)
        {
            float sd = CoastDist(x, z);
            if (sd < 0) return Mathf.Lerp(WaterLevel - 1.5f, SeaFloor, S01(-sd / 90f));
            float h = Mathf.Lerp(WaterLevel + 1.2f, 0f, S01(sd / 22f));
            float land = S01(sd / 60f);
            // gentle swell
            h += land * (Fbm(x * 0.004f, z * 0.004f, 3) - 0.45f) * 3f;
            // hills: old town rising to the ground arena; the island's crown
            h += land * Hill(x, z, -1450, 1150, 700, 46);
            h += land * Hill(x, z, -1850, 300, 500, 22);
            h += land * Hill(x, z, 1080, -1300, 300, 12);
            h += land * Hill(x, z, 2080, -820, 120, 28);
            h += land * Hill(x, z, 60, -640, 80, 18);
            // mountains: a ridge west and north of the old town, low hills far east
            float m = Mathf.Max(S01((-x - 2000) / 520f), S01((z - 1640) / 560f));
            if (x < 0) h += land * m * (150 + 430 * Fbm(x * 0.0016f + 5, z * 0.0016f, 5));
            float e = S01((x - 2200) / 450f) * S01((z + 380) / 200f);
            if (x > 0) h += land * e * (40 + 120 * Fbm(x * 0.002f, z * 0.002f + 9, 4));
            // the land mass slopes into the sea before the edge of the map
            float edge = S01((Half - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z))) / 380f);
            h = Mathf.Lerp(SeaFloor, h, edge);
            // flatten the venue pads to their own center height
            foreach (var p in Pads)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - p.c.x) - p.half.x), dz = Mathf.Max(0, Mathf.Abs(z - p.c.y) - p.half.y);
                float w = 1 - S01(Mathf.Sqrt(dx * dx + dz * dz) / p.blend);
                if (w > 0) h = Mathf.Lerp(h, PadHeight(p), w);
            }
            return h;
        }

        static readonly Dictionary<int, float> _padH = new();
        static float PadHeight(Pad p)
        {
            int k = (int)(p.c.x * 7 + p.c.y);
            if (_padH.TryGetValue(k, out var v)) return v;
            // height of the hill at the pad, without other pads
            float x = p.c.x, z = p.c.y;
            v = Hill(x, z, -1450, 1150, 700, 46) + Hill(x, z, 1080, -1300, 300, 12) + 0.3f;
            _padH[k] = v;
            return v;
        }

        /// <summary>Ground height at runtime: the generated terrain or a structure, whichever is on top.</summary>
        public static float GroundY(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 900, z), Vector3.down, out var hit, 1200f, ~(1 << 6), QueryTriggerInteraction.Ignore)) return hit.point.y;
            return Mathf.Max(RawHeight(x, z), WaterLevel);
        }

        public static Vector3 Ground(float x, float z) => new(x, GroundY(x, z), z);

        // ------------------------------------------------------------------ districts
        public enum District { None, Hangar, OldTown, Downtown, Island, Airfield }

        public static District DistrictAt(float x, float z)
        {
            if (x < 0) return z < -225 ? District.Hangar : District.OldTown;
            if (z < -600) return District.Island;
            if (z > 1150) return District.Airfield;
            return District.Downtown;
        }

        public struct Venue { public string id, name, label; public Vector3 pos; public int number; }
        public static readonly Venue[] Venues =
        {
            new Venue { id = "hangar", number = 1, name = "HANGAR DISTRICT", label = "Shipyards, rig shops and the night scene", pos = new Vector3(-1000, 0, -900) },
            new Venue { id = "bridge", number = 2, name = "CENTRAL BRIDGE", label = "The span between the shores", pos = new Vector3(0, BridgeDeck, BridgeZ) },
            new Venue { id = "downtown", number = 3, name = "DOWNTOWN", label = "Towers, the Capitol and the league offices", pos = new Vector3(1450, 0, 420) },
            new Venue { id = "groundarena", number = 4, name = "GROUND ARENA", label = "Qualifying bouts in the hills", pos = new Vector3(-1510, 0, 1250) },
            new Venue { id = "flight", number = 5, name = "FLIGHT CIRCUIT", label = "Pylon racing over the water", pos = new Vector3(1950, 0, 1520) },
            new Venue { id = "championship", number = 6, name = "CHAMPIONSHIP ARENA", label = "Home of the Mech Grand Finals", pos = new Vector3(1500, 0, -1300) },
        };

        // ------------------------------------------------------------------ road network
        public enum RoadKind { Street, Avenue, Boulevard, Ramp, Bridge, Causeway, Runway }

        public class Node
        {
            public Vector2 p;
            public float y = float.NaN;   // fixed height (elevated structures); NaN = follows the ground
            public bool Elevated => !float.IsNaN(y);
            public readonly List<int> edges = new();
        }

        public class Edge { public int a, b; public float w; public RoadKind kind; public District district; }

        public static readonly List<Node> Nodes = new();
        public static readonly List<Edge> Edges = new();

        // west grid: hangar district (south) and old town (north)
        public static readonly float[] WestX = { -1960, -1810, -1660, -1510, -1360, -1210, -1060, -910, -760, -620 };
        public static readonly float[] WestZ = { -1500, -1350, -1200, -1050, -900, -750, -600, -450, -300, -150, 50, 200, 350, 500, 650, 800, 950 };
        public static readonly float[] EastX = { 620, 850, 1000, 1150, 1300, 1450, 1600, 1750, 1900, 2050, 2200 };
        public static readonly float[] EastZ = { -400, -250, -100, 50, 200, 350, 500, 650, 800, 950, 1100 };
        public static readonly float[] IslandX = { 900, 1030, 1150, 1270 };
        public static readonly float[] IslandZ = { -1550, -1420, -1290, -1160, -1030, -930 };

        public static float WestWidth(bool vertical, float c)
        {
            if (vertical) return c == -620 ? 26 : c == -1060 ? 32 : c == -1510 ? 24 : 18;
            return c == -900 ? 28 : c == BridgeZ ? 30 : c == -300 ? 24 : c == 500 ? 22 : 18;
        }
        public static float EastWidth(bool vertical, float c)
        {
            if (vertical) return c == 620 ? 26 : c == 1450 ? 34 : c == 1150 || c == 1750 ? 24 : 20;
            return c == BridgeZ ? 30 : c == 350 ? 26 : c == -400 ? 24 : 20;
        }

        public static float IslandWidth(bool vertical, float c) => vertical ? (c == 1150 ? 22 : 16) : (c == -1290 ? 22 : 16);

        static CityLayout() => BuildNetwork();

        public static int AddNode(Vector2 p, float y = float.NaN)
        {
            for (int i = 0; i < Nodes.Count; i++) if ((Nodes[i].p - p).sqrMagnitude < 1f) return i;
            Nodes.Add(new Node { p = p, y = y });
            return Nodes.Count - 1;
        }

        public static void AddEdge(int a, int b, float w, RoadKind kind)
        {
            var mid = (Nodes[a].p + Nodes[b].p) * 0.5f;
            var e = new Edge { a = a, b = b, w = w, kind = kind, district = DistrictAt(mid.x, mid.y) };
            Edges.Add(e);
            Nodes[a].edges.Add(Edges.Count - 1);
            Nodes[b].edges.Add(Edges.Count - 1);
        }

        static bool OnLand(Vector2 p, float margin) => CoastDist(p.x, p.y) > margin;

        /// <summary>Ground streets the bridge ramps pass over: no road there.</summary>
        static bool UnderRamp(Vector2 a, Vector2 b)
        {
            if (Mathf.Abs(a.y - BridgeZ) > 1 || Mathf.Abs(b.y - BridgeZ) > 1) return false;
            float x = (a.x + b.x) * 0.5f;
            return (x > -910 && x < -450) || (x > 450 && x < 850);
        }

        static bool InPad(Vector2 p, float grow)
        {
            foreach (var pad in Pads)
                if (Mathf.Abs(p.x - pad.c.x) < pad.half.x + grow && Mathf.Abs(p.y - pad.c.y) < pad.half.y + grow) return true;
            return false;
        }

        static void Grid(float[] xs, float[] zs, System.Func<bool, float, float> width, float margin)
        {
            var id = new int[xs.Length, zs.Length];
            for (int i = 0; i < xs.Length; i++)
                for (int j = 0; j < zs.Length; j++)
                {
                    var p = new Vector2(xs[i], zs[j]);
                    bool ok = OnLand(p, margin) && !InPad(p, 20) && RawHeight(p.x, p.y) < 70f;
                    id[i, j] = ok ? AddNode(p) : -1;
                }
            for (int i = 0; i < xs.Length; i++)
                for (int j = 0; j < zs.Length; j++)
                {
                    if (id[i, j] < 0) continue;
                    if (i + 1 < xs.Length && id[i + 1, j] >= 0)
                    {
                        Vector2 a = Nodes[id[i, j]].p, b = Nodes[id[i + 1, j]].p;
                        if (OnLand((a + b) * 0.5f, margin * 0.6f) && !UnderRamp(a, b)) AddEdge(id[i, j], id[i + 1, j], width(false, zs[j]), RoadKind.Street);
                    }
                    if (j + 1 < zs.Length && id[i, j + 1] >= 0)
                    {
                        Vector2 a = Nodes[id[i, j]].p, b = Nodes[id[i, j + 1]].p;
                        if (OnLand((a + b) * 0.5f, margin * 0.6f)) AddEdge(id[i, j], id[i, j + 1], width(true, xs[i]), RoadKind.Street);
                    }
                }
        }

        static void BuildNetwork()
        {
            Nodes.Clear(); Edges.Clear();
            Grid(WestX, WestZ, WestWidth, 30);
            Grid(EastX, EastZ, EastWidth, 30);
            Grid(IslandX, IslandZ, IslandWidth, 25);
            foreach (var e in Edges) if (e.w >= 26) e.kind = RoadKind.Boulevard; else if (e.w >= 22) e.kind = RoadKind.Avenue;

            // Central Bridge: ramps up from both shores to the deck
            int w0 = AddNode(new Vector2(-910, BridgeZ));
            int w1 = AddNode(new Vector2(-450, BridgeZ), BridgeDeck);
            int e1 = AddNode(new Vector2(450, BridgeZ), BridgeDeck);
            int e0 = AddNode(new Vector2(850, BridgeZ));
            AddEdge(w0, w1, 30, RoadKind.Ramp);
            AddEdge(w1, e1, 30, RoadKind.Bridge);
            AddEdge(e1, e0, 30, RoadKind.Ramp);

            // causeway to the championship island
            int c0 = AddNode(new Vector2(1150, -400));
            int c1 = AddNode(new Vector2(1150, -455), 1.5f);
            int c2 = AddNode(new Vector2(1150, -905), 1.5f);
            int c3 = AddNode(new Vector2(1150, -930));
            AddEdge(c0, c1, 24, RoadKind.Avenue);
            AddEdge(c1, c2, 24, RoadKind.Causeway);
            AddEdge(c2, c3, 24, RoadKind.Avenue);

            // up the hill to the ground arena
            int g0 = AddNode(new Vector2(-1510, 950));
            int g1 = AddNode(new Vector2(-1510, 1075));
            AddEdge(g0, g1, 26, RoadKind.Boulevard);
            // island ring road to the championship arena
            int a0 = AddNode(new Vector2(1270, -1290));
            int a1 = AddNode(new Vector2(1290, -1290));
            AddEdge(a0, a1, 22, RoadKind.Avenue);
            // north to the flight circuit
            int f0 = AddNode(new Vector2(1600, 1100));
            int f1 = AddNode(new Vector2(1600, 1340));
            AddEdge(f0, f1, 24, RoadKind.Avenue);
            int r0 = AddNode(new Vector2(1350, 1540)), r1 = AddNode(new Vector2(2560, 1540));
            AddEdge(r0, r1, 60, RoadKind.Runway);
        }

        public static int NearestNode(Vector2 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < Nodes.Count; i++)
            {
                float d = (Nodes[i].p - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Shortest drive along the road network (A*), as points from 'from' to 'to'.</summary>
        public static List<Vector2> Route(Vector2 from, Vector2 to)
        {
            int s = NearestNode(from), g = NearestNode(to);
            var dist = new float[Nodes.Count];
            var prev = new int[Nodes.Count];
            var done = new bool[Nodes.Count];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[s] = 0;
            var open = new List<int> { s };
            while (open.Count > 0)
            {
                int bi = 0; float bf = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    float f = dist[open[i]] + (Nodes[open[i]].p - Nodes[g].p).magnitude;
                    if (f < bf) { bf = f; bi = i; }
                }
                int u = open[bi]; open.RemoveAt(bi);
                if (u == g) break;
                if (done[u]) continue;
                done[u] = true;
                foreach (int ei in Nodes[u].edges)
                {
                    var e = Edges[ei];
                    if (e.kind == RoadKind.Runway) continue;
                    int v = e.a == u ? e.b : e.a;
                    float nd = dist[u] + (Nodes[u].p - Nodes[v].p).magnitude;
                    if (nd < dist[v]) { dist[v] = nd; prev[v] = u; if (!open.Contains(v)) open.Add(v); }
                }
            }
            var path = new List<Vector2> { to };
            for (int n = g; n >= 0; n = prev[n]) { path.Add(Nodes[n].p); if (n == s) break; }
            path.Add(from);
            path.Reverse();
            return path;
        }

        public static bool IsElevated(Edge e) => e.kind == RoadKind.Ramp || e.kind == RoadKind.Bridge || e.kind == RoadKind.Causeway;

        /// <summary>Height of an elevated road at a point along an edge (ramps interpolate).</summary>
        public static float ElevatedY(Edge e, float t, float groundA, float groundB)
        {
            float ya = Nodes[e.a].Elevated ? Nodes[e.a].y : groundA;
            float yb = Nodes[e.b].Elevated ? Nodes[e.b].y : groundB;
            if (e.kind == RoadKind.Bridge)
            {
                // gentle crown in the middle of the span
                return Mathf.Lerp(ya, yb, t) + Mathf.Sin(t * Mathf.PI) * 4f;
            }
            float s = t * t * (3 - 2 * t);
            return Mathf.Lerp(ya, yb, Mathf.Lerp(t, s, 0.5f));
        }

        // ------------------------------------------------------------------ events
        public class RouteDef
        {
            public string id, name, type, blurb, venue;
            public int laps, rivals, repRequired;
            public float skill;
            public bool night;
            public int[] payout, repGain;
            public Vector2[] nodes;
            public List<Vector3> Points()
            {
                var l = new List<Vector3>();
                foreach (var n in nodes) l.Add(Ground(n.x, n.y));
                return l;
            }
        }

        public static readonly RouteDef[] Routes =
        {
            new RouteDef {
                id = "neonmile", name = "NEON MILE", type = "CIRCUIT", venue = "hangar", laps = 2, rivals = 3, repRequired = 0, skill = 0.9f,
                blurb = "Two laps of the Hangar District. Dockside straight, tight turns between the sheds.",
                payout = new[] { 4000, 2200, 1100, 400 }, repGain = new[] { 4, 2, 1, 0 },
                nodes = new Vector2[] { new(-1060, -900), new(-1060, -450), new(-620, -450), new(-620, -1350), new(-1210, -1350), new(-1210, -1050), new(-1060, -1050) },
            },
            new RouteDef {
                id = "bridgerun", name = "BRIDGE RUN", type = "SPRINT", venue = "bridge", laps = 1, rivals = 3, repRequired = 2, skill = 1.0f,
                blurb = "From the docks over the Central Bridge into Downtown. Mind the ramps.",
                payout = new[] { 6000, 3000, 1500, 500 }, repGain = new[] { 5, 3, 1, 0 },
                nodes = new Vector2[] { new(-1060, -600), new(-1060, 50), new(-910, 50), new(-200, 50), new(200, 50), new(850, 50), new(1300, 50), new(1300, 500), new(1750, 500) },
            },
            new RouteDef {
                id = "capitol", name = "CAPITOL CIRCUIT", type = "CIRCUIT", venue = "downtown", laps = 2, rivals = 4, repRequired = 5, skill = 1.05f,
                blurb = "Two laps through the Downtown towers and around the Capitol.",
                payout = new[] { 8000, 4000, 2000, 700 }, repGain = new[] { 6, 3, 2, 0 },
                nodes = new Vector2[] { new(1150, -250), new(1150, 650), new(1750, 650), new(1750, 200), new(1450, 200), new(1450, -250) },
            },
        };

        /// <summary>1 vs 1 bouts inside the arenas. Registration is the marker outside the gate.</summary>
        public class DuelDef
        {
            public string id, name, venue, subtitle, blurb, rival;
            public int reward, repGain, repRequired;
            public float skill, timeLimit = 120f;
            public Vector2 field;   // half extents of the arena floor
            public Vector2 registration, arena;
            public float arenaFieldZ;   // half depth of the field (spawn spacing)
        }

        public static readonly DuelDef[] Duels =
        {
            new DuelDef {
                id = "qualifier", name = "QUALIFIER", venue = "GROUND ARENA", subtitle = "MECH ONLY · 1 vs 1", rival = "VANTA",
                blurb = "Win a bout in the hills to earn a seeding for Championship Week.",
                reward = 800, repGain = 3, repRequired = 0, skill = 0.85f,
                registration = new Vector2(-1510, 1092), arena = new Vector2(-1510, 1250), arenaFieldZ = 50, field = new Vector2(92, 70),
            },
            new DuelDef {
                id = "grandfinal", name = "GRAND FINAL", venue = "CHAMPIONSHIP ARENA", subtitle = "MECH ONLY · 1 vs 1", rival = "IRONCLAD",
                blurb = "The Mech Grand Finals. One bout, the whole bay watching.",
                reward = 12000, repGain = 10, repRequired = 8, skill = 1.15f,
                registration = new Vector2(1302, -1292), arena = new Vector2(1500, -1300), arenaFieldZ = 65, field = new Vector2(112, 92),
            },
        };

        /// <summary>Elevated maglev loop through Downtown (corners, clockwise) and its rail height.</summary>
        public static readonly Vector2[] Monorail = { new(1450, -400), new(1450, 950), new(1900, 950), new(1900, -400) };
        public const float MonorailY = 24f;

        public static readonly Vector2 RigShop = new(-835, -900);       // in front of hangar 03
        public static readonly Vector2 PlayerSpawn = new(-880, -905);
        public static Vector3 GarageDoor => new(RigShop.x, 0, RigShop.y + 14);
    }
}
