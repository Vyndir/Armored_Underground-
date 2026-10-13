using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AU
{
    /// <summary>Scene entry point: sets up the camera stack and atmosphere, then hands off to Game.</summary>
    public class Bootstrap : MonoBehaviour
    {
        public static Camera MainCam;

        void Awake()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.4f;
            cam.farClipPlane = 9000f;
            cam.fieldOfView = 62f;
            cam.allowHDR = true;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<PlanarReflection>();
            MainCam = cam;
            new GameObject("DayNight").AddComponent<DayNight>();
            var atmo = new GameObject("Atmosphere").AddComponent<Atmosphere>();
            atmo.follow = camGo.transform;
        }

        /// <summary>Camera positions that roughly match the concept art views.</summary>
        public static (string, Vector3, Vector3)[] TourShots(string prefix) => new[]
        {
            (prefix + "overview", new Vector3(-900, 520, -2300), new Vector3(300, 0, 300)),
            (prefix + "bridge_deck", new Vector3(-560, 44, 38), new Vector3(400, 70, 70)),
            (prefix + "hangar_street", new Vector3(-850, 4.5f, -1040), new Vector3(-850, 30, 400)),
            (prefix + "championship", new Vector3(1150, 90, -980), new Vector3(1500, 10, -1300)),
            (prefix + "downtown", new Vector3(950, 90, -520), new Vector3(1520, 110, 520)),
            (prefix + "ground_arena", new Vector3(-1300, 120, 900), new Vector3(-1510, 40, 1250)),
            (prefix + "flight", new Vector3(1300, 60, 1350), new Vector3(2100, 20, 1800)),
        };

        IEnumerator Start()
        {
            if (CaptureTour.Requested(out var dir))
            {
                CaptureTour.Dir = dir;
                var tour = gameObject.AddComponent<CaptureTour>();
                tour.cam = MainCam;
                var g = GetComponent<Game>();
                if (g) g.enabled = false;
                yield return null;
                DayNight.I.frozen = true;
                DayNight.I.SetHour(16.3f);
                yield return tour.Tour(TourShots("d_"));
                DayNight.I.SetHour(22.5f);
                yield return tour.Tour(TourShots("n_"));
                Application.Quit();
            }
        }
    }
}
