using System.Collections.Generic;
using UnityEngine;

namespace AU
{
    /// <summary>
    /// Accumulates simple geometry (boxes, quads, cylinders) with world-scaled UVs,
    /// so many props can be merged into one mesh per material.
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> v = new();
        public readonly List<Vector3> n = new();
        public readonly List<Vector2> uv = new();
        public readonly List<Color> col = new();
        public readonly List<int> t = new();
        public int Count => v.Count;
        /// <summary>Added to every tiled UV (lets buildings sharing a texture look different).</summary>
        public Vector2 uvShift;
        /// <summary>Added to every vertex position (UVs keep using the unshifted coordinates).</summary>
        public Vector3 shift;

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd, Color? color = null)
        {
            // Unity front faces are clockwise; for the triangles below this is the outward normal
            Vector3 nn = Vector3.Cross(d - a, b - a).normalized;
            int i = v.Count;
            v.Add(a + shift); v.Add(b + shift); v.Add(c + shift); v.Add(d + shift);
            n.Add(nn); n.Add(nn); n.Add(nn); n.Add(nn);
            uv.Add(uva); uv.Add(uvb); uv.Add(uvc); uv.Add(uvd);
            Color cc = color ?? Color.white;
            col.Add(cc); col.Add(cc); col.Add(cc); col.Add(cc);
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        /// <summary>Triangle; front face when a, b, c run clockwise seen from the front (Unity's convention).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Color? color = null)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a + shift); v.Add(b + shift); v.Add(c + shift);
            n.Add(nn); n.Add(nn); n.Add(nn);
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
            Color cc = color ?? Color.white;
            col.Add(cc); col.Add(cc); col.Add(cc);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        /// <summary>Quad with UVs from its world size: u along a->b, v along a->d (meters / tile).</summary>
        public void QuadTiled(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 tile, Vector2 offset = default, Color? color = null)
        {
            float w = Vector3.Distance(a, b) / tile.x, h = Vector3.Distance(a, d) / tile.y;
            offset += uvShift;
            Quad(a, b, c, d, offset, offset + new Vector2(w, 0), offset + new Vector2(w, h), offset + new Vector2(0, h), color);
        }

        /// <summary>Axis-aligned box from min/max. faces bitmask: 1=+X 2=-X 4=+Y 8=-Y 16=+Z 32=-Z.</summary>
        public void Box(Vector3 min, Vector3 max, Vector2 tile, int faces = 63, Color? color = null, Matrix4x4? xf = null)
        {
            Vector3 p(float x, float y, float z) => xf.HasValue ? xf.Value.MultiplyPoint3x4(new Vector3(x, y, z)) : new Vector3(x, y, z);
            float x0 = min.x, y0 = min.y, z0 = min.z, x1 = max.x, y1 = max.y, z1 = max.z;
            // walls get u along the wall, v up; offsets keep tiling continuous at world positions
            if ((faces & 32) != 0) QuadTiled(p(x0, y0, z0), p(x1, y0, z0), p(x1, y1, z0), p(x0, y1, z0), tile, new Vector2(x0 / tile.x, y0 / tile.y), color);
            if ((faces & 16) != 0) QuadTiled(p(x1, y0, z1), p(x0, y0, z1), p(x0, y1, z1), p(x1, y1, z1), tile, new Vector2(-x1 / tile.x, y0 / tile.y), color);
            if ((faces & 2) != 0) QuadTiled(p(x0, y0, z1), p(x0, y0, z0), p(x0, y1, z0), p(x0, y1, z1), tile, new Vector2(-z1 / tile.x, y0 / tile.y), color);
            if ((faces & 1) != 0) QuadTiled(p(x1, y0, z0), p(x1, y0, z1), p(x1, y1, z1), p(x1, y1, z0), tile, new Vector2(z0 / tile.x, y0 / tile.y), color);
            if ((faces & 4) != 0) QuadTiled(p(x0, y1, z0), p(x1, y1, z0), p(x1, y1, z1), p(x0, y1, z1), tile, new Vector2(x0 / tile.x, z0 / tile.y), color);
            if ((faces & 8) != 0) QuadTiled(p(x0, y0, z1), p(x1, y0, z1), p(x1, y0, z0), p(x0, y0, z0), tile, new Vector2(x0 / tile.x, -z1 / tile.y), color);
        }

        public void BoxCentered(Vector3 center, Vector3 size, Vector2 tile, Color? color = null, Quaternion? rot = null)
        {
            var xf = Matrix4x4.TRS(center, rot ?? Quaternion.identity, Vector3.one);
            Box(-size * 0.5f, size * 0.5f, tile, 63, color, xf);
        }

        public void Cylinder(Vector3 a, Vector3 b, float r, int sides, Color? color = null, bool caps = true, float r2 = -1)
        {
            if (r2 < 0) r2 = r;
            a += shift; b += shift;
            Vector3 dir = (b - a).normalized;
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, dir);
            Color cc = color ?? Color.white;
            float len = Vector3.Distance(a, b);
            int start = v.Count;
            for (int k = 0; k <= sides; k++)
            {
                float ang = k / (float)sides * Mathf.PI * 2;
                Vector3 o = Mathf.Cos(ang) * side + Mathf.Sin(ang) * up;
                v.Add(a + o * r); n.Add(o); uv.Add(new Vector2(k / (float)sides, 0)); col.Add(cc);
                v.Add(b + o * r2); n.Add(o); uv.Add(new Vector2(k / (float)sides, len)); col.Add(cc);
            }
            for (int k = 0; k < sides; k++)
            {
                int i = start + k * 2;
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i + 1); t.Add(i + 3); t.Add(i + 2);
            }
            if (!caps) return;
            foreach (var (c, rr, sgn) in new[] { (a, r, -1f), (b, r2, 1f) })
            {
                int ci = v.Count;
                v.Add(c); n.Add(dir * sgn); uv.Add(new Vector2(0.5f, 0.5f)); col.Add(cc);
                for (int k = 0; k <= sides; k++)
                {
                    float ang = k / (float)sides * Mathf.PI * 2;
                    Vector3 o = Mathf.Cos(ang) * side + Mathf.Sin(ang) * up;
                    v.Add(c + o * rr); n.Add(dir * sgn); uv.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f)); col.Add(cc);
                }
                for (int k = 0; k < sides; k++)
                {
                    if (sgn > 0) { t.Add(ci); t.Add(ci + 1 + k); t.Add(ci + 2 + k); }
                    else { t.Add(ci); t.Add(ci + 2 + k); t.Add(ci + 1 + k); }
                }
            }
        }

        public void Append(Mesh m, Matrix4x4 xf, Color? color = null)
        {
            int start = v.Count;
            var mv = m.vertices; var mn = m.normals; var muv = m.uv;
            for (int i = 0; i < mv.Length; i++)
            {
                v.Add(xf.MultiplyPoint3x4(mv[i]) + shift);
                n.Add(mn.Length > i ? xf.MultiplyVector(mn[i]).normalized : Vector3.up);
                uv.Add(muv.Length > i ? muv[i] : Vector2.zero);
                col.Add(color ?? Color.white);
            }
            foreach (var idx in m.triangles) t.Add(start + idx);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }
}
