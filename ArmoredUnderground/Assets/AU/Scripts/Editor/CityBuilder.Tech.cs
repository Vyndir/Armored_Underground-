using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static AU.CityLayout;

namespace AU.EditorTools
{
    /// <summary>
    /// The futuristic layer of the bay city: sculpted Downtown towers with LED
    /// edge lighting and holo screens, skybridges, the elevated maglev loop, and
    /// the combat floors of both arenas (trenches, cover, high-rises, platforms,
    /// energy shields).
    /// </summary>
    public partial class CityBuilder
    {
        readonly List<(Rect r, float top)> talls = new();
        readonly List<(Vector2 c, float rx, float rz)> arenaCarves = new();

        Material _ledMagenta, _ledWhite, _shield, _hexMetal, _trenchMat, _hazard, _rust;
        Material LedMagenta => _ledMagenta ? _ledMagenta : (_ledMagenta = Mats.Neon("LedMagenta", new Color(1f, 0.2f, 0.65f) * 4f));
        Material LedWhite => _ledWhite ? _ledWhite : (_ledWhite = Mats.Neon("LedWhite", new Color(0.85f, 0.95f, 1f) * 3f));
        Material Shield => _shield ? _shield : (_shield = Mats.Glow("ShieldWall", new Color(0.2f, 0.75f, 1f) * 0.9f, 1.2f, 0f));
        Material HexMetal => _hexMetal ? _hexMetal : (_hexMetal = Mats.LitMat("HexMetal", new Color(0.2f, 0.23f, 0.27f), 0.6f, 0.75f));
        Material TrenchMat => _trenchMat ? _trenchMat : (_trenchMat = Mats.LitMat("TrenchConcrete", new Color(2.0f, 1.95f, 1.85f), 0.25f, 0f, (Texture2D)concreteB.GetTexture("_BaseMap"), (Texture2D)concreteB.GetTexture("_BumpMap")));
        Material Hazard => _hazard ? _hazard : (_hazard = Mats.LitMat("HazardYellow", new Color(0.85f, 0.65f, 0.08f), 0.4f, 0.2f));
        Material Rust => _rust ? _rust : (_rust = Mats.LitMat("RustSteel", new Color(0.32f, 0.2f, 0.14f), 0.3f, 0.6f));

        // ------------------------------------------------------------------ futuristic towers
        void FuturisticTower(Rect f, bool[] street, float h, Material facade, Material trim, Transform parent)
        {
            var glassFacade = R() < 0.5f ? DayFacade("FacadeDayGlass") : DayFacade("FacadeDayGold");
            var led = R() < 0.7f ? ledBlue : LedMagenta;
            // two-storey podium with a glass lobby band and a lit canopy edge
            B(trim).Box(new Vector3(f.xMin, -5, f.yMin), new Vector3(f.xMax, 10f, f.yMax), Tile(8, 8), 63 & ~4 & ~8);
            B(shipGlass).Box(new Vector3(f.xMin - 0.1f, 1f, f.yMin - 0.1f), new Vector3(f.xMax + 0.1f, 7f, f.yMax + 0.1f), Tile(4, 6), 63 & ~4 & ~8);
            B(trim).Box(new Vector3(f.xMin - 1.2f, 9.4f, f.yMin - 1.2f), new Vector3(f.xMax + 1.2f, 10.4f, f.yMax + 1.2f), Tile(8, 1));
            B(led).Box(new Vector3(f.xMin - 1.25f, 9.4f, f.yMin - 1.25f), new Vector3(f.xMax + 1.25f, 9.6f, f.yMax + 1.25f), Tile(8, 1), 63 & ~4 & ~8);
            AddBoxCollider(parent, new Vector3(f.xMin, -5, f.yMin), new Vector3(f.xMax, 10.4f, f.yMax));
            float side = Mathf.Min(f.width, f.height) * 0.86f;
            Vector2 c = f.center;
            int v = rnd.Next(3);
            float top = h;
            if (v == 0)
            {
                // tapered tiers with lit edges
                int tiers = 5;
                float y = 10.4f;
                for (int k = 0; k < tiers; k++)
                {
                    float s = side * Mathf.Lerp(1f, 0.52f, k / (float)(tiers - 1));
                    float th = (h - 10.4f) / tiers * (k == tiers - 1 ? 0.8f : 1f);
                    var r = new Rect(c.x - s / 2, c.y - s / 2, s, s);
                    B(glassFacade).Box(new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMax, y + th, r.yMax), Tile(12, 14.4f), 63 & ~8);
                    B(led).Box(new Vector3(r.xMin - 0.15f, y + th - 0.4f, r.yMin - 0.15f), new Vector3(r.xMax + 0.15f, y + th, r.yMax + 0.15f), Tile(4, 1), 63 & ~4 & ~8);
                    foreach (float x in new[] { r.xMin, r.xMax })
                        foreach (float z in new[] { r.yMin, r.yMax })
                            B(led).Box(new Vector3(x - 0.18f, y, z - 0.18f), new Vector3(x + 0.18f, y + th, z + 0.18f), Tile(1, 8));
                    AddBoxCollider(parent, new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMax, y + th, r.yMax), "Tower");
                    y += th;
                }
                top = y;
            }
            else if (v == 1)
            {
                // twisting stack of floor plates
                float y = 10.4f;
                int k = 0;
                while (y < h)
                {
                    float sh = Mathf.Min(7.2f, h - y);
                    var rot = Quaternion.Euler(0, k * 2.6f, 0);
                    B(glassFacade).BoxCentered(new Vector3(c.x, y + sh / 2, c.y), new Vector3(side * 0.82f, sh, side * 0.82f), Tile(12, 14.4f), null, rot);
                    if (k % 3 == 2) B(led).BoxCentered(new Vector3(c.x, y + sh - 0.15f, c.y), new Vector3(side * 0.86f, 0.3f, side * 0.86f), Tile(4, 1), null, rot);
                    y += sh; k++;
                }
                AddBoxCollider(parent, new Vector3(c.x - side * 0.41f, 10.4f, c.y - side * 0.41f), new Vector3(c.x + side * 0.41f, h, c.y + side * 0.41f), "Tower");
                top = h;
            }
            else
            {
                // round tower with ring lights
                float rad = side * 0.48f;
                int segs = 28;
                for (int k = 0; k < segs; k++)
                {
                    float a0 = k / (float)segs * Mathf.PI * 2, a1 = (k + 1) / (float)segs * Mathf.PI * 2;
                    Vector3 p0 = new(c.x + Mathf.Cos(a0) * rad, 0, c.y + Mathf.Sin(a0) * rad), p1 = new(c.x + Mathf.Cos(a1) * rad, 0, c.y + Mathf.Sin(a1) * rad);
                    Vector3 n = new(Mathf.Cos((a0 + a1) / 2), 0, Mathf.Sin((a0 + a1) / 2));
                    float u0 = a0 * rad / 12f;
                    QuadAuto(glassFacade, p0 + Vector3.up * 10.4f, p1 + Vector3.up * 10.4f, p1 + Vector3.up * h, p0 + Vector3.up * h, n, 12, 14.4f, new Vector2(u0, 10.4f / 14.4f));
                    TriAuto(roof, new Vector3(c.x, h, c.y), p0 + Vector3.up * h, p1 + Vector3.up * h, Vector3.up);
                }
                for (float y = 34f; y < h - 4; y += 24f)
                    B(led).Cylinder(new Vector3(c.x, y, c.y), new Vector3(c.x, y + 0.5f, c.y), rad + 0.25f, segs);
                B(metal).Cylinder(new Vector3(c.x, h, c.y), new Vector3(c.x, h + 2.5f, c.y), rad * 0.9f, segs);
                AddBoxCollider(parent, new Vector3(c.x - rad * 0.85f, 10.4f, c.y - rad * 0.85f), new Vector3(c.x + rad * 0.85f, h, c.y + rad * 0.85f), "Tower");
                top = h + 2.5f;
            }
            // crown: helipad or spire
            Vector3 tc = new(c.x, top, c.y);
            if (R() < 0.5f)
            {
                B(concreteA).Cylinder(tc, tc + Vector3.up * 0.6f, side * 0.28f, 20);
                B(paintWhite).Cylinder(tc + Vector3.up * 0.6f, tc + Vector3.up * 0.65f, side * 0.24f, 20, null, true, side * 0.24f);
                B(concreteA).Cylinder(tc + Vector3.up * 0.6f, tc + Vector3.up * 0.7f, side * 0.21f, 20);
                for (int k = 0; k < 8; k++)
                {
                    float a = k / 8f * Mathf.PI * 2;
                    B(LedWhite).BoxCentered(tc + new Vector3(Mathf.Cos(a) * side * 0.27f, 0.75f, Mathf.Sin(a) * side * 0.27f), Vector3.one * 0.5f, Tile(1, 1));
                }
            }
            else
            {
                B(metal).Cylinder(tc, tc + Vector3.up * R(25, 55), 1.2f, 8, null, true, 0.12f);
                B(beacon).BoxCentered(tc + Vector3.up * 3, Vector3.one * 1.4f, Tile(1, 1));
            }
            // holo screen hung on the podium's street face
            for (int s = 0; s < 4; s++)
            {
                if (!street[s] || R() > 0.55f) continue;
                FaceInfo(f, s, out Vector3 a, out Vector3 dir, out Vector3 outN, out float len);
                float w = Mathf.Min(len * 0.55f, 16f);
                Vector3 m = a + dir * (len * 0.5f) + outN * 1.6f + Vector3.up * 12f;
                Vector3 hw = dir * (w / 2);
                QuadAuto(Pick(screens), m - hw, m + hw, m + hw + Vector3.up * w * 0.6f, m - hw + Vector3.up * w * 0.6f, outN, w, w * 0.6f);
                B(darkMetal).BoxCentered(m + Vector3.up * w * 0.3f - outN * 0.3f, Abs(dir) * (w + 0.6f) + Abs(outN) * 0.4f + Vector3.up * (w * 0.6f + 0.6f), Tile(2, 2));
                break;
            }
        }

        // ------------------------------------------------------------------ skybridges
        void Skybridges()
        {
            var g = new GameObject("Skybridges").transform;
            g.SetParent(root, false);
            int made = 0;
            for (int i = 0; i < talls.Count && made < 28; i++)
                for (int j = i + 1; j < talls.Count && made < 28; j++)
                {
                    var (a, ta) = talls[i];
                    var (b, tb) = talls[j];
                    float y = Mathf.Min(ta, tb) * R(0.45f, 0.7f);
                    if (y < 30) continue;
                    // across a street running north-south (bridge along x)
                    float gapX = b.xMin > a.xMax ? b.xMin - a.xMax : a.xMin - b.xMax;
                    float ovZ0 = Mathf.Max(a.yMin, b.yMin), ovZ1 = Mathf.Min(a.yMax, b.yMax);
                    float gapZ = b.yMin > a.yMax ? b.yMin - a.yMax : a.yMin - b.yMax;
                    float ovX0 = Mathf.Max(a.xMin, b.xMin), ovX1 = Mathf.Min(a.xMax, b.xMax);
                    Vector3 p0, p1;
                    if (gapX > 18 && gapX < 50 && ovZ1 - ovZ0 > 12)
                    {
                        float z = (ovZ0 + ovZ1) / 2;
                        float x0 = b.xMin > a.xMax ? a.xMax : b.xMax, x1 = b.xMin > a.xMax ? b.xMin : a.xMin;
                        p0 = new Vector3(x0 - 4, y, z); p1 = new Vector3(x1 + 4, y, z);
                    }
                    else if (gapZ > 18 && gapZ < 50 && ovX1 - ovX0 > 12)
                    {
                        float x = (ovX0 + ovX1) / 2;
                        float z0 = b.yMin > a.yMax ? a.yMax : b.yMax, z1 = b.yMin > a.yMax ? b.yMin : a.yMin;
                        p0 = new Vector3(x, y, z0 - 4); p1 = new Vector3(x, y, z1 + 4);
                    }
                    else continue;
                    if (R() < 0.45f) continue;
                    Vector3 d = (p1 - p0).normalized;
                    Vector3 sideV = Vector3.Cross(Vector3.up, d);
                    float len = Vector3.Distance(p0, p1);
                    Vector3 mid = (p0 + p1) / 2;
                    var rot = Quaternion.LookRotation(d);
                    B(metal).BoxCentered(mid, new Vector3(7f, 0.8f, len), Tile(4, 4), null, rot);
                    B(metal).BoxCentered(mid + Vector3.up * 5.2f, new Vector3(7f, 0.5f, len), Tile(4, 4), null, rot);
                    B(shipGlass).BoxCentered(mid + Vector3.up * 2.6f + sideV * 3.3f, new Vector3(0.2f, 4.4f, len), Tile(4, 4), null, rot);
                    B(shipGlass).BoxCentered(mid + Vector3.up * 2.6f - sideV * 3.3f, new Vector3(0.2f, 4.4f, len), Tile(4, 4), null, rot);
                    B(ledBlue).BoxCentered(mid - Vector3.up * 0.5f, new Vector3(4f, 0.15f, len), Tile(4, 1), null, rot);
                    for (float s = 0; s <= len; s += 6f)
                        foreach (int k in new[] { -1, 1 })
                            B(metal).BoxCentered(p0 + d * s + sideV * 3.4f * k + Vector3.up * 2.6f, new Vector3(0.35f, 5.2f, 0.35f), Tile(1, 4), null, rot);
                    var col = new GameObject("Skybridge");
                    col.transform.SetParent(g, false);
                    col.transform.SetPositionAndRotation(mid + Vector3.up * 2.6f, rot);
                    col.AddComponent<BoxCollider>().size = new Vector3(7f, 6f, len);
                    made++;
                }
            Flush("Skybridges_Geo", g);
        }

        // ------------------------------------------------------------------ maglev
        void MonorailTrack()
        {
            var g = new GameObject("Monorail").transform;
            g.SetParent(root, false);
            float y = MonorailY;
            for (int k = 0; k < Monorail.Length; k++)
            {
                Vector2 a = Monorail[k], b = Monorail[(k + 1) % Monorail.Length];
                Vector3 p0 = new(a.x, y, a.y), p1 = new(b.x, y, b.y);
                Vector3 d = (p1 - p0).normalized;
                float len = Vector3.Distance(p0, p1);
                var rot = Quaternion.LookRotation(d);
                Vector3 mid = (p0 + p1) / 2;
                B(concreteA).BoxCentered(mid, new Vector3(3.2f, 1.8f, len + 3.2f), Tile(4, 2), null, rot);
                B(metal).BoxCentered(mid + Vector3.up * 1.05f, new Vector3(1.2f, 0.3f, len + 3.2f), Tile(4, 1), null, rot);
                B(ledBlue).BoxCentered(mid - Vector3.up * 0.95f, new Vector3(0.5f, 0.12f, len), Tile(4, 1), null, rot);
                foreach (int s in new[] { -1, 1 })
                    B(LedWhite).BoxCentered(mid + Vector3.Cross(Vector3.up, d) * 1.62f * s + Vector3.up * 0.5f, new Vector3(0.06f, 0.12f, len), Tile(4, 1), null, rot);
                var col = new GameObject("Beam");
                col.transform.SetParent(g, false);
                col.transform.SetPositionAndRotation(mid, rot);
                col.AddComponent<BoxCollider>().size = new Vector3(3.2f, 1.8f, len + 3.2f);
                for (float s = 20; s < len - 10; s += 42f)
                {
                    Vector3 pp = p0 + d * s;
                    float gy = H(pp.x, pp.z) + 0.12f;
                    B(concreteB).Cylinder(new Vector3(pp.x, gy, pp.z), new Vector3(pp.x, y - 0.9f, pp.z), 1.1f, 12);
                    B(concreteB).BoxCentered(new Vector3(pp.x, y - 1.6f, pp.z), new Vector3(4.2f, 1.2f, 4.2f), Tile(2, 1), null, rot);
                    B(Hazard).Cylinder(new Vector3(pp.x, gy, pp.z), new Vector3(pp.x, gy + 1.2f, pp.z), 1.2f, 12);
                    AddBoxCollider(g, new Vector3(pp.x - 1, gy, pp.z - 1), new Vector3(pp.x + 1, y, pp.z + 1), "Pylon");
                }
            }
            // two stations on the long sides
            foreach (var sc in new[] { new Vector2(Monorail[0].x, 280), new Vector2(Monorail[2].x, 280) })
            {
                Vector3 c = new(sc.x, y, sc.y);
                foreach (int s in new[] { -1, 1 })
                {
                    Vector3 pc = c + Vector3.right * 4.6f * s;
                    B(concreteA).BoxCentered(pc + Vector3.up * 0.3f, new Vector3(5f, 1.2f, 60f), Tile(4, 4));
                    B(Hazard).BoxCentered(pc + Vector3.up * 0.92f - Vector3.right * 2.3f * s, new Vector3(0.4f, 0.04f, 60f), Tile(1, 4));
                    B(shipGlass).BoxCentered(pc + Vector3.up * 3.2f + Vector3.right * 2.4f * s, new Vector3(0.15f, 4.6f, 58f), Tile(4, 4));
                    AddBoxCollider(g, pc + new Vector3(-2.5f, -0.3f, -30), pc + new Vector3(2.5f, 0.9f, 30), "Platform");
                }
                B(metal).BoxCentered(c + Vector3.up * 6.4f, new Vector3(16f, 0.5f, 64f), Tile(4, 4));
                B(ledBlue).BoxCentered(c + Vector3.up * 6.1f, new Vector3(14f, 0.1f, 60f), Tile(4, 4));
                foreach (int s in new[] { -1, 1 })
                    for (float z = -28; z <= 28; z += 14)
                        B(metal).Cylinder(c + new Vector3(7.5f * s, -y + H(c.x, c.z + z), z), c + new Vector3(7.5f * s, 6.4f, z), 0.4f, 8);
            }
            Flush("Monorail_Geo", g);
        }

        // ------------------------------------------------------------------ arena floors
        class Trench { public Rect r; public bool alongX; }

        /// <summary>
        /// Builds the arena floor as 6 m tiles over the oval, dropping the tiles inside
        /// each trench 3.5 m (with ramps at both ends), plus colliders.
        /// </summary>
        void ArenaFloor(Vector2 c, float y, float rx, float rz, Material floorMat, Material trenchMat, List<Trench> trenches, Transform parent)
        {
            const float cell = 6f, depth = 3.5f, ramp = 12f;
            var cols = new GameObject("Floor").transform;
            cols.SetParent(parent, false);
            int nx = Mathf.CeilToInt(rx * 2 / cell), nz = Mathf.CeilToInt(rz * 2 / cell);
            float x0 = -nx * cell / 2, z0 = -nz * cell / 2;
            bool Inside(float lx, float lz) => (lx / rx) * (lx / rx) + (lz / rz) * (lz / rz) <= 1.12f;
            Trench TrenchAt(float lx, float lz) { foreach (var t in trenches) if (t.r.Contains(new Vector2(lx, lz))) return t; return null; }
            // floor surface height at a local point (ramps slope down into trenches)
            float Floor(float lx, float lz)
            {
                var t = TrenchAt(lx, lz);
                if (t == null) return y;
                float along = t.alongX ? Mathf.Min(lx - t.r.xMin, t.r.xMax - lx) : Mathf.Min(lz - t.r.yMin, t.r.yMax - lz);
                return y - depth * Mathf.Clamp01(along / ramp);
            }
            for (int j = 0; j < nz; j++)
            {
                int runStart = -1;
                for (int i = 0; i <= nx; i++)
                {
                    float lx = x0 + (i + 0.5f) * cell, lz = z0 + (j + 0.5f) * cell;
                    bool solid = i < nx && Inside(lx, lz) && TrenchAt(lx, lz) == null;
                    if (solid && runStart < 0) runStart = i;
                    if (!solid && runStart >= 0)
                    {
                        // one collider and one quad per run of plain floor
                        float ax = x0 + runStart * cell, bx = x0 + i * cell, az = z0 + j * cell, bz = az + cell;
                        Vector3 A = new(c.x + ax, y + 0.08f, c.y + az), Bq = new(c.x + bx, y + 0.08f, c.y + az);
                        QuadAuto(floorMat, A, Bq, Bq + Vector3.forward * cell, A + Vector3.forward * cell, Vector3.up, 8, 8, new Vector2((c.x + ax) / 8, (c.y + az) / 8));
                        AddBoxCollider(cols, new Vector3(c.x + ax, y - 2f, c.y + az), new Vector3(c.x + bx, y + 0.08f, c.y + bz), "Tile");
                        runStart = -1;
                    }
                }
            }
            foreach (var t in trenches)
            {
                // trench bed in 3 m strips so the ramps slope smoothly
                int steps = Mathf.CeilToInt((t.alongX ? t.r.width : t.r.height) / 3f);
                for (int k = 0; k < steps; k++)
                {
                    float s0 = k / (float)steps, s1 = (k + 1) / (float)steps;
                    Vector2 a0, a1, b0, b1;
                    if (t.alongX)
                    {
                        float xa = Mathf.Lerp(t.r.xMin, t.r.xMax, s0), xb = Mathf.Lerp(t.r.xMin, t.r.xMax, s1);
                        a0 = new Vector2(xa, t.r.yMin); a1 = new Vector2(xa, t.r.yMax); b0 = new Vector2(xb, t.r.yMin); b1 = new Vector2(xb, t.r.yMax);
                    }
                    else
                    {
                        float za = Mathf.Lerp(t.r.yMin, t.r.yMax, s0), zb = Mathf.Lerp(t.r.yMin, t.r.yMax, s1);
                        a0 = new Vector2(t.r.xMin, za); a1 = new Vector2(t.r.xMax, za); b0 = new Vector2(t.r.xMin, zb); b1 = new Vector2(t.r.xMax, zb);
                    }
                    Vector3 W(Vector2 p) => new(c.x + p.x, Floor(p.x, p.y) + 0.08f, c.y + p.y);
                    QuadAuto(trenchMat, W(a0), W(a1), W(b1), W(b0), Vector3.up, 4, 4);
                    // collider strip following the slope
                    Vector3 m0 = (W(a0) + W(a1)) / 2, m1 = (W(b0) + W(b1)) / 2;
                    var go = new GameObject("TrenchBed");
                    go.transform.SetParent(cols, false);
                    go.transform.SetPositionAndRotation((m0 + m1) / 2 - Vector3.up * 0.5f, Quaternion.LookRotation(m1 - m0));
                    go.AddComponent<BoxCollider>().size = new Vector3(t.alongX ? t.r.height : t.r.width, 1f, Vector3.Distance(m0, m1) + 0.2f);
                }
                // side walls of the cut, with a hazard lip
                foreach (int sd in new[] { -1, 1 })
                {
                    int segs = Mathf.CeilToInt((t.alongX ? t.r.width : t.r.height) / 3f);
                    for (int k = 0; k < segs; k++)
                    {
                        float s0 = k / (float)segs, s1 = (k + 1) / (float)segs;
                        Vector2 p0, p1;
                        if (t.alongX) { float zz = sd < 0 ? t.r.yMin : t.r.yMax; p0 = new Vector2(Mathf.Lerp(t.r.xMin, t.r.xMax, s0), zz); p1 = new Vector2(Mathf.Lerp(t.r.xMin, t.r.xMax, s1), zz); }
                        else { float xx = sd < 0 ? t.r.xMin : t.r.xMax; p0 = new Vector2(xx, Mathf.Lerp(t.r.yMin, t.r.yMax, s0)); p1 = new Vector2(xx, Mathf.Lerp(t.r.yMin, t.r.yMax, s1)); }
                        float f0 = Floor(p0.x - (t.alongX ? 0 : sd * 0.01f), p0.y - (t.alongX ? sd * 0.01f : 0));
                        float f1 = Floor(p1.x - (t.alongX ? 0 : sd * 0.01f), p1.y - (t.alongX ? sd * 0.01f : 0));
                        Vector3 inward = t.alongX ? new Vector3(0, 0, -sd) : new Vector3(-sd, 0, 0);
                        Vector3 A = new(c.x + p0.x, f0, c.y + p0.y), Bq = new(c.x + p1.x, f1, c.y + p1.y);
                        if (y - Mathf.Min(f0, f1) > 0.05f)
                            QuadAuto(trenchMat, A, Bq, new Vector3(Bq.x, y + 0.08f, Bq.z), new Vector3(A.x, y + 0.08f, A.z), inward, 3, 3);
                    }
                    Vector3 l0, l1;
                    if (t.alongX) { float zz = sd < 0 ? t.r.yMin : t.r.yMax; l0 = new Vector3(c.x + t.r.xMin, y, c.y + zz); l1 = new Vector3(c.x + t.r.xMax, y, c.y + zz); }
                    else { float xx = sd < 0 ? t.r.xMin : t.r.xMax; l0 = new Vector3(c.x + xx, y, c.y + t.r.yMin); l1 = new Vector3(c.x + xx, y, c.y + t.r.yMax); }
                    Vector3 ld = (l1 - l0).normalized;
                    B(Hazard).BoxCentered((l0 + l1) / 2 + Vector3.up * 0.12f, new Vector3(Mathf.Abs(ld.x) * Vector3.Distance(l0, l1) + 0.35f, 0.1f, Mathf.Abs(ld.z) * Vector3.Distance(l0, l1) + 0.35f), Tile(2, 1));
                }
            }
        }

        // ------------------------------------------------------------------ arena props (all positions local to the arena center)
        Vector3 L(Vector2 c, float y, float x, float z) => new(c.x + x, y, c.y + z);

        void CoverWall(Vector2 c, float y, float x, float z, float len, float hgt, float yaw, Transform parent, Material m = null)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 p = L(c, y, x, z);
            B(m ? m : concreteB).BoxCentered(p + Vector3.up * hgt / 2, new Vector3(len, hgt, 2.2f), Tile(4, 4), null, rot);
            B(darkMetal).BoxCentered(p + Vector3.up * (hgt + 0.15f), new Vector3(len + 0.2f, 0.3f, 2.4f), Tile(4, 1), null, rot);
            // buttresses on the back face
            for (float s = -len / 2 + 1.5f; s <= len / 2 - 1.5f; s += 4f)
                B(m ? m : concreteB).BoxCentered(p + rot * new Vector3(s, hgt * 0.35f, -1.6f), new Vector3(0.9f, hgt * 0.7f, 1.2f), Tile(2, 2), null, rot);
            B(Hazard).BoxCentered(p + rot * new Vector3(0, hgt * 0.75f, 1.12f), new Vector3(len * 0.9f, 0.4f, 0.05f), Tile(2, 1), null, rot);
            Col(parent, p + Vector3.up * hgt / 2, rot, new Vector3(len, hgt, 2.4f));
        }

        void Col(Transform parent, Vector3 center, Quaternion rot, Vector3 size)
        {
            var go = new GameObject("Cover");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<BoxCollider>().size = size;
        }

        void ContainerStack(Vector2 c, float y, float x, float z, float yaw, int levels, Transform parent)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 p = L(c, y, x, z);
            for (int l = 0; l < levels; l++)
            {
                float jitter = R(-0.8f, 0.8f);
                B(Pick(containerMats)).BoxCentered(p + rot * new Vector3(jitter, 1.3f + l * 2.6f, 0), new Vector3(2.45f, 2.6f, 12.2f), Tile(6, 2.6f), null, rot * Quaternion.Euler(0, R(-4, 4), 0));
                if (l == 0 && R() < 0.6f)
                    B(Pick(containerMats)).BoxCentered(p + rot * new Vector3(2.6f, 1.3f, R(-2, 2)), new Vector3(2.45f, 2.6f, 12.2f), Tile(6, 2.6f), null, rot);
            }
            Col(parent, p + Vector3.up * levels * 1.3f, rot, new Vector3(5.4f, levels * 2.6f, 12.4f));
        }

        void Barriers(Vector2 c, float y, float x, float z, float yaw, int count, Transform parent)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 p = L(c, y, x, z);
            for (int k = 0; k < count; k++)
            {
                Vector3 bp = p + rot * new Vector3((k - (count - 1) / 2f) * 4.2f, 0, Mathf.Sin(k * 1.7f) * 0.8f);
                var br = rot * Quaternion.Euler(0, R(-12, 12), 0);
                // jersey profile: wide foot, narrow cap
                B(concreteA).BoxCentered(bp + Vector3.up * 0.6f, new Vector3(4f, 1.2f, 2.0f), Tile(2, 1), null, br);
                B(concreteA).BoxCentered(bp + Vector3.up * 2.1f, new Vector3(4f, 1.8f, 1.1f), Tile(2, 1), null, br);
                B(Hazard).BoxCentered(bp + Vector3.up * 2.4f + br * new Vector3(0, 0, 0.56f), new Vector3(3.6f, 0.3f, 0.02f), Tile(1, 1), null, br);
            }
            Col(parent, p + Vector3.up * 1.5f, rot, new Vector3(count * 4.2f, 3f, 2.2f));
        }

        /// <summary>A gutted mid-rise inside the arena: shell walls, broken top floors, rebar.</summary>
        void RuinTower(Vector2 c, float y, float x, float z, float w, float d, float h, Transform parent)
        {
            Vector3 p = L(c, y, x, z);
            var facade = R() < 0.5f ? DayFacade("FacadeDayStone") : DayFacade("FacadeDayOffice");
            B(facade).Box(p + new Vector3(-w / 2, 0, -d / 2), p + new Vector3(w / 2, h, d / 2), Tile(12, 14.4f), 63 & ~8 & ~4);
            // jagged broken crown
            for (float s = -w / 2; s < w / 2 - 0.1f; s += 3f)
            {
                float hh = R(1f, 7f);
                B(concreteB).Box(p + new Vector3(s, h, -d / 2), p + new Vector3(s + 3f, h + hh, -d / 2 + 0.6f), Tile(4, 4));
                B(concreteB).Box(p + new Vector3(s, h, d / 2 - 0.6f), p + new Vector3(s + 3f, h + R(0.5f, 5f), d / 2), Tile(4, 4));
                if (R() < 0.5f) B(Rust).Cylinder(p + new Vector3(s + 1.5f, h + hh, -d / 2 + 0.3f), p + new Vector3(s + 1.5f + R(-1, 1), h + hh + R(1.5f, 3.5f), -d / 2 + 0.3f), 0.08f, 4);
            }
            B(roof).Box(p + new Vector3(-w / 2, h - 0.3f, -d / 2), p + new Vector3(w / 2, h, d / 2), Tile(6, 6), 4);
            // a blast hole at ground level reads as a breach you can shoot through
            B(darkMetal).Box(p + new Vector3(-2.5f, 0.2f, -d / 2 - 0.05f), p + new Vector3(2.5f, 6.5f, -d / 2), Tile(4, 4), 32);
            B(darkMetal).Box(p + new Vector3(-2.5f, 0.2f, d / 2), p + new Vector3(2.5f, 6.5f, d / 2 + 0.05f), Tile(4, 4), 16);
            AddBoxCollider(parent, p + new Vector3(-w / 2, 0, -d / 2), p + new Vector3(w / 2, h, d / 2), "HighRise");
        }

        /// <summary>Raised slab with a ramp on one side (rampDir: 0 +x, 1 -x, 2 +z, 3 -z).</summary>
        void Platform(Vector2 c, float y, float x, float z, float w, float d, float h, int[] ramps, Transform parent, Material top = null, bool tech = false)
        {
            Vector3 p = L(c, y, x, z);
            B(tech ? HexMetal : concreteB).Box(p + new Vector3(-w / 2, 0, -d / 2), p + new Vector3(w / 2, h, d / 2), Tile(4, 4), 63 & ~4 & ~8);
            QuadAuto(top ? top : concreteA, p + new Vector3(-w / 2, h, -d / 2), p + new Vector3(w / 2, h, -d / 2), p + new Vector3(w / 2, h, d / 2), p + new Vector3(-w / 2, h, d / 2), Vector3.up, 4, 4);
            B(tech ? ledBlue : Hazard).Box(p + new Vector3(-w / 2 - 0.05f, h - 0.4f, -d / 2 - 0.05f), p + new Vector3(w / 2 + 0.05f, h - 0.15f, d / 2 + 0.05f), Tile(4, 1), 63 & ~4 & ~8);
            AddBoxCollider(parent, p + new Vector3(-w / 2, 0, -d / 2), p + new Vector3(w / 2, h, d / 2), "Platform");
            foreach (int rampDir in ramps) Ramp(p, w, d, h, rampDir, parent, top, tech);
        }

        void Ramp(Vector3 p, float w, float d, float h, int rampDir, Transform parent, Material top, bool tech)
        {
            float rl = h * 3.2f;
            Vector3 dir = rampDir switch { 0 => Vector3.right, 1 => Vector3.left, 2 => Vector3.forward, _ => Vector3.back };
            float halfAcross = rampDir < 2 ? d / 2 * 0.7f : w / 2 * 0.7f;
            float edge = rampDir < 2 ? w / 2 : d / 2;
            Vector3 across = rampDir < 2 ? Vector3.forward : Vector3.right;
            Vector3 topEdge = p + dir * edge + Vector3.up * h, foot = p + dir * (edge + rl);
            QuadAuto(top ? top : concreteA, topEdge - across * halfAcross, topEdge + across * halfAcross, foot + across * halfAcross, foot - across * halfAcross, Vector3.up, 4, 4);
            foreach (int s in new[] { -1, 1 })
                TriAuto(tech ? HexMetal : concreteB, topEdge + across * halfAcross * s, foot + across * halfAcross * s, p + dir * edge + across * halfAcross * s, across * s);
            var go = new GameObject("Ramp");
            go.transform.SetParent(parent, false);
            Vector3 mid = (topEdge + foot) / 2;
            go.transform.SetPositionAndRotation(mid - Vector3.up * 0.4f, Quaternion.LookRotation(foot - topEdge));
            go.AddComponent<BoxCollider>().size = new Vector3(halfAcross * 2, 0.8f, Vector3.Distance(topEdge, foot));
        }

        void Bunker(Vector2 c, float y, float x, float z, float yaw, Transform parent)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 p = L(c, y, x, z);
            B(concreteB).BoxCentered(p + Vector3.up * 2.4f, new Vector3(12f, 4.8f, 8f), Tile(4, 4), null, rot);
            B(concreteA).BoxCentered(p + Vector3.up * 5.1f, new Vector3(13f, 0.6f, 9f), Tile(4, 4), null, rot);
            B(darkMetal).BoxCentered(p + rot * new Vector3(0, 3.6f, 4.02f), new Vector3(9f, 0.8f, 0.05f), Tile(2, 1), null, rot);
            B(Hazard).BoxCentered(p + rot * new Vector3(0, 0.4f, 4.02f), new Vector3(12f, 0.3f, 0.04f), Tile(2, 1), null, rot);
            Col(parent, p + Vector3.up * 2.7f, rot, new Vector3(12f, 5.4f, 8f));
        }

        void HexPillar(Vector2 c, float y, float x, float z, float r, float h, Transform parent)
        {
            Vector3 p = L(c, y, x, z);
            B(HexMetal).Cylinder(p, p + Vector3.up * h, r, 6);
            for (float yy = 8f; yy < h - 2; yy += 10f)
                B(ledBlue).Cylinder(p + Vector3.up * yy, p + Vector3.up * (yy + 0.35f), r + 0.12f, 6);
            B(ledBlue).Cylinder(p + Vector3.up * (h - 0.6f), p + Vector3.up * h, r + 0.2f, 6);
            B(HexMetal).Cylinder(p + Vector3.up * h, p + Vector3.up * (h + 1.4f), r + 1.2f, 6, null, true, r * 0.8f);
            // panel seams
            for (int k = 0; k < 6; k++)
            {
                float a = (k + 0.5f) / 6f * Mathf.PI * 2;
                Vector3 o = new(Mathf.Cos(a) * r * 0.87f, 0, Mathf.Sin(a) * r * 0.87f);
                B(darkMetal).Box(p + o + new Vector3(-0.15f, 0, -0.15f), p + o + new Vector3(0.15f, h, 0.15f), Tile(1, 8));
            }
            AddBoxCollider(parent, p + new Vector3(-r * 0.86f, 0, -r * 0.86f), p + new Vector3(r * 0.86f, h + 1.4f, r * 0.86f), "Pillar");
        }

        void ShieldWall(Vector2 c, float y, float x, float z, float len, float hgt, float yaw, Transform parent)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 p = L(c, y, x, z);
            Vector3 a = rot * Vector3.right;
            // emitter posts and a translucent energy panel between them
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 pp = p + a * (len / 2) * s;
                B(HexMetal).BoxCentered(pp + Vector3.up * (hgt / 2 + 0.3f), new Vector3(1.2f, hgt + 0.6f, 1.6f), Tile(1, 4), null, rot);
                B(ledBlue).BoxCentered(pp + Vector3.up * (hgt / 2 + 0.3f) + rot * new Vector3(0, 0, 0.82f), new Vector3(0.3f, hgt, 0.04f), Tile(1, 4), null, rot);
            }
            B(HexMetal).BoxCentered(p + Vector3.up * 0.4f, new Vector3(len, 0.8f, 1.4f), Tile(4, 1), null, rot);
            Vector3 b0 = p - a * (len / 2 - 0.6f) + Vector3.up * 0.8f, b1 = p + a * (len / 2 - 0.6f) + Vector3.up * 0.8f;
            Vector3 n = rot * Vector3.forward;
            QuadAuto(Shield, b0, b1, b1 + Vector3.up * hgt, b0 + Vector3.up * hgt, n, len, hgt);
            QuadAuto(Shield, b0, b1, b1 + Vector3.up * hgt, b0 + Vector3.up * hgt, -n, len, hgt);
            Col(parent, p + Vector3.up * (hgt / 2 + 0.4f), rot, new Vector3(len, hgt + 0.8f, 1.4f));
        }

        void TechCrate(Vector2 c, float y, float x, float z, float s, float yaw, Transform parent)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 p = L(c, y, x, z) + Vector3.up * s / 2;
            B(HexMetal).BoxCentered(p, Vector3.one * s, Tile(2, 2), null, rot);
            foreach (int k in new[] { -1, 1 })
                B(ledBlue).BoxCentered(p + rot * new Vector3(0, k * (s / 2 - 0.2f), s / 2 + 0.02f), new Vector3(s * 0.8f, 0.12f, 0.02f), Tile(1, 1), null, rot);
            Col(parent, p, rot, Vector3.one * s);
        }

        void Catwalk(Vector3 a, Vector3 b, Transform parent)
        {
            Vector3 d = (b - a).normalized;
            float len = Vector3.Distance(a, b);
            var rot = Quaternion.LookRotation(d);
            Vector3 mid = (a + b) / 2;
            B(HexMetal).BoxCentered(mid, new Vector3(5f, 0.6f, len), Tile(2, 4), null, rot);
            foreach (int s in new[] { -1, 1 })
            {
                B(metal).BoxCentered(mid + rot * new Vector3(2.4f * s, 1.1f, 0), new Vector3(0.12f, 0.12f, len), Tile(1, 4), null, rot);
                B(ledBlue).BoxCentered(mid + rot * new Vector3(2.5f * s, -0.25f, 0), new Vector3(0.08f, 0.1f, len), Tile(1, 4), null, rot);
            }
            Col(parent, mid, rot, new Vector3(5f, 0.6f, len));
        }

        // ------------------------------------------------------------------ the two arena layouts
        /// <summary>Ground Arena (dirt floor, 92 x 70): an urban ruin course with trenches and containers.</summary>
        void GroundArenaLayout(Vector2 c, float y, Transform g)
        {
            var trenches = new List<Trench>
            {
                new() { r = Rect.MinMaxRect(-62, -4, 62, 4), alongX = true },
                new() { r = Rect.MinMaxRect(-82, -36, -73, 30), alongX = false },
                new() { r = Rect.MinMaxRect(73, -30, 82, 36), alongX = false },
            };
            ArenaFloor(c, y, 97, 75, fieldSand, TrenchMat, trenches, g);
            // two gutted high-rises on opposite flanks
            RuinTower(c, y, -34, 24, 16, 14, 34, g);
            RuinTower(c, y, 36, -24, 14, 16, 42, g);
            // raised firing platforms with ramps
            Platform(c, y, 58, 26, 18, 12, 6.5f, new[] { 1 }, g);
            Platform(c, y, -58, -26, 18, 12, 6.5f, new[] { 0 }, g);
            // cover walls staggered across the mid-line
            CoverWall(c, y, -36, -14, 16, 6.5f, 0, g);
            CoverWall(c, y, 34, 14, 16, 6.5f, 0, g);
            CoverWall(c, y, 0, 20, 14, 6f, 0, g);
            CoverWall(c, y, 0, -20, 14, 6f, 0, g);
            CoverWall(c, y, -60, 44, 12, 6.5f, 35, g);
            CoverWall(c, y, 60, -44, 12, 6.5f, 35, g);
            // container stacks and barrier lines
            ContainerStack(c, y, -18, 40, 80, 3, g);
            ContainerStack(c, y, 20, -40, 80, 3, g);
            ContainerStack(c, y, -66, 12, 10, 2, g);
            ContainerStack(c, y, 66, -12, 10, 2, g);
            ContainerStack(c, y, 48, 46, -30, 2, g);
            ContainerStack(c, y, -48, -46, -30, 2, g);
            Barriers(c, y, -20, 9, 0, 3, g);
            Barriers(c, y, 20, -9, 0, 3, g);
            Barriers(c, y, -84, 42, 70, 2, g);
            Barriers(c, y, 84, -42, 70, 2, g);
            Bunker(c, y, -14, -32, 0, g);
            Bunker(c, y, 14, 32, 180, g);
        }

        /// <summary>Championship Arena (polished floor, 112 x 92): tech pillars, shields, a raised dais and a trench ring.</summary>
        void ChampionshipLayout(Vector2 c, float y, Transform g)
        {
            var trenches = new List<Trench>
            {
                new() { r = Rect.MinMaxRect(-46, 38, 46, 46), alongX = true },
                new() { r = Rect.MinMaxRect(-46, -46, 46, -38), alongX = true },
                new() { r = Rect.MinMaxRect(54, -30, 62, 30), alongX = false },
                new() { r = Rect.MinMaxRect(-62, -30, -54, 30), alongX = false },
            };
            ArenaFloor(c, y, 117, 97, fieldFloor, HexMetal, trenches, g);
            // floor light grid
            for (float x = -96; x <= 96; x += 24)
                B(ledBlue).Box(L(c, y + 0.1f, x - 0.15f, -80), L(c, y + 0.14f, x + 0.15f, 80), Tile(1, 4), 4);
            // central dais with ramps to north and south, and catwalks out to the flank towers
            Platform(c, y, 0, 0, 26, 26, 7, new[] { 2, 3 }, g, null, true);
            // hex pillar high-rises
            var pillars = new (float x, float z, float r, float h)[] { (-82, 40, 7, 48), (82, -40, 7, 48), (-80, -48, 6, 30), (80, 48, 6, 30), (-30, 74, 5, 24), (30, -74, 5, 24), (34, 72, 6, 56), (-34, -72, 6, 56) };
            foreach (var p in pillars) HexPillar(c, y, p.x, p.z, p.r, p.h, g);
            // flank platforms joined to the dais by catwalks at deck height
            Platform(c, y, -88, 0, 16, 22, 9, new[] { 2 }, g, null, true);
            Platform(c, y, 88, 0, 16, 22, 9, new[] { 3 }, g, null, true);
            Catwalk(L(c, y + 9, -80, 0), L(c, y + 7, -13, 0), g);
            Catwalk(L(c, y + 9, 80, 0), L(c, y + 7, 13, 0), g);
            // energy shield cover
            ShieldWall(c, y, -24, 24, 14, 7, 0, g);
            ShieldWall(c, y, 24, -24, 14, 7, 0, g);
            ShieldWall(c, y, 24, 26, 12, 7, 20, g);
            ShieldWall(c, y, -24, -26, 12, 7, 20, g);
            ShieldWall(c, y, -96, 30, 12, 7, 90, g);
            ShieldWall(c, y, 96, -30, 12, 7, 90, g);
            ShieldWall(c, y, -60, 60, 14, 7, 45, g);
            ShieldWall(c, y, 60, -60, 14, 7, 45, g);
            // tech crate clusters
            foreach (var (x, z) in new[] { (-44, 20f), (44, -20f), (-70, -20f), (70, 20f), (-16, 56f), (16, -56f), (50, 56f), (-50, -56f) })
            {
                TechCrate(c, y, x, z, 4.5f, R(0, 90), g);
                TechCrate(c, y, x + 4.6f, z + R(-1, 1), 3.6f, R(0, 90), g);
                if (R() < 0.6f) TechCrate(c, y + 4.5f, x + 0.4f, z, 3.2f, R(0, 90), g);
            }
        }
    }
}
