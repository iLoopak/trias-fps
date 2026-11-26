using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        Texture2D overlay, portraits;
        GUIStyle title, heading, section, caption, center, action, field;
        readonly Color lime = new Color(.78f, 1f, .43f);
        readonly Color muted = new Color(.62f, .72f, .83f);

        void OnGUI()
        {
            if (catalog == null) return;
            if (title == null) Styles();
            Matrix4x4 saved = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float w = Screen.width / scale, h = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            if (world == null || own == null) Lobby(w, h);
            else MatchHUD(w, h, scale);
            GUI.matrix = saved;
        }

        void Styles()
        {
            title = Label(65, FontStyle.Bold, Color.white);
            heading = Label(25, FontStyle.Bold, Color.white);
            section = Label(16, FontStyle.Bold, Color.white);
            caption = Label(13, FontStyle.Normal, muted);
            center = Label(17, FontStyle.Bold, Color.white);
            center.alignment = TextAnchor.MiddleCenter;
            field = new GUIStyle(GUI.skin.textField) { fontSize = 16, padding = new RectOffset(9, 9, 5, 3) };
            action = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        static GUIStyle Label(int size, FontStyle weight, Color color)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight, clipping = TextClipping.Clip, normal = { textColor = color } };
        }

        void Lobby(float w, float h)
        {
            if (overlay != null) GUI.DrawTexture(new Rect(0, 0, w, h), overlay, ScaleMode.ScaleAndCrop);
            Fill(new Rect(0, 0, w, h), new Color(.021f, .037f, .069f, .70f));
            float x = w / 2 - 552;
            float y = h / 2 - 344;
            Fill(new Rect(x, y + 7, 1104, 2), lime);
            GUI.Label(new Rect(x, y + 20, 870, 88), "TRIAS", title);
            Panel(new Rect(x, y + 157, 680, 490));
            GUI.Label(new Rect(x + 16, y + 168, 430, 32), "Mapa", heading);
            for (int i = 0; i < catalog.maps.Length; i++)
            {
                var map = catalog.maps[i];
                Rect box = new Rect(x + 16 + i % 2 * 328, y + 210 + i / 2 * 58, 316, 49);
                if (Preset(box, map.label, null, map.id == selectedMap))
                {
                    selectedMap = map.id;
                    if ((selectedMode == "siege" && selectedMap != "basin") || (selectedMode == "horde" && selectedMap != "relay")) selectedMode = "tdm";
                }
            }
            GUI.Label(new Rect(x + 16, y + 332, 400, 32), "Režim", heading);
            for (int i = 0; i < catalog.modes.Length; i++)
            {
                var game = catalog.modes[i];
                if (Preset(new Rect(x + 16 + i * 108, y + 374, 102, 55), game.@short, game.id == "tdm" ? null : game.label, game.id == selectedMode))
                {
                    selectedMode = game.id;
                    if (game.id == "siege") selectedMap = "basin";
                    if (game.id == "horde") selectedMap = "relay";
                }
            }
            GUI.Label(new Rect(x + 16, y + 448, 400, 32), "Pilot", heading);
            for (int i = 0; i < catalog.pilots.Length; i++)
            {
                var pilot = catalog.pilots[i];
                var button = new Rect(x + 16 + i * 164, y + 491, 155, 136);
                if (Preset(button, null, null, pilot.id == selectedPilot)) selectedPilot = pilot.id;
                if (portraits != null) GUI.DrawTextureWithTexCoords(new Rect(button.x + 43, button.y + 3, 70, 70), portraits, new Rect(i * .25f, 0, .25f, 1));
                GUI.Label(new Rect(button.x + 13, button.y + 77, 131, 26), pilot.label, section);
                GUI.Label(new Rect(button.x + 13, button.y + 104, 131, 22), Catalog.Find(catalog.weapons, pilot.weapon)?.label, caption);
            }

            float right = x + 696;
            Panel(new Rect(right, y + 157, 408, 490));
            GUI.Label(new Rect(right + 17, y + 168, 374, 36), "Místnost", heading);
            GUI.Label(new Rect(right + 17, y + 218, 365, 23), "Adresa serveru", caption);
            serverAddress = GUI.TextField(new Rect(right + 17, y + 242, 374, 34), serverAddress, 160, field);
            GUI.Label(new Rect(right + 17, y + 289, 365, 23), "Kód místnosti a přezdívka", caption);
            roomCode = GUI.TextField(new Rect(right + 17, y + 314, 178, 34), roomCode, 24, field);
            nickname = GUI.TextField(new Rect(right + 207, y + 314, 184, 34), nickname, 18, field);
            if (selectedMode == "horde")
                GUI.Label(new Rect(right + 17, y + 365, 374, 29), "1–4 piloti v jednom týmu · 8 vln", section);
            else fillBots = GUI.Toggle(new Rect(right + 17, y + 365, 374, 29), fillBots, "Doplnit volná místa boty");
            if (Preset(new Rect(right + 17, y + 413, 374, 55), "Vstoupit do arény", null, true) && socket == null)
                Join(serverAddress, roomCode, nickname, selectedMap, selectedMode, selectedPilot, fillBots);
            if (status != null) GUI.Label(new Rect(right + 17, y + 479, 375, 34), status, section);
            if (socket != null && GUI.Button(new Rect(right + 17, y + 519, 174, 36), "Zrušit připojení", action)) Disconnect(null);
            if (socket == null && GUI.Button(new Rect(right + 17, y + 568, 374, 43), "Trénink offline · Relay Station", action))
                SceneManager.LoadScene("Relay Station");
        }

        bool Preset(Rect box, string text, string sub, bool selected)
        {
            Fill(box, selected ? new Color(.20f, .28f, .17f, .96f) : new Color(.048f, .09f, .149f, .98f));
            Fill(new Rect(box.x, box.y, box.width, selected ? 2 : 1), selected ? lime : new Color(.27f, .35f, .45f));
            bool pressed = GUI.Button(box, GUIContent.none, GUIStyle.none);
            if (text != null) GUI.Label(new Rect(box.x + 8, box.y + (sub == null ? 12 : 5), box.width - 14, 24), text, section);
            if (sub != null) GUI.Label(new Rect(box.x + 8, box.y + 30, box.width - 14, 20), sub, caption);
            return pressed;
        }

        void MatchHUD(float w, float h, float scale)
        {
            Panel(new Rect(22, 21, 226, 60));
            GUI.Label(new Rect(34, 25, 203, 28), world.Spec.label, section);
            GUI.Label(new Rect(34, 53, 203, 21), room + "  ·  " + latency + " ms", caption);
            float cx = w * .5f;
            Panel(new Rect(cx - 220, 21, 440, 55));
            int time = state["time"].Int;
            if (mode == "horde")
            {
                var defense = state["horde"];
                var core = defense["core"];
                GUI.Label(new Rect(cx - 213, 24, 426, 28), defense["countdown"].Int > 0
                    ? "DALŠÍ VLNA ZA " + defense["countdown"].Int + " s"
                    : "VLNA " + defense["wave"].Int + "/" + defense["waves"].Int + "    ·    ZBÝVÁ " + defense["enemies"].Int, center);
                Fill(new Rect(cx - 192, 63, 384, 4), new Color(.13f,.21f,.30f));
                float strength = core["hp"].Float / Mathf.Max(1, core["maxHp"].Float);
                Fill(new Rect(cx - 192, 63, 384 * strength, 4), Color.Lerp(new Color(1f, .29f, .23f), teamTint, strength));
                GUI.Label(new Rect(cx - 183, 45, 367, 21), "JÁDRO " + core["hp"].Int + " HP", center);
            }
            else if (mode == "ffa")
            {
                int leading = 0;
                foreach (var value in state["players"].Items) leading = Mathf.Max(leading, value[11].Int);
                GUI.Label(new Rect(cx - 216, 26, 330, 37), "TY " + own[11].Int + "   /   VEDENÍ " + leading, center);
            }
            else
            {
                for (int team = 0; team < 3; team++)
                {
                    var core = state["siege"]["cores"][team];
                    int score = mode == "siege" ? core["hp"].Int : state["scores"][team].Int;
                    var area = new Rect(cx - 207 + 111 * team, 31, 104, 29);
                    Fill(new Rect(area.x + 6, area.y + 25, area.width - 12, 2), Catalog.Tint(catalog.teams[team].color));
                    GUI.Label(area, catalog.teams[team].name + " " + score, center);
                }
            }
            if (mode != "horde")
                GUI.Label(new Rect(cx + 125, 30, 85, 30), (time / 60) + ":" + (time % 60).ToString("00"), center);
            if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toast))
            {
                Panel(new Rect(w - 268, 30, 246, 35));
                GUI.Label(new Rect(w - 257, 34, 229, 25), toast, caption);
            }
            bool alive = own[6].Int > 0;
            PaintObjectiveHints(h, scale);
            PaintConstruction(w, h, scale);
            if (alive) Minimap();
            if (hurt > 0)
            {
                var flash = new Color(1, .27f, .26f, hurt * .76f);
                Fill(new Rect(0, 0, w, 5), flash); Fill(new Rect(0, h - 5, w, 5), flash);
                Fill(new Rect(0, 0, 5, h), flash); Fill(new Rect(w - 5, 0, 5, h), flash);
            }
            if (alive && state["winner"]["label"].Text == null)
            {
                Color cross = hit > 0 ? lime : Color.white;
                Fill(new Rect(cx - 9, h / 2 - 1, 6, 2), cross); Fill(new Rect(cx + 3, h / 2 - 1, 6, 2), cross);
                Fill(new Rect(cx - 1, h / 2 - 9, 2, 6), cross); Fill(new Rect(cx - 1, h / 2 + 3, 2, 6), cross);
                Panel(new Rect(22, h - 87, 153, 64));
                GUI.Label(new Rect(34, h - 84, 120, 20), catalog.teams[own[3].Int].name, caption);
                GUI.Label(new Rect(33, h - 62, 95, 32), own[6].Int + " HP", heading);
                Fill(new Rect(34, h - 30, 126 * own[6].Float / 150, 3), teamTint);
                if (own[7].Int > 0) GUI.Label(new Rect(116, h - 57, 55, 25), "+" + own[7].Int, caption);
                var hull = state["vehicles"][own[16].Int];
                var gun = Catalog.Find(catalog.weapons, weaponName);
                int i = Array.IndexOf(Arsenal, weaponName);
                var mags = own[10][i];
                Panel(new Rect(w - 203, h - 87, 181, 64));
                GUI.Label(new Rect(w - 191, h - 84, 162, 22), own[16].Int >= 0 ? (hull[2].Text == "bastion" ? "BASTION" : "SKIMMER")
                    : own[15].Int >= 0 ? "HNÍZDO" : gun?.label.ToUpperInvariant(), section);
                string ammo = own[16].Int >= 0 ? hull[5].Int + " HP" : own[15].Int >= 0 ?
                    Mathf.RoundToInt(state["turrets"][own[15].Int]["heat"].Float * 100) + " %" :
                    mags[0].Int + " / " + (mags[1].Int < 0 ? "∞" : mags[1].Int.ToString());
                GUI.Label(new Rect(w - 192, h - 61, 164, 37), ammo, heading);
                string stateLabel = Prompt();
                if (stateLabel != null) GUI.Label(new Rect(cx - 240, h - 88, 480, 27), stateLabel, center);
                GUI.Label(new Rect(cx - 240, h - 56, 480, 27), "G  GRANÁT " + own[17].Int + "     ·     Q  ÚHYB     ·     1–5  ZBRANĚ", center);
            }
            else if (state["winner"]["label"].Text != null)
            {
                Panel(new Rect(cx - 206, h - 136, 412, 92));
                GUI.Label(new Rect(cx - 202, h - 128, 404, 38), "Výsledek · " + state["winner"]["label"].Text, center);
                GUI.Label(new Rect(cx - 202, h - 87, 404, 28), "Další kolo za " + state["restart"].Int + " s", center);
            }
            else
            {
                bool replay = Time.unscaledTime < replayUntil;
                string lead = replay ? "KILL CAM" : spectator != null ? "SLEDUJEŠ" : "VYŘAZENÍ";
                string who = replay ? killer : spectator != null && pilots.TryGetValue(spectator, out var actor) ? actor.Row[1].Text : "Čekáš na návrat";
                Panel(new Rect(cx - 182, h - 156, 364, 113));
                GUI.Label(new Rect(cx - 165, h - 147, 330, 21), lead + "    /    " + Mathf.CeilToInt(own[29].Float) + " s", section);
                GUI.Label(new Rect(cx - 165, h - 122, 330, 37), who, heading);
                GUI.Label(new Rect(cx - 165, h - 81, 330, 24), replay ? "Mezerník · přeskočit" : "E · další spoluhráč", caption);
            }
            if (scoreboard)
            {
                Panel(new Rect(cx - 290, h / 2 - 213, 580, 426));
                GUI.Label(new Rect(cx - 270,h / 2 - 201,540,34), "Piloti    /    vyřazení    /    pády", heading);
                int j = 0;
                foreach (var pilot in state["players"].Items)
                {
                    string name = pilot[1].Text + ((pilot[2].Int & 1) == 1 ? "  BOT" : "");
                    Fill(new Rect(cx - 270, h / 2 - 158 + j * 27, 4, 22), PlayerTint(pilot));
                    GUI.Label(new Rect(cx - 257, h / 2 - 160 + j * 27, 410, 30), name, section);
                    GUI.Label(new Rect(cx + 136, h / 2 - 160 + j * 27, 137, 30), pilot[11].Int + "           " + pilot[12].Int, section);
                    j++;
                }
            }
            if (!captured)
            {
                Fill(new Rect(0,0,w,h),new Color(.02f,.035f,.06f,.70f));
                Panel(new Rect(cx-240,h/2-116,480,232));
                GUI.Label(new Rect(cx-219,h/2-100,435,42),"Ovládání",heading);
                GUI.Label(new Rect(cx-219,h/2-49,435,25),"WASD  /  myš  /  Shift  /  mezerník  /  Q  /  C",section);
                GUI.Label(new Rect(cx-219,h/2-21,435,25),"E nastoupit   ·   R přebít   ·   F mód Scatteru",caption);
                if (GUI.Button(new Rect(cx-219,h/2+17,435,35),"Hrát",action)) Capture(true);
                if (GUI.Button(new Rect(cx-219,h/2+63,435,35),"Opustit místnost",action)) Disconnect(null);
            }
        }

        string Prompt()
        {
            if (own[16].Int >= 0) return "E vystoupit   /   mezerník brzda   /   Shift turbo";
            if (own[15].Int >= 0) return "E · opustit hnízdo";
            if ((own[2].Int & 64) != 0) return "Přebíjíš…";
            if (own[8].Int > 0) return "OVERCHARGE · " + own[8].Int + " s";
            foreach (var hull in state["vehicles"].Items)
                if (hull[5].Int > 0 && string.IsNullOrEmpty(hull[4].Text) && (new Vector3(hull[7].Float,0,hull[8].Float)-display).sqrMagnitude < 10)
                    return "E · nastoupit · " + (hull[2].Text == "bastion" ? "Bastion" : "Skimmer");
            foreach (var gun in world.Spec.turrets)
                if ((gun.Position - display).sqrMagnitude < 5.8f) return "E · obsloužit hnízdo";
            return null;
        }

        void PaintObjectiveHints(float h, float scale)
        {
            if (!captured || own[6].Int <= 0) return;
            if (world.DefendedCore != null && world.DefendedCore.gameObject.activeSelf)
                Objective(world.DefendedCore.position + Vector3.up * 1.42f, "BRAŇ JÁDRO", teamTint, h, scale);
            var points = mode == "siege" ? world.Spec.siege.nodes : mode == "dom" ? world.Spec.points : Array.Empty<Site>();
            var status = mode == "siege" ? state["siege"]["nodes"] : state["points"];
            for (int i = 0; i < points.Length; i++)
            {
                int team = status[i]["capture"].Int >= 0 ? status[i]["capture"].Int : status[i]["owner"].Int;
                float progress = status[i]["progress"].Float;
                Objective(points[i].Position + Vector3.up * 2.35f, points[i].label + (progress > 0 && progress < 1 ? " " + Mathf.RoundToInt(progress * 100) + "%" : ""),
                    team < 0 ? Color.white : Catalog.Tint(catalog.teams[team].color), h, scale);
            }
            for (int i = 0; i < world.Flags.Length; i++)
                Objective(world.Flags[i].position + Vector3.up * 2.1f, catalog.teams[i].name + " · VLAJKA", Catalog.Tint(catalog.teams[i].color), h, scale);
            for (int i = 0; i < world.CoreShield.Length; i++)
            {
                var core = state["siege"]["cores"][i];
                if (core["hp"].Int == 0) continue;
                Objective(world.Spec.siege.cores[i].Position + Vector3.up * 2.1f,
                    catalog.teams[i].name + ((core["opened"].Int & (1 << own[3].Int)) != 0 ? " · ODKRYTO" : " · JÁDRO"),
                    Catalog.Tint(catalog.teams[i].color), h, scale);
            }
        }

        void Objective(Vector3 position, string name, Color color, float h, float scale)
        {
            Vector3 pixel = view.WorldToScreenPoint(position);
            if (pixel.z < 2) return;
            var box = new Rect(pixel.x / scale - 70, h - pixel.y / scale - 14, 140, 26);
            Fill(box, new Color(.028f,.057f,.095f,.76f));
            Fill(new Rect(box.x,box.y + 24,box.width,2),color);
            GUI.Label(box,name,center);
        }

        void Minimap()
        {
            var rect = new Rect(22,92,156,156);
            Fill(rect,new Color(.025f,.063f,.106f,.85f));
            if (Event.current.type != EventType.Repaint) return;
            float factor = 70 / world.Spec.length;
            Vector2 Mark(float x, float z) => new Vector2(rect.center.x + x * factor, rect.center.y + z * factor);
            foreach (var cover in world.Spec.cover)
            {
                var p=Mark(cover.x,cover.z);
                Fill(new Rect(p.x-cover.w*factor*.5f,p.y-cover.d*factor*.5f,cover.w*factor,cover.d*factor),new Color(.22f,.29f,.37f,.9f));
            }
            if (mode != "ffa") foreach (var actor in pilots.Values)
            {
                if (actor.Row[6].Int <= 0 || actor.Row[3].Int != own[3].Int) continue;
                var p = Mark(actor.Position.x,actor.Position.z);
                Fill(new Rect(p.x-2,p.y-2,4,4),teamTint);
            }
            var self=Mark(display.x,display.z);
            Fill(new Rect(self.x-3,self.y-3,6,6),Color.white);
            if (world.DefendedCore != null && world.DefendedCore.gameObject.activeSelf)
            {
                var at = world.DefendedCore.position;
                var mark = Mark(at.x, at.z);
                Fill(new Rect(mark.x - 4, mark.y - 4, 8, 8), teamTint);
            }
        }

        static void Panel(Rect rect) => Fill(rect,new Color(.029f,.060f,.103f,.93f));
        static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=saved;
        }
    }
}
