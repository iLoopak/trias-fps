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
    // Composition order: displayed body position (last prediction step -> next step `peek`, plus the fading
    // reconciliation correction) -> eye height (stance) -> head bob and landing dip -> ceiling clamp -> aim
    // rotation -> optional roll (KineticView.Lean x Tuning.Roll) -> FOV. The crosshair is drawn at the screen
    // centre, which is the aim direction. Frame-rate independent weapon sway and inertia live in KineticView.
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

        // Reconciliation (kinetic flow): the prediction jumps to the corrected state at once and the visible
        // difference is kept in a separate offset that fades with this time constant. A disagreement of
        // CorrectionSnap or more (teleport, respawn race) is shown at once.
        internal const float CorrectionFade = .065f, CorrectionSnap = 1.5f;

        // How far the view is between the last prediction step and the next one (`peek`).
        internal static float StepFraction(float stepAt, float now)
            => stepAt < 0 ? 0 : Mathf.Clamp01((now - stepAt) / Motor.Tick);

        internal static Vector3 FadeCorrection(Vector3 correction, float dt)
            => correction * Mathf.Exp(-Mathf.Max(0, dt) / CorrectionFade);

        internal static Vector3 AddCorrection(Vector3 correction, Vector3 before, Vector3 after)
            => Vector3.Distance(before, after) < CorrectionSnap ? correction + before - after : Vector3.zero;

        internal static float Fov(in Tuning tuning, float baseFov, float speed, float pulse, float kick, float motion, bool driving)
            => baseFov + (driving ? 3 : (Mathf.Clamp01((speed - 6) / 4f) * tuning.FovSpeed + pulse * tuning.FovPulse + kick * tuning.FovKick) * motion);

        internal static float FollowFov(float shown, float target, float dt) => Mathf.Lerp(shown, target, 1 - Mathf.Exp(-dt * 6));

        // Crosshair centre in GUI units, snapped to a whole screen pixel so its 2 px core stays sharp.
        internal static Vector2 Reticle(float w, float h, float scale)
            => new Vector2(Mathf.Round(w * .5f * scale) / scale, Mathf.Round(h * .5f * scale) / scale);
    }
}
