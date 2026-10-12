using System.Collections.Generic;
using UnityEngine;

namespace AU
{
    /// <summary>
    /// A tiny single-stroke font on a 4x6 grid. Each glyph is a list of polylines.
    /// Used to bend neon tube signs and to paint text into generated textures.
    /// </summary>
    public static class StrokeFont
    {
        public const float W = 4f, H = 6f, Advance = 5.2f;

        // Points are "x,y" on the 0..4 x 0..6 grid; '|' separates polylines.
        static readonly Dictionary<char, string> G = new()
        {
            ['A'] = "0,0 0,4 2,6 4,4 4,0|0,3 4,3",
            ['B'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3|3,3 4,2 4,1 3,0 0,0",
            ['C'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1",
            ['D'] = "0,0 0,6 2.5,6 4,4.5 4,1.5 2.5,0 0,0",
            ['E'] = "4,6 0,6 0,0 4,0|0,3 3,3",
            ['F'] = "4,6 0,6 0,0|0,3 3,3",
            ['G'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,3 2,3",
            ['H'] = "0,0 0,6|4,0 4,6|0,3 4,3",
            ['I'] = "1,6 3,6|2,6 2,0|1,0 3,0",
            ['J'] = "4,6 4,1 3,0 1,0 0,1",
            ['K'] = "0,0 0,6|4,6 0,2.5|1.3,3.7 4,0",
            ['L'] = "0,6 0,0 4,0",
            ['M'] = "0,0 0,6 2,3.5 4,6 4,0",
            ['N'] = "0,0 0,6 4,0 4,6",
            ['O'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0",
            ['P'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3",
            ['Q'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0|2.5,1.5 4,0",
            ['R'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3|2,3 4,0",
            ['S'] = "4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1",
            ['T'] = "0,6 4,6|2,6 2,0",
            ['U'] = "0,6 0,1 1,0 3,0 4,1 4,6",
            ['V'] = "0,6 2,0 4,6",
            ['W'] = "0,6 1,0 2,3 3,0 4,6",
            ['X'] = "0,0 4,6|0,6 4,0",
            ['Y'] = "0,6 2,3 4,6|2,3 2,0",
            ['Z'] = "0,6 4,6 0,0 4,0",
            ['0'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0|0.5,1 3.5,5",
            ['1'] = "1,5 2,6 2,0|1,0 3,0",
            ['2'] = "0,5 1,6 3,6 4,5 4,4 0,0 4,0",
            ['3'] = "0,5 1,6 3,6 4,5 4,4 3,3 1.5,3|3,3 4,2 4,1 3,0 1,0 0,1",
            ['4'] = "3,0 3,6 0,2 4,2",
            ['5'] = "4,6 0,6 0,3.5 3,3.5 4,2.5 4,1 3,0 0,0",
            ['6'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,2 3,3 0,3",
            ['7'] = "0,6 4,6 1.5,0",
            ['8'] = "1,3 0,4 0,5 1,6 3,6 4,5 4,4 3,3 1,3 0,2 0,1 1,0 3,0 4,1 4,2 3,3",
            ['9'] = "4,3 1,3 0,4 0,5 1,6 3,6 4,5 4,1 3,0 1,0",
            ['-'] = "0.5,3 3.5,3",
            ['/'] = "0,0 4,6",
            ['.'] = "1.8,0 2.2,0.4",
            ['&'] = "4,0 1,4 1,5 2,6 3,5 3,4 0,2 0,1 1,0 2,0 4,2.5",
            ['!'] = "2,6 2,1.6|2,0.4 2,0",
            ['+'] = "0.5,3 3.5,3|2,1.5 2,4.5",
            ['*'] = "2,1 2,5|0.5,2 3.5,4|0.5,4 3.5,2",
            ['\''] = "2,6 2,4.6",
            [':'] = "2,4 2,4.4|2,1 2,1.4",
        };

        /// <summary>Polylines for a string in glyph units, laid out left to right.</summary>
        public static List<List<Vector2>> Layout(string text, out float width)
        {
            var lines = new List<List<Vector2>>();
            float x = 0;
            foreach (char raw in text.ToUpperInvariant())
            {
                if (raw == ' ') { x += Advance * 0.7f; continue; }
                if (G.TryGetValue(raw, out var def))
                {
                    foreach (var part in def.Split('|'))
                    {
                        var pl = new List<Vector2>();
                        foreach (var pt in part.Trim().Split(' '))
                        {
                            var xy = pt.Split(',');
                            pl.Add(new Vector2(float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture) + x,
                                               float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)));
                        }
                        lines.Add(pl);
                    }
                }
                x += Advance;
            }
            width = Mathf.Max(0, x - (Advance - W));
            return lines;
        }

        /// <summary>
        /// Build a neon tube mesh for the text. Origin at bottom-left, X right, Y up,
        /// tubes lie in the XY plane. glyphHeight is the cap height in meters.
        /// </summary>
        public static Mesh TubeMesh(string text, float glyphHeight, float radius, int sides = 6)
        {
            var lines = Layout(text, out _);
            float s = glyphHeight / H;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            foreach (var pl in lines)
            {
                for (int i = 0; i < pl.Count - 1; i++)
                {
                    Vector3 a = pl[i] * s, b = pl[i + 1] * s;
                    AddTube(a, b, radius, sides, verts, normals, uvs, tris);
                    // joint ball so corners look continuous
                    AddTube(b - (b - a).normalized * radius * 0.5f, b + (b - a).normalized * radius * 0.5f, radius, sides, verts, normals, uvs, tris);
                }
            }
            var m = new Mesh { name = "Neon_" + text };
            m.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(verts); m.SetNormals(normals); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        static void AddTube(Vector3 a, Vector3 b, float r, int sides, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            Vector3 dir = (b - a);
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            Vector3 side = Vector3.Cross(dir, Vector3.forward);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(dir, Vector3.up);
            side.Normalize();
            Vector3 up = Vector3.Cross(side, dir);
            int baseIdx = v.Count;
            for (int k = 0; k <= sides; k++)
            {
                float ang = k / (float)sides * Mathf.PI * 2f;
                Vector3 off = (Mathf.Cos(ang) * side + Mathf.Sin(ang) * up);
                v.Add(a + off * r); n.Add(off); uv.Add(new Vector2(k / (float)sides, 0));
                v.Add(b + off * r); n.Add(off); uv.Add(new Vector2(k / (float)sides, 1));
            }
            for (int k = 0; k < sides; k++)
            {
                int i0 = baseIdx + k * 2;
                t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
                t.Add(i0 + 1); t.Add(i0 + 3); t.Add(i0 + 2);
            }
        }

        /// <summary>Paint text into a pixel buffer with soft glowing strokes.</summary>
        public static void Paint(Color[] px, int texW, int texH, string text, Rect area, Color color, float thickness, float glow)
        {
            var lines = Layout(text, out float width);
            if (width <= 0) return;
            float s = Mathf.Min(area.width / width, area.height / H);
            float ox = area.x + (area.width - width * s) * 0.5f;
            float oy = area.y + (area.height - H * s) * 0.5f;
            float reach = thickness + glow;
            foreach (var pl in lines)
            {
                for (int i = 0; i < pl.Count - 1; i++)
                {
                    Vector2 a = new Vector2(ox + pl[i].x * s, oy + pl[i].y * s);
                    Vector2 b = new Vector2(ox + pl[i + 1].x * s, oy + pl[i + 1].y * s);
                    int x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - reach)), x1 = Mathf.Min(texW - 1, (int)(Mathf.Max(a.x, b.x) + reach));
                    int y0 = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - reach)), y1 = Mathf.Min(texH - 1, (int)(Mathf.Max(a.y, b.y) + reach));
                    Vector2 ab = b - a;
                    float len2 = Mathf.Max(1e-5f, ab.sqrMagnitude);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                            float tt = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                            float d = Vector2.Distance(p, a + ab * tt);
                            float core = Mathf.Clamp01(thickness - d + 0.5f);
                            float halo = glow > 0 ? Mathf.Pow(Mathf.Clamp01(1f - (d - thickness) / glow), 2f) * 0.55f : 0f;
                            float k = Mathf.Max(core, halo);
                            if (k <= 0) continue;
                            int idx = y * texW + x;
                            Color c = core >= halo ? Color.Lerp(color, Color.white, core * 0.45f) : color;
                            px[idx] = Color.Lerp(px[idx], c, k);
                        }
                }
            }
        }
    }
}
