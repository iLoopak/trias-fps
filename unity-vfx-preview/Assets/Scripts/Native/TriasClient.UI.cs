using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        Texture2D overlay, portraits;
        bool renderedPortraits;
        Font displayFont, textFont;
        readonly HudText hudText = new HudText();
        readonly TextMemo linkText = new(), clockText = new(), duelText = new(), grenadeText = new(), promptText = new();
        readonly TextMemo[] teamText = TextMemo.Row(3);
        GUIStyle title, heading, section, caption, center, scoreLabel, field;
        readonly Color lime = new Color(.78f, 1f, .43f);
        readonly Color muted = new Color(.66f, .72f, .77f);
        static readonly Color Amber = new Color(1f, .72f, .38f);

        void OnGUI()
        {
            bool repaint = Event.current.type == EventType.Repaint;
            AllocationBegin();
            DrawInterface();
            AllocationEnd(repaint ? "gui_repaint" : "gui_other");
        }

        void DrawInterface()
        {
            if (catalog == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (labMode && labHideHud && captured) return;
#endif
            if (title == null) Styles();
            Matrix4x4 saved = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float w = Screen.width / scale, h = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            if (reconnectUntil > 0) DrawReconnect(w,h);
            else if (world == null || own == null) DrawLobby(w,h);
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
            lobbyWrap = new GUIStyle(caption) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            center = Label(17, displayFont, Chalk);
            center.alignment = TextAnchor.MiddleCenter;
            scoreLabel = new GUIStyle(center) { fontSize = 15 };
            field = new GUIStyle(GUI.skin.textField) {
                font = textFont, fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(9, 9, 2, 2)
            };
        }

        static GUIStyle Label(int size, Font font, Color color)
        {
            return new GUIStyle(GUI.skin.label) {
                font = font, fontSize = size, clipping = TextClipping.Overflow, wordWrap = false,
                alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), normal = { textColor = color }
            };
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
            if (text != null) hudText.Draw(new Rect(box.x + 8, box.y + (sub == null ? (box.height-24)*.5f : 5), box.width - 14, 24), text, section);
            if (sub != null) hudText.Draw(new Rect(box.x + 8, box.y + 30, box.width - 14, 20), sub, caption);
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
            HudStrip(new Rect(22, 18, 176, 39), teamTint);
            if (mode == "ffa") DrawInsignia(new Vector2(38,37), 9, Amber);
            else DrawFaction(new Vector2(38,37), 9, own[3].Int, teamTint);
            hudText.Draw(new Rect(53, 18, 137, 22), world.Spec.label, section);
            hudText.Draw(new Rect(53, 38, 137, 18), linkText.Of(latency, 0, 0, room, static (l, _, _, r) => l + " ms  ·  " + r), caption);
            float cx = w * .5f;
            int time = state["time"].Int;
            if (mode == "horde") { var mark = AllocMark(); PaintHordeGauge(cx); AllocNote("hud_hordegauge", mark); }
            else
            {
                float width = mode == "ffa" ? 236 : 372;
                var scores = new Rect(cx - width * .5f, 18, width, 35);
                HudStrip(scores, teamTint);
                if (mode == "ffa")
                {
                    int leading = 0;
                    foreach (var value in state["players"].Items) leading = Mathf.Max(leading, value[11].Int);
                    hudText.Draw(new Rect(scores.x + 5, 20, 162, 28), duelText.Of(own[11].Int, leading, 0, null, static (mine, best, _, _) => "TY " + mine + "  /  VEDENÍ " + best), center);
                }
                else for (int team = 0; team < 3; team++)
                {
                    var core = state["siege"]["cores"][team];
                    int score = mode == "siege" ? core["hp"].Int : state["scores"][team].Int;
                    var area = new Rect(scores.x + 5 + 102 * team, 20, 100, 29);
                    var tint = Catalog.Tint(catalog.teams[team].color);
                    Fill(new Rect(area.x + 4, area.y + 26, area.width - 9, 2), tint);
                    DrawFaction(new Vector2(area.x + 11, area.y + 13), 6, team, tint);
                    hudText.Draw(new Rect(area.x + 19,area.y,area.width - 19,area.height), teamText[team].Of(score, mode == "siege" ? 1 : 0, 0, catalog.teams[team].name, static (value, siege, _, name) => siege == 1 ? value.ToString() : name + " " + value), scoreLabel);
                }
                hudText.Draw(new Rect(scores.xMax - 60, 20, 54, 28), clockText.Of(time, 0, 0, null, static (t, _, _, _) => (t / 60) + ":" + (t % 60).ToString("00")), center);
            }
            { var mark = AllocMark(); PaintArenaRhythm(cx); AllocNote("hud_arenarhythm", mark); }
            { var mark = AllocMark(); PaintPractice(cx); AllocNote("hud_practice", mark); }
            if (mode == "horde") { var mark = AllocMark(); PaintCouncil(cx); AllocNote("hud_council", mark); }
            if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toast))
            {
                HudStrip(new Rect(w - 300, 18, 278, 33), teamTint);
                hudText.Draw(new Rect(w - 291, 20, 260, 28), toast, caption);
            }
            bool alive = own[6].Int > 0;
            { var mark = AllocMark(); PaintObjectiveHints(h, scale); AllocNote("hud_objectivehints", mark); }
            { var mark = AllocMark(); PaintConstruction(w, h, scale); AllocNote("hud_construction", mark); }
            { var mark = AllocMark(); PaintPerks(w); AllocNote("hud_perks", mark); }
            if (captured && !scoreboard) { var mark = AllocMark(); PaintKillFeed(w); AllocNote("hud_killfeed", mark); }
            if (alive && captured && !scoreboard) { var mark = AllocMark(); PaintRadar(); AllocNote("hud_radar", mark); }
            { var mark = AllocMark(); PaintWaveBanner(cx, h); AllocNote("hud_wavebanner", mark); }
            if (alive && captured && !scoreboard) { var mark = AllocMark(); PaintBotRadio(h); AllocNote("hud_botradio", mark); }
            if (alive && state["winner"]["label"].Text == null)
            {
                { var mark = AllocMark(); PaintReticle(w, h); AllocNote("hud_reticle", mark); }
                { var mark = AllocMark(); PaintSignature(w, h); AllocNote("hud_signature", mark); }
                { var mark = AllocMark(); PaintVitals(w, h); AllocNote("hud_vitals", mark); }
                { var mark = AllocMark(); PaintDeployment(w,h); AllocNote("hud_deployment", mark); }
                string stateLabel = Prompt();
                if (stateLabel != null)
                {
                    HudStrip(new Rect(cx - 166, h - 78, 332, 27), teamTint);
                    hudText.Draw(new Rect(cx - 157, h - 78, 314, 27), stateLabel, center);
                }
                HudStrip(new Rect(cx - 61, h - 45, 122, 23), own[17].Int > 0 ? teamTint : Subtle);
                hudText.Draw(new Rect(cx - 60, h - 45, 120, 23), grenadeText.Of(own[17].Int, 0, 0, null, static (g, _, _, _) => "G   GRANÁT   " + g), center);
            }
            else if (state["winner"]["label"].Text != null) PaintResult(cx, h);
            else PaintDeath(cx,h);
            if (scoreboard)
            {
                Panel(new Rect(cx - 290, h / 2 - 213, 580, 426));
                hudText.Draw(new Rect(cx - 270,h / 2 - 201,540,34), "Piloti    /    vyřazení    /    pády", heading);
                int j = 0;
                foreach (var pilot in state["players"].Items)
                {
                    string name = pilot[1].Text + ((pilot[2].Int & 1) == 1 ? "  BOT" : "");
                    Fill(new Rect(cx - 270, h / 2 - 158 + j * 27, 4, 22), PlayerTint(pilot));
                    hudText.Draw(new Rect(cx - 257, h / 2 - 160 + j * 27, 378, 30), name, section);
                    hudText.Draw(new Rect(cx + 136, h / 2 - 160 + j * 27, 137, 30), pilot[11].Int + "           " + pilot[12].Int, section);
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
            if (own[8].Int > 0) return promptText.Of(own[8].Int, 0, 0, null, static (s, _, _, _) => "OVERCHARGE · " + s + " s");
            foreach (var hull in state["vehicles"].Items)
                if (hull[5].Int > 0 && string.IsNullOrEmpty(hull[4].Text) && (new Vector3(hull[7].Float,0,hull[8].Float)-display).sqrMagnitude < 10)
                    return "E · nastoupit · " + (hull[2].Text == "bastion" ? "Bastion" : "Skimmer");
            foreach (var gun in world.Spec.turrets)
                if ((gun.Position - display).sqrMagnitude < 5.8f) return "E · obsloužit hnízdo";
            return null;
        }

        struct ObjectiveHint
        {
            internal Rect Box;
            internal float Depth, Progress;
            internal int Faction;
            internal Color Tint;
            internal string Name;
        }

        readonly List<ObjectiveHint> objectiveHints = new(9);
        readonly List<Rect> objectiveBoxes = new(9);

        void PaintObjectiveHints(float h, float scale)
        {
            if (Event.current.type != EventType.Repaint || !captured || scoreboard || own[6].Int <= 0) return;
            objectiveHints.Clear();
            objectiveBoxes.Clear();
            if (world.DefendedCore != null && world.DefendedCore.gameObject.activeSelf)
                Objective(world.DefendedCore.position + Vector3.up * 1.42f, "BRAŇ JÁDRO", teamTint, h, scale, faction: own[3].Int);
            var points = mode == "siege" ? world.Spec.siege.nodes : mode == "dom" ? world.Spec.points : Array.Empty<Site>();
            var status = mode == "siege" ? state["siege"]["nodes"] : state["points"];
            for (int i = 0; i < points.Length; i++)
            {
                int team = status[i]["capture"].Int >= 0 ? status[i]["capture"].Int : status[i]["owner"].Int;
                float progress = status[i]["progress"].Float;
                bool contested = status[i]["contested"].Bool;
                Objective(points[i].Position + Vector3.up * 2.35f, SiegeNodeLabel(i, points[i], status[i], status),
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
            objectiveHints.Sort(static (a,b) => a.Depth.CompareTo(b.Depth));
            foreach (var hint in objectiveHints)
            {
                bool overlaps = false;
                foreach (var box in objectiveBoxes)
                    if (box.Overlaps(hint.Box)) { overlaps = true; break; }
                if (overlaps) continue;
                objectiveBoxes.Add(hint.Box);
                DrawObjective(hint);
            }
        }

        void Objective(Vector3 position, string name, Color color, float h, float scale, float progress = 0, int faction = -1)
        {
            Vector3 pixel = view.WorldToScreenPoint(position);
            if (pixel.z < 2 || pixel.x < 0 || pixel.x > Screen.width || pixel.y < 0 || pixel.y > Screen.height) return;
            float width = 126;
            int separator = name.IndexOf(" · ", StringComparison.Ordinal);
            if (pixel.z > 28 && separator > 0 && progress <= 0)
            {
                bool singleLetter = separator == 1;
                name = singleLetter || faction < 0 ? name.Substring(0,separator) : name.Substring(separator + 3);
                width = singleLetter ? (faction < 0 ? 26 : 40) : 88;
            }
            var box = new Rect(pixel.x / scale - width / 2, h - pixel.y / scale - 11, width, 22);
            objectiveHints.Add(new ObjectiveHint { Box=box, Depth=pixel.z, Name=name, Tint=color, Progress=progress, Faction=faction });
        }

        void DrawObjective(ObjectiveHint hint)
        {
            var box = hint.Box;
            Fill(box, new Color(.04f,.074f,.11f,.40f));
            float progress = hint.Progress;
            bool capturing = progress > 0 && progress < 1;
            Fill(new Rect(box.x, box.yMax - 2, box.width, 2), capturing ? Subtle : hint.Tint);
            if (capturing) Fill(new Rect(box.x, box.yMax - 2, box.width * progress, 2), hint.Tint);
            if (hint.Faction >= 0)
                DrawFaction(new Vector2(box.x + 11,box.y + 10),6,hint.Faction,hint.Tint);
            float indent = hint.Faction >= 0 ? 18 : 0;
            hudText.Draw(new Rect(box.x + indent,box.y,box.width - indent,box.height),hint.Name,scoreLabel);
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
