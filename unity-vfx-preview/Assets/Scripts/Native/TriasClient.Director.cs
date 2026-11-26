using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        int arenaSeed, arenaSecond = -1;
        float arenaSampleAt;
        bool arenaPreparation;
        ArenaRhythm arenaRhythm;

        void SyncDirector(WireValue frame, float now)
        {
            if (frame.Count != 3)
            {
                arenaSecond = -1;
                arenaRhythm = default;
                effects.MixArenaRhythm(0);
                return;
            }
            if (arenaSeed != frame[0].Int || arenaSecond != frame[1].Int)
            {
                arenaSeed = frame[0].Int;
                arenaSecond = frame[1].Int;
                arenaSampleAt = now;
            }
            arenaPreparation = frame[2].Int == 1;
        }

        void UpdateArenaRhythm(float now)
        {
            if (arenaSecond < 0) return;
            arenaRhythm = ArenaRhythm.At(arenaSeed, arenaSecond + Mathf.Clamp01(now - arenaSampleAt), arenaPreparation);
            effects.MixArenaRhythm(arenaRhythm.Level);
        }

        void PaintArenaRhythm(float cx)
        {
            if (!captured || scoreboard || arenaRhythm.Warning == 0 || own[6].Int <= 0 ||
                Time.unscaledTime < deploymentUntil || state["winner"]["label"].Text != null) return;
            string cycle = world.Spec.id switch
            {
                "foundry" => "Cyklus tavicí komory",
                "basin" => "Přelet těžebního konvoje",
                "island" => "Poryv nad ostrovem",
                _ => "Cyklus orbitálního relé"
            };
            var strip = new Rect(cx - 151, 83, 302, 24);
            CornerPanel(strip, Subtle);
            hudText.Draw(strip, $"{cycle} za {arenaRhythm.Warning} s", center);
        }
    }
}
