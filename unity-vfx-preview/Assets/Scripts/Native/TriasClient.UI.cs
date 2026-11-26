using System;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        Texture2D overlay, portraits;
        bool renderedPortraits;
        Font displayFont, textFont;
        GUIStyle title, heading, section, caption, center, action, field;
        readonly Color lime = new Color(.78f, 1f, .43f);
        readonly Color muted = new Color(.66f, .72f, .77f);
        static readonly Color Amber = new Color(1f, .72f, .38f);

        void OnGUI()
        {
            if (catalog == null) return;
            if (title == null) Styles();
            Matrix4x4 saved = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float w = Screen.width / scale, h = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            if (world == null || own == null) DrawLobby(w,h);
            else MatchHUD(w, h, scale);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (labMode && own != null && captured) PaintLab(w);
#endif
            if (sceneFade > 0) Fill(new Rect(0, 0, w, h), new Color(.025f, .043f, .07f, sceneFade));
            GUI.matrix = saved;
        }

        void Styles()
        {
            title = Label(78, displayFont, Chalk);
            heading = Label(26, displayFont, Chalk);
            section = Label(17, displayFont, Chalk);
            caption = Label(15, textFont, muted);
            lobbyWrap = new GUIStyle(caption) {wordWrap=true};
            center = Label(17, displayFont, Chalk);
            center.alignment = TextAnchor.MiddleCenter;
            field = new GUIStyle(GUI.skin.textField) { font = textFont, fontSize = 18, padding = new RectOffset(9, 9, 5, 3) };
            action = new GUIStyle(GUI.skin.button) { font = displayFont, fontSize = 16, alignment = TextAnchor.MiddleCenter };
        }

        static GUIStyle Label(int size, Font font, Color color)
        {
            return new GUIStyle(GUI.skin.label) { font = font, fontSize = size, clipping = TextClipping.Clip, normal = { textColor = color } };
        }

        bool Preset(Rect box, string text, string sub, bool selected)
        {
            bool hovered = box.Contains(Event.current.mousePosition);
            Fill(box, selected ? new Color(.24f, .15f, .072f, .96f) : hovered ? new Color(.061f, .11f, .15f, 1f) : new Color(.041f, .071f, .105f, .98f));
            Fill(new Rect(box.x, box.y, box.width, selected ? 2 : 1), selected ? Amber : Subtle);
            if (selected && text != null)
                CornerNotch(new Vector2(box.xMax - 5, box.yMax - 6), Amber);
            bool pressed = GUI.Button(box, GUIContent.none, GUIStyle.none);
            if (pressed) effects.InterfaceClick();
            if (text != null) GUI.Label(new Rect(box.x + 8, box.y + (sub == null ? (box.height-24)*.5f : 5), box.width - 14, 24), text, section);
            if (sub != null) GUI.Label(new Rect(box.x + 8, box.y + 30, box.width - 14, 20), sub, caption);
            return pressed;
        }

        static void CornerNotch(Vector2 at, Color tint)
        {
            Stroke(at + new Vector2(-10, 0), at, 1.4f, tint);
            Stroke(at, at + new Vector2(0, -8), 1.4f, tint);
        }

        static void DrawInsignia(Vector2 position, float radius, Color tint)
        {
            for (int blade = 0; blade < 3; blade++)
            {
                float direction = blade * 120f;
                Vector2 outward = Polar(direction);
                Vector2 fold = Polar(direction + 47);
                var tip = position + outward * radius;
                var shoulder = position + outward * (radius * .55f) + fold * (radius * .25f);
                Stroke(tip, shoulder, radius * .075f, tint);
                Stroke(shoulder, position + fold * (radius * .31f), radius * .075f, tint);
                Stroke(tip, position + Polar(direction - 52) * (radius * .21f), radius * .033f, tint);
            }
        }

        static void DrawFaction(Vector2 centerPoint, float size, int faction, Color tint)
        {
            foreach (var line in TriasMarkings.FactionStrokes[faction])
                Stroke(centerPoint + new Vector2(line.x,-line.y)*size,
                    centerPoint + new Vector2(line.z,-line.w)*size, Mathf.Max(1.3f,size*.15f),tint);
        }

        void MatchHUD(float w, float h, float scale)
        {
            CornerPanel(new Rect(26, 21, 214, 58), teamTint);
            if (mode == "ffa") DrawInsignia(new Vector2(47,49), 12, Amber);
            else DrawFaction(new Vector2(47,49), 12, own[3].Int, teamTint);
            GUI.Label(new Rect(67, 25, 165, 28), world.Spec.label, section);
            GUI.Label(new Rect(67, 51, 165, 21), room + "  ·  " + latency + " ms", caption);
            float cx = w * .5f;
            if (mode != "horde") CornerPanel(new Rect(cx - 220, 21, 440, 55), teamTint);
            int time = state["time"].Int;
            if (mode == "horde") PaintHordeGauge(cx);
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
                    DrawFaction(new Vector2(area.x + 14, area.y + 13), 7, team, Catalog.Tint(catalog.teams[team].color));
                    GUI.Label(new Rect(area.x + 23,area.y,area.width - 23,area.height), catalog.teams[team].name + " " + score, center);
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
            PaintPerks(w);
            if (captured && !scoreboard) PaintKillFeed(w);
            if (alive && captured && !scoreboard) PaintRadar();
            PaintWaveBanner(cx, h);
            if (alive && captured && !scoreboard) PaintBotRadio(h);
            if (alive && state["winner"]["label"].Text == null)
            {
                PaintReticle(w, h);
                PaintSignature(w, h);
                PaintVitals(w, h);
                PaintDeployment(w,h);
                string stateLabel = Prompt();
                if (stateLabel != null) GUI.Label(new Rect(cx - 240, h - 88, 480, 27), stateLabel, center);
                CornerPanel(new Rect(cx - 95, h - 55, 190, 28), own[17].Int > 0 ? teamTint : Subtle);
                GUI.Label(new Rect(cx - 93, h - 55, 186, 28), "G   GRANÁT   " + own[17].Int, center);
            }
            else if (state["winner"]["label"].Text != null) PaintResult(cx, h);
            else PaintDeath(cx,h);
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
            if (!captured && world != null && state["winner"]["label"].Text == null) PaintPause(w,h);
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
                Objective(world.DefendedCore.position + Vector3.up * 1.42f, "BRAŇ JÁDRO", teamTint, h, scale, faction: own[3].Int);
            var points = mode == "siege" ? world.Spec.siege.nodes : mode == "dom" ? world.Spec.points : Array.Empty<Site>();
            var status = mode == "siege" ? state["siege"]["nodes"] : state["points"];
            for (int i = 0; i < points.Length; i++)
            {
                int team = status[i]["capture"].Int >= 0 ? status[i]["capture"].Int : status[i]["owner"].Int;
                float progress = status[i]["progress"].Float;
                bool contested = status[i]["contested"].Bool;
                Objective(points[i].Position + Vector3.up * 2.35f, contested ? points[i].label + " · SPOR" : points[i].label,
                    contested ? Amber : team < 0 ? Chalk : Catalog.Tint(catalog.teams[team].color), h, scale, progress, team);
            }
            for (int i = 0; i < world.Flags.Length; i++)
                Objective(world.Flags[i].position + Vector3.up * 2.1f, catalog.teams[i].name + " · VLAJKA", Catalog.Tint(catalog.teams[i].color), h, scale, faction: i);
            for (int i = 0; i < world.CoreShield.Length; i++)
            {
                var core = state["siege"]["cores"][i];
                if (core["hp"].Int == 0) continue;
                Objective(world.Spec.siege.cores[i].Position + Vector3.up * 2.1f,
                    catalog.teams[i].name + ((core["opened"].Int & (1 << own[3].Int)) != 0 ? " · ODKRYTO" : " · JÁDRO"),
                    Catalog.Tint(catalog.teams[i].color), h, scale, faction: i);
            }
        }

        void Objective(Vector3 position, string name, Color color, float h, float scale, float progress = 0, int faction = -1)
        {
            Vector3 pixel = view.WorldToScreenPoint(position);
            if (pixel.z < 2 || pixel.x < 0 || pixel.x > Screen.width || pixel.y < 0 || pixel.y > Screen.height) return;
            var box = new Rect(pixel.x / scale - 70, h - pixel.y / scale - 14, 140, 26);
            Fill(box, new Color(.028f,.057f,.095f,.76f));
            bool capturing = progress > 0 && progress < 1;
            Fill(new Rect(box.x, box.y + 24, box.width, 2), capturing ? Subtle : color);
            if (capturing) Fill(new Rect(box.x, box.y + 24, box.width * progress, 2), color);
            if (faction >= 0)
                DrawFaction(new Vector2(box.x + 12,box.y + 12),7,faction,color);
            GUI.Label(new Rect(box.x + (faction >= 0 ? 20 : 0),box.y,box.width - (faction >= 0 ? 20 : 0),box.height),name,center);
        }

        static void Panel(Rect rect) => CornerPanel(rect, Amber);
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
