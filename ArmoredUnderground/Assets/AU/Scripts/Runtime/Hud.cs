using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static AU.UIKit;

namespace AU
{
    public enum HudMode { Explore, Race, Combat, Flight }

    /// <summary>What the HUD should show this frame; filled in by Game.</summary>
    public class HudState
    {
        public HudMode mode;
        public MechController player, lockT;
        public Camera cam;
        public Race race;
        public Duel duel;
        public int credits, rep;
        public string location, objectiveTitle, objectiveText;
        public bool hasWaypoint;
        public Vector3 waypoint;
        public string waypointName;
        public Interaction interact;
        public List<Hud.Marker> markers;
        public float hour;
    }

    public class Interaction
    {
        public string title, line1, line2, action, sub;
        public bool locked;
    }

    /// <summary>
    /// League-broadcast HUD after the Iron Ascendant mockups: location and clock,
    /// objective, credits, a north-up minimap with a GPS route, a status card for the
    /// frame, interaction cards, and mode-specific layers for races (position, lap,
    /// timer, checkpoint) and arena bouts (versus bar, timer, pilot comms, weapons).
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public Canvas canvas;
        RectTransform _root;
        public struct Marker { public Vector3 pos; public Color color; }
        public static string FormatTime(float t) { int m = (int)(t / 60); float s = t - m * 60; return $"{m:00}:{s:00.00}"; }

        // shared
        Text _loc, _clock, _objTitle, _objText, _credits, _rep, _cardName, _cardStatus, _cardSpeed, _cardSpeedUnit, _cardAlt, _waypointLabel, _toast, _countdown, _down, _style;
        Image _objIcon, _cardIntBar, _cardOdBar, _hitMarker, _vignette;
        Image[] _boost = new Image[4];
        RectTransform _card, _cardIntRow, _cardOdRow, _cardSpeedRow, _cardAltRow, _cardWeapons, _objPanel, _creditsPanel;
        Text _wpRow, _wpAmmo, _shRow, _shState;
        Image _shBar;
        // minimap
        RectTransform _mapInner, _mapPlayer;
        readonly List<Image> _route = new(), _mapMarkers = new(), _mapRivals = new();
        Image _mapWaypoint;
        List<Vector2> _routePts;
        float _routeT;
        Vector3 _routeTarget;
        const float S = 0.55f;
        // world markers
        RectTransform _wp, _chev;
        Text _wpDist, _chevText;
        // interaction card
        RectTransform _int;
        Text _intTitle, _intL1, _intL2, _intAction, _intSub;
        Image _intKey;
        // race layer
        RectTransform _racePanel, _timerPanel;
        Text _posBig, _posTotal, _lapBig, _lapTotal, _lapLabel, _timer, _bestLap;
        RectTransform _board;
        readonly List<Text> _boardRows = new();
        // combat layer
        RectTransform _vsPanel, _badge, _comms;
        Text _vsName, _vsPct, _vsRival, _vsRivalPct, _vsTime, _commsText, _badgeText;
        Image _vsBar, _vsRivalBar;
        readonly Image[] _wave = new Image[14];
        readonly Image[] _lockCorners = new Image[4];
        Text _lockName;
        // state
        float _toastT, _hitT, _dmgT, _styleT, _commsT;
        HudMode _mode = (HudMode)(-1);
        float _lapStart, _bestLapTime = -1;
        int _lastLap;
        Race _lapRace;
        float _lastPct = 1f;

        RectTransform _bigMap, _bigPlayer;
        readonly List<Image> _bigMarkers = new();
        public bool WorldMapOpen => _bigMap && _bigMap.gameObject.activeSelf;

        // ------------------------------------------------------------------ build
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
            var ch = Rect(_root, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            foreach (var (p, s) in new[] { (new Vector2(-14, 0), new Vector2(10, 2)), (new Vector2(14, 0), new Vector2(10, 2)), (new Vector2(0, 14), new Vector2(2, 10)), (new Vector2(0, -14), new Vector2(2, 10)) })
                Img(ch, "Tick", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p, s, new Color(Teal.r, Teal.g, Teal.b, 0.85f));
            _hitMarker = Img(_root, "HitMarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), new Color(1, 1, 1, 0), Diamond);

            // lock brackets
            for (int i = 0; i < 4; i++)
            {
                var c = Rect(_root, "LockCorner", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
                // a top-left corner; the others are mirrored copies (see Tick)
                var h = Img(c, "H", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 3), Rival);
                Img(c, "V", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-11.5f, -11.5f), new Vector2(3, 26), Rival);
                _lockCorners[i] = h;
                c.gameObject.SetActive(false);
            }
            _lockName = Txt(_root, "LockName", Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(300, 22), 17, TextAnchor.LowerCenter, Rival);

            BuildTopLeft();
            BuildTopRight();
            BuildMinimap();
            BuildCard();
            BuildWorldMarkers();
            BuildInteraction();
            BuildRaceLayer();
            BuildCombatLayer();

            _countdown = Txt(_root, "Countdown", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(600, 220), 180, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            Glow(_countdown, Teal, 3);
            _toast = Txt(_root, "Toast", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(1200, 50), 36, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            Glow(_toast, Color.black, 2);
            _style = Txt(_root, "Style", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260, 60), new Vector2(500, 40), 24, TextAnchor.MiddleLeft, Gold, FontStyle.Bold);
            _down = Txt(_root, "Down", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(900, 80), 44, TextAnchor.MiddleCenter, Rival, FontStyle.Bold);
            Txt(_root, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(900, 20), 13, TextAnchor.LowerCenter, new Color(1, 1, 1, 0.35f), FontStyle.Normal).text = "M  MAP     N  SKIP TO DUSK / DAWN     E  INTERACT";
        }

        void BuildTopLeft()
        {
            var lp = Panel2(_root, "Location", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -30), new Vector2(380, 64));
            _loc = Txt(lp.transform, "Name", new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -8), new Vector2(360, 30), 25, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            _clock = Txt(lp.transform, "Clock", new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -38), new Vector2(360, 20), 16, TextAnchor.UpperLeft, Dim, FontStyle.Normal);

            var op = Panel2(_root, "Objective", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -106), new Vector2(440, 74), new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            _objPanel = op.rectTransform;
            Img(op.transform, "Edge", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(3, 58), Gold);
            _objIcon = Img(op.transform, "Icon", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 0), new Vector2(30, 30), Gold, Diamond);
            Img(_objIcon.transform, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7), Gold, Diamond);
            _objTitle = Txt(op.transform, "Title", new Vector2(0, 1), new Vector2(0, 1), new Vector2(62, -12), new Vector2(370, 22), 16, TextAnchor.UpperLeft, Gold, FontStyle.Bold);
            _objText = Txt(op.transform, "Text", new Vector2(0, 1), new Vector2(0, 1), new Vector2(62, -36), new Vector2(370, 26), 19, TextAnchor.UpperLeft, Ink, FontStyle.Normal);
        }

        void BuildTopRight()
        {
            var cp = Panel2(_root, "Credits", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -30), new Vector2(250, 58));
            _creditsPanel = cp.rectTransform;
            var coin = Img(cp.transform, "Coin", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38, 0), new Vector2(32, 32), Gold, Ring);
            Txt(coin.transform, "C", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32, 32), 18, TextAnchor.MiddleCenter, Gold, FontStyle.Bold).text = "C";
            _credits = Txt(cp.transform, "CR", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-22, 2), new Vector2(190, 30), 23, TextAnchor.MiddleRight, Ink, FontStyle.Bold);
            _rep = Txt(_root, "Rep", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -92), new Vector2(250, 20), 14, TextAnchor.UpperRight, Dim, FontStyle.Normal);
        }

        void BuildMinimap()
        {
            var holder = Rect(_root, "Minimap", new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 74), new Vector2(250, 250));
            var map = Img(holder, "Disc", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250, 250), new Color(0.03f, 0.05f, 0.08f, 0.88f), Circle);
            map.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _mapInner = Rect(map.transform, "Inner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var mapTex = GameAssets.I.worldMap;
            if (mapTex)
            {
                var ri = new GameObject("WorldMapTex", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                ri.transform.SetParent(_mapInner, false);
                ri.texture = mapTex;
                ri.rectTransform.sizeDelta = Vector2.one * CityLayout.Half * 2 * S;
                ri.color = new Color(0.8f, 0.85f, 0.95f, 0.95f);
            }
            for (int i = 0; i < 90; i++) { var r = Img(_mapInner, "Route", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4), Teal); r.enabled = false; _route.Add(r); }
            for (int i = 0; i < 10; i++) { var d = Img(_mapInner, "Marker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20), Gold, Diamond); d.enabled = false; _mapMarkers.Add(d); }
            for (int i = 0; i < 8; i++) { var d = Img(_mapInner, "Rival", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16), Rival, Diamond); d.enabled = false; _mapRivals.Add(d); }
            _mapWaypoint = Img(_mapInner, "Waypoint", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22), Teal, Diamond);
            var arrow = Img(map.transform, "Me", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 26), Color.white, Arrow);
            _mapPlayer = arrow.rectTransform;
            Img(holder, "Rim", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(254, 254), new Color(1, 1, 1, 0.3f), Ring);
            Txt(holder, "N", new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, -4), new Vector2(30, 22), 16, TextAnchor.LowerCenter, Ink, FontStyle.Bold).text = "N";
            var lbl = Rect(_root, "WaypointLabel", new Vector2(0, 0), new Vector2(0, 0), new Vector2(44, 26), new Vector2(500, 30));
            var di = Img(lbl, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(20, 20), Teal, Diamond);
            Img(di.transform, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 5), Teal, Diamond);
            _waypointLabel = Txt(lbl, "Text", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(460, 30), 18, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
        }

        void BuildCard()
        {
            var cp = Panel2(_root, "Card", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 40), new Vector2(380, 130));
            _card = cp.rectTransform;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            icon.transform.SetParent(_card, false);
            var irt = icon.rectTransform; irt.anchorMin = irt.anchorMax = new Vector2(0, 1); irt.pivot = new Vector2(0, 1);
            irt.anchoredPosition = new Vector2(16, -12); irt.sizeDelta = new Vector2(78, 104);
            icon.texture = MechIcon; icon.color = new Color(0.85f, 0.95f, 1f, 0.85f); icon.raycastTarget = false;
            _cardName = Txt(_card, "Name", new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -12), new Vector2(250, 30), 24, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            Img(_card, "Rule", new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -46), new Vector2(250, 1), new Color(1, 1, 1, 0.2f));
            // rows are stacked by Layout() depending on the mode
            _cardSpeedRow = Rect(_card, "SpeedRow", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(260, 50));
            _cardSpeed = Txt(_cardSpeedRow, "Speed", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(150, 50), 44, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            _cardSpeedUnit = Txt(_cardSpeedRow, "Unit", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, -6), new Vector2(80, 30), 19, TextAnchor.MiddleLeft, Ink, FontStyle.Normal);
            _cardSpeedUnit.text = "km/h";
            _cardIntRow = Row("INTEGRITY", out _cardIntBar, Teal);
            var boostRow = Rect(_card, "BoostRow", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(260, 24));
            Txt(boostRow, "L", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(90, 20), 15, TextAnchor.MiddleLeft, Ink, FontStyle.Bold).text = "BOOST";
            for (int i = 0; i < 4; i++) _boost[i] = Img(boostRow, "Seg", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(76 + i * 37, 0), new Vector2(34, 12), Teal);
            _boostRow = boostRow;
            _cardOdRow = Row("OVERDRIVE", out _cardOdBar, Gold);
            _cardAltRow = Rect(_card, "AltRow", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(260, 26));
            Txt(_cardAltRow, "L", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 24), 16, TextAnchor.MiddleLeft, Ink, FontStyle.Bold).text = "ALT";
            _cardAlt = Txt(_cardAltRow, "V", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(150, 24), 22, TextAnchor.MiddleLeft, Gold, FontStyle.Bold);
            _cardWeapons = Rect(_card, "Weapons", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(360, 70));
            Img(_cardWeapons, "Rule", new Vector2(0, 1), new Vector2(0, 1), new Vector2(-94, 0), new Vector2(350, 1), new Color(1, 1, 1, 0.2f));
            _wpRow = Txt(_cardWeapons, "W", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -8), new Vector2(150, 24), 16, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            _wpAmmo = Txt(_cardWeapons, "WA", new Vector2(0, 1), new Vector2(1, 1), new Vector2(250, -6), new Vector2(160, 26), 22, TextAnchor.UpperRight, Ink, FontStyle.Bold);
            _shRow = Txt(_cardWeapons, "S", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -40), new Vector2(150, 24), 16, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            _shState = Txt(_cardWeapons, "SS", new Vector2(0, 1), new Vector2(1, 1), new Vector2(250, -36), new Vector2(160, 20), 15, TextAnchor.UpperRight, Gold, FontStyle.Bold);
            Img(_cardWeapons, "SBack", new Vector2(0, 1), new Vector2(1, 1), new Vector2(250, -58), new Vector2(140, 4), new Color(1, 1, 1, 0.15f));
            _shBar = Img(_cardWeapons, "SBar", new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -58), new Vector2(140, 4), Gold);
            _cardStatus = Txt(_card, "Status", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(260, 22), 15, TextAnchor.UpperLeft, Teal, FontStyle.Bold);
        }

        RectTransform _boostRow;

        RectTransform Row(string label, out Image bar, Color c)
        {
            var row = Rect(_card, label + "Row", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(260, 24));
            Txt(row, "L", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(110, 20), 15, TextAnchor.MiddleLeft, Ink, FontStyle.Bold).text = label;
            Img(row, "Back", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(140, 10), new Color(1, 1, 1, 0.12f));
            bar = Img(row, "Bar", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(140, 10), c);
            return row;
        }

        void LayoutCard(HudMode m, bool airborne)
        {
            float y = -54;
            void Place(RectTransform r, bool on, float h)
            {
                r.gameObject.SetActive(on);
                if (!on) return;
                r.anchoredPosition = new Vector2(110, y);
                y -= h;
            }
            bool race = m == HudMode.Race || m == HudMode.Flight;
            Place(_cardSpeedRow, race, 50);
            Place(_cardIntRow, m != HudMode.Explore, 26);
            Place(_boostRow, true, 26);
            Place(_cardOdRow, race, 26);
            Place(_cardAltRow, race && airborne, 28);
            _cardWeapons.gameObject.SetActive(m == HudMode.Combat);
            if (m == HudMode.Combat) { _cardWeapons.anchoredPosition = new Vector2(94 + 16, y - 4); y -= 74; }
            Place(_cardStatus.rectTransform, m == HudMode.Explore, 26);
            _card.sizeDelta = new Vector2(380, Mathf.Max(130, -y + 12));
        }

        void BuildWorldMarkers()
        {
            _wp = Rect(_root, "Waypoint", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            var d = Img(_wp, "D", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36), Teal, Diamond);
            Img(d.transform, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9, 9), Teal, Diamond);
            Glow(d, Color.black, 1.5f);
            _wpDist = Txt(_wp, "Dist", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(200, 22), 17, TextAnchor.UpperCenter, Ink, FontStyle.Bold);
            Glow(_wpDist, Color.black, 1.5f);
            _chev = Rect(_root, "Chevrons", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));
            var l = Txt(_chev, "L", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-130, -40), new Vector2(60, 110), 96, TextAnchor.MiddleCenter, Teal, FontStyle.Normal);
            l.text = "‹";
            var r = Txt(_chev, "R", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(130, -40), new Vector2(60, 110), 96, TextAnchor.MiddleCenter, Teal, FontStyle.Normal);
            r.text = "›";
            _chevText = Txt(_chev, "T", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -26), new Vector2(360, 24), 18, TextAnchor.UpperCenter, Ink, FontStyle.Bold);
            Glow(_chevText, Color.black, 1.5f);
        }

        void BuildInteraction()
        {
            var p = Panel2(_root, "Interact", new Vector2(0.5f, 0f), new Vector2(0, 0), new Vector2(40, 140), new Vector2(400, 178));
            _int = p.rectTransform;
            _intTitle = Txt(_int, "Title", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(340, 34), 28, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            _intL1 = Txt(_int, "L1", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -54), new Vector2(340, 22), 16, TextAnchor.UpperLeft, Ink, FontStyle.Normal);
            _intL1.supportRichText = true;
            _intL2 = Txt(_int, "L2", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -78), new Vector2(340, 22), 16, TextAnchor.UpperLeft, Ink, FontStyle.Normal);
            _intL2.supportRichText = true;
            Img(_int, "Rule", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -104), new Vector2(330, 1), new Color(1, 1, 1, 0.2f));
            _intKey = Img(_int, "Key", new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 36), new Vector2(34, 34), Ink, Chamfer);
            _intKey.type = Image.Type.Sliced;
            Img(_intKey.transform, "Inner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30), new Color(0.03f, 0.06f, 0.1f, 1f), Chamfer).type = Image.Type.Sliced;
            Txt(_intKey.transform, "E", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34), 18, TextAnchor.MiddleCenter, Ink, FontStyle.Bold).text = "E";
            _intAction = Txt(_int, "Action", new Vector2(0, 0), new Vector2(0, 0), new Vector2(72, 24), new Vector2(290, 26), 19, TextAnchor.LowerLeft, Ink, FontStyle.Bold);
            _intSub = Txt(_int, "Sub", new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 6), new Vector2(355, 28), 13, TextAnchor.UpperLeft, Dim, FontStyle.Normal);
            _intSub.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void BuildRaceLayer()
        {
            var rp = Panel2(_root, "RaceHeader", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(440, 96));
            _racePanel = rp.rectTransform;
            Txt(_racePanel, "PL", new Vector2(0.25f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(200, 20), 15, TextAnchor.UpperCenter, Dim, FontStyle.Bold).text = "POSITION";
            Txt(_racePanel, "LL", new Vector2(0.75f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(200, 20), 15, TextAnchor.UpperCenter, Dim, FontStyle.Bold).text = "LAP";
            _lapLabel = _racePanel.GetChild(_racePanel.childCount - 1).GetComponent<Text>();
            Img(_racePanel, "Div", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(1, 56), new Color(1, 1, 1, 0.25f));
            _posBig = Txt(_racePanel, "Pos", new Vector2(0.25f, 0), new Vector2(1, 0), new Vector2(4, 8), new Vector2(100, 60), 50, TextAnchor.LowerRight, Gold, FontStyle.Bold);
            _posTotal = Txt(_racePanel, "PosT", new Vector2(0.25f, 0), new Vector2(0, 0), new Vector2(8, 14), new Vector2(100, 40), 30, TextAnchor.LowerLeft, Ink, FontStyle.Bold);
            _lapBig = Txt(_racePanel, "Lap", new Vector2(0.75f, 0), new Vector2(1, 0), new Vector2(4, 8), new Vector2(100, 60), 50, TextAnchor.LowerRight, Gold, FontStyle.Bold);
            _lapTotal = Txt(_racePanel, "LapT", new Vector2(0.75f, 0), new Vector2(0, 0), new Vector2(8, 14), new Vector2(100, 40), 30, TextAnchor.LowerLeft, Ink, FontStyle.Bold);

            var tp = Panel2(_root, "Timer", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -24), new Vector2(300, 96));
            _timerPanel = tp.rectTransform;
            var sw = Img(_timerPanel, "Watch", new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(42, -36), new Vector2(34, 34), Gold, Ring);
            Img(sw.transform, "Hand", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(3, 11), Gold);
            Img(sw.transform, "Crown", new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 0), new Vector2(8, 5), Gold);
            _timer = Txt(_timerPanel, "Time", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -8), new Vector2(220, 50), 40, TextAnchor.UpperRight, Ink, FontStyle.Bold);
            Img(_timerPanel, "Rule", new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 38), new Vector2(260, 1), new Color(1, 1, 1, 0.2f));
            _bestLap = Txt(_timerPanel, "Best", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 10), new Vector2(260, 24), 17, TextAnchor.LowerRight, Ink, FontStyle.Bold);
            _bestLap.supportRichText = true;

            _board = Rect(_root, "Standings", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -196), new Vector2(260, 200));
            for (int i = 0; i < 6; i++)
            {
                var bg = Img(_board, "Row", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 28), new Vector2(240, 25), new Color(0.03f, 0.06f, 0.1f, 0.65f));
                var t = Txt(bg.transform, "T", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(220, 25), 16, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
                _boardRows.Add(t);
            }
        }

        void BuildCombatLayer()
        {
            _vsPanel = Rect(_root, "Versus", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(860, 66));
            var l = Panel2(_vsPanel, "Me", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(330, 62));
            _vsName = Txt(l.transform, "N", new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -8), new Vector2(200, 26), 20, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            _vsPct = Txt(l.transform, "P", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -8), new Vector2(100, 26), 20, TextAnchor.UpperRight, Ink, FontStyle.Bold);
            Img(l.transform, "B", new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 14), new Vector2(286, 8), new Color(1, 1, 1, 0.12f));
            _vsBar = Img(l.transform, "Bar", new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 14), new Vector2(286, 8), Teal);
            var m = Panel2(_vsPanel, "Clock", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 62));
            _vsTime = Txt(m.transform, "T", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 50), 32, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            var r = Panel2(_vsPanel, "Rival", new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(330, 62));
            _vsRival = Txt(r.transform, "N", new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -8), new Vector2(200, 26), 20, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            _vsRivalPct = Txt(r.transform, "P", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -8), new Vector2(100, 26), 20, TextAnchor.UpperRight, Ink, FontStyle.Bold);
            Img(r.transform, "B", new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 14), new Vector2(286, 8), new Color(1, 1, 1, 0.12f));
            _vsRivalBar = Img(r.transform, "Bar", new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 14), new Vector2(286, 8), Rival);

            var b = Panel2(_root, "Badge", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -30), new Vector2(230, 58));
            _badge = b.rectTransform;
            foreach (float a in new[] { 45f, -45f })
            {
                var blade = Img(_badge, "Blade", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(5, 34), Gold);
                blade.rectTransform.localEulerAngles = new Vector3(0, 0, a);
                var guard = Img(blade.transform, "Guard", new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 4), Gold);
                _ = guard;
            }
            _badgeText = Txt(_badge, "T", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(150, 30), 22, TextAnchor.MiddleRight, Ink, FontStyle.Bold);

            var c = Panel2(_root, "Comms", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -190), new Vector2(440, 150));
            _comms = c.rectTransform;
            var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            portrait.transform.SetParent(_comms, false);
            var prt = portrait.rectTransform; prt.anchorMin = prt.anchorMax = new Vector2(0, 0.5f); prt.pivot = new Vector2(0, 0.5f);
            prt.anchoredPosition = new Vector2(12, 0); prt.sizeDelta = new Vector2(126, 126);
            portrait.texture = Portrait; portrait.raycastTarget = false;
            Img(_comms, "Frame", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(146, 0), new Vector2(1, 120), new Color(1, 1, 1, 0.2f));
            _commsText = Txt(_comms, "Line", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(162, 14), new Vector2(270, 60), 20, TextAnchor.MiddleLeft, Ink, FontStyle.Normal);
            for (int i = 0; i < _wave.Length; i++)
                _wave[i] = Img(_comms, "Wave", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170 + i * 6, -38), new Vector2(3, 6), Teal);
        }

        // ------------------------------------------------------------------ world map (M)
        public void ToggleWorldMap()
        {
            if (!_bigMap) BuildWorldMap();
            _bigMap.gameObject.SetActive(!_bigMap.gameObject.activeSelf);
        }

        void BuildWorldMap()
        {
            const float size = 920f;
            var panel = Panel2(_root, "WorldMap", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size + 40, size + 90));
            _bigMap = panel.rectTransform;
            Txt(_bigMap, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(size, 40), 28, TextAnchor.UpperCenter, Ink, FontStyle.Bold).text = "THE BAY   ·   [M] CLOSE";
            var area = Rect(_bigMap, "Area", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(size, size));
            var ri = new GameObject("Tex", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            ri.transform.SetParent(area, false);
            ri.texture = GameAssets.I.worldMap;
            ri.rectTransform.sizeDelta = new Vector2(size, size);
            float k = size / (CityLayout.Half * 2);
            foreach (var v in CityLayout.Venues)
            {
                var pos = new Vector2(v.pos.x, v.pos.z) * k;
                Img(area, "Venue", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(30, 30), new Color(0.05f, 0.08f, 0.16f, 0.95f), Circle);
                Img(area, "Ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(32, 32), Ink, Ring);
                var n = Txt(area, "Num", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(30, 30), 17, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
                n.text = v.number.ToString();
                var l = Txt(area, "Name", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), pos + new Vector2(22, 0), new Vector2(320, 24), 16, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
                l.text = v.name;
                Glow(l, Color.black, 1.5f);
            }
            for (int i = 0; i < 10; i++) { var d = Img(area, "Marker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18), Gold, Diamond); d.enabled = false; _bigMarkers.Add(d); }
            var arrow = Img(area, "Me", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 26), Color.white, Arrow);
            _bigPlayer = arrow.rectTransform;
            _bigMap.gameObject.SetActive(false);
        }

        void TickWorldMap(MechController p, List<Marker> markers)
        {
            if (!WorldMapOpen) return;
            float k = 920f / (CityLayout.Half * 2);
            _bigPlayer.anchoredPosition = new Vector2(p.transform.position.x, p.transform.position.z) * k;
            _bigPlayer.localEulerAngles = new Vector3(0, 0, -p.yaw);
            int mi = 0;
            if (markers != null)
                foreach (var m in markers)
                {
                    if (mi >= _bigMarkers.Count) break;
                    var d = _bigMarkers[mi++];
                    d.enabled = true; d.color = m.color;
                    d.rectTransform.anchoredPosition = new Vector2(m.pos.x, m.pos.z) * k;
                }
            for (; mi < _bigMarkers.Count; mi++) _bigMarkers[mi].enabled = false;
        }

        // ------------------------------------------------------------------ events
        public void Toast(string text, Color c, float dur = 2.2f) { _toast.text = text; _toast.color = c; _toastT = dur; }
        public void Style(string text) { _style.text = text; _styleT = 1.4f; }
        public void HitMarker() => _hitT = 0.15f;
        public void Hurt(float k = 0.5f) => _dmgT = Mathf.Max(_dmgT, k);
        public void Comms(string line, float dur = 3.5f) { _commsText.text = line; _commsT = dur; }
        public void SetVisible(bool v) => canvas.enabled = v;

        Vector2 ToCanvas(Camera cam, Vector3 world, out bool front)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            front = sp.z > 0;
            float k = (canvas.transform as RectTransform).rect.width / Screen.width;
            return new Vector2(sp.x * k, sp.y * k);
        }

        // ------------------------------------------------------------------ tick
        public void Tick(HudState s)
        {
            float dt = Time.unscaledDeltaTime;
            var p = s.player;
            var st = p.stats;
            var mode = s.mode;
            bool race = mode == HudMode.Race || mode == HudMode.Flight;
            bool combat = mode == HudMode.Combat;
            if (mode != _mode)
            {
                _mode = mode;
                _racePanel.gameObject.SetActive(race);
                _timerPanel.gameObject.SetActive(race);
                _board.gameObject.SetActive(race);
                _vsPanel.gameObject.SetActive(combat);
                _badge.gameObject.SetActive(combat);
                _creditsPanel.gameObject.SetActive(!race && !combat);
                _rep.gameObject.SetActive(!race && !combat);
                if (combat) _lastPct = 1f;
            }

            // location, clock, objective, credits
            _loc.text = s.location;
            int hh = Mathf.FloorToInt(s.hour) % 24, mm = Mathf.FloorToInt((s.hour - Mathf.Floor(s.hour)) * 60);
            _clock.text = $"{(hh % 12 == 0 ? 12 : hh % 12)}:{mm:00} {(hh < 12 ? "AM" : "PM")}";
            _objPanel.gameObject.SetActive(!string.IsNullOrEmpty(s.objectiveTitle));
            _objTitle.text = s.objectiveTitle;
            _objText.text = s.objectiveText;
            _objText.rectTransform.anchoredPosition = new Vector2(62, string.IsNullOrEmpty(s.objectiveText) ? -24 : -36);
            _objTitle.rectTransform.anchoredPosition = new Vector2(62, string.IsNullOrEmpty(s.objectiveText) ? -26 : -12);
            _credits.text = $"{s.credits:N0} CR";
            _rep.text = $"REP  {s.rep}";

            // status card
            _cardName.text = p.pilot == "YOU" ? "KESTREL" : p.pilot;
            float apf = Mathf.Clamp01(p.ap / st.ap);
            float enf = Mathf.Clamp01(p.en / st.enCap);
            for (int i = 0; i < 4; i++)
            {
                float seg = Mathf.Clamp01(enf * 4 - i);
                _boost[i].color = p.overheated ? new Color(Rival.r, Rival.g, Rival.b, 0.5f + seg * 0.5f) : seg >= 0.99f ? Teal : Color.Lerp(new Color(1, 1, 1, 0.15f), Teal, seg);
            }
            _cardIntBar.rectTransform.sizeDelta = new Vector2(140 * apf, 10);
            _cardIntBar.color = apf < 0.3f ? Rival : Teal;
            _cardOdBar.rectTransform.sizeDelta = new Vector2(140 * p.overdrive, 10);
            _cardOdBar.color = p.overdriveActive ? Color.Lerp(Gold, Color.white, Mathf.PingPong(Time.time * 4, 1) * 0.5f) : Gold;
            int kmh = Mathf.RoundToInt(p.Speed * 3.6f);
            _cardSpeed.text = kmh.ToString();
            _cardSpeedUnit.rectTransform.anchoredPosition = new Vector2(_cardSpeed.preferredWidth + 8, -6);
            bool airborne = p.heightAboveGround > 3f;
            _cardAlt.text = $"{p.heightAboveGround:0} m";
            _cardStatus.text = p.IsDown ? "SYSTEMS DOWN" : apf < 0.3f ? "CRITICAL DAMAGE" : apf < 0.7f ? "ARMOR DAMAGED" : "SYSTEMS NORMAL";
            _cardStatus.color = apf < 0.3f || p.IsDown ? Rival : apf < 0.7f ? Gold : Teal;
            var w = st.weapon;
            _wpRow.text = ShortName(w.name);
            _wpAmmo.text = p.reloadTimer > 0 ? "RELOAD" : $"{p.ammo} / {w.mag}";
            _shRow.text = ShortName(st.shoulder.name);
            float shReady = st.shoulder.cooldown > 0 ? 1f - Mathf.Clamp01(p.shoulderCd / st.shoulder.cooldown) : 1f;
            _shState.text = shReady < 1f ? $"CHARGING  {shReady * 100:0}%" : (s.lockT || st.shoulder.shoulder == ShoulderKind.Mines ? "READY" : "NO LOCK");
            _shState.color = shReady < 1f ? Gold : Teal;
            _shBar.rectTransform.sizeDelta = new Vector2(140 * shReady, 4);
            LayoutCard(mode, airborne);

            TickMinimap(s, dt);
            TickWaypoint(s);
            TickInteraction(s);
            if (race) TickRace(s);
            if (combat) TickCombat(s, dt);
            _comms.gameObject.SetActive(_commsT > 0);
            if (_commsT > 0)
            {
                _commsT -= dt;
                for (int i = 0; i < _wave.Length; i++)
                    _wave[i].rectTransform.sizeDelta = new Vector2(3, 4 + Mathf.Abs(Mathf.Sin(Time.time * 13f + i * 1.7f) * Mathf.Sin(Time.time * 5f + i)) * 16f);
            }
            _board.gameObject.SetActive(race && _commsT <= 0);

            // lock brackets
            bool showLock = false;
            if (s.lockT && s.cam)
            {
                var c = ToCanvas(s.cam, s.lockT.Center, out bool front);
                if (front)
                {
                    showLock = true;
                    float d = Vector3.Distance(s.cam.transform.position, s.lockT.Center);
                    float half = Mathf.Clamp(2400f / Mathf.Max(d, 1f), 34f, 160f);
                    for (int i = 0; i < 4; i++)
                    {
                        int sx = i % 2 == 0 ? -1 : 1, sy = i < 2 ? 1 : -1;
                        var rt = (RectTransform)_lockCorners[i].transform.parent;
                        rt.anchoredPosition = c + new Vector2(sx * half, sy * half * 1.15f);
                        rt.localEulerAngles = Vector3.zero;
                        rt.localScale = new Vector3(-sx, sy, 1);
                    }
                    _lockName.rectTransform.anchoredPosition = c + new Vector2(0, half * 1.15f + 8);
                    _lockName.text = $"{s.lockT.pilot}   {Vector3.Distance(s.lockT.Center, p.Center):0} m";
                }
            }
            for (int i = 0; i < 4; i++) _lockCorners[i].transform.parent.gameObject.SetActive(showLock);
            _lockName.gameObject.SetActive(showLock && !combat);

            // countdown
            if (s.race != null && s.race.state == "countdown") _countdown.text = Mathf.CeilToInt(s.race.countdown) <= 3 ? Mathf.CeilToInt(s.race.countdown).ToString() : "";
            else if (s.duel != null && s.duel.state == "countdown") _countdown.text = Mathf.CeilToInt(s.duel.countdown) <= 3 ? Mathf.CeilToInt(s.duel.countdown).ToString() : "";
            else if (s.race != null && s.race.time < 1f && s.race.state == "running") _countdown.text = "GO!";
            else if (s.duel != null && s.duel.state == "fight" && s.duel.timeLeft > s.duel.def.timeLimit - 1f) _countdown.text = "FIGHT!";
            else _countdown.text = "";

            if (_toastT > 0) { _toastT -= dt; _toast.canvasRenderer.SetAlpha(Mathf.Clamp01(_toastT * 3)); } else _toast.text = "";
            if (_styleT > 0) { _styleT -= dt; _style.canvasRenderer.SetAlpha(Mathf.Clamp01(_styleT * 2)); } else _style.text = "";
            _down.text = p.IsDown ? "SYSTEMS DOWN  ·  REBOOTING" : p.IsStaggered ? "STAGGERED" : "";
            _hitT -= dt; _hitMarker.color = new Color(1, 1, 1, _hitT > 0 ? 1 : 0);
            _dmgT = Mathf.Max(0, _dmgT - dt);
            _vignette.color = new Color(1f, 0.08f, 0.06f, Mathf.Clamp01(_dmgT) * 0.5f + (apf < 0.25f ? 0.15f + Mathf.PingPong(Time.time, 0.15f) : 0));
            TickWorldMap(p, s.markers);
        }

        static string ShortName(string n)
        {
            n = n.ToUpperInvariant();
            int i = n.LastIndexOf(' ');
            return i > 0 && i < n.Length - 1 ? n.Substring(i + 1) : n;
        }

        void TickMinimap(HudState s, float dt)
        {
            var p = s.player;
            Vector2 me = new Vector2(p.transform.position.x, p.transform.position.z);
            _mapInner.anchoredPosition = -me * S;
            _mapPlayer.localEulerAngles = new Vector3(0, 0, -p.yaw);
            // GPS route
            _routeT -= dt;
            if (s.hasWaypoint)
            {
                if (_routePts == null || _routeT <= 0 || (s.waypoint - _routeTarget).sqrMagnitude > 4f)
                {
                    _routePts = CityLayout.Route(me, new Vector2(s.waypoint.x, s.waypoint.z));
                    _routeTarget = s.waypoint;
                    _routeT = 1f;
                }
                _routePts[0] = me;
            }
            int ri = 0;
            if (s.hasWaypoint && _routePts != null)
            {
                for (int i = 0; i + 1 < _routePts.Count && ri < _route.Count; i++)
                {
                    Vector2 a = _routePts[i] * S, b = _routePts[i + 1] * S;
                    float len = Vector2.Distance(a, b);
                    if (len < 0.5f) continue;
                    var r = _route[ri++];
                    r.enabled = true;
                    r.rectTransform.anchoredPosition = (a + b) * 0.5f;
                    r.rectTransform.sizeDelta = new Vector2(len + 4, 5);
                    r.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                }
            }
            for (; ri < _route.Count; ri++) _route[ri].enabled = false;
            _mapWaypoint.enabled = s.hasWaypoint;
            if (s.hasWaypoint) _mapWaypoint.rectTransform.anchoredPosition = new Vector2(s.waypoint.x, s.waypoint.z) * S;
            int mi = 0;
            if (s.markers != null)
                foreach (var m in s.markers)
                {
                    if (mi >= _mapMarkers.Count) break;
                    var d = _mapMarkers[mi++];
                    d.enabled = true; d.color = m.color;
                    d.rectTransform.anchoredPosition = new Vector2(m.pos.x, m.pos.z) * S;
                }
            for (; mi < _mapMarkers.Count; mi++) _mapMarkers[mi].enabled = false;
            int di = 0;
            if (s.race != null)
                foreach (var r in s.race.racers)
                {
                    if (r == s.race.player || di >= _mapRivals.Count) continue;
                    var d = _mapRivals[di++];
                    d.enabled = true; d.color = r.mech.IsDown ? Dim : Rival;
                    d.rectTransform.anchoredPosition = new Vector2(r.mech.transform.position.x, r.mech.transform.position.z) * S;
                }
            if (s.duel != null && s.duel.rival && di < _mapRivals.Count)
            {
                var d = _mapRivals[di++];
                d.enabled = true; d.color = Rival;
                d.rectTransform.anchoredPosition = new Vector2(s.duel.rival.transform.position.x, s.duel.rival.transform.position.z) * S;
            }
            for (; di < _mapRivals.Count; di++) _mapRivals[di].enabled = false;
        }

        void TickWaypoint(HudState s)
        {
            bool race = s.mode == HudMode.Race || s.mode == HudMode.Flight;
            float dist = s.hasWaypoint ? Vector3.Distance(s.player.transform.position, s.waypoint) : 0f;
            string dtext = dist >= 1000 ? $"{dist / 1000f:0.0} km" : $"{dist:0} m";
            _waypointLabel.transform.parent.gameObject.SetActive(s.hasWaypoint);
            _waypointLabel.text = s.hasWaypoint ? $"{s.waypointName}  ·  {dtext}" : "";
            bool show = s.hasWaypoint && s.cam && dist > 12f;
            _wp.gameObject.SetActive(show && !race && s.mode != HudMode.Combat);
            _chev.gameObject.SetActive(show && race);
            if (!show) return;
            Vector3 world = s.waypoint + Vector3.up * (race ? 13f : 9f);
            var c = ToCanvas(s.cam, world, out bool front);
            var size = (canvas.transform as RectTransform).rect.size;
            if (!front) { c = new Vector2(size.x - c.x, 80); }
            c.x = Mathf.Clamp(c.x, 60, size.x - 60);
            c.y = Mathf.Clamp(c.y, 120, size.y - 140);
            if (race)
            {
                _chev.anchoredPosition = c;
                float spread = Mathf.Clamp(5200f / Mathf.Max(dist, 1f), 60f, 220f);
                _chev.GetChild(0).GetComponent<RectTransform>().anchoredPosition = new Vector2(-spread, -60);
                _chev.GetChild(1).GetComponent<RectTransform>().anchoredPosition = new Vector2(spread, -60);
                _chevText.text = $"CHECKPOINT  ·  {dtext}";
            }
            else
            {
                _wp.anchoredPosition = c;
                _wpDist.text = dtext;
            }
        }

        void TickInteraction(HudState s)
        {
            var it = s.interact;
            _int.gameObject.SetActive(it != null);
            if (it == null) return;
            _intTitle.text = it.title;
            _intL1.text = it.line1;
            _intL2.text = it.line2;
            _intAction.text = it.locked ? "LOCKED" : it.action;
            _intAction.color = it.locked ? Rival : Ink;
            _intSub.text = it.sub;
            _intKey.gameObject.SetActive(!it.locked);
            _intAction.rectTransform.anchoredPosition = new Vector2(it.locked ? 24 : 72, 42);
        }

        void TickRace(HudState s)
        {
            var race = s.race;
            if (race == null) return;
            var stand = race.Standings();
            int pos = stand.IndexOf(race.player) + 1;
            _posBig.text = pos.ToString();
            _posTotal.text = "/ " + stand.Count;
            bool circuit = race.def.type == "CIRCUIT";
            _lapLabel.text = circuit ? "LAP" : "GATE";
            _lapBig.text = circuit ? race.Lap.ToString() : Mathf.Min(race.player.next + 1, race.gates.Count).ToString();
            _lapTotal.text = "/ " + (circuit ? race.def.laps : race.gates.Count);
            // lap splits for the best-lap readout
            if (_lapRace != race) { _lapRace = race; _lapStart = 0; _bestLapTime = -1; _lastLap = 1; }
            int lap = race.Lap;
            if (circuit && lap != _lastLap && race.state == "running")
            {
                float split = race.time - _lapStart;
                if (_bestLapTime < 0 || split < _bestLapTime) _bestLapTime = split;
                _lapStart = race.time; _lastLap = lap;
            }
            _timer.text = FormatTime(race.time);
            _bestLap.text = circuit ? $"<color=#9AA6B8>BEST LAP</color>   {(_bestLapTime > 0 ? FormatTime(_bestLapTime) : "--:--.--")}" : $"<color=#9AA6B8>{race.def.name}</color>";
            for (int i = 0; i < _boardRows.Count; i++)
            {
                var row = _boardRows[i];
                bool on = i < stand.Count;
                row.transform.parent.gameObject.SetActive(on);
                if (!on) continue;
                var r = stand[i];
                row.text = $"{i + 1}   {(r == race.player ? "KESTREL" : r.mech.pilot)}{(r.mech.IsDown ? "   DOWN" : "")}";
                row.color = r == race.player ? Gold : r.mech.IsDown ? Dim : Ink;
            }
        }

        void TickCombat(HudState s, float dt)
        {
            var duel = s.duel;
            if (duel == null) return;
            float mine = duel.PlayerPct, theirs = duel.RivalPct;
            _vsName.text = "KESTREL";
            _vsPct.text = $"{mine * 100:0}%";
            _vsBar.rectTransform.sizeDelta = new Vector2(286 * mine, 8);
            _vsRival.text = duel.def.rival;
            _vsRivalPct.text = $"{theirs * 100:0}%";
            _vsRivalBar.rectTransform.sizeDelta = new Vector2(286 * theirs, 8);
            float t = Mathf.Max(0, duel.timeLeft);
            _vsTime.text = $"{(int)(t / 60):00}:{(int)(t % 60):00}";
            _badgeText.text = "DUEL";
            // pilot chatter at the big moments
            if (duel.state == "fight")
            {
                if (_lastPct > 0.7f && mine <= 0.7f) Comms("Taking hits. Keep moving.");
                else if (_lastPct > 0.4f && mine <= 0.4f) Comms("I can still win this.");
                else if (_lastPct > 0.15f && mine <= 0.15f) Comms("Armor's almost gone!");
                if (theirs < 0.25f && _commsT <= 0 && Random.value < dt * 0.3f) Comms("They're breaking. Finish it.");
            }
            _lastPct = mine;
        }
    }
}
