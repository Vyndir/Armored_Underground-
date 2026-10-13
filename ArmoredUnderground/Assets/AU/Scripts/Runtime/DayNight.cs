using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AU
{
    /// <summary>
    /// Time of day for the bay city. Moves the sun (the same light becomes the
    /// moon after dark), blends sky, ambient, fog and post-processing between the
    /// day and night looks, and switches the city's lights on at dusk: window and
    /// sign emission, street lamps (nearest ones as real lights) and wet roads.
    /// </summary>
    public class DayNight : MonoBehaviour
    {
        public static DayNight I;
        public float hour = 16.2f;
        public float minutesPerDay = 30f;   // real minutes for a full 24 hours
        public bool frozen;
        public float Night { get; private set; }
        public bool Raining => Night > 0.6f && rainAtNight;
        public bool rainAtNight = true;
        public event Action<bool> NightChanged;
        public static bool IsNight => I && I.Night > 0.5f;

        Light _sun;
        Volume _dayVol, _nightVol;
        Material _asphalt;
        readonly List<(Material m, int id, Color c, float day)> _emissive = new();
        readonly List<Light> _lights = new();
        float _lightTimer, _envTimer, _warp;
        bool _wasNight;

        static readonly int SunDirId = Shader.PropertyToID("_AU_SunDir");
        static readonly int MoonDirId = Shader.PropertyToID("_AU_MoonDir");
        static readonly int NightId = Shader.PropertyToID("_AU_Night");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake() { I = this; }

        void Start()
        {
            var sunGo = GameObject.Find("Sun");
            _sun = sunGo ? sunGo.GetComponent<Light>() : RenderSettings.sun;
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v.name == "DayVolume") _dayVol = v;
                else if (v.name == "NightVolume") _nightVol = v;
            }
            var world = GameObject.Find("World");
            var seen = new HashSet<Material>();
            if (world)
            {
                foreach (var r in world.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (!m || !seen.Add(m)) continue;
                        string n = m.name;
                        if (n.StartsWith("Asphalt")) _asphalt = m;
                        if (m.HasProperty(ColorId) && (m.shader.name == "AU/Neon" || m.shader.name == "AU/Glow"))
                            _emissive.Add((m, ColorId, m.GetColor(ColorId), DayLevel(n)));
                        else if (m.IsKeywordEnabled("_EMISSION") && m.HasProperty(EmissionId))
                            _emissive.Add((m, EmissionId, m.GetColor(EmissionId), DayLevel(n)));
                    }
                foreach (var l in world.GetComponentsInChildren<Light>(true))
                    if (l.type != LightType.Directional) { _lights.Add(l); l.enabled = false; }
            }
            Apply(true);
        }

        /// <summary>How much of a material's glow survives in daylight.</summary>
        static float DayLevel(string n)
        {
            if (n.StartsWith("Lamp") || n.StartsWith("Facade") || n.StartsWith("Window") || n.StartsWith("Head") || n.StartsWith("Tail")) return 0f;
            if (n.StartsWith("Storefront")) return 0.15f;
            if (n.StartsWith("Screen") || n.StartsWith("Billboard")) return 0.7f;
            if (n.StartsWith("Runway") || n.StartsWith("Beacon")) return 0.5f;
            return 0.3f;
        }

        /// <summary>Fast-forward to the next dusk or dawn.</summary>
        public void SkipToNext()
        {
            float target = Night > 0.5f ? 9.5f : 21.5f;
            float delta = (target - hour + 24f) % 24f;
            _warp = delta;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame) SkipToNext();
            float dt = Time.unscaledDeltaTime;
            if (_warp > 0)
            {
                float step = Mathf.Min(_warp, dt * 6f);
                hour += step; _warp -= step;
            }
            else if (!frozen && Time.timeScale > 0) hour += dt * 24f / (minutesPerDay * 60f);
            hour = (hour + 24f) % 24f;
            Apply(false);
        }

        public void SetHour(float h) { hour = h; _warp = 0; Apply(true); }

        /// <summary>Hand lighting to the garage studio (dim moonlight, night ambient) or take it back.</summary>
        public void Suspend(bool on)
        {
            enabled = !on;
            if (!on) { Apply(true); return; }
            if (_sun) { _sun.intensity = 0.22f; _sun.color = new Color(0.5f, 0.58f, 0.9f); _sun.transform.rotation = Quaternion.Euler(42, -35, 0); }
            RenderSettings.ambientSkyColor = new Color(0.08f, 0.09f, 0.15f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.05f, 0.09f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.03f);
            foreach (var l in _lights) l.enabled = false;
        }

        /// <summary>Unit vector toward a point in the sky: elevation and compass bearing (0 = north, 90 = east), degrees.</summary>
        static Vector3 Dir(float elev, float bearing)
        {
            float e = elev * Mathf.Deg2Rad, b = bearing * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(e) * Mathf.Sin(b), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(b));
        }

        void Apply(bool force)
        {
            // sun path: rises in the east (+x), sets in the west, leaning south
            float t = (hour - 6f) / 12f;                  // 0 at sunrise, 1 at sunset
            float elev = Mathf.Sin(t * Mathf.PI) * 58f;    // degrees; negative at night
            float azim = 90f + Mathf.Clamp01(t) * 180f;    // compass bearing: east, south at noon, west
            Vector3 sunDir = Dir(elev, azim);
            Vector3 moonDir = Dir(40f, 150f + hour * 2f);

            float night = 1f - Mathf.Clamp01((elev + 4f) / 12f);
            Night = night;
            float twilight = Mathf.Clamp01(1f - Mathf.Abs(elev - 2f) / 14f);
            Shader.SetGlobalVector(SunDirId, sunDir);
            Shader.SetGlobalVector(MoonDirId, moonDir.normalized);
            Shader.SetGlobalFloat(NightId, night);

            if (_sun)
            {
                bool moon = night > 0.5f;
                Vector3 dir = moon ? moonDir : sunDir;
                if (!moon && dir.y < 0.06f) dir.y = 0.06f; // keep long shadows sane at the horizon
                _sun.transform.rotation = Quaternion.LookRotation(-dir.normalized);
                float sunI = Mathf.Lerp(0.15f, 1.9f, Mathf.Clamp01(elev / 25f));
                Color sunC = Color.Lerp(new Color(1f, 0.55f, 0.32f), new Color(1f, 0.95f, 0.88f), Mathf.Clamp01((elev - 3f) / 30f));
                float fade = Mathf.Clamp01(Mathf.Abs(night - 0.5f) * 4f); // dim through the swap
                _sun.intensity = (moon ? 0.28f : sunI) * fade;
                _sun.color = moon ? new Color(0.55f, 0.62f, 0.95f) : sunC;
                _sun.shadowStrength = moon ? 0.7f : 0.9f;
            }

            // ambient + fog
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Color skyD = new(0.5f, 0.6f, 0.78f), eqD = new(0.46f, 0.47f, 0.47f), grD = new(0.24f, 0.22f, 0.2f);
            Color skyN = new(0.1f, 0.11f, 0.19f), eqN = new(0.085f, 0.07f, 0.11f), grN = new(0.02f, 0.02f, 0.03f);
            Color skyT = new(0.55f, 0.42f, 0.45f), eqT = new(0.6f, 0.4f, 0.3f);
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(skyD, skyT, twilight * 0.6f), skyN, night);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(eqD, eqT, twilight * 0.6f), eqN, night);
            RenderSettings.ambientGroundColor = Color.Lerp(grD, grN, night);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            Color fogD = new(0.64f, 0.73f, 0.84f), fogT = new(0.85f, 0.6f, 0.45f), fogN = new(0.04f, 0.045f, 0.08f);
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(fogD, fogT, twilight * 0.7f), fogN, night);
            RenderSettings.fogDensity = Mathf.Lerp(0.0002f, 0.0009f, night) + (Raining ? 0.0009f : 0f);

            if (_dayVol) _dayVol.weight = 1f - night;
            if (_nightVol) _nightVol.weight = night;

            // city glow
            foreach (var (m, id, c, day) in _emissive)
                if (m) m.SetColor(id, c * Mathf.Lerp(day, 1f, night));
            if (_asphalt)
            {
                _asphalt.SetFloat("_Wetness", Raining ? 0.75f : Mathf.Lerp(0.06f, 0.3f, night));
                _asphalt.SetFloat("_RainAmount", Raining ? 1f : 0f);
                _asphalt.SetFloat("_ReflectionStrength", Mathf.Lerp(0.4f, 1f, night));
            }

            bool isNight = night > 0.5f;
            if (isNight != _wasNight || force)
            {
                _wasNight = isNight;
                if (!force) NightChanged?.Invoke(isNight);
                _lightTimer = 0;
            }

            // only the closest street lamps get real lights
            _lightTimer -= Time.unscaledDeltaTime;
            if (_lightTimer <= 0)
            {
                _lightTimer = 0.4f;
                var cam = Camera.main;
                if (cam && _lights.Count > 0)
                {
                    Vector3 p = cam.transform.position;
                    _lights.Sort((a, b) => (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude));
                    for (int i = 0; i < _lights.Count; i++)
                        _lights[i].enabled = night > 0.35f && i < 96 && (_lights[i].transform.position - p).sqrMagnitude < 320f * 320f;
                }
            }
            _envTimer -= Time.unscaledDeltaTime;
            if (_envTimer <= 0 || force) { _envTimer = 3f; DynamicGI.UpdateEnvironment(); }
        }
    }
}
