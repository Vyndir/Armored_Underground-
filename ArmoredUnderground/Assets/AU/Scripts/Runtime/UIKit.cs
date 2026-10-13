using UnityEngine;
using UnityEngine.UI;

namespace AU
{
    /// <summary>Helpers to build uGUI in code, plus generated sprites.</summary>
    public static class UIKit
    {
        public static Font Font => _font ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        static Font _font;
        public static readonly Color Neon = new(0f, 0.9f, 1f);
        public static readonly Color Hot = new(1f, 0.17f, 0.6f);
        public static readonly Color Amber = new(1f, 0.7f, 0.1f);
        public static readonly Color Ink = new(0.92f, 0.94f, 0.97f);
        public static readonly Color Dim = new(0.55f, 0.6f, 0.68f);
        public static readonly Color Panel = new(0.02f, 0.035f, 0.07f, 0.82f);

        static Sprite _white, _circle, _ring, _soft, _vignette, _diamond;
        public static Sprite White => _white ? _white : (_white = Gen(4, (x, y) => 1f, false));
        public static Sprite Circle => _circle ? _circle : (_circle = Gen(128, (x, y) => Mathf.Clamp01((1f - R(x, y)) * 64f), false));
        public static Sprite Soft => _soft ? _soft : (_soft = Gen(64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - R(x, y)), 2f), false));
        public static Sprite Ring => _ring ? _ring : (_ring = Gen(256, (x, y) => { float r = R(x, y); return Mathf.Clamp01((r - 0.8f) * 128f) * Mathf.Clamp01((1f - r) * 128f); }, false));
        public static Sprite Vignette => _vignette ? _vignette : (_vignette = Gen(256, (x, y) => Mathf.Pow(Mathf.Clamp01(R(x, y) - 0.45f) / 0.55f, 1.6f), false));
        public static Sprite Diamond => _diamond ? _diamond : (_diamond = Gen(128, (x, y) => { float d = Mathf.Abs(x - 0.5f) + Mathf.Abs(y - 0.5f); return Mathf.Clamp01((0.5f - d) * 200f) * Mathf.Clamp01((d - 0.42f) * 200f); }, false));

        static float R(float x, float y) => Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) * 2f;

        // ------------------------------------------------------------------ league HUD style
        public static readonly Color Teal = new(0.18f, 0.9f, 0.86f);
        public static readonly Color Gold = new(0.98f, 0.72f, 0.2f);
        public static readonly Color Rival = new(0.95f, 0.27f, 0.2f);
        public static readonly Color Glass = new(0.03f, 0.06f, 0.1f, 0.78f);

        static Sprite _chamfer, _chamferLine, _arrow;
        /// <summary>Panel fill with clipped top-left and bottom-right corners (9-sliced).</summary>
        public static Sprite Chamfer => _chamfer ? _chamfer : (_chamfer = Sliced(64, 14, (x, y, c) => Inside(x, y, c) ? 1f : 0f));
        /// <summary>Thin outline of the chamfered panel.</summary>
        public static Sprite ChamferLine => _chamferLine ? _chamferLine : (_chamferLine = Sliced(64, 14, (x, y, c) => Inside(x, y, c) && !Inside(x, y, c, 1.6f) ? 1f : 0f));
        /// <summary>Navigation arrowhead pointing up.</summary>
        public static Sprite Arrow => _arrow ? _arrow : (_arrow = Gen(64, (x, y) =>
        {
            float dx = Mathf.Abs(x - 0.5f);
            bool head = y > 0.08f && y < 0.92f && dx < (0.92f - y) * 0.42f && !(y < 0.36f && dx < (0.36f - y) * 0.9f);
            return head ? 1f : 0f;
        }, false));

        static bool Inside(float x, float y, float c, float inset = 0)
        {
            const float S = 64;
            if (x < inset || y < inset || x > S - inset || y > S - inset) return false;
            if (x + (S - y) < c + inset * 1.414f) return false;     // top-left cut
            if ((S - x) + y < c + inset * 1.414f) return false;     // bottom-right cut
            return true;
        }

        static Sprite Sliced(int s, float cut, System.Func<float, float, float, float> a)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // 3x3 supersample for clean diagonals
                    float acc = 0;
                    for (int sy = 0; sy < 3; sy++) for (int sx = 0; sx < 3; sx++) acc += a(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f, cut);
                    px[y * s + x] = new Color(1, 1, 1, acc / 9f);
                }
            t.SetPixels(px); t.Apply();
            float b = cut + 4;
            return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Chamfered glass panel with a teal outline; returns the fill image.</summary>
        public static Image Panel2(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color? line = null, Color? fill = null)
        {
            var bg = Img(parent, name, anchor, pivot, pos, size, fill ?? Glass, Chamfer);
            bg.type = Image.Type.Sliced;
            var ln = Img(bg.transform, "Line", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, line ?? new Color(Teal.r, Teal.g, Teal.b, 0.45f), ChamferLine);
            ln.type = Image.Type.Sliced;
            ln.rectTransform.anchorMin = Vector2.zero; ln.rectTransform.anchorMax = Vector2.one; ln.rectTransform.sizeDelta = Vector2.zero;
            return bg;
        }

        /// <summary>Text with extra letter spacing, as the league HUD uses for labels.</summary>
        public static string Spaced(string s) => string.Join(" ", s.ToCharArray());

        static Texture2D _mechIcon, _portrait;
        /// <summary>Wireframe-style outline of the player's frame for the status card.</summary>
        public static Texture2D MechIcon
        {
            get
            {
                if (_mechIcon) return _mechIcon;
                const int W = 96, H = 128;
                var fill = new bool[W * H];
                void P(params float[] uv)
                {
                    var poly = new Vector2[uv.Length / 2];
                    for (int i = 0; i < poly.Length; i++) poly[i] = new Vector2(uv[i * 2] * W, uv[i * 2 + 1] * H);
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            bool inside = false;
                            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                                if (((poly[i].y > y) != (poly[j].y > y)) && (x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) inside = !inside;
                            if (inside) fill[y * W + x] = true;
                        }
                }
                P(0.36f, 0.0f, 0.47f, 0.0f, 0.46f, 0.38f, 0.38f, 0.38f);
                P(0.53f, 0.0f, 0.64f, 0.0f, 0.62f, 0.38f, 0.54f, 0.38f);
                P(0.37f, 0.36f, 0.63f, 0.36f, 0.66f, 0.48f, 0.34f, 0.48f);
                P(0.32f, 0.46f, 0.68f, 0.46f, 0.74f, 0.7f, 0.26f, 0.7f);
                P(0.45f, 0.69f, 0.55f, 0.69f, 0.56f, 0.78f, 0.44f, 0.78f);
                P(0.18f, 0.62f, 0.3f, 0.7f, 0.28f, 0.45f, 0.19f, 0.36f, 0.14f, 0.5f);
                P(0.82f, 0.62f, 0.7f, 0.7f, 0.72f, 0.45f, 0.81f, 0.36f, 0.86f, 0.5f);
                P(0.15f, 0.38f, 0.22f, 0.38f, 0.23f, 0.18f, 0.14f, 0.18f);
                P(0.85f, 0.38f, 0.78f, 0.38f, 0.77f, 0.18f, 0.86f, 0.18f);
                P(0.2f, 0.66f, 0.27f, 0.68f, 0.33f, 1.0f, 0.27f, 0.96f);
                P(0.8f, 0.66f, 0.73f, 0.68f, 0.67f, 1.0f, 0.73f, 0.96f);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        bool f = fill[y * W + x];
                        bool edge = f && (x == 0 || y == 0 || x == W - 1 || y == H - 1 || !fill[y * W + x - 1] || !fill[y * W + x + 1] || !fill[(y - 1) * W + x] || !fill[(y + 1) * W + x]);
                        bool seam = f && (y % 13 == 0 || (x + y / 3) % 17 == 0);
                        px[y * W + x] = new Color(1, 1, 1, edge ? 1f : seam ? 0.25f : f ? 0.06f : 0f);
                    }
                _mechIcon = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                _mechIcon.SetPixels(px); _mechIcon.Apply();
                return _mechIcon;
            }
        }

        /// <summary>Stylized pilot portrait for the comms panel (helmet-less head and shoulders).</summary>
        public static Texture2D Portrait
        {
            get
            {
                if (_portrait) return _portrait;
                const int S = 128;
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float u = x / (float)S, v = y / (float)S;
                        Color c = Color.Lerp(new Color(0.04f, 0.08f, 0.14f), new Color(0.08f, 0.18f, 0.26f), v);
                        float head = new Vector2((u - 0.5f) / 0.2f, (v - 0.58f) / 0.25f).magnitude;
                        float hair = new Vector2((u - 0.5f) / 0.24f, (v - 0.68f) / 0.22f).magnitude;
                        bool shoulders = v < 0.3f && Mathf.Abs(u - 0.5f) < 0.18f + (0.3f - v) * 1.4f;
                        bool neck = v < 0.4f && v > 0.25f && Mathf.Abs(u - 0.5f) < 0.07f;
                        if (shoulders) c = new Color(0.1f, 0.13f, 0.18f) + (Mathf.Abs(u - 0.5f) < 0.03f ? new Color(0.1f, 0.5f, 0.5f) : Color.clear);
                        if (neck) c = new Color(0.72f, 0.55f, 0.43f);
                        if (head < 1f) c = new Color(0.86f, 0.68f, 0.55f) * (0.85f + 0.15f * (1 - head));
                        if (hair < 1f && (v > 0.66f || Mathf.Abs(u - 0.5f) > 0.17f) && v > 0.42f) c = new Color(0.42f, 0.24f, 0.12f) * (0.8f + 0.2f * Mathf.Sin(u * 60f));
                        if (head < 1f && Mathf.Abs(v - 0.6f) < 0.025f && Mathf.Abs(Mathf.Abs(u - 0.5f) - 0.08f) < 0.03f) c = new Color(0.1f, 0.12f, 0.15f);
                        px[y * S + x] = c;
                    }
                _portrait = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                _portrait.SetPixels(px); _portrait.Apply();
                return _portrait;
            }
        }

        static Sprite Gen(int s, System.Func<float, float, float> a, bool _)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) px[y * s + x] = new Color(1, 1, 1, a((x + 0.5f) / s, (y + 0.5f) / s));
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Img(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, Sprite s = null)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = s ? s : White;
            im.color = c;
            im.raycastTarget = false;
            return im;
        }

        public static Text Txt(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c, FontStyle style = FontStyle.Bold)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = fontSize; t.alignment = align; t.color = c; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Outline Glow(Graphic g, Color c, float d = 1.5f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(c.r, c.g, c.b, 0.45f);
            o.effectDistance = new Vector2(d, -d);
            return o;
        }

        public static Button Btn(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, System.Action onClick, Color? bg = null, int fontSize = 24)
        {
            var im = Img(parent, "Btn_" + label, anchor, new Vector2(0.5f, 0.5f), pos, size, bg ?? Neon);
            im.raycastTarget = true;
            var b = im.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); colors.selectedColor = new Color(1.15f, 1.15f, 1.15f);
            b.colors = colors;
            b.onClick.AddListener(() => { AudioFX.I?.UI("ui"); onClick?.Invoke(); });
            var t = Txt(im.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, bg.HasValue && bg.Value.grayscale > 0.5f || !bg.HasValue ? new Color(0.02f, 0.06f, 0.1f) : Ink);
            t.text = label;
            return b;
        }
    }
}
