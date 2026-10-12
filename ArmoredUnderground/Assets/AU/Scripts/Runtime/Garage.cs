using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static AU.UIKit;

namespace AU
{
    /// <summary>
    /// The Rig Shop: a showroom set off-map with a turntable, plus the assembly UI
    /// (slots, part lists with buy/equip, stat comparison, paint).
    /// </summary>
    public class Garage : MonoBehaviour
    {
        public static readonly Vector3 Origin = new(6000, 0, 6000);
        public bool IsOpen { get; private set; }
        public event Action Closed;
        public event Action LoadoutChanged;

        SaveData _save;
        Camera _cam;
        GameObject _set, _preview;
        MechRig _previewRig;
        Canvas _canvas;
        RectTransform _slots, _parts, _stats;
        Text _credits, _slotTitle;
        Slot _slot = Slot.Legs;
        float _angle = 0.6f, _savedFogDensity;
        bool _savedFog;
        Vector3 _camPos; Quaternion _camRot; float _camFov;
        readonly Dictionary<Slot, Text> _slotLabels = new();

        public void Init(SaveData save, Camera cam)
        {
            _save = save; _cam = cam;
            BuildSet();
            BuildUI();
            _set.SetActive(false);
            _canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ showroom
        void BuildSet()
        {
            _set = new GameObject("Showroom");
            _set.transform.position = Origin;
            var a = GameAssets.I;
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.SetColor("_BaseColor", new Color(0.05f, 0.055f, 0.065f)); floorMat.SetFloat("_Smoothness", 0.85f); floorMat.SetFloat("_Metallic", 0.6f);
            var wallMat = new Material(floorMat); wallMat.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.035f)); wallMat.SetFloat("_Smoothness", 0.4f);
            GameObject Prim(PrimitiveType t, Vector3 p, Vector3 s, Material m, Quaternion? r = null)
            {
                var g = GameObject.CreatePrimitive(t);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(_set.transform, false);
                g.transform.localPosition = p; g.transform.localScale = s; g.transform.localRotation = r ?? Quaternion.identity;
                g.GetComponent<Renderer>().sharedMaterial = m;
                return g;
            }
            Prim(PrimitiveType.Cylinder, new Vector3(0, -0.5f, 0), new Vector3(80, 0.5f, 80), floorMat);
            Prim(PrimitiveType.Cylinder, new Vector3(0, 0.1f, 0), new Vector3(13, 0.2f, 13), wallMat);
            var ring = Prim(PrimitiveType.Cylinder, new Vector3(0, 0.12f, 0), new Vector3(13.4f, 0.18f, 13.4f), a.gate);
            ring.GetComponent<Renderer>().sharedMaterial = a.gate;
            Prim(PrimitiveType.Cylinder, new Vector3(0, 0.22f, 0), new Vector3(13f, 0.2f, 13f), wallMat);
            // back walls in a half hexagon with vertical neon strips
            for (int i = -2; i <= 2; i++)
            {
                float ang = i * 30f;
                var rot = Quaternion.Euler(0, ang + 180, 0);
                Vector3 p = rot * new Vector3(0, 10, -26);
                Prim(PrimitiveType.Cube, p, new Vector3(28, 22, 1), wallMat, rot);
                Prim(PrimitiveType.Cube, p + rot * new Vector3(-9, 0, 0.6f), new Vector3(0.25f, 20, 0.2f), i % 2 == 0 ? a.gate : a.thrusterCore, rot);
                Prim(PrimitiveType.Cube, p + rot * new Vector3(9, 0, 0.6f), new Vector3(0.25f, 20, 0.2f), a.gate, rot);
            }
            // overhead truss with lights
            void Spot(Vector3 p, Color c, float intensity, float angle)
            {
                var l = new GameObject("Spot").AddComponent<Light>();
                l.transform.SetParent(_set.transform, false);
                l.transform.localPosition = p;
                l.transform.LookAt(Origin + Vector3.up * 4);
                l.type = LightType.Spot; l.color = c; l.intensity = intensity; l.range = 60; l.spotAngle = angle; l.shadows = LightShadows.Soft;
            }
            Spot(new Vector3(8, 18, 10), new Color(1f, 0.88f, 0.75f), 900, 45);
            Spot(new Vector3(-12, 10, -8), new Color(0.2f, 0.75f, 1f), 700, 50);
            Spot(new Vector3(12, 6, -10), new Color(1f, 0.2f, 0.6f), 500, 50);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(_set.transform, false); fill.transform.localPosition = new Vector3(0, 6, 14);
            fill.type = LightType.Point; fill.range = 30; fill.intensity = 8; fill.color = new Color(0.7f, 0.8f, 1f);
        }

        void RebuildPreview()
        {
            if (_preview) Destroy(_preview);
            _preview = Instantiate(GameAssets.I.mechPrefab, Origin + Vector3.up * 0.32f, Quaternion.Euler(0, 180, 0));
            _previewRig = _preview.AddComponent<MechRig>();
            _previewRig.Init(null);
            _previewRig.ApplyPaint(_save.loadout);
            MechVariants.Apply(_preview.transform, _save.loadout);
        }

        // ------------------------------------------------------------------ UI
        void BuildUI()
        {
            var go = new GameObject("GarageUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 20;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var root = Stretch(go.transform, "Root");

            var left = Img(root, "Left", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(400, 1080), Panel);
            var title = Txt(left.transform, "Title", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(360, 50), 44, TextAnchor.UpperLeft, Amber, FontStyle.BoldAndItalic);
            title.text = "RIG SHOP";
            _credits = Txt(left.transform, "Credits", new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -86), new Vector2(360, 30), 22, TextAnchor.UpperLeft, Dim);
            _credits.supportRichText = true;
            _slots = Rect(left.transform, "Slots", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -130), new Vector2(360, 600));
            int i = 0;
            foreach (Slot s in Parts.Slots)
            {
                var slot = s;
                var b = Btn(_slots, "", new Vector2(0, 1), new Vector2(180, -32 - i * 70), new Vector2(360, 62), () => { _slot = slot; Refresh(); }, new Color(1, 1, 1, 0.05f));
                var lab = b.GetComponentInChildren<Text>();
                lab.alignment = TextAnchor.MiddleLeft; lab.rectTransform.anchoredPosition = new Vector2(16, -8); lab.fontSize = 22; lab.color = Ink;
                var cap = Txt(b.transform, "Cap", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -6), new Vector2(300, 18), 13, TextAnchor.UpperLeft, Dim);
                cap.text = Parts.SlotLabel(s);
                _slotLabels[s] = lab;
                i++;
            }
            // paint
            var paint = Rect(left.transform, "Paint", new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 110), new Vector2(360, 200));
            Txt(paint, "PT", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 22), 16, TextAnchor.UpperLeft, Dim).text = "PAINT  (HUE)";
            HueSlider(paint, "PRIMARY", -40, () => _save.loadout.paint, c => _save.loadout.paint = c, 0.35f);
            HueSlider(paint, "ACCENT", -90, () => _save.loadout.accent, c => _save.loadout.accent = c, 0.85f);
            HueSlider(paint, "NEON", -140, () => _save.loadout.neon, c => _save.loadout.neon = c, 1f);
            Btn(left.transform, "BACK TO STREETS", new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(340, 56), () => Close(), Amber, 22);

            var right = Img(root, "Right", new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(560, 1080), Panel);
            _slotTitle = Txt(right.transform, "SlotTitle", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -34), new Vector2(500, 30), 20, TextAnchor.UpperLeft, Dim);
            _parts = Rect(right.transform, "Parts", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -80), new Vector2(520, 640));
            _stats = Rect(right.transform, "Stats", new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 30), new Vector2(520, 280));
        }

        void HueSlider(Transform parent, string label, float y, Func<Color> get, Action<Color> set, float sat)
        {
            Txt(parent, label, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(110, 30), 15, TextAnchor.MiddleLeft, Ink).text = label;
            var bg = Img(parent, "Track", new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(120, y - 15), new Vector2(230, 16), Color.white);
            // rainbow track
            var tex = new Texture2D(64, 1) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++) tex.SetPixel(x, 0, Color.HSVToRGB(x / 63f, sat, 1f));
            tex.Apply();
            bg.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f));
            var sgo = new GameObject("Slider", typeof(RectTransform));
            sgo.transform.SetParent(bg.transform, false);
            var srt = (RectTransform)sgo.transform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
            var slider = sgo.AddComponent<Slider>();
            var handleArea = Stretch(sgo.transform, "HandleArea");
            var handle = Img(handleArea, "Handle", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 28), Ink);
            handle.raycastTarget = true;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            Color.RGBToHSV(get(), out float h, out _, out float v);
            slider.value = h;
            slider.onValueChanged.AddListener(val =>
            {
                Color.RGBToHSV(get(), out _, out _, out float vv);
                var c = Color.HSVToRGB(val, sat, label == "PRIMARY" ? Mathf.Max(0.15f, Mathf.Min(vv, 0.45f)) : 1f);
                if (label == "PRIMARY") c = Color.HSVToRGB(val, 0.35f, 0.3f);
                set(c);
                _previewRig?.ApplyPaint(_save.loadout);
                _save.Save();
                LoadoutChanged?.Invoke();
            });
            bg.raycastTarget = true;
        }

        void Refresh()
        {
            _credits.text = $"CR <color=#FFB31A>{_save.credits:N0}</color>    REP <color=#FFB31A>{_save.rep}</color>";
            foreach (var kv in _slotLabels)
            {
                kv.Value.text = _save.loadout.P(kv.Key).name;
                kv.Value.transform.parent.GetComponent<Image>().color = kv.Key == _slot ? new Color(0, 0.9f, 1f, 0.18f) : new Color(1, 1, 1, 0.05f);
            }
            _slotTitle.text = Parts.SlotLabel(_slot);
            foreach (Transform c in _parts) Destroy(c.gameObject);
            int i = 0;
            foreach (var p in Parts.OfSlot(_slot))
            {
                var part = p;
                bool owned = _save.Owns(p.id), equipped = _save.loadout[_slot] == p.id, afford = _save.credits >= p.price;
                var card = Img(_parts, "Card", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 156), new Vector2(520, 148), equipped ? new Color(0, 0.9f, 1f, 0.12f) : new Color(1, 1, 1, 0.04f));
                card.raycastTarget = true;
                if (equipped) Img(card.transform, "Bar", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 148), Neon);
                Txt(card.transform, "Name", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -10), new Vector2(330, 26), 22, TextAnchor.UpperLeft, Ink).text = p.name;
                Txt(card.transform, "Maker", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12), new Vector2(200, 22), 14, TextAnchor.UpperRight, Dim).text = p.maker;
                Txt(card.transform, "Desc", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -40), new Vector2(490, 20), 14, TextAnchor.UpperLeft, Dim, FontStyle.Normal).text = p.desc;
                Txt(card.transform, "Stats", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -66), new Vector2(490, 40), 15, TextAnchor.UpperLeft, Ink, FontStyle.Normal).text = StatLine(p);
                string label = equipped ? "EQUIPPED" : owned ? "EQUIP" : $"BUY  {p.price:N0} CR";
                var btn = Btn(card.transform, label, new Vector2(1, 0), new Vector2(-110, 26), new Vector2(200, 36), () =>
                {
                    if (equipped) return;
                    if (!owned)
                    {
                        if (_save.credits < part.price) return;
                        _save.credits -= part.price; _save.owned.Add(part.id);
                        AudioFX.I?.UI("buy", 0.8f);
                    }
                    _save.loadout[_slot] = part.id;
                    _save.Save();
                    RebuildPreview();
                    LoadoutChanged?.Invoke();
                    Refresh();
                }, equipped ? new Color(1, 1, 1, 0.1f) : (!owned && !afford) ? new Color(0.4f, 0.4f, 0.45f) : owned ? Neon : Amber, 17);
                btn.interactable = !equipped && (owned || afford);
                var trig = card.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => { var l = _save.loadout.Clone(); l[_slot] = part.id; ShowStats(l); });
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => ShowStats(_save.loadout));
                trig.triggers.Add(enter); trig.triggers.Add(exit);
                i++;
            }
            ShowStats(_save.loadout);
        }

        static string StatLine(Part p)
        {
            var parts = new List<string>();
            if (p.ap > 0) parts.Add($"AP {p.ap:0}");
            parts.Add($"WT {p.weight:0}");
            switch (p.slot)
            {
                case Slot.Head: parts.Add($"LOCK {p.lockRange:0}m"); break;
                case Slot.Core: parts.Add($"BOOST {p.boostMult * 100:0}%"); parts.Add($"STABILITY {p.stability * 100:0}%"); break;
                case Slot.Arms: parts.Add($"FIRE RATE {p.fireRate * 100:0}%"); break;
                case Slot.Legs: parts.Add($"LOAD {p.load:0}"); parts.Add($"SPEED {p.speed * 100:0}%"); parts.Add($"JUMP {p.jump * 100:0}%"); parts.Add($"GRIP {p.grip * 100:0}%"); break;
                case Slot.Booster: parts.Add($"THRUST {p.thrust * 100:0}%"); parts.Add($"DRAIN {p.drain * 100:0}%"); parts.Add($"QB {p.qbForce * 100:0}%"); break;
                case Slot.Generator: parts.Add($"EN {p.enCap:0}"); parts.Add($"RECOVERY {p.enRegen:0}/s"); break;
                case Slot.Weapon: parts.Add($"DMG {p.dmg:0}{(p.pellets > 1 ? "x" + p.pellets : "")}"); parts.Add($"RATE {p.rate:0.#}/s"); parts.Add($"MAG {p.mag}"); parts.Add($"IMPACT {p.impact:0}"); break;
                case Slot.Shoulder: parts.Add($"DMG {p.dmg:0}{(p.count > 1 ? "x" + p.count : "")}"); parts.Add($"COOLDOWN {p.cooldown:0.#}s"); break;
            }
            return string.Join("    ", parts);
        }

        void ShowStats(Loadout l)
        {
            foreach (Transform c in _stats) Destroy(c.gameObject);
            var cur = MechStats.From(_save.loadout);
            var s = MechStats.From(l);
            var rows = new (string label, float v, float b, string fmt)[]
            {
                ("AP", s.ap, cur.ap, "0"),
                ("GROUND SPEED km/h", s.groundSpeed * 3.6f, cur.groundSpeed * 3.6f, "0"),
                ("BOOST SPEED km/h", s.boostSpeed * 3.6f, cur.boostSpeed * 3.6f, "0"),
                ("OVERDRIVE km/h", s.overdriveSpeed * 3.6f, cur.overdriveSpeed * 3.6f, "0"),
                ("EN CAPACITY", s.enCap, cur.enCap, "0"),
                ("BOOST TIME s", s.enCap / s.boostDrain, cur.enCap / cur.boostDrain, "0.0"),
                ("LOCK RANGE m", s.lockRange, cur.lockRange, "0"),
                ("WEAPON DPS", s.weapon.dmg * s.weapon.pellets * s.weapon.rate * s.fireRateMult, cur.weapon.dmg * cur.weapon.pellets * cur.weapon.rate * cur.fireRateMult, "0"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                int col = i % 2, row = i / 2;
                var cell = Rect(_stats, "Cell", new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * 260, -row * 58), new Vector2(250, 54));
                Txt(cell, "L", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(250, 18), 13, TextAnchor.UpperLeft, Dim).text = r.label;
                Txt(cell, "V", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -18), new Vector2(120, 30), 26, TextAnchor.UpperLeft, Ink).text = r.v.ToString(r.fmt);
                float d = r.v - r.b;
                if (Mathf.Abs(d) > 0.05f)
                    Txt(cell, "D", new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -24), new Vector2(120, 24), 18, TextAnchor.UpperLeft, d > 0 ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.25f)).text = (d > 0 ? "+" : "") + d.ToString(r.fmt);
            }
            var w = Txt(_stats, "Weight", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(520, 24), 16, TextAnchor.LowerLeft, s.overweight ? new Color(1f, 0.3f, 0.25f) : Dim);
            w.text = $"WEIGHT {s.carried:0} / LOAD LIMIT {s.load:0}" + (s.overweight ? "   OVERWEIGHT: SPEED PENALTY" : "");
        }

        // ------------------------------------------------------------------ open / close
        public void Open()
        {
            IsOpen = true;
            _set.SetActive(true);
            _canvas.gameObject.SetActive(true);
            _camPos = _cam.transform.position; _camRot = _cam.transform.rotation; _camFov = _cam.fieldOfView;
            _savedFog = RenderSettings.fog; _savedFogDensity = RenderSettings.fogDensity;
            RenderSettings.fog = false;
            RebuildPreview();
            Refresh();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (_preview) Destroy(_preview);
            _set.SetActive(false);
            _canvas.gameObject.SetActive(false);
            RenderSettings.fog = _savedFog; RenderSettings.fogDensity = _savedFogDensity;
            _cam.transform.SetPositionAndRotation(_camPos, _camRot); _cam.fieldOfView = _camFov;
            Closed?.Invoke();
        }

        void LateUpdate()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed && !EventSystem.current.IsPointerOverGameObject()) _angle -= m.delta.ReadValue().x * 0.006f;
            else _angle += dt * 0.15f;
            var pad = Gamepad.current;
            if (pad != null) _angle -= pad.rightStick.ReadValue().x * dt * 2f;
            if (PlayerDriver.BackPressed) { Close(); return; }
            float r = 27f;
            Vector3 focus = Origin + Vector3.up * 4.6f;
            _cam.fieldOfView = 34f;
            _cam.transform.position = Origin + new Vector3(Mathf.Sin(_angle) * r, 7f, Mathf.Cos(_angle) * r);
            _cam.transform.LookAt(focus);
            // shift subject left of center so the panels don't cover it
            _cam.transform.position += _cam.transform.right * 2.2f;
        }
    }
}
