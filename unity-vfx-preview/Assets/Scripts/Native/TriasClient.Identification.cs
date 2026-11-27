using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        Avatar identified;

        void IdentifyPilot(Vector3 eye, Vector3 ray)
        {
            identified = null;
            if (!captured || scoreboard || building || own[15].Int >= 0 || own[16].Int >= 0) return;
            float horizontal = ray.x * ray.x + ray.z * ray.z;
            if (horizontal < .05f) return;
            float limit = Mathf.Min(34, Vector3.Distance(eye, reticleWorld));
            foreach (var candidate in pilots.Values)
            {
                var row = candidate.Row;
                if (row[6].Int <= 0 || row[16].Int >= 0 || !candidate.Root.gameObject.activeSelf) continue;
                var offset = candidate.Position - eye;
                float along = (offset.x * ray.x + offset.z * ray.z) / horizontal;
                if (along < .75f || along >= limit) continue;
                float dx = offset.x - along * ray.x, dz = offset.z - along * ray.z;
                if (dx * dx + dz * dz > .39f * .39f) continue;
                float y = eye.y + along * ray.y - candidate.Position.y;
                if (y < .08f || y > ((row[2].Int & 16) != 0 ? 1.08f : 1.71f)) continue;
                if (Physics.Linecast(view.transform.position, eye + along * ray, TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore)) continue;
                identified = candidate;
                limit = along;
            }
        }

        void PaintPilotIdentification(Vector2 reticle)
        {
            if (identified == null || identified.Row[6].Int <= 0 || own[15].Int >= 0 || own[16].Int >= 0 || building) return;
            var row = identified.Row;
            bool foe = row[31].Text != null;
            int faction = row[3].Int;
            bool teams = mode != "ffa" && !foe;
            bool allied = teams && faction == own[3].Int;
            var tint = PlayerTint(row);
            var plate = new Rect(reticle.x - 104, reticle.y - 52, 208, 24);
            HudStrip(plate, tint);
            if (teams) DrawFaction(new Vector2(plate.x + 14, plate.y + 12), 6, faction, tint);
            string label = allied ? "SPOJENEC · " + row[1].Text
                : teams ? catalog.teams[faction].name + " · " + row[1].Text : row[1].Text;
            hudText.Draw(new Rect(plate.x + (teams ? 25 : 9), plate.y, plate.width - (teams ? 31 : 15), plate.height), label, caption);
        }
    }
}
