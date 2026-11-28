using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        sealed class BuildSite
        {
            internal Transform Marker, Beacon, Model, Gun;
            internal BoxSpec Box;
            internal string Kind;
            internal int Health, Capacity;
            internal float HitAt = -9;
        }

        static readonly string[] BuildKinds = { "barricade", "turret" };
        static readonly string[] BuildNames = { "BARIKÁDA", "VĚŽ" };

        readonly Dictionary<int, BuildSite> sites = new Dictionary<int, BuildSite>();
        readonly List<BoxSpec> obstacles = new List<BoxSpec>();
        readonly List<(Vector3 At, string Text, float Born)> salvageFloats = new();
        ArenaSpec movement;
        WireValue construction;
        bool building;
        string buildKind = "barricade";
        int aimedSite = -1, nearSite = -1;
        float scrapPulse;
        Transform ghost;
        string ghostShape;
        Material ghostReady, ghostBlocked;

        // Defenders can build whenever they are alive: during preparation freely, during a wave as long
        // as no infected is at the socket (the server checks; the ghost turns red when it would refuse).
        bool CanBuild => mode == "horde" && own != null && own[6].Int > 0 && construction != null
            && state["winner"]["label"].Text == null;

        bool Preparing => state["horde"]["countdown"].Int > 0;

        void ResetConstruction()
        {
            sites.Clear();
            obstacles.Clear();
            salvageFloats.Clear();
            movement = null;
            construction = null;
            building = false;
            aimedSite = nearSite = -1;
            buildKind = "barricade";
            ghost = null;
            ghostShape = null;
        }

        bool ReadConstruction()
        {
            if (!CanBuild) { building = false; return false; }
            if (Input.GetKeyDown(KeyCode.B)) building = !building;
            if (!building) return false;
            if (Input.GetKeyDown(KeyCode.Alpha1)) buildKind = BuildKinds[0];
            if (Input.GetKeyDown(KeyCode.Alpha2)) buildKind = BuildKinds[1];
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .1f)
                buildKind = BuildKinds[(System.Array.IndexOf(BuildKinds, buildKind) + 1) % BuildKinds.Length];
            if (Input.GetMouseButtonDown(0) && aimedSite >= 0)
            {
                var site = sites[aimedSite];
                string kind = site.Kind != null ? "repair" : buildKind;
                if (site.Kind != null && site.Health >= site.Capacity) AddNotice("Obrana je v pořádku.");
                else if (construction["scrap"].Int < Cost(kind)) AddNotice("Týmu chybí šrot.");
                else if (Contested(site)) AddNotice("Nepřátelé jsou u stavebního místa.");
                else if (site.Kind == null && Standing(site)) AddNotice("Odstup ze stavebního místa.");
                else socket.Send($"{{\"type\":\"build\",\"site\":{aimedSite},\"kind\":\"{kind}\"}}");
            }
            return true;
        }

        int Cost(string kind) => kind == "repair" ? construction["repair"]["cost"].Int : construction["types"][kind]["cost"].Int;

        // Mirrors the server's clearance rule so the ghost and the hint can warn before a refused click.
        bool Contested(BuildSite site)
        {
            if (Preparing) return false;
            float clearance = construction["clearance"].Float;
            var at = site.Marker.position;
            foreach (var actor in pilots.Values)
                if (actor.Row[6].Int > 0 && actor.Row[31].Text != null
                    && new Vector2(actor.Position.x - at.x, actor.Position.z - at.z).sqrMagnitude < clearance * clearance) return true;
            return false;
        }

        // The server refuses a socket a pilot stands on; the most common case is the builder's own body.
        bool Standing(BuildSite site)
        {
            var shape = construction["types"][buildKind];
            var at = site.Marker.position;
            float angle = site.Marker.eulerAngles.y * Mathf.Deg2Rad, dx = display.x - at.x, dz = display.z - at.z;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            return Mathf.Abs(dx * cos - dz * sin) < shape["w"].Float / 2 + .43f && Mathf.Abs(dx * sin + dz * cos) < shape["d"].Float / 2 + .43f;
        }

        void SyncConstruction(WireValue frame)
        {
            if (mode != "horde" || frame["sites"].Count == 0) return;
            construction = frame;
            bool changed = false;
            foreach (var entry in frame["sites"].Items)
            {
                int id = entry["id"].Int;
                if (!sites.TryGetValue(id, out var site))
                {
                    var at = new Vector3(entry["x"].Float, entry["y"].Float, entry["z"].Float);
                    var marker = TriasWorld.Group(world.Root, "Construction socket " + id, at);
                    marker.rotation = Quaternion.Euler(0, entry["angle"].Float * Mathf.Rad2Deg, 0);
                    TriasWorld.Part(marker, world.Dark, Vector3.up * .035f, new Vector3(1.85f, .035f, 1.85f), PrimitiveType.Cylinder);
                    // A lit footprint and a tall beacon make free sockets findable from across the arena.
                    var glow = world.MaterialAt("#c7ff6e", true);
                    TriasWorld.Part(marker, glow, Vector3.up * .08f, new Vector3(1.05f, .012f, .11f));
                    TriasWorld.Part(marker, glow, Vector3.up * .081f, new Vector3(.11f, .012f, 1.05f));
                    for (int side = 0; side < 4; side++)
                    {
                        var edge = Quaternion.Euler(0, side * 90, 0);
                        var bar = TriasWorld.Part(marker, glow, edge * new Vector3(0, .07f, .9f), new Vector3(1.3f, .02f, .05f));
                        bar.localRotation = edge;
                    }
                    var beacon = TriasWorld.Group(marker, "Socket beacon");
                    TriasWorld.Part(beacon, glow, Vector3.up * 1.6f, new Vector3(.07f, 1.6f, .07f), PrimitiveType.Cylinder);
                    TriasWorld.Part(beacon, glow, Vector3.up * 3.25f, new Vector3(.26f, .05f, .26f), PrimitiveType.Cylinder);
                    site = new BuildSite { Marker = marker, Beacon = beacon };
                    sites.Add(id, site);
                }
                string kind = entry["kind"].Text;
                site.Health = entry["hp"].Int;
                site.Capacity = entry["maxHp"].Int;
                if (site.Kind != kind)
                {
                    changed = true;
                    if (site.Model != null)
                    {
                        site.Model.gameObject.SetActive(false);
                        Destroy(site.Model.gameObject);
                    }
                    site.Model = site.Gun = null;
                    site.Box = null;
                    site.Kind = kind;
                    if (kind != null) MakeDefense(site, frame["types"][kind], entry);
                }
                if (site.Gun != null)
                    site.Gun.rotation = Quaternion.Euler(-entry["pitch"].Float * Mathf.Rad2Deg, entry["yaw"].Float * Mathf.Rad2Deg, 0);
            }
            if (!changed) return;
            obstacles.Clear();
            obstacles.AddRange(world.Spec.cover);
            foreach (var site in sites.Values) if (site.Box != null) obstacles.Add(site.Box);
            movement = world.Spec.WithCover(obstacles.ToArray());
        }

        void MakeDefense(BuildSite site, WireValue shape, WireValue entry)
        {
            float w = shape["w"].Float, d = shape["d"].Float, h = shape["h"].Float;
            var size = new Vector3(w, h, d);
            var at = site.Marker.position;
            site.Box = new BoxSpec { x = at.x, z = at.z, w = w, d = d, bottom = at.y, h = at.y + h, angle = entry["angle"].Float };
            bool tower = site.Kind == "turret";
            site.Model = TriasWorld.Group(world.Root, tower ? "Sentry turret" : "Barricade", at);
            site.Model.rotation = site.Marker.rotation;
            var solid = TriasWorld.Part(site.Model, world.Dark, Vector3.up * h / 2, size, PrimitiveType.Cube, true);
            solid.GetComponent<Renderer>().enabled = !world.Kit.SupplyCrate(site.Model, !tower, size);
            TriasWorld.Part(site.Model, world.TeamLight[0], new Vector3(0, h * .28f, -d * .505f), new Vector3(w * .72f, .035f, .022f));
            if (tower)
            {
                TriasWorld.Part(site.Model, world.Steel, Vector3.up * (h + .135f), new Vector3(.40f, .15f, .40f), PrimitiveType.Cylinder);
                site.Gun = TriasWorld.Group(site.Model, "Automatic blaster", Vector3.up * 1.27f);
                site.Gun.localScale = Vector3.one * 1.40f;
                TriasModels.Gun(world, site.Gun, "arc", world.TeamLight[0]);
            }
        }

        // Outline of the structure that a click would place: twelve lit edges, lime when the server would
        // accept it, red when salvage is short or infected stand at the socket.
        void ShowGhost(BuildSite site, bool ready)
        {
            ghostReady ??= world.MaterialAt("#c7ff6e", true);
            ghostBlocked ??= world.MaterialAt("#ff5a4e", true);
            if (ghostShape != buildKind || ghost == null)
            {
                if (ghost != null) Destroy(ghost.gameObject);
                ghost = TriasWorld.Group(world.Root, "Construction preview");
                var shape = construction["types"][buildKind];
                float w = shape["w"].Float, d = shape["d"].Float, h = shape["h"].Float, t = .045f;
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    TriasWorld.Part(ghost, ghostReady, new Vector3(sx * w / 2, h / 2, sz * d / 2), new Vector3(t, h, t));
                foreach (float y in new[] { .02f, h })
                for (int s = -1; s <= 1; s += 2)
                {
                    TriasWorld.Part(ghost, ghostReady, new Vector3(0, y, s * d / 2), new Vector3(w, t, t));
                    TriasWorld.Part(ghost, ghostReady, new Vector3(s * w / 2, y, 0), new Vector3(t, t, d));
                }
                ghostShape = buildKind;
            }
            ghost.SetPositionAndRotation(site.Marker.position, site.Marker.rotation);
            var paint = ready ? ghostReady : ghostBlocked;
            foreach (Transform edge in ghost)
            {
                var renderer = edge.GetComponent<Renderer>();
                if (renderer.sharedMaterial != paint) renderer.sharedMaterial = paint;
            }
            ghost.gameObject.SetActive(true);
        }

        void RenderConstruction()
        {
            aimedSite = nearSite = -1;
            if (construction == null) return;
            float range = construction["range"].Float;
            float closest = .91f, nearest = range * range;
            bool canBuild = CanBuild;
            foreach (var pair in sites)
            {
                var site = pair.Value;
                site.Marker.gameObject.SetActive(canBuild && site.Kind == null);
                site.Beacon.gameObject.SetActive(Preparing || building);
                if (!canBuild || !captured) continue;
                Vector3 point = site.Marker.position;
                if (Mathf.Abs(point.y - display.y) > 1.4f) continue;
                float flat = new Vector2(point.x - display.x, point.z - display.z).sqrMagnitude;
                if (flat > range * range) continue;
                if (flat < nearest) { nearest = flat; nearSite = pair.Key; }
                if (!building) continue;
                Vector3 aim = point + Vector3.up * (site.Kind == null ? .2f : .6f) - view.transform.position;
                float angle = Vector3.Dot(view.transform.forward, aim.normalized);
                if (angle < closest) continue;
                if (Physics.Raycast(view.transform.position, aim.normalized, out var blocker, aim.magnitude - .05f, TriasWorld.TerrainMask)
                    && (site.Model == null || !blocker.transform.IsChildOf(site.Model))) continue;
                closest = angle;
                aimedSite = pair.Key;
            }
            bool preview = building && aimedSite >= 0 && sites[aimedSite].Kind == null;
            if (preview)
            {
                var site = sites[aimedSite];
                ShowGhost(site, construction["scrap"].Int >= Cost(buildKind) && !Contested(site) && !Standing(site));
            }
            else if (ghost != null) ghost.gameObject.SetActive(false);
        }

        void NoteSalvage(WireValue evt, float now)
        {
            scrapPulse = 1;
            var at = evt["position"];
            if (salvageFloats.Count > 12) salvageFloats.RemoveAt(0);
            salvageFloats.Add((new Vector3(at[0].Float, at[1].Float, at[2].Float), "+" + evt["amount"].Int + " ŠROT", now));
        }

        void NoteStructureHit(WireValue evt, float now)
        {
            if (sites.TryGetValue(evt["site"].Int, out var site)) site.HitAt = now;
        }

        string ConstructionPrompt()
        {
            if (!CanBuild || building || nearSite < 0) return null;
            var site = sites[nearSite];
            if (site.Kind == null) return "B · stavět   ·   ŠROT " + construction["scrap"].Int;
            return site.Health < site.Capacity ? "B · opravit " + site.Health + "/" + site.Capacity + " HP" : null;
        }

        void PaintConstruction(float w, float h, float scale)
        {
            if (construction == null) return;
            float now = Time.unscaledTime;
            scrapPulse = Mathf.MoveTowards(scrapPulse, 0, Time.unscaledDeltaTime * 2.2f);
            float cx = w / 2;
            int scrap = construction["scrap"].Int;
            float width = building ? 470 : 230;
            var bar = new Rect(cx - width / 2, 69, width, building ? 52 : 27);
            HudStrip(bar, CanBuild ? Color.Lerp(lime, Amber, scrapPulse) : Subtle);
            if (scrapPulse > 0) Fill(new Rect(bar.x, bar.yMax - 2, bar.width * scrapPulse, 2), Amber);
            if (!building)
            {
                hudText.Draw(new Rect(bar.x, bar.y, bar.width, 27), CanBuild ? "ŠROT  " + scrap + "    ·    B STAVĚNÍ" : "ŠROT  " + scrap, center);
            }
            else
            {
                // Two cards: the selected one lit, each priced and dimmed when the team cannot afford it.
                for (int i = 0; i < BuildKinds.Length; i++)
                {
                    int cost = Cost(BuildKinds[i]);
                    bool chosen = buildKind == BuildKinds[i], affordable = scrap >= cost;
                    var card = new Rect(bar.x + 8 + i * 152, bar.y + 4, 146, 25);
                    Fill(card, chosen ? new Color(.20f, .27f, .10f, .95f) : new Color(.05f, .08f, .11f, .9f));
                    if (chosen) Fill(new Rect(card.x, card.yMax - 2, card.width, 2), lime);
                    hudText.Draw(card, (i + 1) + "  " + BuildNames[i] + "  " + cost, affordable ? (chosen ? section : caption) : caption);
                    if (!affordable) Fill(new Rect(card.x + 4, card.center.y, card.width - 8, 1), Fault);
                }
                hudText.Draw(new Rect(bar.x + 312, bar.y + 4, 150, 25), "ŠROT  " + scrap, section);
                string action = "Zamiř na stavební místo   ·   kolečko mění   ·   B zavřít";
                if (aimedSite >= 0)
                {
                    var site = sites[aimedSite];
                    int cost = Cost(site.Kind != null ? "repair" : buildKind);
                    action = site.Kind != null
                        ? site.Health >= site.Capacity ? "Obrana je v pořádku" : "KLIK  opravit +" + construction["repair"]["hp"].Int + " HP  ·  " + cost + " šrotu"
                        : Contested(site) ? "Nepřátelé u místa · počkej"
                        : Standing(site) ? "Stojíš na místě · odstup"
                        : scrap < cost ? "Chybí šrot · " + (cost - scrap) : "KLIK  postavit " + BuildNames[System.Array.IndexOf(BuildKinds, buildKind)];
                }
                hudText.Draw(new Rect(bar.x, bar.y + 28, bar.width, 23), action, caption);
            }
            if (!captured || scoreboard) return;
            foreach (var pair in sites)
            {
                var site = pair.Value;
                Vector3 at = site.Marker.position;
                float distance = (at - display).sqrMagnitude;
                bool selected = pair.Key == aimedSite;
                if (site.Kind != null)
                {
                    // Every built defense carries a health bar; it flashes red while it is being struck.
                    if (distance > 34 * 34) continue;
                    float life = site.Health / Mathf.Max(1f, site.Capacity);
                    Color tint = now - site.HitAt < .45f ? Fault : Color.Lerp(Fault, lime, life);
                    string name = site.Kind == "turret" ? "VĚŽ" : "BARIKÁDA";
                    Objective(at + Vector3.up * (site.Kind == "turret" ? 2.05f : 1.6f), selected ? name + " · OPRAVIT" : name,
                        tint, h, scale, life >= 1 ? 0 : Mathf.Max(.02f, life));
                }
                else if (CanBuild && (building || Preparing) && distance < 40 * 40)
                    Objective(at + Vector3.up * (building ? .6f : 3.55f), selected ? "STAVĚT ZDE" : "STAVEBNÍ MÍSTO", selected ? lime : Amber, h, scale);
            }
            for (int i = salvageFloats.Count - 1; i >= 0; i--)
            {
                var (at, text, born) = salvageFloats[i];
                float age = now - born;
                if (age > 1.2f) { salvageFloats.RemoveAt(i); continue; }
                Vector3 pixel = view.WorldToScreenPoint(at + Vector3.up * age * .9f);
                if (pixel.z < .5f) continue;
                var previous = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((1.2f - age) * 2.5f));
                hudText.Draw(new Rect(pixel.x / scale - 50, h - pixel.y / scale - 12, 100, 24), text, center);
                GUI.color = previous;
            }
        }
    }
}
