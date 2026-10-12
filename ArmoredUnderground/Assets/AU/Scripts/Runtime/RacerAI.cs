using UnityEngine;

namespace AU
{
    /// <summary>
    /// Rival pilot: follows the street legs between gates (never cutting across
    /// blocks), brakes for corners, manages boost/overdrive, and fights with
    /// bursts, missiles, mines and the occasional blade lunge.
    /// </summary>
    public class RacerAI : MonoBehaviour
    {
        MechController _m;
        Race _race;
        Racer _r;
        float _skill, _lane;
        float _burst, _pause = 1.5f, _stuck, _qbTimer = 3f, _odTimer;
        Vector3 _aimErr;

        public void Init(MechController m, Race race, Racer r, float skill, float lane)
        {
            _m = m; _race = race; _r = r; _skill = skill; _lane = lane;
            _odTimer = Random.Range(8f, 20f);
            m.damageOut = Mathf.Lerp(0.4f, 0.7f, Mathf.InverseLerp(0.8f, 1.15f, skill));
        }

        void Update()
        {
            if (!_m || _race == null) return;
            float dt = Time.deltaTime;
            var c = new MechCommand { face = _m.yaw };
            if (_race.state != "running" || _m.IsDown) { _m.cmd = c; return; }
            if (_r.finished) { _m.cmd = c; return; }

            // ---------------- navigation
            var gates = _race.gates;
            Vector3 g = gates[_r.next];
            Vector3 prev = _r.next == 0 ? _race.start : gates[_r.next - 1];
            Vector3 seg = g - prev; seg.y = 0;
            float segLen = seg.magnitude; seg /= Mathf.Max(0.01f, segLen);
            Vector3 right = Vector3.Cross(Vector3.up, seg);
            Vector3 rel = _m.transform.position - prev; rel.y = 0;
            float along = Mathf.Clamp(Vector3.Dot(rel, seg), 0, segLen);
            float lateral = Vector3.Dot(rel, right) - _lane;
            float look = Mathf.Max(0, 38f - Mathf.Abs(lateral) * 2.5f);
            Vector3 target = prev + seg * Mathf.Min(segLen, along + look) + right * _lane;
            Vector3 toT = target - _m.transform.position; toT.y = 0;
            float dist = new Vector2(g.x - _m.transform.position.x, g.z - _m.transform.position.z).magnitude;
            float corner = 0;
            if (_r.next + 1 < gates.Count)
            {
                Vector3 seg2 = gates[_r.next + 1] - g; seg2.y = 0;
                corner = Vector3.Angle(seg, seg2);
            }
            Vector3 dir = toT.sqrMagnitude > 0.01f ? toT.normalized : seg;

            float speed = _m.Speed;
            float cornerSpeed = corner > 20f ? 30f + (1 - corner / 180f) * 28f : 999f;
            float brakeDist = Mathf.Max(0, (speed * speed - cornerSpeed * cornerSpeed) / (2f * 58f)) + 14f;
            bool braking = dist < brakeDist && speed > cornerSpeed;
            c.move = dir * (braking ? 0.3f : 1f);
            if (braking && _m.velocity.sqrMagnitude > 1)
            {
                Vector3 v = _m.velocity; v.y = 0;
                c.move -= v.normalized * 0.6f;
            }
            float enF = _m.en / _m.stats.enCap;
            c.boost = !braking && (_m.boosting ? enF > 0.05f : enF > 0.35f);
            Vector3 face = _m.velocity.sqrMagnitude > 4 ? new Vector3(_m.velocity.x, 0, _m.velocity.z) : dir;
            c.face = Quaternion.LookRotation(face).eulerAngles.y;

            _qbTimer -= dt;
            if (_qbTimer <= 0 && !braking && dist > 120f && enF > 0.6f) { c.quickBoost = true; _qbTimer = 3f + Random.value * 4f / _skill; }
            _odTimer -= dt;
            if (_odTimer <= 0 && !braking && dist > 150f && _m.overdrive >= 0.3f) { c.overdrive = true; _odTimer = Random.Range(10f, 18f); }
            // AIs slowly bank overdrive so they can use it
            _m.AddOverdrive(dt * 0.02f * _skill);

            if (speed < 4f) _stuck += dt; else _stuck = 0;
            if (_stuck > 1.4f) { c.jumpPressed = true; c.jumpHeld = true; c.quickBoost = true; c.move = dir; _stuck = 0; }

            // ---------------- combat
            MechController best = null; float bestScore = 180f;
            foreach (var o in _race.racers)
            {
                var om = o.mech;
                if (om == _m || om.IsDown) continue;
                float d = Vector3.Distance(om.transform.position, _m.transform.position);
                float score = d * (om.isPlayer ? 0.7f : 1f);
                if (score >= bestScore) continue;
                Vector3 to = (om.transform.position - _m.transform.position); to.y = 0;
                if (Vector3.Dot(_m.Forward, to.normalized) < 0.3f) continue;
                best = om; bestScore = score;
            }
            c.target = best;
            if (best)
            {
                Vector3 from = _m.Center + Vector3.up * 1.5f;
                bool los = _race.time > 5f && !Physics.Linecast(from, best.Center, ~(1 << 6 | 1 << 7), QueryTriggerInteraction.Ignore);
                if (los)
                {
                    var w = _m.stats.weapon;
                    if (_burst > 0)
                    {
                        _burst -= dt;
                        float t = Vector3.Distance(from, best.Center) / Mathf.Max(100f, w.speedMps);
                        float err = 3.2f / _skill;
                        _aimErr = Vector3.Lerp(_aimErr, Random.insideUnitSphere * err, dt * 2f);
                        c.aimPoint = best.Center + best.velocity * t * 0.8f + _aimErr;
                        c.fire = true;
                        if (_burst <= 0) _pause = (1f + Random.value * 1.8f) / _skill;
                    }
                    else
                    {
                        _pause -= dt;
                        if (_pause <= 0) _burst = 0.8f + Random.value * 1.2f;
                    }
                    if (_m.stats.shoulder.shoulder != ShoulderKind.Mines && Random.value < dt * 0.22f * _skill) c.fire2 = true;
                    if (Vector3.Distance(best.transform.position, _m.transform.position) < 22f && Random.value < dt * 0.4f * _skill) c.blade = true;
                }
                Vector3 to2 = best.Center - _m.Center;
                c.aimPitch = Mathf.Atan2(to2.y, new Vector2(to2.x, to2.z).magnitude) * Mathf.Rad2Deg;
            }
            if (_m.stats.shoulder.shoulder == ShoulderKind.Mines)
            {
                foreach (var o in _race.racers)
                {
                    if (o.mech == _m) continue;
                    Vector3 to = o.mech.transform.position - _m.transform.position; to.y = 0;
                    if (to.magnitude < 45f && Vector3.Dot(-_m.Forward, to.normalized) > 0.7f && Random.value < dt * 0.8f) c.fire2 = true;
                }
            }
            _m.cmd = c;
        }
    }
}
