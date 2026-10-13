using System;
using UnityEngine;

namespace AU
{
    /// <summary>A 1 vs 1 arena bout: spawns, countdown, time limit and the result.</summary>
    public class Duel : MonoBehaviour
    {
        public CityLayout.DuelDef def;
        public MechController player, rival;
        public string state = "countdown";   // countdown, fight, done
        public float countdown = 4f, timeLeft;
        public bool playerWon;
        public event Action<int> Beep;
        public event Action Finished;

        public float PlayerPct => player ? Mathf.Clamp01(player.ap / player.stats.ap) : 0f;
        public float RivalPct => rival ? Mathf.Clamp01(rival.ap / rival.stats.ap) : 0f;

        static readonly Loadout[] RivalBuilds =
        {
            new Loadout { head = "hd_moth", core = "cr_wasp", arms = "ar_needle", legs = "lg_hopper", booster = "bs_comet", generator = "gn_surge", weapon = "wp_gale", shoulder = "sh_swarm", paint = new Color(0.42f, 0.1f, 0.1f), accent = new Color(0.75f, 0.72f, 0.7f), neon = new Color(1f, 0.25f, 0.2f) },
            new Loadout { head = "hd_bastion", core = "cr_anvil", arms = "ar_hauler", legs = "lg_bulwark", booster = "bs_tern", generator = "gn_coil", weapon = "wp_maul", shoulder = "sh_spear", paint = new Color(0.2f, 0.22f, 0.26f), accent = new Color(0.85f, 0.15f, 0.12f), neon = new Color(1f, 0.3f, 0.1f) },
        };

        public static Duel Create(CityLayout.DuelDef def, MechController player, Func<string, Loadout, float, bool, MechController> spawn)
        {
            var go = new GameObject("Duel_" + def.id);
            var d = go.AddComponent<Duel>();
            d.def = def;
            d.player = player;
            d.timeLeft = def.timeLimit;
            Vector3 c = CityLayout.Ground(def.arena.x, def.arena.y);
            player.Repair();
            player.Teleport(c + new Vector3(0, 0.4f, -def.arenaFieldZ), 0f);
            var build = RivalBuilds[def.skill > 1f ? 1 : 0];
            d.rival = spawn(def.rival, build, def.skill, false);
            d.rival.Teleport(c + new Vector3(0, 0.4f, def.arenaFieldZ), 180f);
            d.rival.damageOut = Mathf.Lerp(0.45f, 0.8f, Mathf.InverseLerp(0.8f, 1.2f, def.skill));
            // a tall invisible wall around the floor keeps the bout on the field
            var wall = new GameObject("BoutWall").transform;
            wall.SetParent(go.transform, false);
            for (int k = 0; k < 64; k++)
            {
                float a0 = k / 64f * Mathf.PI * 2, a1 = (k + 1) / 64f * Mathf.PI * 2;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0) * (def.field.x + 1), 0, Mathf.Sin(a0) * (def.field.y + 1));
                Vector3 p1 = c + new Vector3(Mathf.Cos(a1) * (def.field.x + 1), 0, Mathf.Sin(a1) * (def.field.y + 1));
                var seg = new GameObject("Seg");
                seg.transform.SetParent(wall, false);
                Vector3 outN = new Vector3(Mathf.Cos((a0 + a1) * 0.5f) / def.field.x, 0, Mathf.Sin((a0 + a1) * 0.5f) / def.field.y).normalized;
                seg.transform.SetPositionAndRotation((p0 + p1) * 0.5f + Vector3.up * 30f, Quaternion.LookRotation(outN));
                seg.AddComponent<BoxCollider>().size = new Vector3(Vector3.Distance(p0, p1) + 1f, 70f, 2f);
            }
            var ai = d.rival.gameObject.AddComponent<DuelAI>();
            ai.Init(d.rival, player, def.skill, () => d.state == "fight");
            return d;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (state == "countdown")
            {
                int before = Mathf.CeilToInt(countdown);
                countdown -= dt;
                int after = Mathf.CeilToInt(countdown);
                if (after != before && after <= 3) Beep?.Invoke(after);
                foreach (var m in new[] { player, rival }) { m.cmd.move = Vector3.zero; m.cmd.boost = false; m.cmd.fire = m.cmd.fire2 = false; m.cmd.quickBoost = false; }
                if (countdown <= 0) state = "fight";
                return;
            }
            if (state != "fight") return;
            timeLeft -= dt;
            if (rival.IsDown) Finish(true);
            else if (player.IsDown) Finish(false);
            else if (timeLeft <= 0) Finish(PlayerPct >= RivalPct);
        }

        void Finish(bool won)
        {
            state = "done";
            playerWon = won;
            Finished?.Invoke();
        }

        public void Teardown()
        {
            if (rival) Destroy(rival.gameObject);
            Destroy(gameObject);
        }
    }

    /// <summary>Arena opponent: keeps a fighting range, circles, dodges with quick boosts and shoots.</summary>
    public class DuelAI : MonoBehaviour
    {
        MechController _m, _t;
        float _skill, _strafe = 1f, _strafeT, _qbT, _burst, _pause, _jumpT;
        Func<bool> _live;
        Vector3 _aimErr;

        public void Init(MechController m, MechController target, float skill, Func<bool> live)
        {
            _m = m; _t = target; _skill = skill; _live = live;
        }

        void Update()
        {
            if (!_m || !_t || _live == null || !_live()) return;
            float dt = Time.deltaTime;
            var c = new MechCommand();
            Vector3 to = _t.transform.position - _m.transform.position; to.y = 0;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.1f ? to / dist : _m.Forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * _strafe;
            _strafeT -= dt;
            if (_strafeT <= 0) { _strafe = UnityEngine.Random.value < 0.5f ? -1 : 1; _strafeT = UnityEngine.Random.Range(1.8f, 4f); }
            Vector3 move = side * 0.8f;
            if (dist > 55f) move += dir;
            else if (dist < 25f) move -= dir * 0.8f;
            c.move = move.normalized;
            float enF = _m.en / _m.stats.enCap;
            c.boost = (dist > 90f || enF > 0.6f) && enF > 0.15f;
            c.face = Quaternion.LookRotation(dir).eulerAngles.y;
            _qbT -= dt;
            if (_qbT <= 0 && enF > 0.4f) { c.quickBoost = true; c.move = side; _qbT = UnityEngine.Random.Range(2.5f, 5f) / _skill; }
            _jumpT -= dt;
            if (_jumpT <= 0) { c.jumpPressed = true; c.jumpHeld = true; _jumpT = UnityEngine.Random.Range(5f, 10f); }
            else if (_jumpT > 4.2f && !_m.grounded) c.jumpHeld = true;
            c.target = _t;
            Vector3 from = _m.Center + Vector3.up * 1.5f;
            bool los = !Physics.Linecast(from, _t.Center, ~(1 << 6 | 1 << 7), QueryTriggerInteraction.Ignore);
            if (los && !_t.IsDown)
            {
                var w = _m.stats.weapon;
                if (_burst > 0)
                {
                    _burst -= dt;
                    float lead = Vector3.Distance(from, _t.Center) / Mathf.Max(100f, w.speedMps);
                    _aimErr = Vector3.Lerp(_aimErr, UnityEngine.Random.insideUnitSphere * 3.5f / _skill, dt * 2f);
                    c.aimPoint = _t.Center + _t.velocity * lead * 0.8f + _aimErr;
                    c.fire = true;
                    if (_burst <= 0) _pause = (0.8f + UnityEngine.Random.value * 1.6f) / _skill;
                }
                else
                {
                    _pause -= dt;
                    if (_pause <= 0) _burst = 0.9f + UnityEngine.Random.value * 1.3f;
                }
                if (UnityEngine.Random.value < dt * 0.3f * _skill) c.fire2 = true;
                if (dist < 20f && UnityEngine.Random.value < dt * 0.6f) c.blade = true;
            }
            Vector3 to2 = _t.Center - _m.Center;
            c.aimPitch = Mathf.Atan2(to2.y, new Vector2(to2.x, to2.z).magnitude) * Mathf.Rad2Deg;
            if (!c.fire) c.aimPoint = _t.Center;
            _m.cmd = c;
        }
    }
}
