using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        static readonly string[] Perks = { "scan", "module", "strike", "overdrive" };
        static readonly string[] PerkNames = { "Sken", "Modul", "Úder", "Pohon" };
        static readonly int[] PerkGoals = { 3, 5, 7, 9 };
        static readonly KeyCode[] PerkKeys = { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9 };
        readonly Dictionary<int, (Transform marker, LineRenderer circle)> orbitalWarnings = new Dictionary<int, (Transform, LineRenderer)>();
        readonly HashSet<int> activeWarnings = new HashSet<int>();
        readonly List<int> previousWarnings = new List<int>();
        Material orbitalAmber;
        int streakCount, perkMask;
        float scanLeft;
        bool streaksAvailable;

        void ReadPerks()
        {
            for (int i = 0; i < Perks.Length; i++)
            {
                if ((perkMask & (1 << i)) == 0 || !Input.GetKeyDown(PerkKeys[i])) continue;
                socket.Send(FormattableString.Invariant($"{{\"type\":\"perk\",\"perk\":\"{Perks[i]}\",\"yaw\":{WebYaw:F5},\"pitch\":{WebPitch:F5}}}"));
            }
        }

        void ResetPerks()
        {
            orbitalWarnings.Clear();
            activeWarnings.Clear();
            previousWarnings.Clear();
            orbitalAmber = null;
            streakCount = perkMask = 0;
            scanLeft = 0;
            streaksAvailable = false;
        }

        void SyncPerks(WireValue frame)
        {
            streaksAvailable = frame["charges"].Items != null;
            if (!streaksAvailable) return;
            streakCount = perkMask = 0;
            scanLeft = 0;
            foreach (var entry in frame["charges"].Items)
            {
                if (entry[0].Text != identity) continue;
                streakCount = entry[1].Int;
                perkMask = entry[2].Int;
                break;
            }
            string scope = mode == "ffa" ? identity : own[3].Int.ToString();
            foreach (var scan in frame["scans"].Items)
                if (scan[0].Text == scope) scanLeft = scan[1].Float;

            activeWarnings.Clear();
            foreach (var zone in frame["strikes"].Items)
            {
                int id = zone[0].Int;
                activeWarnings.Add(id);
                if (!orbitalWarnings.TryGetValue(id, out var warning))
                {
                    if (orbitalAmber == null) orbitalAmber = world.MaterialAt("#ff7d49", true);
                    var at = new Vector3(zone[1].Float, zone[2].Float + .09f, zone[3].Float);
                    var marker = TriasWorld.Group(world.Root, "Orbitální úder", at);
                    var ring = TriasWorld.Ring(marker, Vector3.zero, zone[5].Float, orbitalAmber, .12f, 36);
                    TriasWorld.Line(marker, Vector3.zero, Vector3.up * 13, orbitalAmber, .035f);
                    orbitalWarnings.Add(id, (marker, ring));
                }
                else warning.circle.widthMultiplier = .10f + .10f * (1 - Mathf.Clamp01(zone[4].Float / 2.15f));
            }
            previousWarnings.Clear();
            foreach (var pair in orbitalWarnings) if (!activeWarnings.Contains(pair.Key)) previousWarnings.Add(pair.Key);
            foreach (int id in previousWarnings)
            {
                Destroy(orbitalWarnings[id].marker.gameObject);
                orbitalWarnings.Remove(id);
            }
        }

        string ModuleName()
        {
            return own[5].Text switch
            {
                "bulwark" => "Štít",
                "wraith" => "Munice",
                "kestrel" => "Granáty",
                _ => "Léčení"
            };
        }

        readonly TextMemo streakText = new(), scanText = new();
        TextMemo[] perkText;

        void PaintPerks(float width)
        {
            if (!streaksAvailable || own[6].Int <= 0 || !captured || scoreboard) return;
            const float tile = 56;
            perkText ??= TextMemo.Row(Perks.Length);
            float x = width - 246;
            HudStrip(new Rect(x, 55, 224, 57), perkMask > 0 ? Amber : Subtle);
            hudText.Draw(new Rect(x + 7, 55, 92, 22), streakText.Of(streakCount, 0, 0, null, static (n, _, _, _) => "SÉRIE  " + n), section);
            if (scanLeft > 0) hudText.Draw(new Rect(x + 112, 56, 106, 20), scanText.Of(Mathf.CeilToInt(scanLeft), 0, 0, null, static (n, _, _, _) => "PRŮZKUM " + n + " s"), caption);
            for (int i = 0; i < Perks.Length; i++)
            {
                float at = x + i * tile;
                bool ready = (perkMask & (1 << i)) != 0;
                var tint = ready ? Amber : Subtle;
                Fill(new Rect(at + 5, 76, tile - 12, 2), tint);
                string name = i == 1 ? ModuleName() : PerkNames[i];
                hudText.Draw(new Rect(at + 5, 76, tile - 7, 20), name, caption);
                hudText.Draw(new Rect(at + 5, 93, tile - 7, 18), perkText[i].Of(ready ? 1 : 0, 6 + i, PerkGoals[i], null, static (on, key, goal, _) => on == 1 ? "» " + key : goal + " výř."), caption);
            }
        }
    }
}
