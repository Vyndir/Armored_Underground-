using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AU
{
    /// <summary>
    /// Over-the-shoulder chase camera with collision, speed-driven FOV and a
    /// speed post-process volume (chromatic aberration, lens warp, vignette, blur),
    /// plus trauma-based shake.
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        public MechController target;
        public float yaw, pitch = 8f;
        public float sensitivity = 0.11f, padSensitivity = 150f;
        public bool inputEnabled = true;
        public bool followMechYaw; // autopilot / replays: swing behind the mech automatically
        public Camera Cam { get; private set; }
        float _trauma, _fov = 62f, _dist = 15f;
        Vector3 _pivot;
        Volume _speedVol, _dmgVol;
        float _dmg;
        ChromaticAberration _ca; LensDistortion _ld; Vignette _vg; MotionBlur _mb;

        void Awake()
        {
            Cam = GetComponent<Camera>();
            var go = new GameObject("SpeedVolume");
            _speedVol = go.AddComponent<Volume>();
            _speedVol.isGlobal = true; _speedVol.priority = 10; _speedVol.weight = 0;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            _ca = p.Add<ChromaticAberration>(); _ca.intensity.Override(0.75f);
            _ld = p.Add<LensDistortion>(); _ld.intensity.Override(-0.28f); _ld.scale.Override(1.02f);
            _vg = p.Add<Vignette>(); _vg.intensity.Override(0.48f); _vg.smoothness.Override(0.5f);
            _mb = p.Add<MotionBlur>(); _mb.intensity.Override(0.25f); _mb.mode.Override(MotionBlurMode.CameraOnly);
            _speedVol.profile = p;

            var dgo = new GameObject("DamageVolume");
            _dmgVol = dgo.AddComponent<Volume>();
            _dmgVol.isGlobal = true; _dmgVol.priority = 11; _dmgVol.weight = 0;
            var dp = ScriptableObject.CreateInstance<VolumeProfile>();
            var dv = dp.Add<Vignette>(); dv.intensity.Override(0.55f); dv.color.Override(new Color(0.8f, 0.02f, 0.02f)); dv.smoothness.Override(0.6f);
            var dc = dp.Add<ColorAdjustments>(); dc.saturation.Override(-35f);
            var dca = dp.Add<ChromaticAberration>(); dca.intensity.Override(0.6f);
            _dmgVol.profile = dp;
        }

        /// <summary>Hit feedback (0..1) plus a constant floor when armor is critical.</summary>
        public void Damage(float k) => _dmg = Mathf.Clamp01(Mathf.Max(_dmg, k));

        public void AddTrauma(float t) => _trauma = Mathf.Clamp01(_trauma + t);

        public void SnapBehind()
        {
            if (!target) return;
            yaw = target.yaw; pitch = 8f;
            _pivot = PivotTarget();
        }

        Vector3 PivotTarget()
        {
            Vector3 right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            return target.transform.position + Vector3.up * 8.2f + right * 2.6f;
        }

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.deltaTime;
            if (inputEnabled && Cursor.lockState == CursorLockMode.Locked)
            {
                var m = Mouse.current;
                if (m != null)
                {
                    Vector2 d = m.delta.ReadValue();
                    yaw += d.x * sensitivity;
                    pitch -= d.y * sensitivity;
                }
            }
            var pad = Gamepad.current;
            if (inputEnabled && pad != null)
            {
                Vector2 rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > 0.02f) { yaw += rs.x * padSensitivity * dt; pitch -= rs.y * padSensitivity * 0.6f * dt; }
            }
            if (followMechYaw) { yaw = Mathf.LerpAngle(yaw, target.yaw, 1 - Mathf.Exp(-dt * 3f)); pitch = Mathf.Lerp(pitch, 6f, dt * 2f); }
            pitch = Mathf.Clamp(pitch, -35f, 60f);

            float speed = target.Speed;
            Vector3 want = PivotTarget();
            _pivot = Vector3.Lerp(_pivot, want, 1 - Mathf.Exp(-dt * 18f));
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
            float dist = 15f + Mathf.Min(speed, 100f) * 0.045f + (target.overdriveActive ? 2f : 0f);
            _dist = Mathf.Lerp(_dist, dist, 1 - Mathf.Exp(-dt * 4f));
            Vector3 back = rot * Vector3.back;
            float d2 = _dist;
            if (Physics.SphereCast(_pivot, 0.8f, back, out var hit, _dist, ~(1 << 6 | 1 << 7), QueryTriggerInteraction.Ignore))
                d2 = Mathf.Max(2.5f, hit.distance - 0.3f);
            Vector3 pos = _pivot + back * d2;
            float floor = Mathf.Max(CityLayout.WaterLevel, _pivot.y - 30f) + 0.8f;
            if (pos.y < floor) pos.y = floor;

            // shake
            _trauma = Mathf.Max(0, _trauma - dt * 1.6f);
            float sh = _trauma * _trauma;
            float tt = Time.time * 25f;
            Vector3 shakeOff = new Vector3(Mathf.PerlinNoise(tt, 0) - 0.5f, Mathf.PerlinNoise(0, tt) - 0.5f, 0) * sh * 1.6f;
            float roll = (Mathf.PerlinNoise(tt, tt) - 0.5f) * sh * 6f;
            // high-speed buffeting
            float buffet = Mathf.Clamp01((speed - 50f) / 50f) * 0.06f;
            shakeOff += new Vector3(Mathf.PerlinNoise(tt * 0.5f, 3) - 0.5f, Mathf.PerlinNoise(5, tt * 0.5f) - 0.5f, 0) * buffet;

            transform.SetPositionAndRotation(pos, rot * Quaternion.Euler(0, 0, roll));
            transform.position += transform.rotation * shakeOff;

            float fovT = 62f + Mathf.Clamp01(speed / 80f) * 16f + (target.overdriveActive ? 9f : 0f);
            _fov = Mathf.Lerp(_fov, fovT, 1 - Mathf.Exp(-dt * 3f));
            Cam.fieldOfView = _fov;

            float sv = Mathf.Clamp01((speed - 35f) / 55f) * 0.75f + (target.overdriveActive ? 0.35f : 0f);
            _speedVol.weight = Mathf.Lerp(_speedVol.weight, Mathf.Clamp01(sv), 1 - Mathf.Exp(-dt * 4f));
            _dmg = Mathf.Max(0, _dmg - dt * 1.8f);
            float critical = target.ap / target.stats.ap < 0.25f ? 0.35f + Mathf.PingPong(Time.time * 0.8f, 0.25f) : 0f;
            _dmgVol.weight = Mathf.Max(_dmg, critical, target.IsDown ? 0.9f : 0f);
        }
    }
}
