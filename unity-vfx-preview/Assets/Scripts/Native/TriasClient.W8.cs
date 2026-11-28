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

        // Horde preparation keys sit next to the movement hand: V cycles the council vote (it used to be
        // F1-F3, a reach off the home row mid-fight) and N toggles "ready for the next wave".
        void ReadCouncil()
        {
            if (!captured || mode != "horde" || Input.GetKey(KeyCode.Tab) || own[6].Int <= 0) return;
            var horde = state["horde"];
            if (Input.GetKeyDown(KeyCode.N) && horde["countdown"].Int > 0)
                socket.Send("{\"type\":\"horde-ready\"}");
            if (!Input.GetKeyDown(KeyCode.V) || horde["council"]["counts"].Count != 3) return;
            int next = (System.Array.IndexOf(CouncilKeys, OwnVote(horde["council"])) + 1) % CouncilKeys.Length;
            socket.Send("{\"type\":\"horde-vote\",\"option\":\"" + CouncilKeys[next] + "\"}");
        }

        string OwnVote(WireValue council)
        {
            foreach (var vote in council["votes"].Items)
                if (vote[0].Text == identity) return vote[1].Text;
            return null;
        }

        bool OwnReady
        {
            get
            {
                foreach (var id in state["horde"]["ready"]["ids"].Items) if (id.Text == identity) return true;
                return false;
            }
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
            string voted = OwnVote(panel);
            float x = cx - 262;
            float y = construction != null ? 126 : 88;
            CornerPanel(new Rect(x, y, 524, 65), Amber);
            hudText.Draw(new Rect(x + 12, y + 3, 494, 23), (voted == null ? "RADA OBRANY  ·  V HLASUJ  ·  " : "RADA OBRANY  ·  V ZMĚNIT  ·  ")
                + horde["countdown"].Int + " s", section);
            for (int i = 0; i < CouncilKeys.Length; i++)
            {
                var box = new Rect(x + 12 + i * 168, y + 28, 164, 28);
                bool selected = voted == CouncilKeys[i];
                Fill(box, selected ? new Color(.28f,.19f,.09f,.94f) : new Color(.055f,.088f,.12f,.93f));
                if (selected) Fill(new Rect(box.x, box.yMax - 2, box.width, 2), Amber);
                hudText.Draw(new Rect(box.x + 5, box.y, box.width - 8, box.height),
                    (selected ? "● " : "") + CouncilNames[i] + " · " + panel["counts"][i].Int, caption);
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
