using UnityEngine;

// Editor checks (Trias.Preview.ViewStabilityCheck) exercise the same composition code as the match.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace Trias.Native
{
    // First-person camera composition. TriasClient.UpdateFeel is the only writer of the match camera's
    // pose and FOV while the pilot is alive; it builds them every frame from a stable base (the displayed
    // eye and the aim angles read from the mouse that frame) plus independent cosmetic terms computed here.
    // Nothing reads the previous frame's camera transform, so offsets cannot accumulate.
    //
    // Composition order: displayed body position (prediction steps interpolated + fading reconciliation
    // correction) -> eye height (stance) -> head bob and landing dip -> ceiling clamp -> aim rotation ->
    // optional roll -> FOV. The crosshair is drawn at the screen centre, which is the aim direction.
    internal static class ViewComposer
    {
        internal enum Style { Competitive, Cinematic }

        internal readonly struct Tuning
        {
            internal readonly float Roll, FovSpeed, FovPulse, FovKick, HeadBob;
            internal Tuning(float roll, float fovSpeed, float fovPulse, float fovKick, float headBob)
            {
                Roll = roll; FovSpeed = fovSpeed; FovPulse = fovPulse; FovKick = fovKick; HeadBob = headBob;
            }
        }

        // Competitive keeps the camera a stable aiming platform: no strafe roll, no FOV pumping from recoil,
        // a small sprint/dodge FOV and half the head bob. Cinematic is the earlier full-motion presentation.
        // Both are scaled by the "Pohyb kamery" slider.
        internal static Tuning For(Style style) => style == Style.Cinematic
            ? new Tuning(1, 3.5f, 2.1f, 1, 1)
            : new Tuning(0, 1.5f, .9f, 0, .5f);

        // Reconciliation: a server correction moves the prediction at once, and the visible difference fades
        // with this time constant. Corrections larger than CorrectionSnap (knockback, pads) are shown at once.
        internal const float CorrectionFade = .08f, CorrectionSnap = 2f;

        // The view follows a line between the last two 30 Hz prediction steps, timed on the step schedule.
        // This is the only interpolation of the own position; it adds at most one input step of delay.
        internal static Vector3 Stepped(Vector3 from, Vector3 to, float stepAt, float now)
            => stepAt < 0 ? to : Vector3.Lerp(from, to, Mathf.Clamp01((now - stepAt) / Motor.Tick));

        internal static Vector3 FadeCorrection(Vector3 correction, float dt)
            => correction * Mathf.Exp(-Mathf.Max(0, dt) / CorrectionFade);

        // shift = new prediction - old prediction. The interpolation line moves with the prediction and the
        // view keeps its place for this frame; the difference then fades out.
        internal static Vector3 AddCorrection(Vector3 correction, Vector3 shift)
        {
            var next = correction - shift;
            return next.sqrMagnitude > CorrectionSnap * CorrectionSnap ? Vector3.zero : next;
        }

        internal static float Roll(in Tuning tuning, Vector3 velocity, Quaternion aim, float motion, bool driving)
            => driving ? 0 : Mathf.Clamp(Vector3.Dot(velocity, aim * Vector3.right) * -.15f, -1.35f, 1.35f) * tuning.Roll * motion;

        internal static float Fov(in Tuning tuning, float baseFov, float speed, float pulse, float kick, float motion, bool driving)
            => baseFov + (driving ? 3 : (Mathf.Clamp01((speed - 6) / 4f) * tuning.FovSpeed + pulse * tuning.FovPulse + kick * tuning.FovKick) * motion);

        internal static float FollowFov(float shown, float target, float dt) => Mathf.Lerp(shown, target, 1 - Mathf.Exp(-dt * 6));

        // Mouse turn per 1/60 s, so weapon sway has the same size at every frame rate.
        internal static float TurnRate(float previous, float current, float dt)
            => Mathf.DeltaAngle(previous, current) / Mathf.Max(dt, 1e-4f) / 60f;

        // Velocity change per input step, so weapon inertia has the same size at every frame rate.
        internal static Vector3 StepAcceleration(Vector3 velocity, Vector3 last, float dt)
            => Vector3.ClampMagnitude((velocity - last) * (Motor.Tick / Mathf.Max(dt, 1e-4f)), 3);

        // Crosshair centre in GUI units, snapped to a whole screen pixel so its 2 px core stays sharp.
        internal static Vector2 Reticle(float w, float h, float scale)
            => new Vector2(Mathf.Round(w * .5f * scale) / scale, Mathf.Round(h * .5f * scale) / scale);
    }
}
