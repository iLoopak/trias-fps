using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        float baseFov, cameraMotion, masterVolume, ambienceVolume;
        float gunTravel, gunTravelVelocity, gunLift, gunLiftVelocity, gunTwist, gunTwistVelocity;
        float cameraKick, cameraKickVelocity, landingStep, landingVelocity, airSpeed;
        float previousPitch, previousYaw, swayX, swayY;
        float sceneFade, resultAge;
        int kickShot;
        bool wasGrounded;
        string roundResult;
        GUIStyle optionTrack, optionThumb;

        void LoadFeel()
        {
            baseFov = Mathf.Clamp(PlayerPrefs.GetFloat("trias.fov", 70), 60, 95);
            cameraMotion = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.motion", .82f));
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.volume", .85f));
            ambienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.ambience", .72f));
            sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("trias.sensitivity", sensitivity), .5f, 5);
            AudioListener.volume = masterVolume;
        }

        void ResetFeel()
        {
            gunTravel = gunTravelVelocity = gunLift = gunLiftVelocity = gunTwist = gunTwistVelocity = 0;
            cameraKick = cameraKickVelocity = landingStep = landingVelocity = airSpeed = 0;
            previousPitch = pitch;
            previousYaw = yaw;
            swayX = swayY = 0;
            kickShot = 0;
            wasGrounded = false;
        }

        void KickWeapon(string key)
        {
            var profile = key switch
            {
                "scatter" => new Vector3(.108f, 14, 3.2f),
                "lance" => new Vector3(.130f, 10.5f, 1.7f),
                "rift" => new Vector3(.154f, 12.9f, 3.9f),
                "arc" => new Vector3(.032f, 4.0f, .92f),
                _ => new Vector3(.052f, 6.5f, 1.15f)
            };
            int sign = (kickShot++ & 1) == 0 ? -1 : 1;
            gunTravel = Mathf.Min(.21f, gunTravel + profile.x);
            gunLift = Mathf.Min(19, gunLift + profile.y);
            gunTwist = Mathf.Clamp(gunTwist + profile.z * sign, -5.5f, 5.5f);
            cameraKick = Mathf.Min(2.1f, cameraKick + profile.y * .085f);
        }

        void UpdateFeel(float now, float dt, bool driving)
        {
            dt = Mathf.Min(.05f, dt);
            float speed = new Vector2(predicted.Velocity.x, predicted.Velocity.z).magnitude;
            if (!predicted.Grounded) airSpeed = Mathf.Min(airSpeed, predicted.Velocity.y);
            else if (!wasGrounded)
            {
                if (airSpeed < -4.3f && !driving)
                {
                    landingStep = Mathf.Min(.125f, -airSpeed * .010f);
                    effects.Land(view.transform.position, Mathf.Clamp01(-airSpeed / 14));
                }
                airSpeed = 0;
            }
            wasGrounded = predicted.Grounded;
            gunTravel = Mathf.SmoothDamp(gunTravel, 0, ref gunTravelVelocity, .105f, Mathf.Infinity, dt);
            gunLift = Mathf.SmoothDamp(gunLift, 0, ref gunLiftVelocity, .133f, Mathf.Infinity, dt);
            gunTwist = Mathf.SmoothDamp(gunTwist, 0, ref gunTwistVelocity, .165f, Mathf.Infinity, dt);
            cameraKick = Mathf.SmoothDamp(cameraKick, 0, ref cameraKickVelocity, .112f, Mathf.Infinity, dt);
            landingStep = Mathf.SmoothDamp(landingStep, 0, ref landingVelocity, .15f, Mathf.Infinity, dt);
            float damping = 1 - Mathf.Exp(-dt * 17);
            swayX = Mathf.Lerp(swayX, Mathf.Clamp(Mathf.DeltaAngle(previousYaw, yaw) * -.48f, -5.5f, 5.5f), damping);
            swayY = Mathf.Lerp(swayY, Mathf.Clamp(Mathf.DeltaAngle(previousPitch, pitch) * -.38f, -4.5f, 4.5f), damping);
            previousYaw = yaw; previousPitch = pitch;
            float reload = (own[2].Int & 64) == 0 ? 0 : Mathf.Sin(Mathf.Clamp01(own[30].Float / Mathf.Max(.1f, reloadPeriod)) * Mathf.PI);
            float motion = cameraMotion;
            float stride = predicted.Grounded ? Mathf.Sin(now * 13) * Mathf.Min(speed * .0033f, .029f) : 0;
            float lean = driving ? 0 : Mathf.Clamp(Vector3.Dot(predicted.Velocity, view.transform.right) * -.20f, -1.55f, 1.55f);
            Quaternion angle = Quaternion.Euler(pitch - cameraKick * motion, yaw, lean * motion);
            if (driving) Chase(display + Vector3.up * 1.45f, angle * Vector3.forward, 5.5f, 2.1f);
            else
                view.transform.SetPositionAndRotation(display + Vector3.up * ((predicted.Crouch ? .92f : 1.58f)
                    + (stride - landingStep) * motion), angle);
            grip.localPosition = new Vector3(.365f + swayX * .0037f * motion + stride * .68f,
                -.305f - reload * .16f - landingStep * .48f + stride, .60f - gunTravel);
            grip.localRotation = Quaternion.Euler(-gunLift + reload * 48 + swayY * motion,
                -4 + swayX * motion, gunTwist + reload * 27);
            float fov = baseFov + (driving ? 3 : Mathf.Clamp01((speed - 6) / 4f) * 5 * motion);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, fov, 1 - Mathf.Exp(-dt * 6));
        }

        void UpdateResult(float dt)
        {
            string current = state["winner"]["label"].Text;
            if (current != roundResult)
            {
                roundResult = current;
                resultAge = 0;
                if (current != null)
                {
                    effects.Wave(false);
                    world.SurgeLandmark();
                }
            }
            resultAge += dt;
            sceneFade = Mathf.MoveTowards(sceneFade, 0, dt * 3.4f);
        }

        void PaintResult(float cx, float h)
        {
            float age = Mathf.Clamp01(resultAge / .34f);
            float y = h * .68f + (1 - age) * (1 - age) * 29;
            var tint = mode == "horde" && state["horde"]["core"]["hp"].Int == 0 ? Fault : Amber;
            var plate = new Rect(cx - 258, y, 516, 94);
            CornerPanel(plate, tint);
            DrawInsignia(new Vector2(plate.x + 43, y + 48), 21, tint);
            GUI.Label(new Rect(plate.x + 74, y + 12, 420, 34), roundResult ?? state["winner"]["label"].Text, heading);
            GUI.Label(new Rect(plate.x + 76, y + 52, 420, 24), "VYŘAZENÍ  " + own[11].Int + "     ·     DALŠÍ KOLO  " + state["restart"].Int + " s", caption);
            Segments(new Rect(plate.x + 77, y + 77, 399, 3), 15, state["restart"].Float / 9, tint);
        }

        void PaintPause(float w, float h)
        {
            float x = w / 2 - 242, y = h / 2 - 205;
            Fill(new Rect(0,0,w,h),new Color(.012f,.024f,.041f,.80f));
            CornerPanel(new Rect(x, y, 484, 410), Amber);
            DrawInsignia(new Vector2(x + 37, y + 35), 17, Amber);
            GUI.Label(new Rect(x + 62, y + 16, 388, 38), "Nastavení", heading);
            GUI.Label(new Rect(x + 24, y + 64, 436, 27), "Online zápas běží dál", caption);
            var box = new Rect(x + 22, y + 94, 440, 34);
            Option(box, "Zorné pole", "trias.fov", ref baseFov, 60, 95, false);
            box.y += 34; Option(box, "Citlivost myši", "trias.sensitivity", ref sensitivity, .5f, 5, false, 1);
            box.y += 34; Option(box, "Pohyb kamery", "trias.motion", ref cameraMotion, 0, 1, true);
            box.y += 34; Option(box, "Hlasitost", "trias.volume", ref masterVolume, 0, 1, true);
            box.y += 34; Option(box, "Atmosféra", "trias.ambience", ref ambienceVolume, 0, 1, true);
            AudioListener.volume = masterVolume;
            effects.AmbienceMix(ambienceVolume, false);
            if (Preset(new Rect(x + 22, y + 277, 440, 42), "Zpět do zápasu", null, true)) Capture(true);
            if (Preset(new Rect(x + 22, y + 327, 440, 41), "Opustit místnost", null, false)) Disconnect(null);
            GUI.Label(new Rect(x + 24, y + 376, 430, 22), "WASD  ·  Shift  ·  mezerník  ·  Q úhyb  ·  R přebít  ·  E použít", caption);
        }

        void Option(Rect row, string name, string key, ref float value, float min, float max, bool percent, int decimals = 0)
        {
            GUI.Label(new Rect(row.x, row.y + 1, 145, 28), name, section);
            optionTrack ??= new GUIStyle { fixedHeight = 19 };
            optionThumb ??= new GUIStyle { fixedWidth = 12, fixedHeight = 19 };
            var control = new Rect(row.x + 156, row.y + 7, 219, 19);
            float next = GUI.HorizontalSlider(control, value, min, max, optionTrack, optionThumb);
            if (Mathf.Abs(next - value) > .0001f)
            {
                value = next;
                PlayerPrefs.SetFloat(key, value);
            }
            float rail = Mathf.Lerp(control.x + 6, control.xMax - 6, Mathf.InverseLerp(min, max, value));
            Fill(new Rect(control.x, control.center.y - 1, control.width, 2), Subtle);
            Fill(new Rect(control.x, control.center.y - 1, rail - control.x, 2), Amber);
            Fill(new Rect(rail - 3, control.center.y - 5, 6, 10), control.Contains(Event.current.mousePosition) ? Chalk : Amber);
            string readout = percent ? Mathf.RoundToInt(value * 100) + "%" : value.ToString(decimals == 0 ? "F0" : "F1");
            if (key == "trias.fov") readout += "°";
            GUI.Label(new Rect(row.x + 385, row.y + 2, 54, 26), readout, caption);
        }
    }
}
