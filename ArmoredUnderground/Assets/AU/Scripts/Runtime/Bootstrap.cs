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
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1200f;
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
            var atmo = new GameObject("Atmosphere").AddComponent<Atmosphere>();
            atmo.follow = camGo.transform;
        }

        IEnumerator Start()
        {
            if (CaptureTour.Requested(out var dir))
            {
                CaptureTour.Dir = dir;
                var tour = gameObject.AddComponent<CaptureTour>();
                tour.cam = MainCam;
                var g = GetComponent<Game>();
                if (g) g.enabled = false;
                yield return tour.Tour(new[]
                {
                    ("street_boulevard", new Vector3(4, 3.5f, -120), new Vector3(0, 6, 40)),
                    ("street_low", new Vector3(-8, 1.2f, 70), new Vector3(30, 4, 80)),
                    ("intersection", new Vector3(-20, 12, -70), new Vector3(0, 0, -40)),
                    ("garage", new Vector3(-60, 6, -70), new Vector3(-60, 6, -20)),
                    ("aerial", new Vector3(-250, 160, -380), new Vector3(0, 0, 0)),
                });
                Application.Quit();
            }
        }
    }
}
