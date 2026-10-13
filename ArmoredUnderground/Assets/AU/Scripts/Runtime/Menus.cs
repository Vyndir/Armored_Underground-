using System;
using UnityEngine;
using UnityEngine.UI;
using static AU.UIKit;

namespace AU
{
    /// <summary>Title screen, pause menu, event card and results screen.</summary>
    public class Menus : MonoBehaviour
    {
        Canvas _canvas;
        public GameObject title, pause, eventCard, results;
        Text _evType, _evName, _evBlurb, _evMeta, _resTitle, _resEvent, _resTable, _resReward;
        Button _evStart, _retire;
        public Action OnStart, OnResume, OnRetire, OnReset, OnQuit, OnRace, OnCancelEvent, OnContinue;

        public void Build()
        {
            var go = new GameObject("Menus", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 30;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var root = Stretch(go.transform, "Root");

            // ---------------- title
            title = Stretch(root, "Title").gameObject;
            var shade = Img(title.transform, "Shade", new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(900, 1080), new Color(0, 0, 0, 0.55f));
            var t1 = Txt(title.transform, "T1", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 230), new Vector2(900, 120), 110, TextAnchor.MiddleLeft, Ink, FontStyle.BoldAndItalic);
            t1.text = "IRON";
            var t2 = Txt(title.transform, "T2", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 120), new Vector2(1100, 120), 110, TextAnchor.MiddleLeft, Neon, FontStyle.BoldAndItalic);
            t2.text = "ASCENDANT";
            Glow(t2, Neon, 4);
            Txt(title.transform, "Tag", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(116, 40), new Vector2(900, 40), 26, TextAnchor.MiddleLeft, Dim, FontStyle.Italic).text = "The championship comes to the bay. After midnight, the streets are yours.";
            Btn(title.transform, "IGNITE", new Vector2(0, 0.5f), new Vector2(316, -50), new Vector2(400, 64), () => OnStart?.Invoke(), Neon, 28);
            var ctrl = Txt(title.transform, "Controls", new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(116, -110), new Vector2(760, 300), 19, TextAnchor.UpperLeft, Dim, FontStyle.Normal);
            ctrl.text =
                "WASD  move           MOUSE  look / aim           SHIFT  boost (hold)\n" +
                "Q  quick boost       SPACE  jump, hold to hover\n" +
                "LMB  arm weapon      RMB  shoulder unit (missiles need lock)      F  pulse blade\n" +
                "X  overdrive (fill it by driving fast, near-misses, drifts, takedowns)\n" +
                "R  reload    E  interact    ESC  pause\n\n" +
                "Gamepad:  LS move · RS look · RB boost · B quick boost · A jump · RT fire · LT shoulder · X blade · LB overdrive · Y interact";

            // ---------------- pause
            pause = Panel(root, "Pause", 520, 460);
            Txt(pause.transform, "H", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(460, 60), 48, TextAnchor.MiddleCenter, Ink, FontStyle.BoldAndItalic).text = "PAUSED";
            Btn(pause.transform, "RESUME", new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(420, 58), () => OnResume?.Invoke());
            _retire = Btn(pause.transform, "RETIRE FROM RACE", new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(420, 58), () => OnRetire?.Invoke());
            Btn(pause.transform, "RESET CAREER", new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(420, 58), () => OnReset?.Invoke(), new Color(0.25f, 0.25f, 0.3f));
            Btn(pause.transform, "QUIT", new Vector2(0.5f, 1), new Vector2(0, -350), new Vector2(420, 58), () => OnQuit?.Invoke(), new Color(0.25f, 0.25f, 0.3f));

            // ---------------- event card
            eventCard = Panel(root, "Event", 720, 470);
            _evType = Txt(eventCard.transform, "Type", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -30), new Vector2(640, 24), 20, TextAnchor.UpperLeft, Hot);
            _evName = Txt(eventCard.transform, "Name", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -58), new Vector2(640, 60), 52, TextAnchor.UpperLeft, Ink, FontStyle.BoldAndItalic);
            _evBlurb = Txt(eventCard.transform, "Blurb", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -130), new Vector2(640, 50), 20, TextAnchor.UpperLeft, Dim, FontStyle.Normal);
            _evBlurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            _evMeta = Txt(eventCard.transform, "Meta", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -190), new Vector2(640, 160), 22, TextAnchor.UpperLeft, Ink, FontStyle.Normal);
            _evMeta.supportRichText = true;
            _evStart = Btn(eventCard.transform, "RACE  [ENTER]", new Vector2(0.5f, 0), new Vector2(-160, 60), new Vector2(300, 60), () => OnRace?.Invoke());
            Btn(eventCard.transform, "BACK  [ESC]", new Vector2(0.5f, 0), new Vector2(160, 60), new Vector2(300, 60), () => OnCancelEvent?.Invoke(), new Color(0.25f, 0.25f, 0.3f));

            // ---------------- results
            results = Panel(root, "Results", 760, 640);
            _resEvent = Txt(results.transform, "Event", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -30), new Vector2(680, 24), 20, TextAnchor.UpperLeft, Hot);
            _resTitle = Txt(results.transform, "Title", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -58), new Vector2(680, 70), 64, TextAnchor.UpperLeft, Ink, FontStyle.BoldAndItalic);
            _resTable = Txt(results.transform, "Table", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -150), new Vector2(680, 300), 24, TextAnchor.UpperLeft, Ink, FontStyle.Normal);
            _resTable.supportRichText = true;
            _resReward = Txt(results.transform, "Reward", new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 120), new Vector2(680, 40), 24, TextAnchor.LowerLeft, Amber);
            Btn(results.transform, "CONTINUE  [ENTER]", new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(400, 60), () => OnContinue?.Invoke());

            HideAll();
        }

        GameObject Panel(Transform root, string name, float w, float h)
        {
            var dim = Img(root, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.6f));
            var rt = dim.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            dim.raycastTarget = true;
            var p = Img(dim.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h), UIKit.Panel);
            Img(p.transform, "Top", new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(w, 4), Neon);
            return p.gameObject;
        }

        public void HideAll()
        {
            title.SetActive(false);
            pause.transform.parent.gameObject.SetActive(false);
            eventCard.transform.parent.gameObject.SetActive(false);
            results.transform.parent.gameObject.SetActive(false);
        }

        public void ShowTitle(bool v) => title.SetActive(v);
        public void ShowPause(bool v, bool inRace) { pause.transform.parent.gameObject.SetActive(v); _retire.gameObject.SetActive(inRace); }

        public void ShowEvent(CityLayout.RouteDef d, SaveData save)
        {
            eventCard.transform.parent.gameObject.SetActive(true);
            bool locked = save.rep < d.repRequired;
            var best = save.Result(d.id);
            _evType.text = d.type + (d.type == "CIRCUIT" ? $"  ·  {d.laps} LAPS" : "");
            _evName.text = d.name;
            _evBlurb.text = d.blurb;
            _evMeta.text = $"RIVALS  <b>{d.rivals}</b>\nFIRST PRIZE  <color=#FFB31A><b>{d.payout[0]:N0} CR</b></color>\nREP REQUIRED  <b>{d.repRequired}</b>\nBEST  <b>{(best != null ? $"P{best.bestPos}  {Hud.FormatTime(best.bestTime)}" : "-")}</b>"
                + (locked ? $"\n<color=#FF4A40>LOCKED: earn {d.repRequired - save.rep} more REP</color>" : "");
            _evStart.gameObject.SetActive(!locked);
        }
        public void HideEvent() => eventCard.transform.parent.gameObject.SetActive(false);

        public void ShowResults(Race race)
        {
            results.transform.parent.gameObject.SetActive(true);
            var r = race.result;
            string[] ord = { "1ST", "2ND", "3RD", "4TH", "5TH", "6TH" };
            _resEvent.text = $"{race.def.type}  ·  {race.def.name}";
            _resTitle.text = r.position == 1 ? "YOU WIN" : $"{ord[r.position - 1]} PLACE";
            _resTitle.color = r.position == 1 ? new Color(0.3f, 1f, 0.4f) : Ink;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < r.order.Count; i++)
            {
                var x = r.order[i];
                string name = x == race.player ? "<color=#00E5FF>YOU</color>" : x.mech.pilot;
                sb.AppendLine($"{i + 1}.  {name}      {(x.finished ? Hud.FormatTime(x.time) : "DNF")}      {x.takedowns} KO");
            }
            _resTable.text = sb.ToString();
            _resReward.text = $"+{r.credits:N0} CR prize" + (r.bonus > 0 ? $"   +{r.bonus:N0} CR takedowns" : "") + $"   +{r.rep} REP";
        }
        public void HideResults() => results.transform.parent.gameObject.SetActive(false);
        public bool EventOpen => eventCard.transform.parent.gameObject.activeSelf;
        public bool ResultsOpen => results.transform.parent.gameObject.activeSelf;
    }
}
