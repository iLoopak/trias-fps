using UnityEngine;

namespace Trias.Native
{
    // Cosmetic first-person motion that must look the same at 30, 60 or 240 fps. The weapon sway and
    // inertia used to follow per-frame deltas (mouse degrees per frame, velocity change per frame), so at
    // 144 fps a turn swung the weapon 2.4x less than at 60 fps and a 30 Hz prediction step arrived as a
    // one-frame spike. These filters take rates (degrees per second, metres per second squared) and scale
    // them back to the 60 fps per-frame values the weapon temperaments were tuned with. Nothing here feeds
    // aim, the Motor or the server.
    internal static class KineticView
    {
        internal const float Reference = 1f / 60f;

        // Exponential approach with a time-based rate: the same curve at every frame rate.
        internal static float Follow(float dt, float rate) => 1 - Mathf.Exp(-dt * rate);

        // Weapon swing from the view's angular velocity (degrees this frame over dt).
        internal static float Sway(float current, float degrees, float dt, float gain, float limit, float rate)
        {
            float target = Mathf.Clamp(degrees / Mathf.Max(dt, 1e-4f) * Reference * gain, -limit, limit);
            return current + (target - current) * Follow(dt, rate);
        }

        // Weapon lag from the body's acceleration in view space, held for the whole prediction step.
        internal static Vector3 Inertia(Vector3 current, Vector3 acceleration, float dt, float rate, float limit = 3)
        {
            var target = acceleration * Reference;
            float length = target.magnitude;
            if (length > limit) target *= limit / length;
            return current + (target - current) * Follow(dt, rate);
        }

        // Roll toward the strafe direction (degrees), from the velocity the view is showing this frame.
        internal static float Lean(float lateralSpeed, float limit = 1.35f) => Mathf.Clamp(lateralSpeed * -.15f, -limit, limit);
    }
}
