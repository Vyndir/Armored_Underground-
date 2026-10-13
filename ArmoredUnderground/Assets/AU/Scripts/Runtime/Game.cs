using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace AU
{
    /// <summary>Game flow: title, free roam, race events, results, garage, pause.</summary>
    public class Game : MonoBehaviour
    {
        public enum State { Title, Roam, EventCard, Race, Results, Garage, Paused, Duel }
        public State state = State.Title;
        State _beforePause;

        public SaveData save;
        public MechController player;
        PlayerDriver _driver;
        ChaseCamera _chase;
        Hud _hud;
        Menus _menus;
        Garage _garage;
        Race _race;
        Duel _duel;
        float _duelEndT;
        CityLayout.RouteDef _pendingEvent;
        readonly List<MechController> _mechs = new();
        readonly List<(Transform t, Vector3 pos, CityLayout.RouteDef def, CityLayout.DuelDef duel, Color color)> _markers = new();
        float _titleT, _raceEndT;
        bool _autotest;
        public static Vector3 PlayerSpawn => CityLayout.Ground(CityLayout.PlayerSpawn.x, CityLayout.PlayerSpawn.y) + Vector3.up * 0.3f;
        Vector3 RigShopFront => CityLayout.Ground(CityLayout.RigShop.x, CityLayout.RigShop.y - 4f);

        void Start()
        {
            if (CaptureTour.Requested(out _)) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            gameObject.AddComponent<Combat>();
            gameObject.AddComponent<AudioFX>();
            var life = gameObject.AddComponent<CityLife>();
            life.Mechs = () => _mechs;
            Combat.I.Mechs = () => _mechs.Where(m => m);
            Combat.I.Hit += OnHit;
            Combat.I.Shake += (p, s) => { if (_chase && player) _chase.AddTrauma(s * Mathf.Clamp01(1.2f - Vector3.Distance(p, player.Center) / 120f)); };

            save = SaveData.Load();
            player = SpawnMech("YOU", save.loadout, 1f, true);
            player.Teleport(PlayerSpawn, 90f);
            player.rig.AddCharacterLights();
            player.Damaged += (d, from) => { _chase?.Damage(Mathf.Clamp01(0.25f + d / 500f)); if (from) AudioFX.I?.Play("hit", player.Center, 0.5f, 0.6f); };
            player.StyleEvent += (n, amt) => _hud?.Style($"{n}  +OVERDRIVE");
            player.Recovered += () => { if (_race == null && _duel == null) { player.Repair(); } };

            var cam = Bootstrap.MainCam;
            _chase = cam.gameObject.AddComponent<ChaseCamera>();
            _chase.target = player;
            _chase.SnapBehind();
            _driver = player.gameObject.AddComponent<PlayerDriver>();
            _driver.mech = player; _driver.cam = _chase;
            _driver.Targets = () => _mechs;

            _hud = gameObject.AddComponent<Hud>();
            _hud.Build();
            _menus = gameObject.AddComponent<Menus>();
            _menus.Build();
            _garage = gameObject.AddComponent<Garage>();
            _garage.Init(save, cam);
            _garage.Closed += OnGarageClosed;
            _garage.LoadoutChanged += () => { player.Setup(save.loadout); player.rig.Configure(save.loadout); };

            _menus.OnStart = StartGame;
            _menus.OnResume = Resume;
            _menus.OnRetire = () => { Resume(); if (_duel != null) { _duel.playerWon = false; EndDuel(); } else EndRace(false); };
            _menus.OnReset = () => { save = SaveData.Reset(); Resume(); ApplyLoadout(); _hud.Toast("CAREER RESET", new Color(1, 0.3f, 0.25f)); BuildMarkers(); };
            _menus.OnQuit = Application.Quit;
            _menus.OnRace = StartRace;
            _menus.OnCancelEvent = CloseEvent;
            _menus.OnContinue = ContinueAfterResults;

            BuildMarkers();
            EnterTitle();

            var args = Environment.GetCommandLineArgs();
            int ai = Array.IndexOf(args, "-autotest");
            if (ai >= 0 && ai + 1 < args.Length) { CaptureTour.Dir = args[ai + 1]; _autotest = true; StartCoroutine(AutoTest()); }
        }

        // ------------------------------------------------------------------ mechs
        public MechController SpawnMech(string pilot, Loadout l, float scale, bool isPlayer)
        {
            var go = Instantiate(GameAssets.I.mechPrefab);
            go.name = "Mech_" + pilot;
            go.layer = 6;
            go.AddComponent<CharacterController>();
            var mc = go.AddComponent<MechController>();
            mc.pilot = pilot; mc.isPlayer = isPlayer;
            mc.Setup(l.Clone(), scale);
            var rig = go.AddComponent<MechRig>();
            rig.Init(mc);
            mc.rig = rig;
            rig.Configure(l);
            _mechs.Add(mc);
            return mc;
        }

        void ApplyLoadout()
        {
            player.Setup(save.loadout);
            player.rig.Configure(save.loadout);
        }

        // ------------------------------------------------------------------ markers
        void BuildMarkers()
        {
            foreach (var m in _markers) if (m.t) Destroy(m.t.gameObject);
            _markers.Clear();
            AddMarker(RigShopFront, null, null, UIKit.Teal);
            foreach (var d in CityLayout.Routes)
            {
                bool locked = save.rep < d.repRequired;
                bool won = save.Result(d.id)?.bestPos == 1;
                AddMarker(d.Points()[0], d, null, locked ? UIKit.Rival : won ? new Color(0.3f, 1f, 0.4f) : UIKit.Gold);
            }
            foreach (var d in CityLayout.Duels)
            {
                bool locked = save.rep < d.repRequired;
                bool won = save.Result(d.id)?.bestPos == 1;
                AddMarker(CityLayout.Ground(d.registration.x, d.registration.y), null, d, locked ? UIKit.Rival : won ? new Color(0.3f, 1f, 0.4f) : UIKit.Gold);
            }
        }

        void AddMarker(Vector3 pos, CityLayout.RouteDef def, CityLayout.DuelDef duel, Color color)
        {
            var root = new GameObject(def != null ? "EventMarker_" + def.id : duel != null ? "DuelMarker_" + duel.id : "GarageMarker").transform;
            root.position = pos;
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pillar.GetComponent<Collider>());
            pillar.transform.SetParent(root, false);
            pillar.transform.localPosition = Vector3.up * 70f;
            pillar.transform.localScale = new Vector3(3.5f, 70f, 3.5f);
            var pr = pillar.GetComponent<Renderer>();
            pr.sharedMaterial = def == null && duel == null ? GameAssets.I.markerGlow : GameAssets.I.gateBeam;
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_Color", color * 0.5f); pr.SetPropertyBlock(mpb);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(root, false);
            ring.transform.localPosition = Vector3.up * 0.05f;
            ring.transform.localScale = new Vector3(14f, 0.02f, 14f);
            var rr = ring.GetComponent<Renderer>();
            rr.sharedMaterial = GameAssets.I.gateBeam;
            var mpb2 = new MaterialPropertyBlock(); mpb2.SetColor("_Color", color * 0.8f); rr.SetPropertyBlock(mpb2);
            var l = new GameObject("L").AddComponent<Light>();
            l.transform.SetParent(root, false); l.transform.localPosition = Vector3.up * 4;
            l.color = color; l.range = 25; l.intensity = 4; l.shadows = LightShadows.None;
            _markers.Add((root, pos, def, duel, color));
        }

        // ------------------------------------------------------------------ states
        void EnterTitle()
        {
            state = State.Title;
            _menus.ShowTitle(true);
            _hud.SetVisible(false);
            _driver.inputEnabled = false;
            _chase.enabled = false;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void StartGame()
        {
            _menus.ShowTitle(false);
            _hud.SetVisible(true);
            _chase.enabled = true;
            _chase.SnapBehind();
            _driver.inputEnabled = true;
            state = State.Roam;
            LockCursor(true);
            _hud.Toast("WELCOME TO THE BAY", UIKit.Neon, 3f);
        }

        static void LockCursor(bool v) { Cursor.lockState = v ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !v; }

        void Pause()
        {
            _beforePause = state;
            state = State.Paused;
            Time.timeScale = 0;
            _menus.ShowPause(true, _race != null || _duel != null);
            LockCursor(false);
        }

        void Resume()
        {
            if (state != State.Paused) return;
            state = _beforePause;
            Time.timeScale = 1;
            _menus.ShowPause(false, false);
            LockCursor(true);
        }

        void OpenEvent(CityLayout.RouteDef d)
        {
            _pendingEvent = d;
            state = State.EventCard;
            _driver.inputEnabled = false;
            _menus.ShowEvent(d, save);
            LockCursor(false);
        }

        void CloseEvent()
        {
            _menus.HideEvent();
            _pendingEvent = null;
            state = State.Roam;
            _driver.inputEnabled = true;
            LockCursor(true);
        }

        void StartRace()
        {
            var d = _pendingEvent;
            if (d == null || save.rep < d.repRequired) return;
            _menus.HideEvent();
            _pendingEvent = null;
            Combat.I.ClearAll();
            foreach (var m in _markers) m.t.gameObject.SetActive(false);
            _race = Race.Create(d, player, (n, l, s, p) => SpawnMech(n, l, s, p));
            _race.Beep += n => AudioFX.I?.UI(n == 0 ? "beephi" : "beep", 0.7f);
            _race.GatePassed += final => AudioFX.I?.UI("gate", 0.6f);
            _race.Finished += () => { _raceEndT = 2.2f; _hud.Toast(_race.result.position == 1 ? "FIRST PLACE!" : "FINISHED", new Color(0.3f, 1f, 0.4f), 2.5f); };
            foreach (var r in _race.racers)
                if (r.mech != player)
                {
                    var rm = r.mech;
                    rm.Disabled += from => { if (from == player) { _hud.Toast($"{rm.pilot} TAKEN DOWN", UIKit.Hot); } Combat.I.Explode(rm.Center, 0, 1, 0, null); };
                }
            player.Disabled += OnPlayerDown;
            _chase.SnapBehind();
            state = State.Race;
            _driver.inputEnabled = true;
            LockCursor(true);
        }

        void OnPlayerDown(MechController from) => _hud.Toast(from ? $"TAKEN DOWN BY {from.pilot}" : "WRECKED", new Color(1f, 0.3f, 0.25f));

        void EndRace(bool completed)
        {
            if (_race == null) return;
            if (completed)
            {
                var r = _race.result;
                save.credits += r.credits + r.bonus;
                save.rep += r.rep;
                var prev = save.Result(_race.def.id);
                if (prev == null) save.results.Add(new EventResult { id = _race.def.id, bestPos = r.position, bestTime = r.time });
                else { prev.bestPos = Mathf.Min(prev.bestPos, r.position); prev.bestTime = Mathf.Min(prev.bestTime, r.time); }
                save.Save();
            }
            var start = _race.def.Points()[0];
            foreach (var r in _race.racers) if (r.mech != player) _mechs.Remove(r.mech);
            player.Disabled -= OnPlayerDown;
            _race.Teardown();
            _race = null;
            Combat.I.ClearAll();
            BuildMarkers();
            player.Repair();
            player.Teleport(start + Vector3.up * 0.3f, player.yaw);
            state = State.Roam;
            _driver.inputEnabled = true;
        }

        void ContinueAfterResults()
        {
            _menus.HideResults();
            EndRace(true);
            LockCursor(true);
        }

        void OpenGarage()
        {
            state = State.Garage;
            _driver.inputEnabled = false;
            _chase.enabled = false;
            _hud.SetVisible(false);
            player.gameObject.SetActive(false);
            _garage.Open();
        }

        void OnGarageClosed()
        {
            player.gameObject.SetActive(true);
            ApplyLoadout();
            player.Teleport(RigShopFront + new Vector3(0, 0.3f, -6f), 180f);
            _chase.enabled = true;
            _chase.SnapBehind();
            _hud.SetVisible(true);
            _driver.inputEnabled = true;
            state = State.Roam;
            LockCursor(true);
        }

        void OnHit(MechController target, float dmg, MechController from)
        {
            if (from == player && target != player) { _hud.HitMarker(); AudioFX.I?.UI("hit", 0.5f); }
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (state == State.Title)
            {
                _titleT += Time.deltaTime;
                var cam = Bootstrap.MainCam.transform;
                Vector3 p = player.transform.position;
                float a = _titleT * 0.12f + 2.4f;
                cam.position = p + new Vector3(Mathf.Sin(a) * 17f, 4.5f + Mathf.Sin(_titleT * 0.3f) * 1.5f, Mathf.Cos(a) * 17f);
                cam.LookAt(p + Vector3.up * 5f + cam.right * -4f);
                if (!_autotest && PlayerDriver.ConfirmPressed) StartGame();
                return;
            }
            if (state == State.Paused) { if (PlayerDriver.PausePressed) Resume(); return; }
            if (state == State.EventCard)
            {
                if (Keyboard.current?.enterKey.wasPressedThisFrame == true || Gamepad.current?.buttonSouth.wasPressedThisFrame == true) StartRace();
                else if (PlayerDriver.BackPressed) CloseEvent();
                return;
            }
            if (state == State.Results)
            {
                if (Keyboard.current?.enterKey.wasPressedThisFrame == true || Gamepad.current?.buttonSouth.wasPressedThisFrame == true) ContinueAfterResults();
                return;
            }
            if (state == State.Garage) return;

            if (PlayerDriver.PausePressed && !_autotest) { Pause(); return; }
            if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !_autotest) LockCursor(true);

            string prompt = null;
            if (state == State.Roam)
            {
                foreach (var m in _markers)
                {
                    if (!m.t) continue;
                    float d = Vector2.Distance(new Vector2(m.pos.x, m.pos.z), new Vector2(player.transform.position.x, player.transform.position.z));
                    if (d < 13f)
                    {
                        prompt = "near";
                        if (PlayerDriver.InteractPressed)
                        {
                            if (m.def != null) { _pendingEvent = m.def; StartRace(); }
                            else if (m.duel != null) StartDuel(m.duel);
                            else OpenGarage();
                        }
                    }
                }
            }
            if (_duel != null && _duel.state == "done" && state == State.Duel)
            {
                _duelEndT -= Time.deltaTime;
                if (_duelEndT <= 0) EndDuel();
            }
            if (_race != null && _race.state == "done" && state == State.Race)
            {
                _raceEndT -= Time.deltaTime;
                if (_raceEndT <= 0) { state = State.Results; _menus.ShowResults(_race); LockCursor(false); }
            }
            if (player.isActiveAndEnabled && player.grounded && player.Speed > 40 && player.driftAngle > 25) Combat.I.SkidFX(player);
            AudioFX.I?.Engine(player.Speed, player.BoostLevel, player.overdriveActive, state == State.Roam || state == State.Race);
        }

        void LateUpdate()
        {
            if (state == State.Title || state == State.Garage || !_hud) return;
            var markers = state == State.Roam ? _markers.Select(m => new Hud.Marker { pos = m.pos, color = m.color }).ToList() : null;
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame && (state == State.Roam || state == State.Race)) _hud.ToggleWorldMap();
            var hs = new HudState
            {
                mode = _duel != null ? HudMode.Combat : _race != null ? HudMode.Race : HudMode.Explore,
                player = player, lockT = _driver.lockTarget, cam = Bootstrap.MainCam, race = _race, duel = _duel,
                credits = save.credits, rep = save.rep, markers = markers,
                hour = DayNight.I ? DayNight.I.hour : 12f, location = LocationName(),
            };
            Objective(hs);
            hs.interact = state == State.Roam ? NearbyInteraction() : null;
            _hud.Tick(hs);
        }

        string LocationName()
        {
            if (_duel != null) return _duel.def.venue;
            if (_race != null) return _race.def.name;
            Vector3 p = player.transform.position;
            foreach (var v in CityLayout.Venues)
                if (v.id != "hangar" && v.id != "downtown" && v.id != "bridge" && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(v.pos.x, v.pos.z)) < 260f) return v.name;
            if (Mathf.Abs(p.z - CityLayout.BridgeZ) < 30f && Mathf.Abs(p.x) < 900f && p.y > 6f) return "CENTRAL BRIDGE";
            if (CityLayout.CoastDist(p.x, p.z) < 0) return "THE BAY";
            return CityLayout.DistrictAt(p.x, p.z) switch
            {
                CityLayout.District.Hangar => "HANGAR DISTRICT",
                CityLayout.District.OldTown => "OLD TOWN",
                CityLayout.District.Downtown => "DOWNTOWN",
                CityLayout.District.Island => "CHAMPIONSHIP ISLAND",
                CityLayout.District.Airfield => "FLIGHT CIRCUIT",
                _ => "THE BAY",
            };
        }

        /// <summary>The next thing to do: race gates, the duel opponent, or the next unfinished event.</summary>
        void Objective(HudState hs)
        {
            if (_race != null)
            {
                hs.objectiveTitle = _race.state == "done" ? "FINISHED" : "NEXT CHECKPOINT";
                hs.objectiveText = _race.state == "done" ? "Collect your winnings" : "Pass through the next gate";
                int n = Mathf.Min(_race.player.next, _race.gates.Count - 1);
                hs.hasWaypoint = _race.state != "done";
                hs.waypoint = _race.gates[n];
                hs.waypointName = n == _race.gates.Count - 1 ? "FINISH" : "CHECKPOINT";
                return;
            }
            if (_duel != null)
            {
                hs.objectiveTitle = _duel.state == "done" ? (_duel.playerWon ? "BOUT WON" : "BOUT LOST") : "DESTROY THE OPPONENT";
                hs.objectiveText = "";
                hs.hasWaypoint = _duel.rival && _duel.state != "done";
                if (hs.hasWaypoint) { hs.waypoint = _duel.rival.transform.position; hs.waypointName = _duel.def.rival; }
                return;
            }
            // next unfinished event the player can enter, in career order
            var order = new List<(string id, string name, string venue, int rep, Vector3 pos, bool duel)>();
            foreach (var r in CityLayout.Routes) order.Add((r.id, r.name, r.type == "SPRINT" ? "the sprint start" : "the circuit grid", r.repRequired, r.Points()[0], false));
            foreach (var d in CityLayout.Duels) order.Add((d.id, d.name, d.venue.ToLowerInvariant(), d.repRequired, CityLayout.Ground(d.registration.x, d.registration.y), true));
            order.Sort((a, b) => a.rep.CompareTo(b.rep));
            foreach (var o in order)
            {
                if (save.rep < o.rep || save.Result(o.id)?.bestPos == 1) continue;
                hs.objectiveTitle = o.duel ? "NEXT BOUT" : "NEXT RACE";
                hs.objectiveText = o.duel ? $"Reach the {UpperFirst(o.venue)} registration gate" : $"{UpperFirst(o.name.ToLowerInvariant())}: reach {o.venue}";
                hs.hasWaypoint = true; hs.waypoint = o.pos; hs.waypointName = o.name;
                return;
            }
            hs.objectiveTitle = "FREE ROAM";
            hs.objectiveText = "Upgrade your frame at the workshop";
            hs.hasWaypoint = true; hs.waypoint = RigShopFront; hs.waypointName = "WORKSHOP";
        }

        static string UpperFirst(string s)
        {
            var words = s.Split(' ');
            for (int i = 0; i < words.Length; i++) if (words[i].Length > 0 && words[i] != "the") words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        Interaction NearbyInteraction()
        {
            foreach (var m in _markers)
            {
                if (!m.t) continue;
                float d = Vector2.Distance(new Vector2(m.pos.x, m.pos.z), new Vector2(player.transform.position.x, player.transform.position.z));
                if (d >= 13f) continue;
                if (m.def != null)
                {
                    var r = m.def;
                    return new Interaction
                    {
                        title = r.name,
                        line1 = $"{r.type}{(r.type == "CIRCUIT" ? $"  ·  {r.laps} LAPS" : "")}  ·  {r.rivals + 1} RACERS",
                        line2 = $"REWARD   <color=#FAB833><b>{r.payout[0]:N0} CR</b></color>",
                        action = "ENTER EVENT", sub = r.blurb, locked = save.rep < r.repRequired,
                    };
                }
                if (m.duel != null)
                {
                    var q = m.duel;
                    return new Interaction
                    {
                        title = q.name, line1 = q.subtitle,
                        line2 = $"REWARD   <color=#FAB833><b>{q.reward:N0} CR</b></color>",
                        action = "ENTER EVENT", sub = save.rep < q.repRequired ? $"Needs {q.repRequired} REP  ·  you have {save.rep}" : q.blurb, locked = save.rep < q.repRequired,
                    };
                }
                return new Interaction { title = "WORKSHOP TERMINAL", line1 = "RIG SHOP  ·  HANGAR 03", line2 = "", action = "INTERACT", sub = "Inspect loadout and upgrades" };
            }
            return null;
        }

        // ------------------------------------------------------------------ duels
        void StartDuel(CityLayout.DuelDef d)
        {
            if (save.rep < d.repRequired) { _hud.Toast($"NEEDS {d.repRequired} REP", UIKit.Rival); return; }
            Combat.I.ClearAll();
            foreach (var m in _markers) m.t.gameObject.SetActive(false);
            if (_hud.WorldMapOpen) _hud.ToggleWorldMap();
            _duel = Duel.Create(d, player, (n, l, s, p) => SpawnMech(n, l, s, p));
            _duel.Beep += n => AudioFX.I?.UI(n == 0 ? "beephi" : "beep", 0.7f);
            _duel.Finished += () =>
            {
                _duelEndT = 3.5f;
                _hud.Toast(_duel.playerWon ? $"{d.name} WON  ·  +{d.reward:N0} CR" : "BOUT LOST", _duel.playerWon ? UIKit.Gold : UIKit.Rival, 3.5f);
                _hud.Comms(_duel.playerWon ? "That's a seeding. On to the finals." : "Not today. Back to the workshop.");
                if (_duel.rival && _duel.rival.IsDown) Combat.I.Explode(_duel.rival.Center, 0, 1, 0, null);
            };
            _chase.SnapBehind();
            _hud.Comms("Systems green. Let's give them a show.");
            state = State.Duel;
            _driver.inputEnabled = true;
            LockCursor(true);
        }

        void EndDuel()
        {
            if (_duel == null) return;
            var d = _duel.def;
            if (_duel.playerWon)
            {
                save.credits += d.reward;
                save.rep += d.repGain;
                var prev = save.Result(d.id);
                if (prev == null) save.results.Add(new EventResult { id = d.id, bestPos = 1, bestTime = d.timeLimit - _duel.timeLeft });
                else prev.bestPos = 1;
                save.Save();
            }
            if (_duel.rival) _mechs.Remove(_duel.rival);
            _duel.Teardown();
            _duel = null;
            Combat.I.ClearAll();
            BuildMarkers();
            player.Repair();
            player.Teleport(CityLayout.Ground(d.registration.x, d.registration.y - 14f) + Vector3.up * 0.3f, 180f);
            _chase.SnapBehind();
            state = State.Roam;
            _driver.inputEnabled = true;
        }

        // ------------------------------------------------------------------ automated playtest
        IEnumerator AutoTest()
        {
            Application.runInBackground = true;
            yield return new WaitForSeconds(2.5f);
            yield return CaptureTour.Shot("00_title", 2);
            StartGame();
            _driver.enabled = false;
            yield return new WaitForSeconds(1.5f);
            yield return CaptureTour.Shot("01_roam", 2);
            // world tour: fixed cameras by day and by night
            _chase.enabled = false;
            DayNight.I.frozen = true;
            foreach (var (hour, pre) in new[] { (16.3f, "w_day_"), (22.5f, "w_night_") })
            {
                DayNight.I.SetHour(hour);
                foreach (var (n, pos, look) in Bootstrap.TourShots(pre))
                {
                    var ct = Bootstrap.MainCam.transform;
                    ct.position = pos; ct.LookAt(look);
                    yield return CaptureTour.Shot(n, 25);
                }
            }
            // the player's mech in the concept-art views
            _chase.enabled = true;
            foreach (var (n, hour, at, yaw) in new[]
            {
                ("h_bridge", 16.3f, new Vector2(-380, 52), 90f), ("h_hangar_day", 16.3f, new Vector2(-910, -1060), 0f),
                ("h_island", 16.3f, new Vector2(1150, -1020), 125f), ("h_downtown_street", 16.3f, new Vector2(1450, 120), 0f), ("h_oldtown", 16.3f, new Vector2(-1360, 420), 0f), ("h_hangar_night", 22.5f, new Vector2(-910, -1060), 0f),
            })
            {
                DayNight.I.SetHour(hour);
                player.Teleport(CityLayout.Ground(at.x, at.y) + Vector3.up * 0.3f, yaw);
                _chase.yaw = yaw; _chase.SnapBehind();
                yield return CaptureTour.Shot(n, 40);
            }
            DayNight.I.SetHour(16.3f);
            player.Teleport(PlayerSpawn, 90f);
            _chase.yaw = 90f; _chase.SnapBehind();
            yield return new WaitForSeconds(0.5f);
            // boost east down the street
            _chase.followMechYaw = true;
            for (float t = 0; t < 3.5f; t += Time.deltaTime)
            {
                player.cmd = new MechCommand { move = Vector3.right, face = 90, boost = true, aimPoint = player.Center + Vector3.right * 100 };
                yield return null;
            }
            yield return CaptureTour.Shot("02_boost", 1);
            for (float t = 0; t < 1.5f; t += Time.deltaTime) { player.cmd = new MechCommand { face = 90, jumpPressed = t < 0.1f, jumpHeld = true }; yield return null; }
            yield return CaptureTour.Shot("03_hover", 1);
            // garage
            save.credits += 20000; save.Save();
            OpenGarage();
            yield return new WaitForSeconds(1.5f);
            yield return CaptureTour.Shot("04_garage", 2);
            // beauty shots of the mech model
            var beauty = new (string, float, float, float, float, float)[]
            {
                ("m1_front34", 0.55f, 15f, 6.5f, 38f, 4.6f), ("m2_back34", 2.6f, 15f, 7f, 38f, 4.8f),
                ("m3_chest", 0.35f, 7.5f, 7.2f, 34f, 6.2f), ("m4_legs", -0.5f, 8f, 2.2f, 36f, 2.2f), ("m5_side", 1.57f, 14f, 5f, 38f, 4.5f),
            };
            foreach (var b in beauty)
            {
                _garage.PhotoMode = (b.Item2, b.Item3, b.Item4, b.Item5, b.Item6);
                yield return CaptureTour.Shot(b.Item1, 6);
            }
            _garage.PhotoMode = null;
            save.owned.Add("lg_glide"); save.loadout.legs = "lg_glide"; save.owned.Add("wp_gale"); save.loadout.weapon = "wp_gale";
            save.loadout.paint = new Color(0.32f, 0.08f, 0.1f); save.loadout.neon = new Color(1f, 0.2f, 0.6f);
            _garage.Close(); OpenGarage();
            yield return new WaitForSeconds(1.5f);
            yield return CaptureTour.Shot("05_garage_custom", 2);
            // alternate builds for model review
            var builds = new (string, Loadout)[]
            {
                ("racer", new Loadout { head = "hd_moth", core = "cr_wasp", arms = "ar_needle", legs = "lg_glide", booster = "bs_comet", weapon = "wp_lance", shoulder = "sh_spear", paint = new Color(0.12f, 0.13f, 0.16f), accent = new Color(0.95f, 0.15f, 0.45f), neon = new Color(1f, 0.2f, 0.6f) }),
                ("heavy", new Loadout { head = "hd_bastion", core = "cr_anvil", arms = "ar_hauler", legs = "lg_bulwark", booster = "bs_tern", weapon = "wp_maul", shoulder = "sh_mines", paint = new Color(0.33f, 0.36f, 0.27f), accent = new Color(0.85f, 0.7f, 0.2f), neon = new Color(1f, 0.6f, 0.1f) }),
                ("hopper", new Loadout { head = "hd_pike", core = "cr_wasp", arms = "ar_hand", legs = "lg_hopper", booster = "bs_flare", weapon = "wp_gale", shoulder = "sh_swarm", paint = new Color(0.62f, 0.64f, 0.68f), accent = new Color(0.1f, 0.5f, 0.95f), neon = new Color(0.2f, 0.8f, 1f) }),
            };
            var savedLoadout = save.loadout;
            foreach (var (bn, bl) in builds)
            {
                foreach (Slot sl in Parts.Slots) if (!save.owned.Contains(bl[sl])) save.owned.Add(bl[sl]);
                save.loadout = bl;
                _garage.Close(); OpenGarage();
                yield return new WaitForSeconds(0.8f);
                _garage.PhotoMode = (0.55f, 15f, 6.5f, 38f, 4.6f);
                yield return CaptureTour.Shot($"b_{bn}_front", 6);
                _garage.PhotoMode = (2.5f, 15f, 7f, 38f, 4.8f);
                yield return CaptureTour.Shot($"b_{bn}_back", 6);
                _garage.PhotoMode = null;
            }
            save.loadout = savedLoadout;
            _garage.Close();
            yield return new WaitForSeconds(0.5f);
            // race
            _pendingEvent = CityLayout.Routes[0];
            StartRace();
            _driver.enabled = false;
            var auto = player.gameObject.AddComponent<RacerAI>();
            auto.Init(player, _race, _race.player, 1.15f, 0f);
            yield return new WaitForSeconds(1.0f);
            yield return CaptureTour.Shot("06_grid", 1);
            yield return new WaitForSeconds(6f);
            yield return CaptureTour.Shot("07_race_start", 1);
            float tt = 0;
            int shot = 0;
            while (_race != null && _race.state != "done" && tt < 260f)
            {
                tt += Time.deltaTime;
                if (shot < 4 && tt > 6f + shot * 9f) { yield return CaptureTour.Shot($"08_race_{shot}", 1); shot++; }
                yield return null;
            }
            Debug.Log($"[AutoTest] race state={_race?.state} t={_race?.time:0.0} pos={_race?.result?.position}");
            yield return new WaitForSeconds(3f);
            yield return CaptureTour.Shot("09_results", 2);
            Destroy(auto);
            ContinueAfterResults();
            yield return new WaitForSeconds(0.5f);

            // HUD cards at the qualifier registration and the workshop
            foreach (var (n, at, yaw) in new[] { ("hud_enter_event", CityLayout.Duels[0].registration + new Vector2(0, -9), 0f), ("hud_workshop", CityLayout.RigShop + new Vector2(3, -12), 0f) })
            {
                player.Teleport(CityLayout.Ground(at.x, at.y) + Vector3.up * 0.3f, yaw);
                _chase.followMechYaw = false; _chase.yaw = yaw; _chase.SnapBehind();
                yield return CaptureTour.Shot(n, 40);
            }

            // arena duel, the player's frame flown by the duel AI
            StartDuel(CityLayout.Duels[0]);
            _driver.enabled = false;
            var pilot = player.gameObject.AddComponent<DuelAI>();
            var duel = _duel;
            pilot.Init(player, duel.rival, 1.25f, () => duel != null && duel.state == "fight");
            _chase.followMechYaw = true;
            yield return new WaitForSeconds(2f);
            yield return CaptureTour.Shot("10_duel_countdown", 1);
            float dt2 = 0; int dshot = 0;
            while (_duel != null && _duel.state != "done" && dt2 < 135f)
            {
                dt2 += Time.deltaTime;
                if (dshot < 3 && dt2 > 6f + dshot * 10f) { yield return CaptureTour.Shot($"11_duel_{dshot}", 1); dshot++; }
                yield return null;
            }
            Debug.Log($"[AutoTest] duel state={_duel?.state} won={_duel?.playerWon} me={_duel?.PlayerPct:0.00} rival={_duel?.RivalPct:0.00}");
            yield return new WaitForSeconds(1f);
            yield return CaptureTour.Shot("12_duel_result", 1);
            Destroy(pilot);
            while (_duel != null) yield return null;
            yield return new WaitForSeconds(0.5f);

            // the Grand Final floor, a short look
            save.rep = Mathf.Max(save.rep, 10);
            StartDuel(CityLayout.Duels[1]);
            var pilot2 = player.gameObject.AddComponent<DuelAI>();
            var duel2 = _duel;
            pilot2.Init(player, duel2.rival, 1.25f, () => duel2 != null && duel2.state == "fight");
            yield return new WaitForSeconds(9f);
            yield return CaptureTour.Shot("13_final_0", 1);
            yield return new WaitForSeconds(9f);
            yield return CaptureTour.Shot("13_final_1", 1);
            Debug.Log($"[AutoTest] final me={_duel?.PlayerPct:0.00} rival={_duel?.RivalPct:0.00}");
            Destroy(pilot2);
            Application.Quit();
        }
    }
}
