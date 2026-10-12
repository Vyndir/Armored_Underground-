using UnityEngine;
using UnityEngine.InputSystem;

namespace AU
{
    /// <summary>Reads keyboard/mouse or gamepad, steers the player mech relative to the
    /// camera and handles soft lock-on.</summary>
    public class PlayerDriver : MonoBehaviour
    {
        public MechController mech;
        public ChaseCamera cam;
        public MechController lockTarget;
        public bool inputEnabled = true;
        public System.Func<System.Collections.Generic.IEnumerable<MechController>> Targets;

        public static bool InteractPressed => (Keyboard.current?.eKey.wasPressedThisFrame ?? false) || (Gamepad.current?.buttonNorth.wasPressedThisFrame ?? false);
        public static bool PausePressed => (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) || (Gamepad.current?.startButton.wasPressedThisFrame ?? false);
        public static bool ConfirmPressed => (Keyboard.current?.enterKey.wasPressedThisFrame ?? false) || (Keyboard.current?.spaceKey.wasPressedThisFrame ?? false) || (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false);
        public static bool BackPressed => (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) || (Gamepad.current?.buttonEast.wasPressedThisFrame ?? false);

        void Update()
        {
            if (!mech || !cam) return;
            var c = new MechCommand { face = cam.yaw };
            var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
            if (inputEnabled)
            {
                Vector2 mv = Vector2.zero;
                if (kb != null)
                {
                    if (kb.wKey.isPressed) mv.y += 1; if (kb.sKey.isPressed) mv.y -= 1;
                    if (kb.dKey.isPressed) mv.x += 1; if (kb.aKey.isPressed) mv.x -= 1;
                    c.boost |= kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                    c.jumpHeld |= kb.spaceKey.isPressed;
                    c.jumpPressed |= kb.spaceKey.wasPressedThisFrame;
                    c.quickBoost |= kb.qKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame;
                    c.reload |= kb.rKey.wasPressedThisFrame;
                    c.blade |= kb.fKey.wasPressedThisFrame;
                    c.overdrive |= kb.xKey.wasPressedThisFrame || kb.leftAltKey.wasPressedThisFrame;
                }
                if (mouse != null)
                {
                    c.fire |= mouse.leftButton.isPressed;
                    c.fire2 |= mouse.rightButton.isPressed;
                    c.overdrive |= mouse.middleButton.wasPressedThisFrame;
                }
                if (pad != null)
                {
                    Vector2 ls = pad.leftStick.ReadValue();
                    if (ls.sqrMagnitude > 0.04f) mv += ls;
                    c.boost |= pad.rightShoulder.isPressed;
                    c.jumpHeld |= pad.buttonSouth.isPressed;
                    c.jumpPressed |= pad.buttonSouth.wasPressedThisFrame;
                    c.quickBoost |= pad.buttonEast.wasPressedThisFrame;
                    c.blade |= pad.buttonWest.wasPressedThisFrame;
                    c.overdrive |= pad.leftShoulder.wasPressedThisFrame || pad.leftStickButton.wasPressedThisFrame;
                    c.fire |= pad.rightTrigger.ReadValue() > 0.4f;
                    c.fire2 |= pad.leftTrigger.ReadValue() > 0.4f;
                    c.reload |= pad.dpad.down.wasPressedThisFrame;
                }
                if (mv.sqrMagnitude > 1) mv.Normalize();
                Vector3 f = Quaternion.Euler(0, cam.yaw, 0) * Vector3.forward;
                Vector3 r = Quaternion.Euler(0, cam.yaw, 0) * Vector3.right;
                c.move = f * mv.y + r * mv.x;
            }

            FindLock();
            c.target = lockTarget;
            var w = mech.stats.weapon;
            if (lockTarget)
            {
                float t = Vector3.Distance(lockTarget.Center, mech.Center) / Mathf.Max(100f, w.speedMps);
                c.aimPoint = lockTarget.Center + lockTarget.velocity * t;
            }
            else
            {
                var ray = cam.Cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                c.aimPoint = Physics.Raycast(ray, out var hit, 800f, ~(1 << 6), QueryTriggerInteraction.Ignore) && hit.distance > 6f ? hit.point : ray.GetPoint(800f);
            }
            Vector3 to = c.aimPoint - (mech.Center + Vector3.up * 1.5f);
            c.aimPitch = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            mech.cmd = c;
        }

        void FindLock()
        {
            if (Targets == null) { lockTarget = null; return; }
            var camT = cam.Cam.transform;
            MechController best = null;
            float bestAng = 9f;
            foreach (var m in Targets())
            {
                if (!m || m == mech || m.IsDown) continue;
                float d = Vector3.Distance(m.Center, mech.Center);
                if (d > mech.stats.lockRange) continue;
                float ang = Vector3.Angle(camT.forward, m.Center - camT.position);
                float limit = m == lockTarget ? 15f : bestAng;
                if (ang > limit) continue;
                if (Physics.Linecast(mech.Center + Vector3.up * 2f, m.Center, out var h, ~(1 << 6), QueryTriggerInteraction.Ignore)) continue;
                best = m; bestAng = Mathf.Min(ang, bestAng);
            }
            if (best && best != lockTarget) AudioFX.I?.UI("ui", 0.4f);
            lockTarget = best;
        }
    }
}
