using System;
using UnityEngine;

namespace AU
{
    /// <summary>What a pilot (player or AI) wants the mech to do this frame.</summary>
    public struct MechCommand
    {
        public Vector3 move;        // desired world direction on XZ, length 0..1
        public float face;          // desired facing yaw (degrees)
        public float aimPitch;      // degrees, + is up
        public bool boost, jumpHeld, jumpPressed, quickBoost, fire, fire2, reload, blade, overdrive;
        public Vector3 aimPoint;
        public MechController target;
    }

    /// <summary>
    /// Mech movement, energy, armor, stagger and weapons. Movement is AC-style
    /// boost-skating on a CharacterController; overdrive is a nitrous-style burst
    /// earned by driving with style.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MechController : MonoBehaviour
    {
        public const float Gravity = 34f;
        public string pilot = "YOU";
        public bool isPlayer;
        public bool isBoss;
        public Loadout loadout;
        public MechStats stats;
        public MechRig rig;
        [NonSerialized] public MechCommand cmd;

        public Vector3 velocity;
        public float yaw;
        public float ap, en, overdrive, stagger;
        public bool grounded, boosting, hovering, overheated, overdriveActive;
        public float disabledTimer, staggerTimer;
        public int ammo;
        public float reloadTimer, fireCd, shoulderCd, qbCd, bladeCd, spin;
        public float BoostLevel { get; private set; }
        public float lastImpact;
        public float heightAboveGround;
        public float driftAngle;
        public float statScale = 1f;
        public float damageOut = 1f;   // AI difficulty: scales damage this mech deals

        public event Action<float, MechController> Damaged;
        public event Action<MechController> Disabled;
        public event Action Recovered;
        public event Action QuickBoosted;
        public event Action<string, float> StyleEvent; // name, overdrive gained

        CharacterController _cc;
        float _staggerDelay, _enDelay, _overdriveHold;
        Vector3 _impactVel;

        public bool IsDown => disabledTimer > 0;
        public bool IsStaggered => staggerTimer > 0;
        public bool CanAct => !IsDown && !IsStaggered;
        public Vector3 Center => transform.position + Vector3.up * 4.6f;
        public float Speed => new Vector2(velocity.x, velocity.z).magnitude;
        public Vector3 Forward => Quaternion.Euler(0, yaw, 0) * Vector3.forward;

        public void Setup(Loadout l, float scale = 1f)
        {
            loadout = l;
            statScale = scale;
            stats = MechStats.From(l, scale);
            _cc = GetComponent<CharacterController>();
            _cc.radius = 2.3f;
            _cc.height = 8.2f;
            _cc.center = new Vector3(0, 4.15f, 0);
            _cc.stepOffset = 1.2f;
            _cc.slopeLimit = 55f;
            _cc.skinWidth = 0.12f;
            _cc.minMoveDistance = 0f;
            Repair();
        }

        public void Repair()
        {
            ap = stats.ap; en = stats.enCap; overheated = false; stagger = 0;
            ammo = stats.weapon.mag; reloadTimer = 0; fireCd = 0; shoulderCd = 0; qbCd = 0; bladeCd = 0;
            disabledTimer = 0; staggerTimer = 0;
        }

        public void Teleport(Vector3 pos, float yawDeg)
        {
            if (!_cc) _cc = GetComponent<CharacterController>();
            _cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yawDeg, 0));
            _cc.enabled = true;
            velocity = Vector3.zero;
            yaw = yawDeg;
            cmd.face = yawDeg;
        }

        public void AddOverdrive(float amount, string reason = null)
        {
            if (overdriveActive) return;
            overdrive = Mathf.Clamp01(overdrive + amount);
            if (reason != null) StyleEvent?.Invoke(reason, amount);
        }

        public void Damage(float dmg, float impact, MechController from, Vector3 at)
        {
            if (IsDown) return;
            if (IsStaggered) dmg *= 1.5f;
            ap -= dmg;
            Damaged?.Invoke(dmg, from);
            if (from && from != this) from.AddOverdrive(dmg / 5000f);
            stagger += impact / (900f * stats.stability);
            _staggerDelay = 1.2f;
            if (stagger >= 1f && !IsStaggered)
            {
                stagger = 0;
                staggerTimer = 1.6f;
                Combat.I?.StaggerFX(this);
            }
            if (ap <= 0)
            {
                ap = 0;
                disabledTimer = 3.2f;
                staggerTimer = 0;
                if (from && from != this) from.AddOverdrive(0.35f, "TAKEDOWN");
                Disabled?.Invoke(from);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0 || stats == null) return;
            Simulate(dt);
        }

        void Simulate(float dt)
        {
            var c = cmd;
            var st = stats;
            lastImpact = 0;

            if (IsDown)
            {
                disabledTimer -= dt;
                c = default;
                c.face = yaw;
                if (disabledTimer <= 0) Recovered?.Invoke();
            }
            else if (IsStaggered)
            {
                staggerTimer -= dt;
                c.move = Vector3.zero; c.boost = false; c.fire = c.fire2 = c.blade = c.quickBoost = c.jumpPressed = false;
            }
            _staggerDelay -= dt;
            if (_staggerDelay <= 0) stagger = Mathf.Max(0, stagger - dt * 0.25f);

            // ---------------- energy
            bool moving = c.move.sqrMagnitude > 0.01f;
            bool wantBoost = c.boost && !overheated && moving;
            hovering = c.jumpHeld && !grounded && !overheated && velocity.y < 8f;
            float drain = 0;
            if (wantBoost && !overdriveActive) drain += st.boostDrain;
            if (hovering) drain += st.boostDrain * 0.8f;
            if (drain > 0) { en -= drain * dt; _enDelay = st.enDelay; }
            else
            {
                _enDelay -= dt;
                if (_enDelay <= 0) en += st.enRegen * dt * (overheated ? 0.75f : 1f) * (grounded ? 1f : 0.6f);
            }
            if (en <= 0) { en = 0; overheated = true; }
            if (overheated && en >= st.enCap * 0.45f) overheated = false;
            en = Mathf.Min(en, st.enCap);

            // ---------------- overdrive (nitrous)
            if (c.overdrive && !overdriveActive && overdrive >= 0.2f && CanAct) { overdriveActive = true; Combat.I?.OverdriveFX(this, true); }
            if (overdriveActive)
            {
                overdrive -= dt / 4f;
                if (overdrive <= 0 || !CanAct) { overdrive = Mathf.Max(0, overdrive); overdriveActive = false; Combat.I?.OverdriveFX(this, false); }
            }
            boosting = (wantBoost || overdriveActive) && moving;
            BoostLevel = Mathf.Lerp(BoostLevel, overdriveActive ? 1.6f : boosting ? 1f : hovering ? 0.6f : 0.08f, 1 - Mathf.Exp(-dt * 8f));

            // ---------------- horizontal movement
            float maxSpeed = overdriveActive ? st.overdriveSpeed : boosting ? st.boostSpeed : st.groundSpeed;
            Vector3 hv = new Vector3(velocity.x, 0, velocity.z);
            Vector3 target = c.move; target.y = 0;
            if (target.sqrMagnitude > 1) target.Normalize();
            if (overdriveActive && !moving) target = Forward;
            target *= maxSpeed;
            Vector3 diff = target - hv;
            float accel;
            if (!moving && !overdriveActive) accel = grounded ? (BoostLevel > 0.3f ? 9f : 36f) : 3.5f;
            else
            {
                accel = st.accel * (boosting ? 1.5f : 1f) * (grounded ? st.grip : 0.6f) * (overdriveActive ? 1.4f : 1f);
                if (hv.magnitude > maxSpeed + 2f) accel *= 0.45f; // bleed quick-boost overspeed gently
            }
            Vector3 dv = Vector3.ClampMagnitude(diff, accel * dt);
            velocity.x += dv.x; velocity.z += dv.z;

            // drift angle: facing vs travel (style + skid FX)
            driftAngle = hv.magnitude > 20f ? Vector3.Angle(Forward, hv) : 0f;
            if (grounded && hv.magnitude > 38f && driftAngle > 25f && driftAngle < 120f) AddOverdrive(0.12f * dt);

            // ---------------- quick boost
            qbCd -= dt;
            if (c.quickBoost && qbCd <= 0 && en > st.qbCost * 0.5f && CanAct)
            {
                Vector3 dir = moving ? c.move.normalized : Forward;
                velocity += dir * st.qbForce;
                en -= st.qbCost; _enDelay = st.enDelay * 1.2f;
                qbCd = 0.42f;
                QuickBoosted?.Invoke();
                Combat.I?.QuickBoostFX(this, dir);
            }

            // ---------------- vertical
            if (c.jumpPressed && grounded && !overheated && CanAct) { velocity.y = st.jump; grounded = false; }
            if (hovering) velocity.y += (Gravity + 10f - Mathf.Max(0, velocity.y) * 2f) * dt;
            velocity.y -= Gravity * dt;
            if (grounded && velocity.y < -2f) velocity.y = -2f;

            // ---------------- move + collide
            Vector3 before = transform.position;
            Vector3 intended = velocity * dt + _impactVel * dt;
            _impactVel = Vector3.Lerp(_impactVel, Vector3.zero, 1 - Mathf.Exp(-dt * 6f));
            var flags = _cc.Move(intended);
            Vector3 actual = transform.position - before;
            bool wasGrounded = grounded;
            grounded = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 ih = new Vector3(intended.x, 0, intended.z), ah = new Vector3(actual.x, 0, actual.z);
                float lost = (ih.magnitude - ah.magnitude) / dt;
                if (lost > 32f)
                {
                    lastImpact = lost;
                    Damage((lost - 32f) * 7f, (lost - 32f) * 5f, null, transform.position);
                }
                // slide along the wall: keep only what we actually achieved (plus a little friction)
                Vector3 newH = ah / dt * 0.97f;
                velocity.x = newH.x; velocity.z = newH.z;
            }
            if (grounded && !wasGrounded && velocity.y < -22f) lastImpact = Mathf.Max(lastImpact, -velocity.y * 0.5f);
            if (grounded) velocity.y = Mathf.Max(velocity.y, -2f);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;

            // near-miss style: skimming walls at speed
            if (isPlayer && Speed > 45f && grounded)
            {
                Vector3 right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                if (Physics.Raycast(transform.position + Vector3.up * 3f, right, 4.2f) || Physics.Raycast(transform.position + Vector3.up * 3f, -right, 4.2f))
                    AddOverdrive(0.1f * dt);
            }
            if (!grounded && Speed > 30f) AddOverdrive(0.04f * dt);

            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var gh, 200f)) heightAboveGround = gh.distance - 0.5f;
            else heightAboveGround = 0;

            // ---------------- facing
            float dyaw = Mathf.DeltaAngle(yaw, c.face);
            yaw += dyaw * (1 - Mathf.Exp(-dt * (IsStaggered ? 2f : 10f)));
            transform.rotation = Quaternion.Euler(0, yaw, 0);

            // ---------------- weapons
            UpdateWeapons(dt, c);
        }

        void UpdateWeapons(float dt, MechCommand c)
        {
            var st = stats;
            var w = st.weapon;
            fireCd -= dt; shoulderCd -= dt; bladeCd -= dt;
            if (reloadTimer > 0)
            {
                reloadTimer -= dt;
                if (reloadTimer <= 0) ammo = w.mag;
            }
            else if ((c.reload && ammo < w.mag) || ammo <= 0) reloadTimer = w.reload;

            // gatling spin-up
            spin = Mathf.MoveTowards(spin, c.fire && w.weapon == WeaponKind.Gatling ? 1f : 0f, dt * 1.5f);
            float rate = w.rate * st.fireRateMult * (w.weapon == WeaponKind.Gatling ? Mathf.Lerp(0.25f, 1f, spin) : 1f);
            if (c.fire && fireCd <= 0 && ammo > 0 && reloadTimer <= 0 && CanAct)
            {
                fireCd = 1f / rate;
                ammo--;
                Combat.I?.FireWeapon(this, w, c.aimPoint, c.target);
            }
            var sp = st.shoulder;
            if (c.fire2 && shoulderCd <= 0 && CanAct && (sp.shoulder == ShoulderKind.Mines || c.target))
            {
                shoulderCd = sp.cooldown;
                Combat.I?.FireShoulder(this, sp, c.target);
            }
            if (c.blade && bladeCd <= 0 && CanAct)
            {
                bladeCd = 2.2f;
                Combat.I?.Blade(this, c.target);
            }
        }

        /// <summary>Called by Combat for lunges and explosions.</summary>
        public void Shove(Vector3 v) => _impactVel += v;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var other = hit.collider.GetComponent<MechController>();
            if (!other || other == this) return;
            Vector3 n = (other.transform.position - transform.position); n.y = 0;
            if (n.sqrMagnitude < 0.01f) return;
            n.Normalize();
            float rel = Vector3.Dot(velocity - other.velocity, n);
            if (rel <= 0) return;
            float imp = rel * 0.9f;
            velocity -= n * imp * 0.6f;
            other.Shove(n * imp * 0.8f);
            if (rel > 20f)
            {
                float dmg = (rel - 20f) * 9f;
                other.Damage(dmg, dmg * 1.2f, this, hit.point);
                Damage(dmg * 0.5f, dmg * 0.4f, other, hit.point);
                Combat.I?.ImpactFX(hit.point, rel);
                if (isPlayer) AddOverdrive(0.15f, "SLAM");
            }
        }
    }
}
