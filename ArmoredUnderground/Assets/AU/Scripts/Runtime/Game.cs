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
        public enum State { Title, Roam, EventCard, Race, Results, Garage, Paused }
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
        CityLayout.RouteDef _pendingEvent;
        readonly List<MechController> _mechs = new();
        readonly List<(Transform t, Vector3 pos, CityLayout.RouteDef def, Color color)> _markers = new();
        float _titleT, _raceEndT;
        bool _autotest;
        public static readonly Vector3 PlayerSpawn = new(-82, 0.3f, -34);

        void Start()
        {
            if (CaptureTour.Requested(out _)) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            gameObject.AddComponent<Combat>();
            gameObject.AddComponent<AudioFX>();
            Combat.I.Mechs = () => _mechs.Where(m => m);
            Combat.I.Hit += OnHit;
            Combat.I.Shake += (p, s) => { if (_chase && player) _chase.AddTrauma(s * Mathf.Clamp01(1.2f - Vector3.Distance(p, player.Center) / 120f)); };

            save = SaveData.Load();
            player = SpawnMech("YOU", save.loadout, 1f, true);
            player.Teleport(PlayerSpawn, 90f);
            player.Damaged += (d, from) => { _chase?.Damage(Mathf.Clamp01(0.25f + d / 500f)); if (from) AudioFX.I?.Play("hit", player.Center, 0.5f, 0.6f); };
            player.StyleEvent += (n, amt) => _hud?.Style($"{n}  +OVERDRIVE");
            player.Recovered += () => { if (_race == null) { player.Repair(); } };

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
            _garage.LoadoutChanged += () => { player.Setup(save.loadout); player.rig.ApplyPaint(save.loadout); MechVariants.Apply(player.transform, save.loadout); };

            _menus.OnStart = StartGame;
            _menus.OnResume = Resume;
            _menus.OnRetire = () => { Resume(); EndRace(false); };
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
            rig.ApplyPaint(l);
            MechVariants.Apply(go.transform, l);
            _mechs.Add(mc);
            return mc;
        }

        void ApplyLoadout()
        {
            player.Setup(save.loadout);
            player.rig.ApplyPaint(save.loadout);
            MechVariants.Apply(player.transform, save.loadout);
        }

        // ------------------------------------------------------------------ markers
        void BuildMarkers()
        {
            foreach (var m in _markers) if (m.t) Destroy(m.t.gameObject);
            _markers.Clear();
            AddMarker(new Vector3(CityLayout.GarageDoor.x, 0, -30f), null, UIKit.Amber);
            foreach (var d in CityLayout.Routes)
            {
                bool locked = save.rep < d.repRequired;
                bool won = save.Result(d.id)?.bestPos == 1;
                AddMarker(d.Points()[0], d, locked ? new Color(1f, 0.25f, 0.2f) : won ? new Color(0.3f, 1f, 0.4f) : UIKit.Neon);
            }
        }

        void AddMarker(Vector3 pos, CityLayout.RouteDef def, Color color)
        {
            var root = new GameObject(def == null ? "GarageMarker" : "EventMarker_" + def.id).transform;
            root.position = pos;
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pillar.GetComponent<Collider>());
            pillar.transform.SetParent(root, false);
            pillar.transform.localPosition = Vector3.up * 70f;
            pillar.transform.localScale = new Vector3(3.5f, 70f, 3.5f);
            var pr = pillar.GetComponent<Renderer>();
            pr.sharedMaterial = def == null ? GameAssets.I.markerGlow : GameAssets.I.gateBeam;
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
            _markers.Add((root, pos, def, color));
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
            _hud.Toast("WELCOME TO KESSEL WARD", UIKit.Neon, 3f);
        }

        static void LockCursor(bool v) { Cursor.lockState = v ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !v; }

        void Pause()
        {
            _beforePause = state;
            state = State.Paused;
            Time.timeScale = 0;
            _menus.ShowPause(true, _race != null);
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
            player.Teleport(new Vector3(CityLayout.GarageDoor.x, 0.3f, -32f), 180f);
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
                        prompt = m.def == null ? "[E]  ENTER RIG SHOP" : $"[E]  {m.def.type}  ·  {m.def.name}";
                        if (PlayerDriver.InteractPressed) { if (m.def == null) OpenGarage(); else OpenEvent(m.def); }
                    }
                }
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
            _hud.Tick(player, Bootstrap.MainCam, _chase.yaw, _driver.lockTarget, _race, save.credits, save.rep, PromptText(), markers);
        }

        string PromptText()
        {
            if (state != State.Roam) return null;
            foreach (var m in _markers)
            {
                if (!m.t) continue;
                float d = Vector2.Distance(new Vector2(m.pos.x, m.pos.z), new Vector2(player.transform.position.x, player.transform.position.z));
                if (d < 13f) return m.def == null ? "[E]  ENTER RIG SHOP" : $"[E]  {m.def.type}  ·  {m.def.name}";
            }
            return null;
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
            save.owned.Add("lg_glide"); save.loadout.legs = "lg_glide"; save.owned.Add("wp_gale"); save.loadout.weapon = "wp_gale";
            save.loadout.paint = new Color(0.32f, 0.08f, 0.1f); save.loadout.neon = new Color(1f, 0.2f, 0.6f);
            _garage.Close(); OpenGarage();
            yield return new WaitForSeconds(1.5f);
            yield return CaptureTour.Shot("05_garage_custom", 2);
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
            Application.Quit();
        }
    }
}
