using UnityEngine;

namespace AU
{
    /// <summary>Rain that follows the camera, splashes on the street, and steam vents.</summary>
    public class Atmosphere : MonoBehaviour
    {
        public Transform follow;
        ParticleSystem _rain, _splash;
        ReflectionProbe _probe;
        float _probeTimer;

        void Start()
        {
            var a = GameAssets.I;
            _rain = MakeRain(a.rain);
            _splash = MakeSplash(a.splash);
            foreach (var v in FindObjectsByType<SteamVentMarker>(FindObjectsSortMode.None)) MakeSteam(v.transform, a.steam);

            // a realtime probe riding with the camera gives metal and glass neon reflections
            var pgo = new GameObject("CameraReflectionProbe");
            _probe = pgo.AddComponent<ReflectionProbe>();
            _probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            _probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            _probe.resolution = 128;
            _probe.size = new Vector3(400, 200, 400);
            _probe.farClipPlane = 300;
            _probe.hdr = true;
            _probe.importance = 10;
        }

        ParticleSystem MakeRain(Material m)
        {
            var go = new GameObject("Rain");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0;
            main.startSize3D = true;
            main.startSizeX = 0.035f; main.startSizeY = 1.4f; main.startSizeZ = 1;
            main.startColor = new Color(0.75f, 0.8f, 1f, 0.35f);
            main.maxParticles = 12000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 9000;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(90, 1, 90);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-2f); vel.y = new ParticleSystem.MinMaxCurve(-34f, -40f); vel.z = new ParticleSystem.MinMaxCurve(1.5f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f; r.lengthScale = 1f;
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeSplash(Material m)
        {
            var go = new GameObject("RainSplash");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 0.25f; main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = new Color(0.8f, 0.85f, 1f, 0.25f); main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 2500;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(50, 0, 50);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.3f, 1, 1.4f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = m;
            ps.Play();
            return ps;
        }

        void MakeSteam(Transform at, Material m)
        {
            var go = new GameObject("Steam");
            go.transform.SetParent(at, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(2f, 4f); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(0.6f, 0.62f, 0.7f, 0.12f); main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 10;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12; sh.radius = 0.4f;
            sh.rotation = new Vector3(-90, 0, 0);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 2.5f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.8f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0.4f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            ps.Play();
        }

        void LateUpdate()
        {
            var cam = follow ? follow : (Camera.main ? Camera.main.transform : null);
            if (!cam) return;
            Vector3 p = cam.position;
            Vector3 f = cam.forward; f.y = 0;
            _rain.transform.position = new Vector3(p.x, p.y + 25f, p.z) + f.normalized * 25f;
            _splash.transform.position = new Vector3(p.x, 0.05f, p.z) + f.normalized * 18f;
            if (_probe)
            {
                _probe.transform.position = p;
                _probeTimer -= Time.unscaledDeltaTime;
                if (_probeTimer <= 0) { _probe.RenderProbe(); _probeTimer = 0.25f; }
            }
        }
    }
}
