using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace AU
{
    /// <summary>
    /// Automated screenshot tour for visual checks: run the player with
    /// "-capture &lt;dir&gt;" and it flies a camera through set shots, saves PNGs, and quits.
    /// "-autoplay" instead drives the game's own systems (see Game).
    /// </summary>
    public class CaptureTour : MonoBehaviour
    {
        public static string Dir;
        public Camera cam;

        public static bool Requested(out string dir)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-capture") { dir = args[i + 1]; return true; }
            dir = null; return false;
        }

        public static IEnumerator Shot(string name, int settleFrames = 30)
        {
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Dir);
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[Capture] " + name);
        }

        public IEnumerator Tour((string name, Vector3 pos, Vector3 look)[] shots)
        {
            foreach (var s in shots)
            {
                cam.transform.position = s.pos;
                cam.transform.LookAt(s.look);
                yield return Shot(s.name, 40);
            }
        }
    }
}
