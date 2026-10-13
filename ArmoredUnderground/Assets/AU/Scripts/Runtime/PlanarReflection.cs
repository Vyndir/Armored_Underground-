using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AU
{
    /// <summary>
    /// Renders the street mirrored about the ground plane (y = planeHeight) into a
    /// half-res texture that AU/WetAsphalt samples in screen space. Uses a proper
    /// (non-mirrored) camera placed below the ground looking up, which renders the
    /// reflection upside down; the shader flips it back.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlanarReflection : MonoBehaviour
    {
        public float planeHeight = 0f;
        [Range(0.25f, 1f)] public float resolutionScale = 0.5f;
        public LayerMask reflectLayers = ~0;
        public float farClip = 2500f;
        float _groundProbe;

        Camera _main;
        Camera _refl;
        RenderTexture _rt;
        static readonly int TexId = Shader.PropertyToID("_PlanarReflectionTex");
        static readonly int FlipId = Shader.PropertyToID("_PlanarReflectionFlip");

        void OnEnable()
        {
            _main = GetComponent<Camera>();
            var go = new GameObject("PlanarReflectionCamera") { hideFlags = HideFlags.DontSave };
            _refl = go.AddComponent<Camera>();
            _refl.enabled = true;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            data.antialiasing = AntialiasingMode.None;
            _refl.depth = _main.depth - 10;
            Shader.SetGlobalFloat(FlipId, 1f);
        }

        void OnDisable()
        {
            if (_refl) Destroy(_refl.gameObject);
            if (_rt) { _rt.Release(); Destroy(_rt); }
            Shader.SetGlobalTexture(TexId, Texture2D.blackTexture);
        }

        void EnsureTarget()
        {
            int w = Mathf.Max(64, (int)(Screen.width * resolutionScale));
            int h = Mathf.Max(64, (int)(Screen.height * resolutionScale));
            if (_rt && _rt.width == w && _rt.height == h) return;
            if (_rt) { _rt.Release(); Destroy(_rt); }
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "PlanarReflection", useMipMap = false };
            _rt.Create();
            _refl.targetTexture = _rt;
            Shader.SetGlobalTexture(TexId, _rt);
        }

        void LateUpdate()
        {
            if (!_main || !_refl) return;
            EnsureTarget();
            _refl.CopyFrom(_main);
            _refl.depth = _main.depth - 10;
            _refl.targetTexture = _rt;
            _refl.cullingMask = reflectLayers;
            _refl.farClipPlane = farClip;
            // mirror about the street when driving it, about the bay when over or near water
            _groundProbe -= Time.unscaledDeltaTime;
            if (_groundProbe <= 0)
            {
                _groundProbe = 0.3f;
                Vector3 cp = _main.transform.position;
                float g = CityLayout.GroundY(cp.x, cp.z);
                planeHeight = g < CityLayout.WaterLevel + 0.5f || cp.y - g > 80f ? CityLayout.WaterLevel : Mathf.Round(g * 2f) * 0.5f;
            }
            _refl.useOcclusionCulling = false;

            // Mirror position and basis about the plane. A pure mirror flips handedness,
            // so keep forward mirrored but negate up: same image, vertically flipped.
            Vector3 p = _main.transform.position;
            p.y = 2f * planeHeight - p.y;
            Vector3 f = _main.transform.forward; f.y = -f.y;
            Vector3 u = _main.transform.up; u.y = -u.y;
            _refl.transform.SetPositionAndRotation(p, Quaternion.LookRotation(f, -u));

            // Oblique near plane so nothing below the ground leaks into the reflection.
            Matrix4x4 m = _refl.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(new Vector3(0, planeHeight + 0.05f, 0));
            Vector3 cnormal = m.MultiplyVector(Vector3.up).normalized;
            Vector4 planeCS = new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
            _refl.projectionMatrix = _main.projectionMatrix;
            try { _refl.projectionMatrix = _refl.CalculateObliqueMatrix(planeCS); }
            catch { /* fall back to plain projection */ }
        }
    }
}
