using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        static readonly string[] CouncilKeys = { "repair", "armor", "scrap" };
        static readonly string[] CouncilNames = { "Opravy jádra a staveb", "Štíty a granáty", "45 šrotu navíc" };
        bool practiceRoom, practiceRequested;

        void StartPractice()
        {
            Join(serverAddress, "DRILL", nickname, "relay", "tdm", "vector", false,
                remember: false, male: selectedMale, intent: "practice");
        }

        void PaintPractice(float cx)
        {
            if (!practiceRoom || !captured || scoreboard) return;
            var drill = state["practice"];
            if (drill.Count != 2) return;
            int phase = drill[0].Int, kills = drill[1].Int;
            string title = phase switch
            {
                0 => "1 / 3   ·   ÚSKOK",
                1 => "2 / 3   ·   COMBAT ECHO",
                2 => "3 / 3   ·   PRVNÍ SÉRIE",
                _ => "VÝCVIK DOKONČEN"
            };
            string instruction = phase switch
            {
                0 => "Stiskni Q a uhni z místa.",
                1 => activeEcho == 0 ? "Vytvoř ozvěnu úskokem Q a hned stiskni X."
                    : "Stiskni X a vystřel z pozice ozvěny.",
                2 => kills < 3 ? $"Sestřel tři terče před sebou · {kills} / 3" : "Stiskni 6 pro radarový průzkum.",
                _ => "Enter · zpět do lobby"
            };
            CornerPanel(new Rect(cx - 234, 88, 468, 55), teamTint);
            hudText.Draw(new Rect(cx - 215, 94, 430, 20), title, section);
            hudText.Draw(new Rect(cx - 215, 116, 430, 22), instruction, caption);
        }

        void ReadCouncil()
        {
            if (!captured || Input.GetKey(KeyCode.Tab) || state["horde"]["council"]["counts"].Count != 3) return;
            for (int slot = 0; slot < CouncilKeys.Length; slot++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.F1 + slot)))
                    socket.Send("{\"type\":\"horde-vote\",\"option\":\"" + CouncilKeys[slot] + "\"}");
        }

        void PaintCouncil(float cx)
        {
            if (!captured || scoreboard || state["winner"]["label"].Text != null) return;
            var horde = state["horde"];
            var panel = horde["council"];
            if (panel["counts"].Count != 3)
            {
                string support = horde["support"].Text;
                int option = System.Array.IndexOf(CouncilKeys, support);
                if (option >= 0 && waveStarts && waveFlash > .38f)
                    hudText.Draw(new Rect(cx - 205, 149, 410, 25), "PODPORA TÝMU   ·   " + CouncilNames[option], center);
                return;
            }
            string voted = null;
            foreach (var vote in panel["votes"].Items)
                if (vote[0].Text == identity) voted = vote[1].Text;
            float x = cx - 262;
            CornerPanel(new Rect(x, 88, 524, 65), Amber);
            hudText.Draw(new Rect(x + 12, 91, 494, 23), "RADA OBRANY  ·  " + horde["countdown"].Int + " s", section);
            for (int i = 0; i < CouncilKeys.Length; i++)
            {
                var box = new Rect(x + 12 + i * 168, 116, 164, 28);
                bool selected = voted == CouncilKeys[i];
                Fill(box, selected ? new Color(.28f,.19f,.09f,.94f) : new Color(.055f,.088f,.12f,.93f));
                hudText.Draw(new Rect(box.x + 5, box.y, box.width - 8, box.height),
                    $"F{i + 1} {CouncilNames[i]} · {panel["counts"][i].Int}", caption);
            }
        }

        string SiegeNodeLabel(int node, Site site, WireValue info, WireValue nodes)
        {
            if (info["contested"].Bool) return site.label + " · SPOR";
            if (info["escort"].Bool) return site.label + " · KRYTÍ";
            if (mode != "siege") return site.label;
            int team = own[3].Int;
            if (node == team || info["owner"].Int == team) return site.label;
            foreach (var link in world.Spec.siege.links)
            {
                int neighbor = link.a == node ? link.b : link.b == node ? link.a : -1;
                if (neighbor >= 0 && nodes[neighbor]["linked"].Bool && nodes[neighbor]["owner"].Int == team)
                    return site.label + " · DOBÝT";
            }
            return site.label + " · ODPOJENO";
        }
    }
}
