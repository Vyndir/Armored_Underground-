using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AU
{
    public class Racer
    {
        public MechController mech;
        public RacerAI ai;
        public int next;
        public bool finished;
        public float time;
        public int takedowns;
        public Vector3 respawn;
        public float respawnYaw;
    }

    /// <summary>One underground race: grid, gates, standings, respawns and payout.</summary>
    public class Race : MonoBehaviour
    {
        public CityLayout.RouteDef def;
        public List<Vector3> gates = new();
        public Vector3 start;
        public float startYaw;
        public List<Racer> racers = new();
        public Racer player;
        public string state = "countdown";
        public float countdown = 4f, time;
        public List<Racer> finishOrder = new();
        public Result result;
        public event Action<int> Beep;         // seconds left, 0 = go
        public event Action<bool> GatePassed;  // final?
        public event Action Finished;

        public class Result { public int position, total, credits, rep, takedowns, bonus; public float time; public List<Racer> order; }

        readonly List<Transform> _gateVis = new();
        readonly List<Action> _unsub = new();
        Transform _beacon;
        public int GatesPerLap { get; private set; }
        public float GateRadius => 20f;

        public static Race Create(CityLayout.RouteDef def, MechController playerMech, Func<string, Loadout, float, bool, MechController> spawnMech)
        {
            var go = new GameObject("Race_" + def.id);
            var r = go.AddComponent<Race>();
            r.Init(def, playerMech, spawnMech);
            return r;
        }

        static readonly (string name, Loadout l)[] Rivals =
        {
            ("KESTREL", new Loadout { head = "hd_moth", core = "cr_wasp", arms = "ar_needle", legs = "lg_hopper", booster = "bs_comet", generator = "gn_surge", weapon = "wp_gale", shoulder = "sh_swarm", paint = new Color(0.35f, 0.06f, 0.1f), accent = new Color(0.8f, 0.8f, 0.8f), neon = new Color(1f, 0.2f, 0.2f) }),
            ("MOTHBALL", new Loadout { head = "hd_bastion", core = "cr_anvil", arms = "ar_hauler", legs = "lg_bulwark", booster = "bs_flare", generator = "gn_coil", weapon = "wp_maul", shoulder = "sh_mines", paint = new Color(0.26f, 0.28f, 0.2f), accent = new Color(0.65f, 0.6f, 0.4f), neon = new Color(1f, 0.7f, 0f) }),
            ("ROOK", new Loadout { weapon = "wp_hornet", booster = "bs_tern", paint = new Color(0.1f, 0.15f, 0.28f), accent = new Color(0.6f, 0.7f, 0.85f), neon = new Color(0.16f, 0.47f, 1f) }),
            ("SOLDER", new Loadout { head = "hd_moth", arms = "ar_needle", legs = "lg_glide", booster = "bs_tern", generator = "gn_surge", weapon = "wp_gale", shoulder = "sh_mines", paint = new Color(0.18f, 0.1f, 0.28f), accent = new Color(0.78f, 0.72f, 0.88f), neon = new Color(0.5f, 0.3f, 1f) }),
            ("GLASSJAW", new Loadout { core = "cr_wasp", legs = "lg_glide", booster = "bs_comet", weapon = "wp_hornet", shoulder = "sh_spear", paint = new Color(0.05f, 0.23f, 0.21f), accent = new Color(0.75f, 0.9f, 0.88f), neon = new Color(0.2f, 1f, 0.1f) }),
        };

        void Init(CityLayout.RouteDef d, MechController playerMech, Func<string, Loadout, float, bool, MechController> spawnMech)
        {
            def = d;
            var pts = d.Points();
            start = pts[0];
            if (d.type == "CIRCUIT")
            {
                for (int l = 0; l < d.laps; l++) { for (int k = 1; k < pts.Count; k++) gates.Add(pts[k]); gates.Add(pts[0]); }
                GatesPerLap = pts.Count;
            }
            else { for (int k = 1; k < pts.Count; k++) gates.Add(pts[k]); GatesPerLap = gates.Count; }

            Vector3 fwd = (pts[1] - pts[0]); fwd.y = 0; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            startYaw = Quaternion.LookRotation(fwd).eulerAngles.y;
            var slots = new List<Vector3>();
            for (int k = 0; k < 6; k++)
            {
                int row = k / 2; float col = k % 2 == 0 ? -1 : 1;
                slots.Add(pts[0] - fwd * (16 + row * 14) + right * col * 6f);
            }
            var pool = Rivals.OrderBy(_ => UnityEngine.Random.value).Take(d.rivals).ToList();
            int i = 0;
            foreach (var (name, l) in pool)
            {
                float skill = d.skill * UnityEngine.Random.Range(0.95f, 1.04f);
                var m = spawnMech(name, l, skill, false);
                var ai = m.gameObject.AddComponent<RacerAI>();
                var rc = new Racer { mech = m, ai = ai };
                ai.Init(m, this, rc, skill, (i % 3 - 1) * 5f);
                m.Teleport(slots[i], startYaw);
                rc.respawn = slots[i]; rc.respawnYaw = startYaw;
                racers.Add(rc);
                i++;
            }
            player = new Racer { mech = playerMech, respawn = slots[i], respawnYaw = startYaw };
            playerMech.Repair();
            playerMech.Teleport(slots[i], startYaw);
            racers.Add(player);
            foreach (var r in racers)
            {
                var rr = r;
                Action<MechController> onDown = from => { var fr = racers.FirstOrDefault(x => x.mech == from); if (fr != null) fr.takedowns++; };
                Action onUp = () => Respawn(rr);
                r.mech.Disabled += onDown;
                r.mech.Recovered += onUp;
                _unsub.Add(() => { if (rr.mech) { rr.mech.Disabled -= onDown; rr.mech.Recovered -= onUp; } });
            }
            BuildGateVisuals();
        }

        void BuildGateVisuals()
        {
            var a = GameAssets.I;
            for (int k = 0; k < 2; k++)
            {
                var g = new GameObject("Gate" + k).transform;
                g.SetParent(transform);
                // two neon pylons + overhead bar, like a street arch
                for (int s = -1; s <= 1; s += 2)
                {
                    var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(p.GetComponent<Collider>());
                    p.transform.SetParent(g, false);
                    p.transform.localPosition = new Vector3(s * 13f, 6f, 0);
                    p.transform.localScale = new Vector3(0.5f, 12f, 0.5f);
                    p.GetComponent<Renderer>().sharedMaterial = a.gate;
                }
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(g, false);
                bar.transform.localPosition = new Vector3(0, 12f, 0);
                bar.transform.localScale = new Vector3(26.5f, 0.5f, 0.5f);
                bar.GetComponent<Renderer>().sharedMaterial = a.gate;
                var curtain = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(curtain.GetComponent<Collider>());
                curtain.transform.SetParent(g, false);
                curtain.transform.localPosition = new Vector3(0, 6f, 0);
                curtain.transform.localScale = new Vector3(26f, 12f, 1);
                curtain.GetComponent<Renderer>().sharedMaterial = a.gateBeam;
                if (k == 1) g.localScale = Vector3.one * 0.999f;
                _gateVis.Add(g);
            }
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(transform);
            beam.transform.localScale = new Vector3(5f, 160f, 5f);
            beam.GetComponent<Renderer>().sharedMaterial = a.gateBeam;
            _beacon = beam.transform;
            PlaceGates();
        }

        public Vector3 GateDir(int i)
        {
            Vector3 a = i == 0 ? start : gates[i - 1];
            Vector3 d = gates[i] - a; d.y = 0;
            return d.normalized;
        }

        void PlaceGates()
        {
            int n = player.next;
            for (int k = 0; k < 2; k++)
            {
                int gi = n + k;
                var g = _gateVis[k];
                if (gi >= gates.Count) { g.gameObject.SetActive(false); continue; }
                g.gameObject.SetActive(true);
                g.position = gates[gi];
                g.rotation = Quaternion.LookRotation(GateDir(gi));
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.enabled = k == 0 || r.name != "Quad";
            }
            if (n < gates.Count) { _beacon.gameObject.SetActive(true); _beacon.position = gates[n] + Vector3.up * 160f; }
            else _beacon.gameObject.SetActive(false);
        }

        void Respawn(Racer r)
        {
            var m = r.mech;
            m.Repair();
            m.ap = m.stats.ap * 0.6f;
            Vector3 next = gates[Mathf.Min(r.next, gates.Count - 1)];
            Vector3 d = next - r.respawn; d.y = 0;
            m.Teleport(r.respawn + Vector3.up * 0.5f, d.sqrMagnitude > 1 ? Quaternion.LookRotation(d).eulerAngles.y : r.respawnYaw);
        }

        public float Progress(Racer r)
        {
            if (r.finished) return 1e9f - r.time;
            var g = gates[Mathf.Min(r.next, gates.Count - 1)];
            Vector3 p = r.mech.transform.position;
            return r.next * 10000f - new Vector2(g.x - p.x, g.z - p.z).magnitude;
        }

        public List<Racer> Standings() => racers.OrderByDescending(Progress).ToList();
        public int Lap => Mathf.Min(def.laps, player.next / GatesPerLap + 1);

        void Update()
        {
            float dt = Time.deltaTime;
            if (state == "countdown")
            {
                int before = Mathf.CeilToInt(countdown);
                countdown -= dt;
                int after = Mathf.CeilToInt(countdown);
                if (after != before && after <= 3) Beep?.Invoke(after);
                foreach (var r in racers) { r.mech.cmd.move = Vector3.zero; r.mech.cmd.boost = false; r.mech.cmd.fire = r.mech.cmd.fire2 = false; r.mech.cmd.quickBoost = false; r.mech.cmd.overdrive = false; }
                if (countdown <= 0) state = "running";
                return;
            }
            if (state == "running") time += dt;
            foreach (var r in racers)
            {
                if (r.finished || r.next >= gates.Count) continue;
                var g = gates[r.next];
                Vector3 p = r.mech.transform.position;
                if (new Vector2(g.x - p.x, g.z - p.z).magnitude < GateRadius)
                {
                    r.respawn = new Vector3(g.x, 0.2f, g.z);
                    r.next++;
                    if (r == player) { GatePassed?.Invoke(r.next >= gates.Count); PlaceGates(); }
                    if (r.next >= gates.Count)
                    {
                        r.finished = true; r.time = time; finishOrder.Add(r);
                        if (r == player) Finish();
                    }
                }
            }
        }

        void Finish()
        {
            state = "done";
            var order = new List<Racer>(finishOrder);
            foreach (var r in Standings()) if (!order.Contains(r)) order.Add(r);
            int pos = order.IndexOf(player);
            result = new Result
            {
                position = pos + 1, total = order.Count, time = time,
                credits = pos < def.payout.Length ? def.payout[pos] : 0,
                rep = pos < def.repGain.Length ? def.repGain[pos] : 0,
                takedowns = player.takedowns, bonus = player.takedowns * 300, order = order,
            };
            Finished?.Invoke();
        }

        public void Teardown()
        {
            foreach (var u in _unsub) u();
            foreach (var r in racers) if (r != player && r.mech) Destroy(r.mech.gameObject);
            Destroy(gameObject);
        }
    }
}
