using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        sealed class BuildSite
        {
            internal Transform Marker, Model, Gun;
            internal BoxSpec Box;
            internal string Kind;
            internal int Health, Capacity;
        }

        readonly Dictionary<int, BuildSite> sites = new Dictionary<int, BuildSite>();
        readonly List<BoxSpec> obstacles = new List<BoxSpec>();
        ArenaSpec movement;
        WireValue construction;
        bool building;
        string buildKind = "barricade";
        int aimedSite = -1;

        bool CanBuild => own != null && own[6].Int > 0 && construction != null
            && state["horde"]["countdown"].Int > 0 && state["winner"]["label"].Text == null;

        void ResetConstruction()
        {
            sites.Clear();
            obstacles.Clear();
            movement = null;
            construction = null;
            building = false;
            aimedSite = -1;
            buildKind = "barricade";
        }

        bool ReadConstruction()
        {
            if (!CanBuild) return false;
            if (Input.GetKeyDown(KeyCode.B)) building = !building;
            if (!building) return false;
            if (Input.GetKeyDown(KeyCode.Alpha1)) buildKind = "barricade";
            if (Input.GetKeyDown(KeyCode.Alpha2)) buildKind = "turret";
            if (Input.GetMouseButtonDown(0) && aimedSite >= 0)
            {
                if (construction["scrap"].Int < construction["types"][buildKind]["cost"].Int)
                    AddNotice("Týmu chybí šrot.");
                else
                    socket.Send($"{{\"type\":\"build\",\"site\":{aimedSite},\"kind\":\"{buildKind}\"}}");
            }
            return true;
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
                    var at = new Vector3(entry["x"].Float, 0, entry["z"].Float);
                    var marker = TriasWorld.Group(world.Root, "Construction socket " + id, at);
                    marker.rotation = Quaternion.Euler(0, entry["angle"].Float * Mathf.Rad2Deg, 0);
                    TriasWorld.Part(marker, world.Dark, Vector3.up * .035f, new Vector3(1.65f, .035f, 1.65f), PrimitiveType.Cylinder);
                    TriasWorld.Part(marker, world.TeamLight[0], Vector3.up * .08f, new Vector3(.95f, .009f, .104f));
                    TriasWorld.Part(marker, world.TeamLight[0], Vector3.up * .081f, new Vector3(.104f, .009f, .95f));
                    site = new BuildSite { Marker = marker };
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
            site.Box = new BoxSpec { x = at.x, z = at.z, w = w, d = d, h = h, angle = entry["angle"].Float };
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

        void RenderConstruction()
        {
            aimedSite = -1;
            if (construction == null) return;
            float range = construction["range"].Float;
            float closest = .925f;
            foreach (var pair in sites)
            {
                var site = pair.Value;
                site.Marker.gameObject.SetActive(CanBuild && site.Kind == null);
                if (!building || !captured || site.Kind != null) continue;
                Vector3 point = site.Marker.position;
                if (Vector2.Distance(new Vector2(point.x, point.z), new Vector2(display.x, display.z)) > range) continue;
                Vector3 aim = point + Vector3.up * .20f - view.transform.position;
                float angle = Vector3.Dot(view.transform.forward, aim.normalized);
                if (angle < closest || Physics.Raycast(view.transform.position, aim.normalized, aim.magnitude - .05f, TriasWorld.TerrainMask)) continue;
                closest = angle;
                aimedSite = pair.Key;
            }
        }

        void PaintConstruction(float w, float h, float scale)
        {
            if (construction == null) return;
            float cx = w / 2;
            Panel(new Rect(cx - 215, 86, 430, 53));
            float x = cx - 208;
            GUI.Label(new Rect(x, 87, 416, 23), "TÝMOVÝ ŠROT  " + construction["scrap"].Int, center);
            string hint = CanBuild ? "B   STAVĚNÍ" : "ZÍSKEJ ŠROT VYŘAZOVÁNÍM NEPŘÁTEL";
            if (building)
            {
                var types = construction["types"];
                string wall = (buildKind == "barricade" ? "● " : "") + "1 BARIKÁDA " + types["barricade"]["cost"].Int;
                string tower = (buildKind == "turret" ? "● " : "") + "2 VĚŽ " + types["turret"]["cost"].Int;
                hint = wall + "    ·    " + tower + "    ·    B ZAVŘÍT";
            }
            GUI.Label(new Rect(x, 110, 416, 26), hint, center);
            if (!captured || scoreboard || !building) return;
            foreach (var pair in sites)
            {
                var site = pair.Value;
                Vector3 at = site.Marker.position;
                if ((at - display).sqrMagnitude > 13 * 13) continue;
                bool selected = pair.Key == aimedSite;
                string label = site.Kind != null ? site.Health + "/" + site.Capacity + " HP"
                    : selected ? "KLIKNI PRO STAVBU" : "STAVEBNÍ MÍSTO";
                Objective(at + Vector3.up * (site.Kind == null ? .48f : 1.78f), label, selected ? lime : teamTint, h, scale);
            }
        }
    }
}
