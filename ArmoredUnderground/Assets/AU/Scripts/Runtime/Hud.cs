using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static AU.UIKit;

namespace AU
{
    /// <summary>
    /// In-game HUD: AC-style armor/energy and weapon readouts, NFS-style speedometer,
    /// overdrive gauge, race position/lap/timer and standings, heading-up minimap,
    /// lock-on reticle, hit markers, style popups and the countdown.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public Canvas canvas;
        RectTransform _root;
        Text _apNum, _speed, _speedUnit, _wpName, _wpAmmo, _shName, _shState, _blade, _pos, _posTotal, _lap, _time, _wallet, _countdown, _toast, _prompt, _down, _style, _lockName, _odLabel;
        Image _apBar, _enBar, _enBack, _staggerSelf, _speedRing, _odBar, _hitMarker, _vignette, _lockBox, _lockAp, _lockStagger, _promptBg;
        RectTransform _board, _mapInner, _mapPlayer, _lockRt;
        readonly List<Text> _boardRows = new();
        readonly List<Image> _mapDots = new();
        readonly List<Image> _mapMarkers = new();
        float _toastT, _hitT, _dmgT, _styleT;
        public static string FormatTime(float t) { int m = (int)(t / 60); float s = t - m * 60; return $"{m}:{s:00.00}"; }

        public void Build()
        {
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = Stretch(go.transform, "Root");

            _vignette = Img(_root, "DamageVignette", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(1f, 0.1f, 0.08f, 0f), Vignette);
            var vr = _vignette.rectTransform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.sizeDelta = Vector2.zero;

            // crosshair
            var ch = Rect(_root, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            foreach (var (p, s) in new[] { (new Vector2(-16, 0), new Vector2(12, 2)), (new Vector2(16, 0), new Vector2(12, 2)), (new Vector2(0, 16), new Vector2(2, 12)), (new Vector2(0, -16), new Vector2(2, 12)) })
                Img(ch, "Tick", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p, s, Neon);
            Img(ch, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 3), Neon);
            _hitMarker = Img(_root, "HitMarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), new Color(1, 1, 1, 0), Diamond);

            // lock reticle
            _lockRt = Rect(_root, "Lock", new Vector2(0, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            _lockBox = Img(_lockRt, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90), Hot, Diamond);
            _lockName = Txt(_lockRt, "Name", new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(300, 24), 18, TextAnchor.LowerCenter, Hot);
            Img(_lockRt, "ApBack", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(110, 5), new Color(1, 1, 1, 0.15f));
            _lockAp = Img(_lockRt, "Ap", new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(-55, -10), new Vector2(110, 5), Hot);
            _lockStagger = Img(_lockRt, "Stagger", new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(-55, -18), new Vector2(110, 3), Amber);

            // bottom center: AP / EN
            var bars = Rect(_root, "Bars", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 96), new Vector2(620, 70));
            Txt(bars, "APLabel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(60, 20), 16, TextAnchor.UpperLeft, Dim).text = "AP";
            _apNum = Txt(bars, "APNum", new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 8), new Vector2(200, 40), 34, TextAnchor.UpperRight, Ink);
            Img(bars, "APBack", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -26), new Vector2(620, 9), new Color(1, 1, 1, 0.12f));
            _apBar = Img(bars, "APBar", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -26), new Vector2(620, 9), Ink);
            Txt(bars, "ENLabel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -40), new Vector2(60, 20), 16, TextAnchor.UpperLeft, Dim).text = "EN";
            _enBack = Img(bars, "ENBack", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -44), new Vector2(590, 7), new Color(1, 1, 1, 0.12f));
            _enBar = Img(bars, "ENBar", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -44), new Vector2(590, 7), Neon);
            _staggerSelf = Img(bars, "Stagger", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(0, 3), Amber);

            // weapons
            var arms = Rect(_root, "Arms", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(760, 60));
            Txt(arms, "R", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(260, 18), 13, TextAnchor.UpperLeft, Dim).text = "R-ARM  ·  LMB / RT";
            _wpName = Txt(arms, "WpName", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -18), new Vector2(260, 22), 19, TextAnchor.UpperLeft, Ink);
            _wpAmmo = Txt(arms, "WpAmmo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -40), new Vector2(260, 22), 19, TextAnchor.UpperLeft, Neon);
            Txt(arms, "B", new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(200, 18), 13, TextAnchor.UpperCenter, Dim).text = "BLADE  ·  F / X";
            _blade = Txt(arms, "Blade", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(200, 22), 19, TextAnchor.UpperCenter, Neon);
            Txt(arms, "S", new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(260, 18), 13, TextAnchor.UpperRight, Dim).text = "SHOULDER  ·  RMB / LT";
            _shName = Txt(arms, "ShName", new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -18), new Vector2(260, 22), 19, TextAnchor.UpperRight, Ink);
            _shState = Txt(arms, "ShState", new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(260, 22), 19, TextAnchor.UpperRight, Neon);

            // speedometer
            var sp = Rect(_root, "Speedo", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(260, 300));
            Img(sp, "Disc", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(240, 240), new Color(0.01f, 0.02f, 0.04f, 0.7f), Circle);
            var ringBack = Img(sp, "RingBack", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(240, 240), new Color(1, 1, 1, 0.1f), Ring);
            ringBack.type = Image.Type.Filled; ringBack.fillMethod = Image.FillMethod.Radial360; ringBack.fillOrigin = (int)Image.Origin360.Bottom; ringBack.fillAmount = 0.75f; ringBack.fillClockwise = true;
            ringBack.rectTransform.localEulerAngles = new Vector3(0, 0, -45);
            _speedRing = Img(sp, "Ring", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(240, 240), Neon, Ring);
            _speedRing.type = Image.Type.Filled; _speedRing.fillMethod = Image.FillMethod.Radial360; _speedRing.fillOrigin = (int)Image.Origin360.Bottom; _speedRing.fillClockwise = true;
            _speedRing.rectTransform.localEulerAngles = new Vector3(0, 0, -45);
            _speed = Txt(sp, "Speed", new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -112), new Vector2(240, 90), 78, TextAnchor.MiddleCenter, Ink, FontStyle.BoldAndItalic);
            Glow(_speed, Neon, 2);
            _speedUnit = Txt(sp, "Unit", new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -165), new Vector2(240, 30), 18, TextAnchor.MiddleCenter, Dim);
            _speedUnit.text = "KM/H";
            _odLabel = Txt(sp, "ODLabel", new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 30), new Vector2(240, 20), 14, TextAnchor.LowerLeft, Dim);
            _odLabel.text = "OVERDRIVE  ·  X / LB";
            Img(sp, "ODBack", new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 10), new Vector2(240, 14), new Color(1, 1, 1, 0.1f));
            _odBar = Img(sp, "ODBar", new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 10), new Vector2(240, 14), Hot);

            // race panel
            var rp = Rect(_root, "Race", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -26), new Vector2(400, 400));
            _pos = Txt(rp, "Pos", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(140, 100), 96, TextAnchor.UpperLeft, Ink, FontStyle.BoldAndItalic);
            Glow(_pos, Neon, 2.5f);
            _posTotal = Txt(rp, "PosTotal", new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -48), new Vector2(100, 50), 34, TextAnchor.UpperLeft, Dim, FontStyle.BoldAndItalic);
            _lap = Txt(rp, "Lap", new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -108), new Vector2(400, 28), 22, TextAnchor.UpperLeft, Neon);
            _board = Rect(rp, "Board", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -146), new Vector2(240, 200));
            for (int i = 0; i < 6; i++)
            {
                var bg = Img(_board, "Row", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 30), new Vector2(230, 27), new Color(0, 0, 0, 0.45f));
                var t = Txt(bg.transform, "T", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(220, 27), 18, TextAnchor.MiddleLeft, Ink);
                _boardRows.Add(t);
            }
            _time = Txt(_root, "Time", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -70), new Vector2(300, 50), 40, TextAnchor.UpperRight, Ink, FontStyle.BoldAndItalic);
            _wallet = Txt(_root, "Wallet", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -24), new Vector2(500, 30), 20, TextAnchor.UpperRight, Dim);
            _wallet.supportRichText = true;

            // minimap
            var map = Img(_root, "Map", new Vector2(0, 0), new Vector2(0, 0), new Vector2(36, 36), new Vector2(270, 270), new Color(0.01f, 0.02f, 0.05f, 0.82f), Circle);
            map.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _mapInner = Rect(map.transform, "Inner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            const float S = MapScale;
            for (int i = 0; i < CityLayout.AvenueX.Length; i++)
            {
                float x = CityLayout.AvenueX[i];
                Img(_mapInner, "Ave", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x * S, (CityLayout.MinZ + CityLayout.MaxZ) * 0.5f * S), new Vector2(Mathf.Max(3, CityLayout.AvenueW[i] * S), (CityLayout.MaxZ - CityLayout.MinZ + 40) * S), new Color(0.5f, 0.58f, 0.7f, 0.45f));
            }
            for (int j = 0; j < CityLayout.StreetZ.Length; j++)
            {
                float z = CityLayout.StreetZ[j];
                Img(_mapInner, "St", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((CityLayout.MinX + CityLayout.MaxX) * 0.5f * S, z * S), new Vector2((CityLayout.MaxX - CityLayout.MinX + 40) * S, Mathf.Max(3, CityLayout.StreetW[j] * S)), new Color(0.5f, 0.58f, 0.7f, 0.45f));
            }
            for (int i = 0; i < 8; i++) { var d = Img(_mapInner, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11, 11), Color.red, Circle); d.enabled = false; _mapDots.Add(d); }
            for (int i = 0; i < 8; i++) { var d = Img(_mapInner, "Marker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16), Amber, Circle); d.enabled = false; _mapMarkers.Add(d); }
            var arrow = Txt(map.transform, "Me", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30), 26, TextAnchor.MiddleCenter, Neon);
            arrow.text = "▲";
            _mapPlayer = arrow.rectTransform;
            var ringMap = Img(_root, "MapRing", new Vector2(0, 0), new Vector2(0, 0), new Vector2(36, 36), new Vector2(270, 270), new Color(0, 0.9f, 1f, 0.5f), Ring);

            // center texts
            _countdown = Txt(_root, "Countdown", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(600, 220), 190, TextAnchor.MiddleCenter, Ink, FontStyle.BoldAndItalic);
            Glow(_countdown, Neon, 4);
            _toast = Txt(_root, "Toast", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(1200, 60), 44, TextAnchor.MiddleCenter, Neon, FontStyle.BoldAndItalic);
            Glow(_toast, Neon, 2);
            _style = Txt(_root, "Style", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260, 60), new Vector2(500, 40), 28, TextAnchor.MiddleLeft, Hot, FontStyle.BoldAndItalic);
            _promptBg = Img(_root, "PromptBg", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 230), new Vector2(560, 50), Panel);
            _prompt = Txt(_promptBg.transform, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 50), 24, TextAnchor.MiddleCenter, Amber);
            _down = Txt(_root, "Down", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(900, 80), 52, TextAnchor.MiddleCenter, new Color(1f, 0.25f, 0.2f), FontStyle.BoldAndItalic);
        }

        public const float MapScale = 0.35f;

        public void Toast(string text, Color c, float dur = 2.2f) { _toast.text = text; _toast.color = c; _toastT = dur; }
        public void Style(string text) { _style.text = text; _styleT = 1.4f; }
        public void HitMarker() => _hitT = 0.15f;
        public void Hurt(float k = 0.5f) => _dmgT = Mathf.Max(_dmgT, k);

        public struct Marker { public Vector3 pos; public Color color; }

        public void Tick(MechController p, Camera cam, float camYaw, MechController lockT, Race race, int credits, int rep, string prompt, List<Marker> markers)
        {
            float dt = Time.unscaledDeltaTime;
            var st = p.stats;
            float apf = p.ap / st.ap;
            _apNum.text = Mathf.CeilToInt(p.ap).ToString();
            _apBar.rectTransform.sizeDelta = new Vector2(620 * apf, 9);
            _apBar.color = apf < 0.3f ? new Color(1f, 0.25f, 0.2f) : Ink;
            _enBar.rectTransform.sizeDelta = new Vector2(590 * p.en / st.enCap, 7);
            _enBar.color = p.overheated ? new Color(1f, 0.25f, 0.2f) : Neon;
            _staggerSelf.rectTransform.sizeDelta = new Vector2(620 * p.stagger, 3);

            int kmh = Mathf.RoundToInt(p.Speed * 3.6f);
            _speed.text = kmh.ToString();
            _speedRing.fillAmount = Mathf.Clamp01(p.Speed / st.overdriveSpeed) * 0.75f;
            _speedRing.color = p.overdriveActive ? Hot : p.boosting ? Color.Lerp(Neon, Color.white, 0.3f) : Neon;
            _odBar.rectTransform.sizeDelta = new Vector2(240 * p.overdrive, 14);
            _odBar.color = p.overdriveActive ? Color.Lerp(Hot, Color.white, Mathf.PingPong(Time.time * 4, 1) * 0.5f) : p.overdrive >= 0.2f ? Hot : new Color(0.5f, 0.2f, 0.35f);

            var w = st.weapon;
            _wpName.text = w.name;
            _wpAmmo.text = p.reloadTimer > 0 ? "RELOADING" : $"{p.ammo} / {w.mag}";
            _shName.text = st.shoulder.name;
            _shState.text = p.shoulderCd > 0 ? $"{p.shoulderCd:0.0}s" : (st.shoulder.shoulder == ShoulderKind.Mines || lockT ? "READY" : "NO LOCK");
            _blade.text = p.bladeCd > 0 ? $"{p.bladeCd:0.0}s" : "READY";

            // race
            bool racing = race != null;
            _pos.enabled = _posTotal.enabled = _lap.enabled = _time.enabled = racing;
            _board.gameObject.SetActive(racing);
            if (racing)
            {
                var stand = race.Standings();
                int pos = stand.IndexOf(race.player) + 1;
                _pos.text = pos.ToString();
                _posTotal.text = "/" + stand.Count;
                _lap.text = race.def.type == "CIRCUIT" ? $"LAP {race.Lap}/{race.def.laps}   ·   {race.def.name}" : $"GATE {Mathf.Min(race.player.next + 1, race.gates.Count)}/{race.gates.Count}   ·   {race.def.name}";
                _time.text = FormatTime(race.time);
                for (int i = 0; i < _boardRows.Count; i++)
                {
                    var row = _boardRows[i];
                    bool on = i < stand.Count;
                    row.transform.parent.gameObject.SetActive(on);
                    if (!on) continue;
                    var r = stand[i];
                    row.text = $"{i + 1}   {(r == race.player ? "YOU" : r.mech.pilot)}{(r.mech.IsDown ? "   DOWN" : "")}";
                    row.color = r == race.player ? Neon : r.mech.IsDown ? Dim : Ink;
                    ((Image)row.transform.parent.GetComponent<Graphic>()).color = r == race.player ? new Color(0, 0.9f, 1f, 0.22f) : new Color(0, 0, 0, 0.45f);
                }
                if (race.state == "countdown")
                {
                    int n = Mathf.CeilToInt(race.countdown);
                    _countdown.text = n <= 3 ? n.ToString() : "";
                }
                else _countdown.text = race.time < 1f ? "GO!" : "";
            }
            else _countdown.text = "";
            _wallet.text = $"CR <color=#FFB31A>{credits:N0}</color>    REP <color=#FFB31A>{rep}</color>";

            // center
            if (_toastT > 0) { _toastT -= dt; _toast.canvasRenderer.SetAlpha(Mathf.Clamp01(_toastT * 3)); } else _toast.text = "";
            if (_styleT > 0) { _styleT -= dt; _style.canvasRenderer.SetAlpha(Mathf.Clamp01(_styleT * 2)); } else _style.text = "";
            _promptBg.gameObject.SetActive(!string.IsNullOrEmpty(prompt));
            _prompt.text = prompt;
            _down.text = p.IsDown ? "SYSTEMS DOWN — REBOOTING" : p.IsStaggered ? "STAGGERED" : "";
            _hitT -= dt; _hitMarker.color = new Color(1, 1, 1, _hitT > 0 ? 1 : 0);
            _dmgT = Mathf.Max(0, _dmgT - dt);
            _vignette.color = new Color(1f, 0.08f, 0.06f, 0f);

            // lock
            if (lockT && cam)
            {
                Vector3 sp = cam.WorldToScreenPoint(lockT.Center);
                if (sp.z > 0)
                {
                    _lockRt.gameObject.SetActive(true);
                    var scaler = canvas.transform as RectTransform;
                    float k = scaler.rect.width / Screen.width;
                    _lockRt.anchoredPosition = new Vector2(sp.x * k, sp.y * k);
                    _lockBox.rectTransform.localEulerAngles = new Vector3(0, 0, Time.time * 90f);
                    _lockName.text = $"{lockT.pilot}   {Vector3.Distance(lockT.Center, p.Center):0}m";
                    _lockAp.rectTransform.sizeDelta = new Vector2(110 * lockT.ap / lockT.stats.ap, 5);
                    _lockStagger.rectTransform.sizeDelta = new Vector2(110 * (lockT.IsStaggered ? 1 : lockT.stagger), 3);
                }
                else _lockRt.gameObject.SetActive(false);
            }
            else _lockRt.gameObject.SetActive(false);

            // minimap: heading-up
            _mapInner.localEulerAngles = new Vector3(0, 0, camYaw);
            _mapInner.anchoredPosition = Vector2.zero;
            Vector2 me = new Vector2(p.transform.position.x, p.transform.position.z) * MapScale;
            _mapInner.pivot = new Vector2(0.5f, 0.5f);
            // move the inner so the player sits at center, then rotate around center
            Quaternion rq = Quaternion.Euler(0, 0, camYaw);
            Vector3 off = rq * new Vector3(-me.x, -me.y, 0);
            _mapInner.anchoredPosition = off;
            _mapPlayer.localEulerAngles = new Vector3(0, 0, -(p.yaw - camYaw));
            int di = 0;
            if (racing)
            {
                foreach (var r in race.racers)
                {
                    if (r == race.player || di >= _mapDots.Count) continue;
                    var d = _mapDots[di++];
                    d.enabled = true;
                    d.rectTransform.anchoredPosition = new Vector2(r.mech.transform.position.x, r.mech.transform.position.z) * MapScale;
                    d.color = r.mech.IsDown ? Dim : r.mech.isBoss ? Hot : new Color(1f, 0.3f, 0.25f);
                }
            }
            for (; di < _mapDots.Count; di++) _mapDots[di].enabled = false;
            int mi = 0;
            if (markers != null)
                foreach (var m in markers)
                {
                    if (mi >= _mapMarkers.Count) break;
                    var d = _mapMarkers[mi++];
                    d.enabled = true; d.color = m.color;
                    d.rectTransform.anchoredPosition = new Vector2(m.pos.x, m.pos.z) * MapScale;
                }
            for (; mi < _mapMarkers.Count; mi++) _mapMarkers[mi].enabled = false;
        }

        public void SetVisible(bool v) => canvas.enabled = v;
    }
}
