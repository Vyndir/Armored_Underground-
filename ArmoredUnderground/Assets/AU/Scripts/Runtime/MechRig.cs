using System.Collections.Generic;
using UnityEngine;

namespace AU
{
    /// <summary>
    /// Procedural animation on the mech skeleton (walk, boost-skate crouch, air tuck,
    /// lean into turns, aim the rifle arm), plus thruster flames, booster light,
    /// paint colors and the muzzle point. Rotations are authored in mech space
    /// (x right, y up, z forward) so they don't depend on imported bone axes.
    /// </summary>
    public class MechRig : MonoBehaviour
    {
        public Transform muzzle, podMount, boosterMount;
        MechController _mech;
        readonly Dictionary<string, Transform> _b = new();
        readonly Dictionary<Transform, Quaternion> _rest = new();
        float _phase;
        Vector2 _lean;
        float _crouch;
        readonly List<Transform> _flames = new();
        readonly List<Vector3> _flameBase = new();
        readonly List<Renderer> _flameRenderers = new();
        Light _boostLight;
        MaterialPropertyBlock _mpb;
        Renderer[] _renderers;
        float _hitFlash, _flashShown;
        readonly List<(Renderer r, int i)> _surfaceSlots = new();

        static readonly string[] BoneNames = { "pelvis", "spine", "chest", "head", "booster", "pod.L",
            "shoulder.L", "upperarm.L", "forearm.L", "hand.L", "shoulder.R", "upperarm.R", "forearm.R", "hand.R",
            "thigh.L", "shin.L", "foot.L", "thigh.R", "shin.R", "foot.R" };

        public void Init(MechController mech)
        {
            _mech = mech;
            foreach (var n in BoneNames)
            {
                var t = Find(transform, n);
                if (t) { _b[n] = t; _rest[t] = t.localRotation; }
            }
            _mpb = new MaterialPropertyBlock();

            boosterMount = Bone("booster") ? Bone("booster") : transform;
            var lgo = new GameObject("BoostLight");
            lgo.transform.SetParent(boosterMount, false);
            lgo.transform.position = transform.TransformPoint(new Vector3(0, 4.5f, -2.5f));
            _boostLight = lgo.AddComponent<Light>();
            _boostLight.type = LightType.Point;
            _boostLight.range = 14f;
            _boostLight.intensity = 0;
            _boostLight.shadows = LightShadows.None;
        }

        /// <summary>Show the equipped parts, then place the muzzle, missile pod mount and booster flames on them.</summary>
        public void Configure(Loadout l)
        {
            MechVariants.Apply(transform, l);
            _renderers = GetComponentsInChildren<Renderer>();

            // muzzle: the barrel end farthest from the hand
            if (muzzle) Destroy(muzzle.gameObject);
            var hand = Bone("hand.R");
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(hand ? hand : transform, false);
            var barrel = MechVariants.Marker(transform, l.weapon, "Barrel");
            if (barrel)
            {
                var (c, a, b) = MechVariants.PoseBounds(barrel);
                Vector3 hp = hand ? hand.position : transform.position;
                muzzle.position = (a - hp).sqrMagnitude > (b - hp).sqrMagnitude ? a : b;
            }
            else muzzle.position = transform.TransformPoint(new Vector3(-1.95f, 3.4f, 4f));
            muzzle.rotation = transform.rotation;

            if (!podMount)
            {
                podMount = new GameObject("PodMount").transform;
                podMount.SetParent(Bone("pod.L") ? Bone("pod.L") : transform, false);
                podMount.position = transform.TransformPoint(new Vector3(-1.15f, 7.4f, 0.4f));
            }

            BuildThrusters(l);
            ApplyPaint(l);
        }

        void BuildThrusters(Loadout l)
        {
            foreach (var f in _flames) if (f) Destroy(f.gameObject);
            _flames.Clear(); _flameBase.Clear(); _flameRenderers.Clear();
            var a = GameAssets.I;
            var nozzles = new List<Vector3>();
            foreach (var r in MechVariants.Markers(transform, l.booster, "Nozzle"))
            {
                var (c, lo, hi) = MechVariants.PoseBounds(r);
                nozzles.Add(c - Vector3.up * 0.3f);
            }
            if (nozzles.Count == 0)
            {
                nozzles.Add(transform.TransformPoint(new Vector3(0.55f, 5.0f, -1.4f)));
                nozzles.Add(transform.TransformPoint(new Vector3(-0.55f, 5.0f, -1.4f)));
            }
            foreach (var p in nozzles)
            {
                var f = new GameObject("Flame").transform;
                f.SetParent(boosterMount, true);
                f.position = p;
                f.rotation = Quaternion.LookRotation((Vector3.down - transform.forward * 0.9f).normalized, -transform.forward);
                var cone = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
                cone.transform.SetParent(f, false);
                cone.GetComponent<MeshFilter>().sharedMesh = ConeMesh;
                var r = cone.GetComponent<Renderer>();
                r.sharedMaterial = a.thruster;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(core.GetComponent<Collider>());
                core.transform.SetParent(f, false);
                core.transform.localScale = new Vector3(0.3f, 0.3f, 0.45f);
                core.transform.localPosition = new Vector3(0, 0, 0.15f);
                var cr = core.GetComponent<Renderer>();
                cr.sharedMaterial = a.thrusterCore;
                cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _flames.Add(f);
                _flameBase.Add(f.localScale);
                _flameRenderers.Add(r);
            }
        }

        static Mesh _cone;
        /// <summary>Flame cone along +Z: wide at the nozzle, tapering to a point. uv.y = 1 at nozzle.</summary>
        static Mesh ConeMesh
        {
            get
            {
                if (_cone) return _cone;
                var mb = new MeshBuilder();
                int sides = 14, rings = 6;
                for (int i = 0; i <= rings; i++)
                {
                    float t = i / (float)rings;
                    float r = 0.42f * (1 - t) * (1 - t * 0.35f) + 0.02f;
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = k / (float)sides * Mathf.PI * 2;
                        var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                        mb.v.Add(o * r + Vector3.forward * t * 2.6f); mb.n.Add(o); mb.uv.Add(new Vector2(k / (float)sides, 1 - t)); mb.col.Add(Color.white);
                    }
                }
                for (int i = 0; i < rings; i++)
                    for (int k = 0; k < sides; k++)
                    {
                        int a = i * (sides + 1) + k, b = a + sides + 1;
                        mb.t.Add(a); mb.t.Add(b); mb.t.Add(a + 1);
                        mb.t.Add(a + 1); mb.t.Add(b); mb.t.Add(b + 1);
                    }
                _cone = mb.ToMesh("FlameCone");
                return _cone;
            }
        }

        /// <summary>Character lighting rig (warm key + cool rim) that travels with the player's mech,
        /// so armour detail reads on dark streets the way a lit hero character does.</summary>
        public void AddCharacterLights()
        {
            void L(string n, Vector3 local, Color c, float intensity, float range)
            {
                var l = new GameObject(n).AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.transform.localPosition = local;
                l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            }
            L("CharKey", new Vector3(2.5f, 9.5f, 6f), new Color(1f, 0.9f, 0.8f), 3.2f, 16f);
            L("CharRim", new Vector3(-3.5f, 7.5f, -6f), new Color(0.45f, 0.75f, 1f), 3.6f, 15f);
            L("CharFill", new Vector3(-4f, 3f, 4f), new Color(0.75f, 0.6f, 1f), 1.2f, 12f);
        }

        public Transform Bone(string n) => _b.TryGetValue(n, out var t) ? t : null;

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f) return f; }
            return null;
        }

        public void ApplyPaint(Loadout l)
        {
            if (_renderers == null) return;
            var a = GameAssets.I;
            _surfaceSlots.Clear();
            foreach (var r in _renderers)
            {
                if (_flameRenderers.Contains(r)) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == a.mechPaint) { r.GetPropertyBlock(_mpb, i); _mpb.SetColor("_BaseColor", l.paint); r.SetPropertyBlock(_mpb, i); }
                    else if (mats[i] == a.mechAccent) { r.GetPropertyBlock(_mpb, i); _mpb.SetColor("_BaseColor", l.accent); r.SetPropertyBlock(_mpb, i); }
                    else if (mats[i] == a.mechGlow) { r.GetPropertyBlock(_mpb, i); _mpb.SetColor("_EmissionColor", l.neon * 3.5f); r.SetPropertyBlock(_mpb, i); }
                    else if (mats[i] && mats[i].shader && mats[i].shader.name == "AU/MechSurface")
                    {
                        r.GetPropertyBlock(_mpb, i);
                        _mpb.SetColor("_PaintColor", l.paint);
                        _mpb.SetColor("_AccentColor", l.accent);
                        r.SetPropertyBlock(_mpb, i);
                        _surfaceSlots.Add((r, i));
                    }
                }
            }
            if (_boostLight) _boostLight.color = Color.Lerp(l.neon, Color.white, 0.3f);
            foreach (var fr in _flameRenderers)
            {
                fr.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", Color.Lerp(l.neon, Color.white, 0.25f) * 2.2f);
                fr.SetPropertyBlock(_mpb);
            }
        }

        public void HitFlash() => _hitFlash = 0.12f;

        // Rotate a bone by a mech-space euler (degrees) relative to its rest pose.
        void Pose(string bone, float pitch, float roll = 0, float twist = 0)
        {
            var t = Bone(bone);
            if (!t) return;
            t.localRotation = _rest[t];
            Quaternion mechRot = transform.rotation;
            Quaternion delta = mechRot * Quaternion.Euler(pitch, twist, roll) * Quaternion.Inverse(mechRot);
            t.rotation = delta * t.rotation;
        }

        void LateUpdate()
        {
            if (!_mech)
            {
                // showroom preview: idle flames
                for (int i = 0; i < _flames.Count; i++) _flames[i].localScale = Vector3.Scale(_flameBase[i], new Vector3(0.65f, 0.65f, 0.35f + Random.value * 0.05f));
                return;
            }
            float dt = Time.deltaTime;
            var m = _mech;
            Vector3 local = Quaternion.Inverse(transform.rotation) * m.velocity;
            float speed = m.Speed;
            bool walking = m.grounded && !m.boosting && speed > 1.5f && m.stats.legType != LegType.Hover;
            _phase += dt * (walking ? Mathf.Min(speed, 30f) * 0.33f : 0f);

            Vector2 leanTarget = new Vector2(Mathf.Clamp(local.z / 70f, -0.5f, 0.6f), Mathf.Clamp(-local.x / 60f, -0.45f, 0.45f));
            if (!m.grounded) leanTarget *= 0.5f;
            _lean = Vector2.Lerp(_lean, leanTarget, 1 - Mathf.Exp(-dt * 6f));
            float crouchTarget = m.IsDown ? 1f : m.IsStaggered ? 0.7f : m.boosting && m.grounded ? 0.55f : m.grounded ? 0.1f : 0.3f;
            _crouch = Mathf.Lerp(_crouch, crouchTarget, 1 - Mathf.Exp(-dt * 8f));

            float lean = _lean.x * (m.boosting ? 32f : 14f);
            float bank = _lean.y * 28f;
            Pose("pelvis", 0, bank * 0.4f);
            Pose("spine", lean * 0.5f + _crouch * 6f, bank * 0.3f);
            Pose("chest", lean * 0.5f + (m.IsStaggered ? -18f : 0f), bank * 0.3f);
            Pose("head", -lean * 0.6f - m.cmd.aimPitch * 0.3f);

            // legs
            for (int s = 0; s < 2; s++)
            {
                string side = s == 0 ? ".L" : ".R";
                float hip = 0, knee = 0, foot = 0;
                if (!m.grounded && !m.IsDown) { hip = -38f; knee = 62f; foot = -20f; }
                else if (walking)
                {
                    float ph = _phase + (s == 0 ? 0 : Mathf.PI);
                    hip = -Mathf.Sin(ph) * 26f;
                    knee = Mathf.Max(0, Mathf.Cos(ph)) * 38f;
                    foot = Mathf.Sin(ph) * 10f;
                }
                // crouch: hips forward, knees back
                hip -= _crouch * 34f;
                knee += _crouch * 60f;
                foot -= _crouch * 24f;
                // skate stance: lead leg forward, trail leg back, both splayed with the bank
                if (m.boosting && m.grounded)
                {
                    float stance = s == 0 ? -10f : 12f;
                    hip += stance;
                }
                Pose("thigh" + side, hip, (s == 0 ? -1 : 1) * 3f + bank * 0.35f);
                Pose("shin" + side, knee);
                Pose("foot" + side, foot);
            }

            // arms: right arm follows aim; left arm braces / swings
            float aim = Mathf.Clamp(m.cmd.aimPitch, -40f, 50f);
            Pose("upperarm.R", -aim * 0.85f - 6f);
            Pose("forearm.R", -aim * 0.15f);
            Pose("upperarm.L", m.boosting ? 22f : walking ? Mathf.Sin(_phase) * 12f : 4f, -6f);
            Pose("forearm.L", -30f);

            // thrusters
            float lvl = m.BoostLevel;
            for (int i = 0; i < _flames.Count; i++)
            {
                float flick = 0.85f + Random.value * 0.3f;
                _flames[i].localScale = Vector3.Scale(_flameBase[i], new Vector3(0.6f + lvl * 0.5f, 0.6f + lvl * 0.5f, (0.25f + lvl * 1.6f) * flick));
            }
            if (_boostLight)
            {
                _boostLight.intensity = Mathf.Lerp(_boostLight.intensity, lvl * (m.overdriveActive ? 14f : 7f), 1 - Mathf.Exp(-dt * 12f));
            }

            float flash = Mathf.Clamp01(_hitFlash / 0.12f);
            if (flash > 0 || _flashShown > 0)
            {
                foreach (var (r, i) in _surfaceSlots)
                {
                    if (!r) continue;
                    r.GetPropertyBlock(_mpb, i);
                    _mpb.SetFloat("_HitFlash", flash);
                    r.SetPropertyBlock(_mpb, i);
                }
                _flashShown = flash;
            }
            if (_hitFlash > 0)
            {
                _hitFlash -= dt;
            }
        }
    }
}
