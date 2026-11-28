using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        static readonly Color RippleTint = Catalog.Tint("#8eebff");
        static readonly Color PadPulseTint = Catalog.Tint("#63f2df");
        float nextSurfacePulse, nextBracedNotice;
        bool sawInterceptHint;

        void HintAtGrenade(Vector3 position)
        {
            if (sawInterceptHint || !Playing || !state["interactions"]["intercept"].Bool || Time.unscaledTime < toastUntil ||
                own[15].Int >= 0 || own[16].Int >= 0 || own[9].Text == "rift") return;
            Vector3 toward = position - view.transform.position;
            float distance = toward.magnitude;
            if (distance < 1 || distance > 12 || Vector3.Dot(view.transform.forward, toward / distance) < .88f ||
                Physics.Raycast(view.transform.position, toward / distance, distance - .15f,
                    TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore)) return;
            sawInterceptHint = true;
            AddNotice("Nepřátelský granát můžeš sestřelit");
        }

        void PresentRecall(WireValue evt, string actorId, bool local, float now)
        {
            if (local)
            {
                echoAwaitUntil = 0;
                if (own[6].Int > 0) SnapDisplay();
                movementPulse = .38f;
                AddNotice("Návrat k ozvěně");
            }
            else if (actorId != null && pilots.TryGetValue(actorId, out var avatar))
            {
                avatar.Snap(now);
            }
            Color tint = local ? teamTint : Catalog.Tint(catalog.teams[evt["team"].Int].color);
            effects.EchoRecall(At(evt["from"]), At(evt["to"]), tint, local, visualEffects);
        }

        void ReactToSurface(Vector3 at, float now, bool explosive)
        {
            if (visualEffects <= .01f || now < nextSurfacePulse || (at - display).sqrMagnitude > 55 * 55) return;
            world.ReactMegastructure(at,now);
            if (world.Spec.id == "island")
            {
                float height = world.Height(at.x, at.z);
                if (!Motor.Shallow(at, world.Spec) || Mathf.Abs(at.y - height) > (explosive ? .95f : .11f)) return;
                float sheet = Mathf.Max(WaterSheet(at, world.Spec.river, false), WaterSheet(at, world.Spec.creek, true));
                if (float.IsNegativeInfinity(sheet)) return;
                at.y = Mathf.Max(height, sheet) + .065f;
                effects.SurfacePulse(at, RippleTint, false, visualEffects, explosive);
                nextSurfacePulse = now + .13f;
                return;
            }
            foreach (var pad in world.Spec.launchPads)
            {
                float dx = at.x - pad.x, dz = at.z - pad.z;
                float reach = pad.radius + (explosive ? 2 : .22f);
                if (Mathf.Abs(at.y - pad.y) > (explosive ? 1.65f : .28f) || dx * dx + dz * dz > reach * reach) continue;
                effects.SurfacePulse(pad.Position + Vector3.up * .12f, PadPulseTint, true, visualEffects, explosive);
                nextSurfacePulse = now + .16f;
                return;
            }
        }

        static float WaterSheet(Vector3 at, Site[] strip, bool creek)
        {
            float axis = creek ? at.z : at.x;
            for (int i = 1; i < strip.Length; i++)
            {
                Site a = strip[i - 1], b = strip[i];
                float start = creek ? a.z : a.x, end = creek ? b.z : b.x;
                if (axis < Mathf.Min(start, end) || axis > Mathf.Max(start, end) || Mathf.Approximately(start, end)) continue;
                float t = (axis - start) / (end - start);
                float cross = creek ? Mathf.Lerp(a.x, b.x, t) - at.x : Mathf.Lerp(a.z, b.z, t) - at.z;
                if (Mathf.Abs(cross) <= (creek ? .49f : 1.83f)) return Mathf.Lerp(a.y, b.y, t) + .038f;
            }
            return float.NegativeInfinity;
        }
    }
}
