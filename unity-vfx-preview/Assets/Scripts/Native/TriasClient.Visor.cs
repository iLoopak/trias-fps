using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        static readonly Color Ink = new Color(.018f, .034f, .054f, .94f);
        static readonly Color Fault = new Color(1f, .31f, .28f);
        static readonly Color Shield = new Color(.48f, .79f, 1f);
        static readonly Color Subtle = new Color(.29f, .43f, .53f, .67f);
        static readonly Color Chalk = new Color(.97f, .94f, .87f);
        VisorComposite visor;
        Texture2D radarPlate;
        Color damageTint = Fault;
        int confirmStyle;
        const float IncomingDuration = .48f;
        const float ShieldReturnDuration = .72f;
        struct IncomingHit
        {
            internal float Heading, Until;
            internal Color Tint;
            internal bool Heavy;
        }
        readonly IncomingHit[] incomingHits = new IncomingHit[4];
        float healthTrail, armorTrail, vitalsPulse, shieldAlertUntil, shieldReturnUntil;
        float waveFlash, coreFlash;
        bool waveStarts;
        struct KillEntry
        {
            internal string Attacker, Victim, Weapon;
            internal Color Tint;
            internal float Time;
        }
        readonly KillEntry[] killFeed = new KillEntry[3];
        int killFeedCount;

        void AddKill(string attacker, string victim, string key, int team, float now)
        {
            for (int i = killFeed.Length - 1; i > 0; i--) killFeed[i] = killFeed[i - 1];
            killFeed[0] = new KillEntry {
                Attacker = attacker, Victim = victim, Weapon = Catalog.Find(catalog.weapons, key)?.label ?? key,
                Tint = team >= 0 && team < catalog.teams.Length ? Catalog.Tint(catalog.teams[team].color) : Chalk,
                Time = now,
            };
            killFeedCount = Mathf.Min(killFeedCount + 1, killFeed.Length);
        }

        void PaintKillFeed(float w)
        {
            for (int i = 0; i < killFeedCount; i++)
            {
                var entry = killFeed[i];
                float age = Time.unscaledTime - entry.Time;
                float opacity = Mathf.Clamp01((3.7f - age) * 2.7f);
                if (opacity == 0) break;
                float x = w - 334 + (1 - Mathf.Clamp01(age * 12)) * 10;
                var row = new Rect(x, 151 + i * 30, 308, 28);
                var previous = GUI.color;
                GUI.color = new Color(1, 1, 1, opacity);
                Fill(row, new Color(Ink.r, Ink.g, Ink.b, Ink.a * opacity));
                Fill(new Rect(x, row.y, 2, row.height), new Color(entry.Tint.r, entry.Tint.g, entry.Tint.b, opacity));
                hudText.Draw(new Rect(x + 8, row.y, 101, row.height), entry.Attacker, caption);
                hudText.Draw(new Rect(x + 111, row.y, 11, row.height), "›", caption);
                hudText.Draw(new Rect(x + 125, row.y, 101, row.height), entry.Victim, caption);
                hudText.Draw(new Rect(x + 232, row.y, 68, row.height), entry.Weapon, caption);
                GUI.color = previous;
            }
        }

        static Vector2 Polar(float degrees) => new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), -Mathf.Cos(degrees * Mathf.Deg2Rad));

        static void Stroke(Vector2 a, Vector2 b, float width, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 delta = b - a;
            var matrix = GUI.matrix;
            var angle = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), angle, Vector3.one);
            Fill(new Rect(0, -width / 2, delta.magnitude, width), color);
            GUI.matrix = matrix;
        }

        static void Arc(Vector2 at, float radius, float start, float sweep, Color color, float width, int steps)
        {
            if (Event.current.type != EventType.Repaint || sweep <= .1f) return;
            Vector2 previous = at + Polar(start) * radius;
            for (int i = 1; i <= steps; i++)
            {
                var point = at + Polar(start + sweep * i / steps) * radius;
                Stroke(previous, point, width, color);
                previous = point;
            }
        }

        static void CornerPanel(Rect box, Color trim)
        {
            Fill(box, Ink);
            Fill(new Rect(box.x, box.y, box.width, 1), Subtle);
            Fill(new Rect(box.x, box.y, 33, 2), trim);
            Fill(new Rect(box.xMax - 1, box.yMax - 13, 1, 13), Subtle);
            Fill(new Rect(box.xMax - 20, box.yMax - 1, 20, 1), Subtle);
        }

        void PaintReticle(float w, float h)
        {
            if (!captured || scoreboard) return;
            Vector2 centerPoint = new Vector2(w / 2, h / 2);
            if (own[15].Int < 0 && own[16].Int < 0 && own[6].Int > 0 && !building)
            {
                Vector3 screen = view.WorldToViewportPoint(reticleWorld);
                if (screen.z > 0) centerPoint = new Vector2(screen.x * w, (1 - screen.y) * h);
            }
            PaintPilotIdentification(centerPoint);
            Color accent = Catalog.Tint(Catalog.Find(catalog.weapons, weaponName)?.color ?? "#daffff");
            accent = Color.Lerp(accent, Fault, dryPulse);
            float kick = tension * 4.6f + Mathf.Min(predicted.Velocity.magnitude * .15f, 2);
            bool aimingBuild = building && CanBuild;
            int feet = aimingBuild ? 4 : weaponName == "scatter" ? 4 : 3;
            float origin = aimingBuild ? 45 : weaponName == "scatter" ? 45 : 0;
            float distance = (weaponName == "lance" ? 7 : 10) + kick;
            for (int i = 0; i < feet; i++)
            {
                var radial = Polar(origin + 360f * i / feet);
                var inner = centerPoint + radial * distance;
                var outer = centerPoint + radial * (distance + (weaponName == "lance" ? 12 : 5));
                Stroke(inner, outer, 4, new Color(.02f,.035f,.055f,.92f));
                Stroke(inner, outer, 1.55f, aimingBuild ? aimedSite >= 0 ? lime : Chalk : accent);
            }
            if (weaponName == "rift" && !aimingBuild)
                Arc(centerPoint, distance + 4, Time.unscaledTime * 48, 260, accent, 1.4f, 19);
            if (weaponName == "arc" && !aimingBuild)
                Arc(centerPoint, distance + 7, 135, 90, accent, 1.4f, 7);
            Fill(new Rect(centerPoint.x - 2, centerPoint.y - 2, 4, 4), Ink);
            Fill(new Rect(centerPoint.x - 1, centerPoint.y - 1, 2, 2), Chalk);
            if (hit > 0)
            {
                Color response = confirmStyle == 3 ? Fault : confirmStyle == 2 ? Amber : confirmStyle == 1 ? Shield : Chalk;
                response.a = Mathf.Clamp01(hit * 2);
                float drift = (1 - hit) * 11;
                for (int i = 0; i < 4; i++)
                {
                    var arm = Polar(45 + i * 90);
                    Stroke(centerPoint + arm * (11 + drift), centerPoint + arm * (22 + drift), 2.4f, response);
                }
                if (confirmStyle == 3) Arc(centerPoint, 32, 15, 330, response, 1.3f, 22);
                if (confirmStyle == 2)
                    for (int i = 0; i < 4; i++)
                        Arc(centerPoint, 25 + drift, i * 90 + 19, 35, response, 1.6f, 4);
            }
            PaintIncoming(centerPoint, Time.unscaledTime);
            float reloading = ReloadProgress(Time.unscaledTime);
            if (reloading > 0 && !aimingBuild)
            {
                Arc(centerPoint, 30, 116, 128, Subtle, 2, 10);
                Arc(centerPoint, 30, 116, 128 * reloading, accent, 2, 10);
            }
            if (movementPulse > .1f && cameraMotion > .01f)
            {
                var motionTint = teamTint;
                motionTint.a = movementPulse * cameraMotion * .4f;
                Arc(centerPoint, 42, 156, 20, motionTint, 1.4f, 5);
                Arc(centerPoint, 42, 184, 20, motionTint, 1.4f, 5);
            }
        }

        float reloadPeriod = 1.55f;

        void RecordIncoming(Vector3 source, Color tint, bool heavy, float now)
        {
            float x = source.x - own[18].Float, z = source.z - own[20].Float;
            if (x * x + z * z < .01f) return;
            float heading = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
            int slot = 0;
            for (int i = 0; i < incomingHits.Length; i++)
            {
                var previous = incomingHits[i];
                if (previous.Until > now && Mathf.Abs(Mathf.DeltaAngle(previous.Heading, heading)) < 32)
                {
                    if (previous.Heavy && !heavy) return;
                    slot = i;
                    break;
                }
                if (previous.Until < incomingHits[slot].Until) slot = i;
            }
            incomingHits[slot] = new IncomingHit { Heading = heading, Until = now + IncomingDuration, Tint = tint, Heavy = heavy };
        }

        void PaintIncoming(Vector2 centerPoint, float now)
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var hit in incomingHits)
            {
                float life = (hit.Until - now) / IncomingDuration;
                if (life <= 0) continue;
                float direction = Mathf.DeltaAngle(view.transform.eulerAngles.y, hit.Heading);
                var danger = hit.Tint;
                danger.a = Mathf.SmoothStep(0, .95f, life);
                float sweep = hit.Heavy ? 42 : 29;
                Arc(centerPoint, 68, direction - sweep / 2, sweep, danger, hit.Heavy ? 4.5f : 3.2f, 9);
                Vector2 radial = Polar(direction);
                Stroke(centerPoint + radial * 55, centerPoint + radial * 62, 2.2f, danger);
                if (hit.Heavy && visualEffects > .01f)
                {
                    danger.a *= visualEffects * .7f;
                    Arc(centerPoint, 75, direction - 11, 22, danger, 1.5f, 5);
                }
            }
        }

        void UpdateShieldFeedback(int previous, int current, float now)
        {
            if (previous == current) return;
            if (current == 0)
            {
                shieldAlertUntil = now + .68f;
                shieldReturnUntil = 0;
            }
            else if (previous == 0)
            {
                shieldAlertUntil = 0;
                shieldReturnUntil = now + ShieldReturnDuration;
                effects.ShieldReturn();
            }
        }

        void UpdateVitals(float now, float dt)
        {
            float health = HealthFraction;
            float armor = Mathf.Clamp01(own[7].Float / 50);
            float fade = 1 - Mathf.Exp(-dt * (vitalsPulse > .36f ? 3 : 12));
            healthTrail = health > healthTrail ? health : Mathf.Lerp(healthTrail, health, fade);
            armorTrail = armor > armorTrail ? armor : Mathf.Lerp(armorTrail, armor, fade);
            vitalsPulse = Mathf.MoveTowards(vitalsPulse, 0, dt * 3);
            if (now >= shieldAlertUntil) shieldAlertUntil = 0;
            if (now >= shieldReturnUntil) shieldReturnUntil = 0;
        }

        void PaintVitals(float w, float h)
        {
            float health = HealthFraction;
            float armor = Mathf.Clamp01(own[7].Float / 50);
            Color vitality = Color.Lerp(Fault, teamTint, Mathf.Clamp01((health - .12f) * 2.8f));
            float y = h - 102;
            var plate = new Rect(26, y, 216, 78);
            CornerPanel(plate, Color.Lerp(vitality, damageTint, vitalsPulse * .58f));
            var dial = new Vector2(70, y + 39);
            Arc(dial, 28, 220, 280, Subtle, 4, 22);
            Arc(dial, 28, 220, 280 * healthTrail, Color.Lerp(Subtle, Fault, .53f), 4, 22);
            Arc(dial, 28, 220, 280 * health, vitality, 4, 22);
            if (health < .24f)
                Arc(dial, 39, 210, 300, new Color(Fault.r, Fault.g, Fault.b, .24f + (.24f - health) * 2), 1.4f, 22);
            Arc(dial, 34, 220, 280 * armorTrail, Color.Lerp(Subtle, Shield, .35f), 1.6f, 16);
            Arc(dial, 34, 220, 280 * armor, Shield, 1.6f, 16);
            if (mode != "ffa" && (shieldReturnUntil > 0 || shieldAlertUntil > 0))
                DrawFaction(new Vector2(70,y + 16),5.5f,own[3].Int,Color.Lerp(Shield,teamTint,.42f));
            if (shieldReturnUntil > 0 && visualEffects > .01f)
            {
                float life = Mathf.Clamp01((shieldReturnUntil - Time.unscaledTime) / ShieldReturnDuration);
                var glow = Shield;
                glow.a = life * visualEffects;
                Arc(dial, 37, 220, 280 * armor * Mathf.SmoothStep(0, 1, (1 - life) * 3), glow, 2.2f, 16);
            }
            hudText.Draw(new Rect(42, y + 25, 56, 29), own[6].Int.ToString(), center);
            hudText.Draw(new Rect(111, y + 9, 123, 22), catalog.teams[own[3].Int].name, section);
            string shieldLabel = shieldAlertUntil > 0 ? "ŠTÍT PRORAŽEN" : shieldReturnUntil > 0 ? "ŠTÍT OBNOVEN"
                : own[7].Int > 0 ? "ŠTÍT " + own[7].Int : "ZDRAVÍ";
            hudText.Draw(new Rect(111, y + 31, 120, 18), shieldLabel, caption);
            Segments(new Rect(111, y + 57, 108, 4), 9, health, vitality);

            var hull = state["vehicles"][own[16].Int];
            var weapon = Catalog.Find(catalog.weapons, weaponName);
            Color energy = Catalog.Tint(weapon?.color ?? "#cfcfff");
            float x = w - 239;
            CornerPanel(new Rect(x, y, 213, 78), energy);
            string tool = own[16].Int >= 0 ? (hull[2].Text == "bastion" ? "BASTION" : "SKIMMER")
                : own[15].Int >= 0 ? "HNÍZDO" : weapon?.label.ToUpperInvariant();
            hudText.Draw(new Rect(x + 13, y + 8, 173, 22), tool, section);
            var mags = own[10][System.Array.IndexOf(Arsenal, weaponName)];
            int ammo = VisibleAmmo(weaponName);
            float heat = state["turrets"][own[15].Int]["heat"].Float;
            string count = own[16].Int >= 0 ? hull[5].Int + " HP" : own[15].Int >= 0 ? Mathf.RoundToInt(heat * 100) + " %"
                : ammo + " / " + (mags[1].Int < 0 ? "∞" : mags[1].Int.ToString());
            hudText.Draw(new Rect(x + 12, y + 28, 174, 33), count, heading);
            float magazine = fireProfiles.TryGetValue(weaponName ?? string.Empty, out var profile) ? profile.Magazine :
                weaponName == "pulse" ? 24 : weaponName == "arc" ? 36 : weaponName == "scatter" ? 6 : 4;
            if (own[15].Int < 0 && own[16].Int < 0 && HasAlternate(weaponName ?? "pulse"))
            {
                string action = profile.AltName ?? "Slug";
                int cost = Mathf.Max(2, profile.AltCost);
                CornerPanel(new Rect(x, y - 28, 213, 23), ammo >= cost ? energy : Amber);
                hudText.Draw(new Rect(x + 10, y - 27, 194, 19), $"F   {action.ToUpperInvariant()}   ·   {cost}", caption);
            }
            if (own[16].Int < 0) Segments(new Rect(x + 14, y + 64, 185, 4), 12, own[15].Int >= 0 ? heat : ammo / magazine,
                own[15].Int < 0 && ammo <= Mathf.Max(1, magazine * .18f) ? Amber : energy);
        }

        static void Segments(Rect row, int count, float fraction, Color active)
        {
            var width = row.width / count;
            for (int i = 0; i < count; i++)
                Fill(new Rect(row.x + i * width, row.y, width - 2, row.height),
                    Color.Lerp(Subtle, active, Mathf.Clamp01(fraction * count - i)));
        }

        void PaintHordeGauge(float cx)
        {
            var defense = state["horde"];
            var core = defense["core"];
            float life = Mathf.Clamp01(core["hp"].Float / Mathf.Max(1, core["maxHp"].Float));
            int countdown = defense["countdown"].Int;
            int wave = defense["wave"].Int;
            CornerPanel(new Rect(cx - 190, 21, 380, 58), countdown > 0 ? lime : teamTint);
            string phase = countdown > 0 ? "PŘÍPRAVA  " + countdown + " s" : "VLNA " + wave + " / " + defense["waves"].Int;
            hudText.Draw(new Rect(cx - 177, 27, 160, 22), phase, section);
            if (countdown == 0) hudText.Draw(new Rect(cx - 177, 52, 160, 20), "HROZBY  " + defense["enemies"].Int, caption);
            else Segments(new Rect(cx - 175, 56, 144, 4), 8, countdown / 22f, lime);
            hudText.Draw(new Rect(cx - 7, 27, 172, 25), "JÁDRO  " + core["hp"].Int, section);
            var tint = Color.Lerp(Fault, teamTint, life);
            tint = Color.Lerp(tint, Fault, coreFlash);
            Segments(new Rect(cx - 4, 57, 178, 5), 12, life, tint);
        }

        void PaintWaveBanner(float cx, float h)
        {
            if (mode != "horde" || waveFlash <= 0 || !captured || scoreboard) return;
            var tint = waveStarts ? Fault : lime;
            var at = new Rect(cx - 131, h * .28f, 262, 36);
            CornerPanel(at, tint);
            hudText.Draw(at, waveStarts ? "VLNA  " + state["horde"]["wave"].Int + "  PŘICHÁZÍ" : "VLNA ODRAŽENA", center);
            Segments(new Rect(at.x + 12, at.yMax - 4, at.width - 24, 2), 12, waveFlash, tint);
        }

        void PaintRadar()
        {
            if (Event.current.type != EventType.Repaint) return;
            var p = new Vector2(106, 176);
            const float radius = 70, range = 24;
            if (radarPlate == null) radarPlate = MakeRadarPlate();
            GUI.DrawTexture(new Rect(p.x - radius - 1, p.y - radius - 1, radius * 2 + 2, radius * 2 + 2), radarPlate);
            Arc(p, radius, 0, 360, Subtle, 1.1f, 32);
            float heading = view.transform.eulerAngles.y;
            Arc(p, radius + 3, -24, 48, teamTint, 2.1f, 8);
            for (int mark = 0; mark < 4; mark++)
            {
                var arm = Polar(mark * 90 - heading);
                Stroke(p + arm * (radius - 5), p + arm * (radius + 4), mark == 0 ? 2.5f : 1.2f, mark == 0 ? Chalk : Subtle);
            }
            Vector2 Map(Vector3 position)
            {
                var d = Quaternion.Euler(0, -heading, 0) * (position - display);
                return p + Vector2.ClampMagnitude(new Vector2(d.x, -d.z) * (radius / range), radius - 4);
            }
            Stroke(p + new Vector2(-4, 4), p + new Vector2(0, -5), 2, Chalk);
            Stroke(p + new Vector2(0, -5), p + new Vector2(4, 4), 2, Chalk);
            if (mode != "ffa") foreach (var actor in pilots.Values)
            {
                if (actor.Row[6].Int <= 0 || actor.Row[3].Int != own[3].Int) continue;
                var spot = Map(actor.Position);
                Fill(new Rect(spot.x - 2, spot.y - 2, 4, 4), teamTint);
            }
            if (scanLeft > 0)
            {
                Arc(p, radius - 4, Time.unscaledTime * 140, 68, Amber, 1.6f, 9);
                foreach (var actor in pilots.Values)
                {
                    if (actor.Row[6].Int <= 0 || mode != "ffa" && actor.Row[3].Int == own[3].Int) continue;
                    var threat = Map(actor.Position);
                    Stroke(threat + new Vector2(-4, 3), threat + new Vector2(0, -3), 1.7f, Fault);
                    Stroke(threat + new Vector2(0, -3), threat + new Vector2(4, 3), 1.7f, Fault);
                }
            }
            if (world.DefendedCore != null && world.DefendedCore.gameObject.activeSelf)
                RadarDiamond(Map(world.DefendedCore.position), teamTint);
            if (mode == "dom" || mode == "siege")
            {
                var frames = mode == "dom" ? state["points"] : state["siege"]["nodes"];
                for (int i = 0; i < world.Zones.Length; i++)
                {
                    int owner = frames[i]["owner"].Int;
                    RadarDiamond(Map(world.Zones[i].position), owner < 0 ? Chalk : Catalog.Tint(catalog.teams[owner].color));
                }
            }
            for (int i = 0; i < world.Flags.Length; i++) RadarDiamond(Map(world.Flags[i].position), Catalog.Tint(catalog.teams[i].color));
        }

        static void RadarDiamond(Vector2 at, Color color)
        {
            for (int i = 0; i < 4; i++)
                Stroke(at + Polar(i * 90) * 5, at + Polar((i + 1) * 90) * 5, 1.8f, color);
        }

        static Texture2D MakeRadarPlate()
        {
            const int resolution = 128;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            var pixels = new Color32[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float distance = new Vector2(x - 63.5f, y - 63.5f).magnitude;
                float grid = Mathf.Min(Mathf.Abs(distance - 26), Mathf.Abs(distance - 46)) < .8f
                    || (x % 12 == 4 && y % 12 == 4) ? .16f : 0;
                pixels[y * resolution + x] = new Color(.025f + grid, .06f + grid, .084f + grid, Mathf.Clamp01(64 - distance) * .89f);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
