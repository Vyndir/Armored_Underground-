using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AU
{
    /// <summary>
    /// Ambient life around the player: traffic driving the road network, pedestrians
    /// on the sidewalks, ferries and speedboats crossing the bay, and mechs parked
    /// on service gantries in the open hangar bays. Everything spawns near the
    /// camera and is recycled as it falls behind.
    /// </summary>
    public class CityLife : MonoBehaviour
    {
        public static CityLife I;
        public Func<IEnumerable<MechController>> Mechs;
        public int carCount = 120, pedCount = 600, boatCount = 7;
        public bool trafficEnabled = true;

        // ------------------------------------------------------------------ shared
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        Material _lit, _glass, _rubber, _head, _tail, _white, _hullDark, _wood;
        readonly List<int> _drivable = new();
        float[] _nodeY;
        int _groundMask;

        Material Lit(Color c, float smooth, float metal)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            m.SetColor(BaseColorId, c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        Material Glow(Color hdr)
        {
            var m = new Material(Shader.Find("AU/Neon"));
            m.SetColor(ColorId, hdr);
            return m;
        }

        void Awake() { I = this; }
        bool _jumped;
        Vector3 _lastFocus;

        void Start()
        {
            _groundMask = ~(1 << 6 | 1 << 7);
            _lit = Lit(Color.white, 0.85f, 0.5f);
            _glass = Lit(new Color(0.04f, 0.05f, 0.07f), 0.95f, 0.3f);
            _rubber = Lit(new Color(0.03f, 0.03f, 0.03f), 0.3f, 0f);
            _head = Glow(new Color(1f, 0.95f, 0.85f) * 3f);
            _tail = Glow(new Color(1f, 0.05f, 0.03f) * 3f);
            _white = Lit(new Color(0.88f, 0.89f, 0.9f), 0.55f, 0.1f);
            _hullDark = Lit(new Color(0.1f, 0.14f, 0.22f), 0.5f, 0.3f);
            _wood = Lit(new Color(0.45f, 0.3f, 0.18f), 0.4f, 0f);
            for (int i = 0; i < CityLayout.Edges.Count; i++)
                if (CityLayout.Edges[i].kind != CityLayout.RoadKind.Runway) _drivable.Add(i);
            _nodeY = new float[CityLayout.Nodes.Count];
            for (int i = 0; i < _nodeY.Length; i++)
            {
                var n = CityLayout.Nodes[i];
                _nodeY[i] = n.Elevated ? n.y : CityLayout.GroundY(n.p.x, n.p.y);
            }
            BuildCarMeshes();
            BuildPedMesh();
            SpawnParkedMechs();
            SpawnBoats();
        }

        Vector3 Focus => Camera.main ? Camera.main.transform.position : Vector3.zero;

        float Ground(Vector3 p, float above)
        {
            if (Physics.Raycast(p + Vector3.up * above, Vector3.down, out var h, above + 40f, _groundMask, QueryTriggerInteraction.Ignore)) return h.point.y;
            return p.y;
        }

        // ------------------------------------------------------------------ road helpers
        (Vector2 a, Vector2 b, float len, float hw) EdgeGeo(int ei)
        {
            var e = CityLayout.Edges[ei];
            Vector2 a = CityLayout.Nodes[e.a].p, b = CityLayout.Nodes[e.b].p;
            return (a, b, Vector2.Distance(a, b), e.w * 0.5f);
        }

        float EdgeY(int ei, float t)
        {
            var e = CityLayout.Edges[ei];
            float ya = _nodeY[e.a], yb = _nodeY[e.b];
            return CityLayout.IsElevated(e) ? CityLayout.ElevatedY(e, t, ya, yb) : Mathf.Lerp(ya, yb, t);
        }

        int NextEdge(int node, int from)
        {
            var list = CityLayout.Nodes[node].edges;
            int tries = 0;
            while (tries++ < 8)
            {
                int e = list[UnityEngine.Random.Range(0, list.Count)];
                if (e != from && CityLayout.Edges[e].kind != CityLayout.RoadKind.Runway) return e;
            }
            return from;
        }

        int RandomEdgeNear(Vector3 c, float minD, float maxD)
        {
            for (int k = 0; k < 40; k++)
            {
                int ei = _drivable[UnityEngine.Random.Range(0, _drivable.Count)];
                var (a, b, _, _) = EdgeGeo(ei);
                Vector2 m = (a + b) * 0.5f;
                float d = Vector2.Distance(m, new Vector2(c.x, c.z));
                if (d > minD && d < maxD) return ei;
            }
            return -1;
        }

        // ------------------------------------------------------------------ traffic
        class Car
        {
            public Transform t; public Renderer r;
            public int edge; public bool fwd; public float s, speed, cruise, lane;
            public Vector3 pos; public Quaternion rot; public float y;
        }
        readonly List<Car> _cars = new();
        readonly List<(Transform t, float until)> _wrecks = new();
        Mesh _sedan, _van;
        static readonly Color[] Paints =
        {
            new(0.03f, 0.03f, 0.035f), new(0.6f, 0.61f, 0.64f), new(0.45f, 0.03f, 0.04f), new(0.82f, 0.82f, 0.84f),
            new(0.04f, 0.1f, 0.3f), new(0.15f, 0.2f, 0.18f), new(0.55f, 0.45f, 0.3f), new(0.25f, 0.27f, 0.3f),
        };

        Mesh CarMesh(float len, float width, float bodyH, float cabinH, float cabinStart, float cabinEnd)
        {
            var paint = new MeshBuilder(); var glass = new MeshBuilder(); var rubber = new MeshBuilder(); var head = new MeshBuilder(); var tail = new MeshBuilder();
            float hl = len / 2, hw = width / 2;
            paint.Box(new Vector3(-hw, 0.35f, -hl), new Vector3(hw, 0.35f + bodyH, hl), Vector2.one * 4);
            glass.Box(new Vector3(-hw + 0.08f, 0.35f + bodyH, cabinStart), new Vector3(hw - 0.08f, 0.35f + bodyH + cabinH, cabinEnd), Vector2.one * 4);
            paint.Box(new Vector3(-hw + 0.06f, 0.35f + bodyH + cabinH, cabinStart + 0.12f), new Vector3(hw - 0.06f, 0.42f + bodyH + cabinH, cabinEnd - 0.12f), Vector2.one * 4);
            foreach (float wx in new[] { -hw + 0.12f, hw - 0.12f })
                foreach (float wz in new[] { -hl + 0.75f, hl - 0.8f })
                    rubber.Cylinder(new Vector3(wx - 0.13f, 0.36f, wz), new Vector3(wx + 0.13f, 0.36f, wz), 0.36f, 10);
            head.Box(new Vector3(-hw + 0.08f, 0.35f + bodyH * 0.55f, hl - 0.02f), new Vector3(-hw + 0.45f, 0.35f + bodyH * 0.8f, hl + 0.02f), Vector2.one);
            head.Box(new Vector3(hw - 0.45f, 0.35f + bodyH * 0.55f, hl - 0.02f), new Vector3(hw - 0.08f, 0.35f + bodyH * 0.8f, hl + 0.02f), Vector2.one);
            tail.Box(new Vector3(-hw + 0.08f, 0.35f + bodyH * 0.55f, -hl - 0.02f), new Vector3(-hw + 0.45f, 0.35f + bodyH * 0.8f, -hl + 0.02f), Vector2.one);
            tail.Box(new Vector3(hw - 0.45f, 0.35f + bodyH * 0.55f, -hl - 0.02f), new Vector3(hw - 0.08f, 0.35f + bodyH * 0.8f, -hl + 0.02f), Vector2.one);
            return Combine("Car", paint, glass, rubber, head, tail);
        }

        static Mesh Combine(string name, params MeshBuilder[] parts)
        {
            var m = new Mesh { name = name };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            foreach (var p in parts) { v.AddRange(p.v); n.AddRange(p.n); uv.AddRange(p.uv); }
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = parts.Length;
            int off = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                var tris = new List<int>(parts[i].t.Count);
                foreach (int t in parts[i].t) tris.Add(t + off);
                m.SetTriangles(tris, i);
                off += parts[i].v.Count;
            }
            m.RecalculateBounds();
            return m;
        }

        void BuildCarMeshes()
        {
            _sedan = CarMesh(4.6f, 1.85f, 0.65f, 0.55f, -1.2f, 0.9f);
            _van = CarMesh(5.4f, 2.05f, 1.4f, 0.5f, 0.6f, 2.2f);
        }

        Car NewCar()
        {
            var go = new GameObject("TrafficCar", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = UnityEngine.Random.value < 0.8f ? _sedan : _van;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterials = new[] { _lit, _glass, _rubber, _head, _tail };
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, Paints[UnityEngine.Random.Range(0, Paints.Length)]);
            r.SetPropertyBlock(mpb, 0);
            return new Car { t = go.transform, r = r };
        }

        void PlaceCar(Car c, int ei, float sFrac)
        {
            var (a, b, len, hw) = EdgeGeo(ei);
            c.edge = ei; c.fwd = UnityEngine.Random.value < 0.5f;
            c.s = len * sFrac;
            c.cruise = UnityEngine.Random.Range(13f, 21f);
            c.speed = c.cruise;
            c.lane = hw >= 11 ? hw * 0.55f : hw * 0.42f;
            c.y = float.NaN;
            UpdateCarPose(c, 0, true);
        }

        void UpdateCarPose(Car c, float dt, bool snap)
        {
            var (a, b, len, _) = EdgeGeo(c.edge);
            Vector2 from = c.fwd ? a : b, to = c.fwd ? b : a;
            Vector2 d = (to - from) / len;
            Vector2 right = new(d.y, -d.x);
            Vector2 p2 = from + d * c.s + right * c.lane;
            float t = c.fwd ? c.s / len : 1 - c.s / len;
            float yRoad = EdgeY(c.edge, t) + 0.12f;
            if (!CityLayout.IsElevated(CityLayout.Edges[c.edge])) yRoad = Ground(new Vector3(p2.x, yRoad, p2.y), 3f);
            Vector3 target = new(p2.x, yRoad, p2.y);
            Quaternion rot = Quaternion.LookRotation(new Vector3(d.x, 0, d.y));
            if (snap) { c.pos = target; c.rot = rot; }
            else
            {
                c.pos = Vector3.Lerp(c.pos, target, 1 - Mathf.Exp(-dt * 6f));
                c.rot = Quaternion.Slerp(c.rot, rot, 1 - Mathf.Exp(-dt * 5f));
            }
            c.t.SetPositionAndRotation(c.pos, c.rot);
        }

        void TickTraffic(float dt)
        {
            Vector3 f = Focus;
            while (_cars.Count < carCount)
            {
                int ei = RandomEdgeNear(f, 80, 650);
                if (ei < 0) break;
                var c = NewCar();
                PlaceCar(c, ei, UnityEngine.Random.value);
                _cars.Add(c);
            }
            var mechs = Mechs?.Invoke()?.Where(m => m && m.isActiveAndEnabled).ToList();
            for (int i = 0; i < _cars.Count; i++)
            {
                var c = _cars[i];
                var (a, b, len, _) = EdgeGeo(c.edge);
                // recycle cars that fell far behind the camera
                if ((c.pos - f).sqrMagnitude > 850f * 850f)
                {
                    int ei = RandomEdgeNear(f, _jumped ? 30 : 300, 650);
                    if (ei >= 0) PlaceCar(c, ei, UnityEngine.Random.value);
                    continue;
                }
                // follow the car ahead in the same lane; slow for crossings
                float want = c.cruise;
                if (len - c.s < 22f) want = Mathf.Min(want, 9f);
                foreach (var o in _cars)
                {
                    if (o == c || o.edge != c.edge || o.fwd != c.fwd) continue;
                    float gap = o.s - c.s;
                    if (gap > 0 && gap < 16f) want = Mathf.Min(want, o.speed * Mathf.Clamp01((gap - 6f) / 10f));
                }
                // brake for mechs standing in the road
                if (mechs != null)
                    foreach (var m in mechs)
                    {
                        Vector3 to = m.transform.position - c.pos;
                        if (to.sqrMagnitude < 18f * 18f && Vector3.Dot(to, c.t.forward) > 0 && Mathf.Abs(Vector3.Dot(to, c.t.right)) < 4f) want = Mathf.Min(want, 2f);
                    }
                c.speed = Mathf.MoveTowards(c.speed, want, dt * 7f);
                c.s += c.speed * dt;
                if (c.s >= len)
                {
                    var e = CityLayout.Edges[c.edge];
                    int node = c.fwd ? e.b : e.a;
                    int next = NextEdge(node, c.edge);
                    if (next == c.edge) { c.fwd = !c.fwd; c.s = 0; }
                    else
                    {
                        c.edge = next;
                        c.fwd = CityLayout.Edges[next].a == node;
                        c.s = 0;
                        var (_, _, _, nhw) = EdgeGeo(next);
                        c.lane = nhw >= 11 ? nhw * 0.55f : nhw * 0.42f;
                    }
                }
                UpdateCarPose(c, dt, false);
                // a mech plowing through sends the car flying
                if (mechs != null)
                    foreach (var m in mechs)
                    {
                        Vector3 rel = c.pos - m.transform.position;
                        if (Mathf.Abs(rel.y) > 5f) continue;
                        rel.y = 0;
                        if (rel.sqrMagnitude < 3.6f * 3.6f && m.Speed > 6f) { Wreck(c, m); _cars.RemoveAt(i); i--; break; }
                    }
            }
            for (int i = _wrecks.Count - 1; i >= 0; i--)
                if (Time.time > _wrecks[i].until) { if (_wrecks[i].t) Destroy(_wrecks[i].t.gameObject); _wrecks.RemoveAt(i); }
            float night = DayNight.I ? DayNight.I.Night : 0f;
            _head.SetColor(ColorId, new Color(1f, 0.95f, 0.85f) * Mathf.Lerp(0.4f, 4f, night));
            _tail.SetColor(ColorId, new Color(1f, 0.05f, 0.03f) * Mathf.Lerp(0.6f, 3.5f, night));
        }

        void Wreck(Car c, MechController m)
        {
            var go = c.t.gameObject;
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.8f, 0); bc.size = new Vector3(1.9f, 1.4f, 4.6f);
            go.layer = 7;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            Vector3 push = (c.pos - m.transform.position); push.y = 0;
            rb.linearVelocity = push.normalized * (6f + m.Speed * 0.5f) + m.velocity * 0.6f + Vector3.up * (5f + m.Speed * 0.12f);
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 4f;
            m.AddOverdrive(0.04f, "TRAFFIC CHECK");
            AudioFX.I?.Play("hit", c.pos, 0.7f, 0.7f);
            Combat.I?.SkidFX(m);
            _wrecks.Add((c.t, Time.time + 14f));
        }

        // ------------------------------------------------------------------ pedestrians
        struct Ped { public int edge; public bool fwd; public float s, side, speed, phase, y, flee; public Vector3 pos, fleeDir; public int mat; }
        Ped[] _peds;
        Mesh _pedMesh;
        Material[] _pedMats;
        readonly List<Matrix4x4>[] _pedBatches = new List<Matrix4x4>[6];
        int _pedCursor;

        void BuildPedMesh()
        {
            var body = new MeshBuilder();
            body.Box(new Vector3(-0.22f, 0.85f, -0.13f), new Vector3(0.22f, 1.45f, 0.13f), Vector2.one);     // torso
            body.Box(new Vector3(-0.2f, 0.0f, -0.11f), new Vector3(-0.03f, 0.88f, 0.11f), Vector2.one);       // legs
            body.Box(new Vector3(0.03f, 0.0f, -0.11f), new Vector3(0.2f, 0.88f, 0.11f), Vector2.one);
            body.Box(new Vector3(-0.3f, 0.82f, -0.08f), new Vector3(-0.22f, 1.42f, 0.08f), Vector2.one);       // arms
            body.Box(new Vector3(0.22f, 0.82f, -0.08f), new Vector3(0.3f, 1.42f, 0.08f), Vector2.one);
            body.Cylinder(new Vector3(0, 1.48f, 0), new Vector3(0, 1.74f, 0), 0.12f, 8);                         // head
            _pedMesh = body.ToMesh("Pedestrian");
            var shirts = new[] { new Color(0.07f, 0.1f, 0.2f), new Color(0.6f, 0.1f, 0.12f), new Color(0.85f, 0.84f, 0.8f), new Color(0.2f, 0.35f, 0.6f), new Color(0.12f, 0.12f, 0.14f), new Color(0.75f, 0.6f, 0.2f) };
            _pedMats = shirts.Select(c => Lit(c, 0.2f, 0f)).ToArray();
            for (int i = 0; i < _pedBatches.Length; i++) _pedBatches[i] = new List<Matrix4x4>(pedCount);
            _peds = new Ped[pedCount];
            for (int i = 0; i < pedCount; i++) SpawnPed(ref _peds[i], Focus, 0, 350);
        }

        void SpawnPed(ref Ped p, Vector3 near, float minD, float maxD)
        {
            int ei = RandomEdgeNear(near, minD, maxD);
            if (ei < 0) ei = _drivable[0];
            var e = CityLayout.Edges[ei];
            if (CityLayout.IsElevated(e) && e.kind != CityLayout.RoadKind.Bridge) ei = _drivable[UnityEngine.Random.Range(0, _drivable.Count)];
            var (_, _, len, hw) = EdgeGeo(ei);
            p.edge = ei; p.fwd = UnityEngine.Random.value < 0.5f; p.s = UnityEngine.Random.value * len;
            float walk = CityLayout.IsElevated(e) ? 2.3f : CityLayout.Sidewalk * 0.5f;
            p.side = (UnityEngine.Random.value < 0.5f ? -1 : 1) * (hw + walk + UnityEngine.Random.Range(-1.2f, 1.2f));
            p.speed = UnityEngine.Random.Range(1.1f, 1.7f);
            p.phase = UnityEngine.Random.value * 10f;
            p.mat = UnityEngine.Random.Range(0, _pedMats.Length);
            p.y = float.NaN; p.flee = 0;
            p.pos = PedTarget(ref p);
        }

        Vector3 PedTarget(ref Ped p)
        {
            var (a, b, len, _) = EdgeGeo(p.edge);
            Vector2 from = p.fwd ? a : b, to = p.fwd ? b : a;
            Vector2 d = (to - from) / len;
            Vector2 right = new(d.y, -d.x);
            Vector2 q = from + d * p.s + right * p.side;
            float t = p.fwd ? p.s / len : 1 - p.s / len;
            float y = float.IsNaN(p.y) ? EdgeY(p.edge, t) + 0.34f : p.y;
            return new Vector3(q.x, y, q.y);
        }

        void TickPeds(float dt)
        {
            Vector3 f = Focus;
            var mechs = Mechs?.Invoke()?.Where(m => m && m.isActiveAndEnabled).ToList();
            foreach (var l in _pedBatches) l.Clear();
            for (int i = 0; i < _peds.Length; i++)
            {
                ref Ped p = ref _peds[i];
                if ((p.pos - f).sqrMagnitude > 420f * 420f) { SpawnPed(ref p, f, _jumped ? 0 : 150, 380); continue; }
                var (_, _, len, _) = EdgeGeo(p.edge);
                // scatter from nearby mechs
                if (mechs != null)
                    foreach (var m in mechs)
                    {
                        Vector3 rel = p.pos - m.transform.position; rel.y = 0;
                        if (rel.sqrMagnitude < 14f * 14f) { p.flee = 2.5f; p.fleeDir = rel.normalized; }
                    }
                Vector3 target;
                if (p.flee > 0)
                {
                    p.flee -= dt;
                    p.pos += p.fleeDir * 5.5f * dt;
                    target = p.pos;
                }
                else
                {
                    p.s += p.speed * dt;
                    if (p.s >= len)
                    {
                        var e = CityLayout.Edges[p.edge];
                        int node = p.fwd ? e.b : e.a;
                        int next = NextEdge(node, p.edge);
                        if (next == p.edge) p.fwd = !p.fwd;
                        else { p.edge = next; p.fwd = CityLayout.Edges[next].a == node; }
                        p.s = 0;
                        p.y = float.NaN;
                    }
                    target = PedTarget(ref p);
                    p.pos = Vector3.Lerp(p.pos, target, 1 - Mathf.Exp(-dt * 3f));
                }
                p.phase += dt * p.speed * (p.flee > 0 ? 9f : 5.5f);
                Vector3 dir = p.flee > 0 ? p.fleeDir : (target - p.pos).sqrMagnitude > 0.0001f ? (target - p.pos).normalized : Vector3.forward;
                var (ea, eb, el, _) = EdgeGeo(p.edge);
                Vector2 ed = ((p.fwd ? eb : ea) - (p.fwd ? ea : eb)) / el;
                if (p.flee <= 0) dir = new Vector3(ed.x, 0, ed.y);
                dir.y = 0;
                Vector3 pos = p.pos + Vector3.up * Mathf.Abs(Mathf.Sin(p.phase)) * 0.06f;
                if ((p.pos - f).sqrMagnitude < 260f * 260f)
                    _pedBatches[p.mat].Add(Matrix4x4.TRS(pos, Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir : Vector3.forward) * Quaternion.Euler(0, 0, Mathf.Sin(p.phase) * 3f), Vector3.one));
            }
            // refine heights a few at a time (ground under the feet)
            for (int k = 0; k < 24; k++)
            {
                _pedCursor = (_pedCursor + 1) % _peds.Length;
                ref Ped q = ref _peds[_pedCursor];
                q.y = Ground(q.pos, 2.5f);
            }
            for (int i = 0; i < _pedMats.Length; i++)
            {
                var list = _pedBatches[i];
                if (list.Count == 0) continue;
                var rp = new RenderParams(_pedMats[i]) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
                for (int start = 0; start < list.Count; start += 1023)
                    Graphics.RenderMeshInstanced(rp, _pedMesh, 0, list, Mathf.Min(1023, list.Count - start), start);
            }
        }

        // ------------------------------------------------------------------ boats
        class Boat { public Transform t; public Vector2 c; public float rx, rz, phase, speed; public ParticleSystem wake; }
        readonly List<Boat> _boats = new();

        void SpawnBoats()
        {
            var ferry = new MeshBuilder(); var ferryGlass = new MeshBuilder(); var ferryDark = new MeshBuilder();
            Hull(ferry, 46, 11, -2.5f, 2.5f);
            ferry.Box(new Vector3(-4.6f, 2.5f, -18), new Vector3(4.6f, 6f, 14), Vector2.one * 4);
            ferryGlass.Box(new Vector3(-4.7f, 3.4f, -16), new Vector3(4.7f, 5.2f, 12), Vector2.one * 4);
            ferry.Box(new Vector3(-3.4f, 6f, -10), new Vector3(3.4f, 8.5f, 6), Vector2.one * 4);
            ferryDark.Box(new Vector3(-1f, 8.5f, -6), new Vector3(1f, 11.5f, -3.5f), Vector2.one);
            var ferryMesh = Combine("Ferry", ferry, ferryGlass, ferryDark);
            var speed = new MeshBuilder(); var speedGlass = new MeshBuilder(); var speedDark = new MeshBuilder();
            Hull(speed, 11, 3.4f, -0.6f, 1.1f);
            speedGlass.Box(new Vector3(-1.3f, 1.1f, -1.5f), new Vector3(1.3f, 2.1f, 0.6f), Vector2.one);
            speedDark.Box(new Vector3(-1.5f, 1.1f, -4.5f), new Vector3(1.5f, 1.4f, -1.5f), Vector2.one);
            var speedMesh = Combine("Speedboat", speed, speedGlass, speedDark);
            var a = GameAssets.I;
            for (int k = 0; k < boatCount; k++)
            {
                // find a loop that stays on open water
                Vector2 c = Vector2.zero; float rx = 0, rz = 0; bool ok = false;
                for (int tries = 0; tries < 200 && !ok; tries++)
                {
                    c = new Vector2(UnityEngine.Random.Range(-500f, 1900f), UnityEngine.Random.Range(-1600f, 1500f));
                    rx = UnityEngine.Random.Range(120f, 420f); rz = UnityEngine.Random.Range(120f, 420f);
                    ok = true;
                    for (int s = 0; s < 24 && ok; s++)
                    {
                        float ang = s / 24f * Mathf.PI * 2;
                        Vector2 p = c + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * rz);
                        if (CityLayout.CoastDist(p.x, p.y) > -50f) ok = false;
                        if (Mathf.Abs(Mathf.Abs(p.x) - CityLayout.BridgeTower) < 45f && Mathf.Abs(p.y - CityLayout.BridgeZ) < 60f) ok = false;
                    }
                }
                if (!ok) continue;
                bool isFerry = k % 2 == 0;
                var go = new GameObject(isFerry ? "Ferry" : "Speedboat", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = isFerry ? ferryMesh : speedMesh;
                go.GetComponent<MeshRenderer>().sharedMaterials = new[] { _white, _glass, isFerry ? _hullDark : _wood };
                var wake = new GameObject("Wake").AddComponent<ParticleSystem>();
                wake.transform.SetParent(go.transform, false);
                wake.transform.localPosition = new Vector3(0, 0.15f, isFerry ? -23f : -5.5f);
                wake.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = wake.main;
                main.startLifetime = isFerry ? 9f : 5f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(isFerry ? 5f : 2.5f, isFerry ? 9f : 4f);
                main.startColor = new Color(0.95f, 0.97f, 1f, 0.35f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 600;
                var em = wake.emission; em.rateOverTime = isFerry ? 40 : 60;
                var sh = wake.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(isFerry ? 9 : 3, 0, 1);
                var sz = wake.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 2.8f));
                var col = wake.colorOverLifetime; col.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
                col.color = g;
                var pr = wake.GetComponent<ParticleSystemRenderer>();
                pr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                pr.sharedMaterial = a.splash;
                wake.Play();
                _boats.Add(new Boat { t = go.transform, c = c, rx = rx, rz = rz, phase = UnityEngine.Random.value * Mathf.PI * 2, speed = isFerry ? 7f : 16f, wake = wake });
            }
        }

        static void Hull(MeshBuilder b, float len, float beam, float lo, float hi)
        {
            var pts = new[] { new Vector3(-beam / 2, 0, -len / 2), new Vector3(beam / 2, 0, -len / 2), new Vector3(beam / 2, 0, len * 0.25f), new Vector3(0, 0, len / 2), new Vector3(-beam / 2, 0, len * 0.25f) };
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 p0 = pts[i], p1 = pts[(i + 1) % pts.Length];
                Vector3 a = p0 + Vector3.up * lo, bb = p1 + Vector3.up * lo, c = p1 + Vector3.up * hi, d = p0 + Vector3.up * hi;
                Vector3 outN = (p0 + p1) * 0.5f;
                if (Vector3.Dot(Vector3.Cross(d - a, bb - a), outN) < 0) { (a, bb) = (bb, a); (c, d) = (d, c); }
                b.Quad(a, bb, c, d, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
            Vector3 ctr = Vector3.up * hi;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 p0 = pts[i] + Vector3.up * hi, p1 = pts[(i + 1) % pts.Length] + Vector3.up * hi;
                if (Vector3.Cross(p0 - ctr, p1 - ctr).y > 0) b.Tri(ctr, p0, p1, Vector2.zero, Vector2.right, Vector2.up);
                else b.Tri(ctr, p1, p0, Vector2.zero, Vector2.right, Vector2.up);
            }
        }

        void TickBoats(float dt)
        {
            foreach (var b in _boats)
            {
                float circ = Mathf.PI * (b.rx + b.rz);
                b.phase += b.speed / circ * Mathf.PI * 2 * dt;
                Vector2 p = b.c + new Vector2(Mathf.Cos(b.phase) * b.rx, Mathf.Sin(b.phase) * b.rz);
                Vector2 tangent = new(-Mathf.Sin(b.phase) * b.rx, Mathf.Cos(b.phase) * b.rz);
                float bob = Mathf.Sin(Time.time * 0.9f + b.phase * 3f) * 0.15f;
                b.t.SetPositionAndRotation(new Vector3(p.x, CityLayout.WaterLevel + bob, p.y),
                    Quaternion.LookRotation(new Vector3(tangent.x, 0, tangent.y)) * Quaternion.Euler(Mathf.Sin(Time.time * 0.7f + b.phase) * 1.2f, 0, Mathf.Sin(Time.time * 0.5f) * 1.5f));
            }
        }

        // ------------------------------------------------------------------ parked mechs
        void SpawnParkedMechs()
        {
            var spots = GameAssets.I.mechSpots;
            if (spots == null || !GameAssets.I.mechPrefab) return;
            var rng = new System.Random(31);
            var palette = new[]
            {
                (new Color(0.42f, 0.44f, 0.4f), new Color(0.85f, 0.6f, 0.15f)), (new Color(0.12f, 0.14f, 0.2f), new Color(0.75f, 0.12f, 0.14f)),
                (new Color(0.78f, 0.78f, 0.76f), new Color(0.1f, 0.25f, 0.6f)), (new Color(0.3f, 0.32f, 0.26f), new Color(0.9f, 0.8f, 0.3f)),
                (new Color(0.5f, 0.12f, 0.1f), new Color(0.85f, 0.85f, 0.85f)),
            };
            foreach (var s in spots)
            {
                var go = Instantiate(GameAssets.I.mechPrefab, new Vector3(s.x, s.y, s.z), Quaternion.Euler(0, s.w, 0), transform);
                go.name = "ParkedMech";
                var l = new Loadout();
                foreach (Slot sl in Parts.Slots)
                {
                    var opts = Parts.OfSlot(sl).ToList();
                    l[sl] = opts[rng.Next(opts.Count)].id;
                }
                var (paint, accent) = palette[rng.Next(palette.Length)];
                l.paint = paint; l.accent = accent;
                MechVariants.Apply(go.transform, l);
                var mpb = new MaterialPropertyBlock();
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] && mats[i].shader && mats[i].shader.name == "AU/MechSurface")
                        {
                            r.GetPropertyBlock(mpb, i);
                            mpb.SetColor("_PaintColor", l.paint);
                            mpb.SetColor("_AccentColor", l.accent);
                            r.SetPropertyBlock(mpb, i);
                        }
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
                }
            }
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0) return;
            // after a teleport, repopulate around the new spot instead of out of sight
            Vector3 f = Focus;
            _jumped = (f - _lastFocus).sqrMagnitude > 250f * 250f;
            _lastFocus = f;
            if (trafficEnabled) TickTraffic(dt);
            TickPeds(dt);
            TickBoats(dt);
        }
    }
}
