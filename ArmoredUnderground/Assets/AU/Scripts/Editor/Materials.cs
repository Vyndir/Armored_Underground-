using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AU.EditorTools
{
    /// <summary>Creates and caches the project's materials as assets.</summary>
    public static class Mats
    {
        public const string Dir = "Assets/AU/Generated/Materials";
        static readonly Dictionary<string, Material> Cache = new();

        public static void Reset() => Cache.Clear();

        static Material Make(string name, Shader shader)
        {
            if (Cache.TryGetValue(name, out var m) && m) return m;
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            Cache[name] = m;
            return m;
        }

        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");

        public static Material LitMat(string name, Color color, float smooth, float metal, Texture2D albedo = null, Texture2D normal = null, Texture2D emission = null, Color? emissionColor = null, float normalScale = 1f)
        {
            var m = Make(name, Lit);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetTexture("_BaseMap", albedo);
            if (normal)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", normalScale);
                m.EnableKeyword("_NORMALMAP");
            }
            else m.DisableKeyword("_NORMALMAP");
            if (emission || emissionColor.HasValue)
            {
                m.SetTexture("_EmissionMap", emission);
                m.SetColor("_EmissionColor", emissionColor ?? Color.white);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Neon(string name, Color hdr, Texture2D tex = null, float flicker = 0, float seed = 0)
        {
            var m = Make(name, Shader.Find("AU/Neon"));
            m.SetColor("_Color", hdr);
            m.SetTexture("_MainTex", tex);
            m.SetFloat("_Flicker", flicker);
            m.SetFloat("_FlickerSeed", seed);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Glow(string name, Color hdr, float edge = 2f, float heightFade = 0.6f, Texture2D tex = null)
        {
            var m = Make(name, Shader.Find("AU/Glow"));
            m.SetColor("_Color", hdr);
            m.SetFloat("_EdgePower", edge);
            m.SetFloat("_HeightFade", heightFade);
            m.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Particle(string name, Texture2D tex, Color color, bool additive)
        {
            var m = Make(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1); // transparent
            m.SetFloat("_Blend", additive ? 2 : 0); // 2 = additive, 0 = alpha
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetInt("_SrcBlend", additive ? (int)BlendMode.SrcAlpha : (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD"); else m.DisableKeyword("_BLENDMODE_ADD");
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Custom(string name, Shader shader)
        {
            var m = Make(name, shader);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
