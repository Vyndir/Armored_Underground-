using System.Collections.Generic;
using UnityEngine;

namespace AU
{
    /// <summary>
    /// Synthesized sound: every clip is generated at startup (no audio files).
    /// Engine/booster/rain loops plus pooled 3D one-shots.
    /// </summary>
    public class AudioFX : MonoBehaviour
    {
        public static AudioFX I;
        const int SR = 44100;
        readonly Dictionary<string, AudioClip> _clips = new();
        readonly List<AudioSource> _pool = new();
        int _next;
        AudioSource _engine, _boost, _rain, _wind;
        System.Random _r = new(5);

        float N() => (float)(_r.NextDouble() * 2 - 1);

        void Awake()
        {
            I = this;
            Build();
            for (int i = 0; i < 24; i++)
            {
                var s = new GameObject("SFX").AddComponent<AudioSource>();
                s.transform.SetParent(transform);
                s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 12f; s.maxDistance = 600f;
                s.dopplerLevel = 0.3f;
                _pool.Add(s);
            }
            _engine = Loop("engine", 0.0f, false);
            _boost = Loop("boost", 0.0f, false);
            _rain = Loop("rain", 0.35f, false);
            _wind = Loop("wind", 0f, false);
        }

        AudioSource Loop(string clip, float vol, bool spatial)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = _clips[clip]; s.loop = true; s.volume = vol; s.spatialBlend = spatial ? 1 : 0;
            s.Play();
            return s;
        }

        AudioClip Make(string name, float seconds, System.Func<int, float, float> f)
        {
            int n = (int)(SR * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i, i / (float)SR), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(data, 0);
            _clips[name] = c;
            return c;
        }

        void Build()
        {
            // --- engine: geared reactor hum + turbine whine, seamless 1s loop (integer Hz)
            Make("engine", 1f, (i, t) =>
            {
                float w = 0;
                w += Saw(t * 55f) * 0.32f + Saw(t * 110f) * 0.18f + Mathf.Sin(t * 2 * Mathf.PI * 220f) * 0.1f;
                w += Mathf.Sin(t * 2 * Mathf.PI * 880f) * 0.04f + Mathf.Sin(t * 2 * Mathf.PI * 1320f) * 0.025f;
                w *= 0.8f + 0.2f * Mathf.Sin(t * 2 * Mathf.PI * 8f);
                return w * 0.6f;
            });
            float lp = 0, lp2 = 0;
            Make("boost", 2f, (i, t) =>
            {
                float x = N();
                lp += (x - lp) * 0.18f; lp2 += (lp - lp2) * 0.3f;
                float roar = lp2 * 2.2f + (x - lp) * 0.25f;
                return roar * 0.8f * Edge(t, 2f);
            });
            lp = 0;
            Make("rain", 4f, (i, t) =>
            {
                float x = N();
                lp += (x - lp) * 0.5f;
                float hiss = (x - lp) * 0.35f + lp * 0.15f;
                float drop = _r.NextDouble() < 0.0015 ? N() * 0.6f : 0f;
                return (hiss + drop) * Edge(t, 4f);
            });
            lp = 0;
            Make("wind", 3f, (i, t) =>
            {
                float x = N();
                lp += (x - lp) * 0.04f;
                return lp * 3f * Edge(t, 3f);
            });
            Make("rifle", 0.28f, (i, t) => (N() * Mathf.Exp(-t * 28f) * 0.8f + Mathf.Sin(t * 2 * Mathf.PI * (120f - t * 200f)) * Mathf.Exp(-t * 18f)) * 0.9f);
            Make("gatling", 0.12f, (i, t) => (N() * Mathf.Exp(-t * 45f) * 0.7f + Mathf.Sin(t * 2 * Mathf.PI * 160f) * Mathf.Exp(-t * 30f) * 0.5f));
            Make("scatter", 0.55f, (i, t) => (N() * Mathf.Exp(-t * 9f) + Mathf.Sin(t * 2 * Mathf.PI * (80f - t * 60f)) * Mathf.Exp(-t * 8f) * 0.9f) * 0.9f);
            Make("rail", 0.9f, (i, t) => (Mathf.Sin(t * 2 * Mathf.PI * (2400f * Mathf.Exp(-t * 4f) + 60f)) * Mathf.Exp(-t * 3f) * 0.6f + N() * Mathf.Exp(-t * 14f) * 0.6f));
            lp = 0;
            Make("missile", 0.9f, (i, t) => { float x = N(); lp += (x - lp) * (0.05f + t * 0.3f); return lp * 2.5f * Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI); });
            Make("mine", 0.25f, (i, t) => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 660f)) * 0.25f * Mathf.Exp(-t * 10f));
            lp = 0;
            Make("boom", 2.4f, (i, t) =>
            {
                float x = N(); lp += (x - lp) * 0.06f;
                float crackle = _r.NextDouble() < 0.02 * Mathf.Exp(-t * 2f) ? N() : 0;
                return (lp * 3.2f * Mathf.Exp(-t * 1.8f) + Mathf.Sin(t * 2 * Mathf.PI * (50f - t * 15f)) * Mathf.Exp(-t * 3f) * 0.8f + crackle * 0.4f);
            });
            lp = 0;
            Make("qb", 0.45f, (i, t) => { float x = N(); lp += (x - lp) * 0.25f; return lp * 2.2f * Mathf.Exp(-t * 7f) + Mathf.Sin(t * 2 * Mathf.PI * 70f) * Mathf.Exp(-t * 14f) * 0.6f; });
            Make("blade", 0.5f, (i, t) => Mathf.Sin(t * 2 * Mathf.PI * (300f + t * 2400f)) * Mathf.Exp(-t * 5f) * 0.5f + N() * Mathf.Exp(-t * 20f) * 0.3f);
            Make("stagger", 1.2f, (i, t) => (Mathf.Sin(t * 2 * Mathf.PI * 523f) + Mathf.Sin(t * 2 * Mathf.PI * 741f) * 0.7f + Mathf.Sin(t * 2 * Mathf.PI * 1187f) * 0.4f) * Mathf.Exp(-t * 4f) * 0.35f);
            lp = 0;
            Make("crash", 0.8f, (i, t) => { float x = N(); lp += (x - lp) * 0.3f; return (lp * 2f + Mathf.Sin(t * 2 * Mathf.PI * 95f) * 0.6f) * Mathf.Exp(-t * 6f); });
            Make("hit", 0.06f, (i, t) => Mathf.Sin(t * 2 * Mathf.PI * 1900f) * Mathf.Exp(-t * 60f) * 0.5f);
            Make("beep", 0.3f, (i, t) => Mathf.Sin(t * 2 * Mathf.PI * 880f) * Mathf.Exp(-t * 6f) * 0.4f);
            Make("beephi", 0.6f, (i, t) => Mathf.Sin(t * 2 * Mathf.PI * 1760f) * Mathf.Exp(-t * 4f) * 0.4f);
            Make("gate", 0.5f, (i, t) => (Mathf.Sin(t * 2 * Mathf.PI * 1320f) + Mathf.Sin(t * 2 * Mathf.PI * 1980f) * 0.5f) * Mathf.Exp(-t * 7f) * 0.3f);
            lp = 0;
            Make("overdrive", 1.6f, (i, t) => { float x = N(); lp += (x - lp) * (0.02f + t * 0.2f); return lp * 2.5f * Mathf.Exp(-t * 1.2f) + Mathf.Sin(t * 2 * Mathf.PI * (200f + t * 600f)) * 0.2f * Mathf.Exp(-t * 2f); });
            Make("ui", 0.05f, (i, t) => Mathf.Sin(t * 2 * Mathf.PI * 1500f) * Mathf.Exp(-t * 80f) * 0.3f);
            Make("buy", 0.4f, (i, t) => (Mathf.Sin(t * 2 * Mathf.PI * (t < 0.12f ? 880f : 1320f))) * Mathf.Exp(-t * 6f) * 0.35f);
        }

        static float Saw(float x) => 2f * (x - Mathf.Floor(x + 0.5f));
        static float Edge(float t, float len) => Mathf.Clamp01(Mathf.Min(t, len - t) / 0.01f);

        public void Play(string clip, Vector3 pos, float vol = 1f, float pitch = 1f, bool ui = false)
        {
            if (!_clips.TryGetValue(clip, out var c)) return;
            var s = _pool[_next++ % _pool.Count];
            s.transform.position = pos;
            s.spatialBlend = ui ? 0f : 1f;
            s.pitch = pitch * Random.Range(0.94f, 1.06f);
            s.volume = vol;
            s.PlayOneShot(c);
        }

        public void UI(string clip, float vol = 0.6f) => Play(clip, Vector3.zero, vol, 1f, true);

        public void Shot(WeaponKind k, Vector3 pos, bool player)
        {
            string c = k switch { WeaponKind.Rail => "rail", WeaponKind.Scatter => "scatter", WeaponKind.Gatling => "gatling", _ => "rifle" };
            Play(c, pos, player ? 0.55f : 0.45f);
        }

        /// <summary>Drive the player's engine/booster loops.</summary>
        public void Engine(float speed, float boost, bool overdrive, bool active)
        {
            if (!_engine) return;
            float targetVol = active ? 0.18f + Mathf.Min(speed, 90f) * 0.002f : 0f;
            _engine.volume = Mathf.Lerp(_engine.volume, targetVol, 0.1f);
            _engine.pitch = Mathf.Lerp(_engine.pitch, 0.6f + speed * 0.012f + boost * 0.25f, 0.1f);
            _boost.volume = Mathf.Lerp(_boost.volume, active ? boost * (overdrive ? 0.65f : 0.4f) : 0f, 0.15f);
            _boost.pitch = 0.8f + speed * 0.006f + (overdrive ? 0.3f : 0f);
            _wind.volume = Mathf.Lerp(_wind.volume, active ? Mathf.Clamp01((speed - 25f) / 60f) * 0.35f : 0f, 0.1f);
        }
    }
}
