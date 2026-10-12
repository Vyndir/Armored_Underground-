using System;
using System.Collections.Generic;
using UnityEngine;

namespace AU
{
    /// <summary>
    /// Weapons and effects: tracers, rail beams, homing missiles, mines, the pulse
    /// blade, explosions, sparks, smoke, light flashes and screen-shake events.
    /// </summary>
    public class Combat : MonoBehaviour
    {
        public static Combat I;
        public event Action<MechController, float, MechController> Hit; // target, dmg, from
        public event Action<Vector3, float> Shake;                      // position, strength
        public Func<IEnumerable<MechController>> Mechs;

        class Bullet { public Transform t; public Vector3 vel; public MechController owner; public float dmg, impact, life; }
        class Missile { public Transform t; public TrailRenderer trail; public Vector3 vel; public MechController owner, target; public float dmg, impact, life, delay; public bool heavy; }
        class Mine { public Transform t; public MechController owner; public float dmg, impact, arm, life; }
        class Fade { public Transform t; public Renderer r; public LineRenderer lr; public float life, max, grow; }
        class Flash { public Light l; public float t, dur, max; }

        readonly List<Bullet> _bullets = new();
        readonly Stack<Transform> _tracerPool = new(), _enemyTracerPool = new();
        readonly List<Missile> _missiles = new();
        readonly List<Mine> _mines = new();
        readonly List<Fade> _fades = new();
        readonly List<Flash> _flashes = new();
        ParticleSystem _sparks, _fire, _smoke, _embers;
        readonly RaycastHit[] _hits = new RaycastHit[16];
        GameAssets A => GameAssets.I;
        MaterialPropertyBlock _mpb;

        void Awake()
        {
            I = this;
            _sparks = MakePS("Sparks", A.spark, true, 2000, ParticleSystemRenderMode.Stretch, 9f);
            _fire = MakePS("Fire", A.fire, true, 800, ParticleSystemRenderMode.Billboard, 0f);
            _smoke = MakePS("Smoke", A.smoke, false, 800, ParticleSystemRenderMode.Billboard, -1.5f);
            _embers = MakePS("Embers", A.spark, true, 600, ParticleSystemRenderMode.Billboard, 4f);
            for (int i = 0; i < 6; i++)
            {
                var l = new GameObject("FlashLight").AddComponent<Light>();
                l.transform.SetParent(transform);
                l.type = LightType.Point; l.range = 30; l.intensity = 0; l.shadows = LightShadows.None;
                _flashes.Add(new Flash { l = l });
            }
        }

        ParticleSystem MakePS(string name, Material m, bool additive, int max, ParticleSystemRenderMode mode, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.gravityModifier = gravity / 9.81f;
            var em = ps.emission; em.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(additive ? 0.6f : 0.8f, 0.4f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1, additive ? AnimationCurve.Linear(0, 1, 1, 0.2f) : AnimationCurve.Linear(0, 0.5f, 1, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode;
            r.sharedMaterial = m;
            if (mode == ParticleSystemRenderMode.Stretch) { r.velocityScale = 0.035f; r.lengthScale = 1.5f; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ primitives
        public void Sparks(Vector3 p, Vector3 dir, int n, float speed, Color c, float size = 0.25f, float life = 0.5f)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = p;
                ep.velocity = (dir + UnityEngine.Random.insideUnitSphere * 0.9f).normalized * speed * UnityEngine.Random.Range(0.4f, 1.2f);
                ep.startColor = c;
                ep.startSize = size * UnityEngine.Random.Range(0.6f, 1.4f);
                ep.startLifetime = life * UnityEngine.Random.Range(0.5f, 1.3f);
                _sparks.Emit(ep, 1);
            }
        }

        void Fire(Vector3 p, int n, float radius, float life = 0.6f)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = p + UnityEngine.Random.insideUnitSphere * radius * 0.5f;
                ep.velocity = UnityEngine.Random.insideUnitSphere * radius * 2f + Vector3.up * 2f;
                ep.startColor = Color.Lerp(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.3f, 0.1f), UnityEngine.Random.value);
                ep.startSize = radius * UnityEngine.Random.Range(0.5f, 1.1f);
                ep.startLifetime = life * UnityEngine.Random.Range(0.5f, 1.2f);
                ep.rotation = UnityEngine.Random.Range(0, 360);
                _fire.Emit(ep, 1);
            }
        }

        public void Smoke(Vector3 p, int n, float size, float life, float rise = 3f, float alpha = 0.6f)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = p + UnityEngine.Random.insideUnitSphere * size * 0.3f;
                ep.velocity = UnityEngine.Random.insideUnitSphere * size * 0.4f + Vector3.up * rise;
                ep.startColor = new Color(0.25f, 0.25f, 0.28f, alpha);
                ep.startSize = size * UnityEngine.Random.Range(0.7f, 1.3f);
                ep.startLifetime = life * UnityEngine.Random.Range(0.7f, 1.3f);
                ep.rotation = UnityEngine.Random.Range(0, 360);
                _smoke.Emit(ep, 1);
            }
        }

        public void FlashLight(Vector3 p, Color c, float intensity, float dur, float range = 30f)
        {
            Flash best = _flashes[0];
            foreach (var f in _flashes) if (f.t <= 0) { best = f; break; } else if (f.t < best.t) best = f;
            best.l.transform.position = p; best.l.color = c; best.l.range = range;
            best.max = intensity; best.t = best.dur = dur;
        }

        Transform NewTracer(bool enemy)
        {
            var pool = enemy ? _enemyTracerPool : _tracerPool;
            if (pool.Count > 0) { var t = pool.Pop(); t.gameObject.SetActive(true); return t; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform);
            go.transform.localScale = new Vector3(0.14f, 0.14f, 5.5f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = enemy ? A.tracerEnemy : A.tracer;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        // ------------------------------------------------------------------ weapons
        public void FireWeapon(MechController owner, Part w, Vector3 aimPoint, MechController target)
        {
            Vector3 muzzle = owner.rig && owner.rig.muzzle ? owner.rig.muzzle.position : owner.Center + owner.Forward * 3f;
            Vector3 dir = (aimPoint - muzzle).normalized;
            Color flashC = w.weapon == WeaponKind.Rail ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.7f, 0.35f);
            FlashLight(muzzle, flashC, w.weapon == WeaponKind.Rail ? 40f : 9f, 0.06f, 20f);
            Sparks(muzzle, dir, 5, 25f, flashC * 2f, 0.3f, 0.12f);
            AudioFX.I?.Shot(w.weapon, muzzle, owner.isPlayer);
            if (w.weapon == WeaponKind.Rail) { Rail(owner, w, muzzle, dir); return; }
            for (int p = 0; p < w.pellets; p++)
            {
                Vector3 d = (dir + UnityEngine.Random.insideUnitSphere * w.spread).normalized;
                var t = NewTracer(!owner.isPlayer);
                t.position = muzzle;
                t.rotation = Quaternion.LookRotation(d);
                _bullets.Add(new Bullet { t = t, vel = d * w.speedMps + owner.velocity, owner = owner, dmg = w.dmg, impact = w.impact, life = 1.4f });
            }
            if (owner.isPlayer) Shake?.Invoke(muzzle, w.pellets > 1 ? 0.25f : 0.06f);
        }

        void Rail(MechController owner, Part w, Vector3 from, Vector3 dir)
        {
            Vector3 end = from + dir * 800f;
            int n = Physics.RaycastNonAlloc(from, dir, _hits, 800f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; RaycastHit? hit = null;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider.GetComponent<MechController>() == owner) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; hit = _hits[i]; }
            }
            if (hit.HasValue)
            {
                end = hit.Value.point;
                var m = hit.Value.collider.GetComponent<MechController>();
                if (m) ApplyHit(m, w.dmg, w.impact, owner, end, dir);
                else Sparks(end, hit.Value.normal, 25, 30f, new Color(0.6f, 0.9f, 1f) * 3f, 0.3f, 0.6f);
            }
            var go = new GameObject("RailBeam");
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, end);
            lr.startWidth = 0.6f; lr.endWidth = 0.4f;
            lr.sharedMaterial = A.gate;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _fades.Add(new Fade { t = go.transform, lr = lr, life = 0.4f, max = 0.4f });
            Shake?.Invoke(from, owner.isPlayer ? 0.5f : 0.2f);
        }

        public void FireShoulder(MechController owner, Part sp, MechController target)
        {
            Vector3 basePos = owner.rig && owner.rig.podMount ? owner.rig.podMount.position : owner.Center + Vector3.up * 2.5f;
            if (sp.shoulder == ShoulderKind.Mines)
            {
                Vector3 back = -owner.Forward;
                Vector3 right = Vector3.Cross(Vector3.up, owner.Forward);
                for (int k = 0; k < sp.count; k++)
                {
                    Vector3 p = owner.transform.position + back * (6 + k * 3.5f) + right * ((k - 1) * 3f);
                    if (Physics.Raycast(p + Vector3.up * 10, Vector3.down, out var gh, 40f)) p = gh.point + Vector3.up * 0.2f;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(transform);
                    go.transform.position = p;
                    go.transform.localScale = new Vector3(1.6f, 0.2f, 1.6f);
                    go.GetComponent<Renderer>().sharedMaterial = A.tracerEnemy;
                    _mines.Add(new Mine { t = go.transform, owner = owner, dmg = sp.dmg, impact = sp.impact, arm = 0.6f, life = 40f });
                }
                AudioFX.I?.Play("mine", basePos, 0.6f);
                return;
            }
            bool heavy = sp.shoulder == ShoulderKind.HeavyMissile;
            for (int k = 0; k < sp.count; k++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform);
                go.transform.position = basePos;
                go.transform.localScale = heavy ? new Vector3(0.5f, 1.0f, 0.5f) : new Vector3(0.25f, 0.5f, 0.25f);
                go.GetComponent<Renderer>().sharedMaterial = A.thrusterCore;
                var tr = go.AddComponent<TrailRenderer>();
                tr.time = 0.9f; tr.startWidth = heavy ? 1.2f : 0.6f; tr.endWidth = 2.2f;
                tr.sharedMaterial = A.smoke;
                tr.minVertexDistance = 0.8f;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var col = new Gradient();
                col.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.7f, 0.4f), 0), new GradientColorKey(new Color(0.3f, 0.3f, 0.35f), 0.15f), new GradientColorKey(new Color(0.2f, 0.2f, 0.22f), 1) },
                            new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.35f, 0.3f), new GradientAlphaKey(0, 1) });
                tr.colorGradient = col;
                Vector3 side = Vector3.Cross(Vector3.up, owner.Forward) * ((k - (sp.count - 1) * 0.5f) * 7f);
                Vector3 vel = owner.Forward * 25f + Vector3.up * 30f + side + owner.velocity;
                _missiles.Add(new Missile { t = go.transform, trail = tr, vel = vel, owner = owner, target = target, dmg = sp.dmg, impact = sp.impact, life = 6f, delay = k * 0.07f, heavy = heavy });
            }
            AudioFX.I?.Play("missile", basePos, 0.8f);
        }

        public void Blade(MechController owner, MechController target)
        {
            Vector3 dir = owner.Forward;
            if (target && Vector3.Distance(target.transform.position, owner.transform.position) < 45f)
            {
                dir = target.transform.position - owner.transform.position; dir.y = 0; dir.Normalize();
                owner.cmd.face = Quaternion.LookRotation(dir).eulerAngles.y;
            }
            owner.Shove(dir * 70f);
            StartCoroutine(BladeHit(owner, dir));
            AudioFX.I?.Play("blade", owner.Center, 1f);
        }

        System.Collections.IEnumerator BladeHit(MechController owner, Vector3 dir)
        {
            yield return new WaitForSeconds(0.22f);
            Vector3 c = owner.Center + dir * 5f;
            // arc FX
            var arc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(arc.GetComponent<Collider>());
            arc.transform.SetParent(transform);
            arc.transform.position = c;
            arc.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, 90);
            arc.transform.localScale = new Vector3(9f, 0.05f, 9f);
            var r = arc.GetComponent<Renderer>();
            r.sharedMaterial = A.thruster;
            _fades.Add(new Fade { t = arc.transform, r = r, life = 0.25f, max = 0.25f, grow = 4f });
            FlashLight(c, new Color(0.4f, 0.8f, 1f), 30f, 0.15f);
            foreach (var m in Mechs?.Invoke() ?? Array.Empty<MechController>())
            {
                if (m == owner || m.IsDown) continue;
                Vector3 to = m.Center - owner.Center;
                if (to.magnitude < 11f && Vector3.Dot(to.normalized, dir) > 0.2f)
                {
                    ApplyHit(m, 780f, 650f, owner, m.Center, dir);
                    m.Shove(dir * 40f + Vector3.up * 5f);
                    Shake?.Invoke(c, 0.9f);
                }
            }
        }

        public void ApplyHit(MechController target, float dmg, float impact, MechController from, Vector3 at, Vector3 dir)
        {
            if (from) { dmg *= from.damageOut; impact *= from.damageOut; }
            target.Damage(dmg, impact, from, at);
            target.rig?.HitFlash();
            Sparks(at, -dir, 10, 18f, new Color(1f, 0.8f, 0.45f) * 2.5f, 0.25f, 0.4f);
            Hit?.Invoke(target, dmg, from);
            if (target.isPlayer) Shake?.Invoke(at, 0.15f + Mathf.Min(0.6f, dmg / 600f));
        }

        public void Explode(Vector3 p, float dmg, float radius, float impact, MechController owner)
        {
            FlashLight(p, new Color(1f, 0.55f, 0.25f), 60f, 0.35f, radius * 5f);
            Fire(p, 26, radius * 0.9f, 0.55f);
            Sparks(p, Vector3.up, 40, 35f, new Color(1f, 0.7f, 0.3f) * 3f, 0.3f, 0.9f);
            Smoke(p, 14, radius * 1.2f, 2.8f, 4f);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(transform);
            ball.transform.position = p;
            ball.transform.localScale = Vector3.one * radius * 0.4f;
            var r = ball.GetComponent<Renderer>();
            r.sharedMaterial = A.explosion;
            _fades.Add(new Fade { t = ball.transform, r = r, life = 0.35f, max = 0.35f, grow = radius * 5f });
            AudioFX.I?.Play("boom", p, 1f);
            Shake?.Invoke(p, 0.8f);
            foreach (var m in Mechs?.Invoke() ?? Array.Empty<MechController>())
            {
                if (m.IsDown) continue;
                float d = Vector3.Distance(m.Center, p);
                if (d > radius) continue;
                float f = 1 - d / radius;
                ApplyHit(m, dmg * (0.4f + 0.6f * f), impact * f, owner, p, (m.Center - p).normalized);
                Vector3 push = (m.transform.position - p); push.y = 0;
                m.Shove(push.normalized * 25f * f + Vector3.up * 6f * f);
            }
        }

        public void StaggerFX(MechController m)
        {
            Sparks(m.Center, Vector3.up, 40, 22f, new Color(1f, 0.85f, 0.3f) * 3f, 0.35f, 0.7f);
            FlashLight(m.Center, new Color(1f, 0.8f, 0.3f), 25f, 0.3f);
            AudioFX.I?.Play("stagger", m.Center, 1f);
        }

        public void QuickBoostFX(MechController m, Vector3 dir)
        {
            Vector3 p = m.rig && m.rig.boosterMount ? m.rig.boosterMount.position : m.Center;
            Sparks(p, -dir, 18, 30f, Color.Lerp(m.loadout.neon, Color.white, 0.4f) * 3f, 0.35f, 0.3f);
            Smoke(m.transform.position + Vector3.up, 6, 2.5f, 1.2f, 1f, 0.35f);
            FlashLight(p, m.loadout.neon, 15f, 0.12f, 18f);
            AudioFX.I?.Play("qb", p, m.isPlayer ? 1f : 0.7f);
            if (m.isPlayer) Shake?.Invoke(p, 0.25f);
        }

        public void OverdriveFX(MechController m, bool on)
        {
            if (on)
            {
                FlashLight(m.Center, m.loadout.neon, 40f, 0.4f, 30f);
                Sparks(m.Center, -m.Forward, 50, 40f, m.loadout.neon * 3f, 0.4f, 0.6f);
                AudioFX.I?.Play("overdrive", m.Center, 1f);
            }
        }

        public void ImpactFX(Vector3 p, float strength)
        {
            Sparks(p, Vector3.up, 30, strength * 0.6f, new Color(1f, 0.8f, 0.4f) * 3f, 0.3f, 0.6f);
            FlashLight(p, new Color(1f, 0.7f, 0.4f), 20f, 0.15f);
            AudioFX.I?.Play("crash", p, 1f);
            Shake?.Invoke(p, 0.7f);
        }

        public void SkidFX(MechController m)
        {
            // sparks off the feet while carving at speed
            Vector3 p = m.transform.position + Vector3.up * 0.3f;
            Sparks(p, -m.velocity.normalized + Vector3.up * 0.3f, 2, 14f, new Color(1f, 0.75f, 0.35f) * 2.5f, 0.18f, 0.35f);
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = Time.deltaTime;
            var mechs = Mechs?.Invoke();
            // bullets
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                Vector3 from = b.t.position;
                Vector3 step = b.vel * dt;
                b.life -= dt;
                bool dead = b.life <= 0;
                if (!dead)
                {
                    int n = Physics.RaycastNonAlloc(from, step.normalized, _hits, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
                    float best = float.MaxValue; int bi = -1;
                    for (int k = 0; k < n; k++)
                    {
                        if (_hits[k].collider.GetComponent<MechController>() == b.owner) continue;
                        if (_hits[k].distance < best) { best = _hits[k].distance; bi = k; }
                    }
                    if (bi >= 0)
                    {
                        var h = _hits[bi];
                        var m = h.collider.GetComponent<MechController>();
                        if (m) ApplyHit(m, b.dmg, b.impact, b.owner, h.point, step.normalized);
                        else { Sparks(h.point, h.normal, 6, 12f, new Color(1f, 0.75f, 0.4f) * 2f, 0.18f, 0.3f); }
                        dead = true;
                    }
                }
                if (dead)
                {
                    b.t.gameObject.SetActive(false);
                    (b.owner && b.owner.isPlayer ? _tracerPool : _enemyTracerPool).Push(b.t);
                    _bullets.RemoveAt(i);
                }
                else b.t.position = from + step;
            }
            // missiles
            for (int i = _missiles.Count - 1; i >= 0; i--)
            {
                var m = _missiles[i];
                m.life -= dt; m.delay -= dt;
                if (m.delay < 0)
                {
                    float spd = m.heavy ? 150f : 175f;
                    if (m.target && !m.target.IsDown)
                    {
                        Vector3 want = (m.target.Center + m.target.velocity * 0.15f - m.t.position).normalized * spd;
                        float turn = m.life < 5.5f ? 3.4f : 0.6f;
                        m.vel = Vector3.Lerp(m.vel, want, 1 - Mathf.Exp(-dt * turn));
                    }
                    if (m.vel.magnitude < spd) m.vel *= 1 + dt * 1.8f;
                }
                else m.vel += Vector3.down * 25f * dt;
                Vector3 from = m.t.position;
                Vector3 step = m.vel * dt;
                bool boom = m.life <= 0;
                if (!boom && Physics.Raycast(from, step.normalized, out var h, step.magnitude + 0.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var hm = h.collider.GetComponent<MechController>();
                    if (hm != m.owner) boom = true;
                }
                if (!boom && mechs != null)
                    foreach (var mc in mechs)
                        if (mc != m.owner && !mc.IsDown && Vector3.Distance(mc.Center, from) < 4f) { boom = true; break; }
                m.t.position = from + step;
                if (m.vel.sqrMagnitude > 1) m.t.rotation = Quaternion.LookRotation(m.vel) * Quaternion.Euler(90, 0, 0);
                if (boom)
                {
                    Explode(m.t.position, m.dmg, m.heavy ? 11f : 6f, m.impact, m.owner);
                    m.trail.transform.SetParent(transform, true);
                    m.trail.autodestruct = true;
                    m.trail.emitting = false;
                    Destroy(m.t.gameObject, 1.2f);
                    m.t.GetComponent<Renderer>().enabled = false;
                    _missiles.RemoveAt(i);
                }
            }
            // mines
            for (int i = _mines.Count - 1; i >= 0; i--)
            {
                var mn = _mines[i];
                mn.arm -= dt; mn.life -= dt;
                float pulse = 1 + Mathf.Sin(Time.time * 12f) * 0.12f;
                mn.t.localScale = new Vector3(1.6f * pulse, 0.2f, 1.6f * pulse);
                bool boom = mn.life <= 0;
                if (mn.arm <= 0 && mechs != null)
                    foreach (var mc in mechs)
                    {
                        if (mc.IsDown || (mc == mn.owner && mn.arm > -1.5f)) continue;
                        if (Vector3.Distance(mc.transform.position, mn.t.position) < 7f) { boom = true; break; }
                    }
                if (boom)
                {
                    Explode(mn.t.position + Vector3.up * 2, mn.dmg, 12f, mn.impact, mn.owner);
                    Destroy(mn.t.gameObject);
                    _mines.RemoveAt(i);
                }
            }
            // fades
            for (int i = _fades.Count - 1; i >= 0; i--)
            {
                var f = _fades[i];
                f.life -= dt;
                float k = Mathf.Clamp01(f.life / f.max);
                if (f.lr) { f.lr.startWidth = 0.7f * k; f.lr.endWidth = 0.5f * k; }
                if (f.grow > 0)
                {
                    var s = f.t.localScale; float g = f.grow * dt;
                    f.t.localScale = s.y < 0.1f ? new Vector3(s.x + g, s.y, s.z + g) : s + Vector3.one * g;
                }
                if (f.r)
                {
                    _mpb ??= new MaterialPropertyBlock();
                    f.r.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_Color", f.r.sharedMaterial.GetColor("_Color") * k);
                    f.r.SetPropertyBlock(_mpb);
                }
                if (f.life <= 0) { Destroy(f.t.gameObject); _fades.RemoveAt(i); }
            }
            foreach (var fl in _flashes)
            {
                if (fl.t > 0) { fl.t -= dt; fl.l.intensity = fl.max * Mathf.Clamp01(fl.t / fl.dur); }
                else fl.l.intensity = 0;
            }
        }

        public void ClearAll()
        {
            foreach (var b in _bullets) { b.t.gameObject.SetActive(false); (b.owner && b.owner.isPlayer ? _tracerPool : _enemyTracerPool).Push(b.t); }
            _bullets.Clear();
            foreach (var m in _missiles) Destroy(m.t.gameObject);
            _missiles.Clear();
            foreach (var m in _mines) Destroy(m.t.gameObject);
            _mines.Clear();
        }
    }
}
