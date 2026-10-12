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
