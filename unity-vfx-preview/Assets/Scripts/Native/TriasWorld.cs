using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Trias.Native
{
    internal sealed class TriasWorld : IDisposable
    {
        internal readonly ArenaSpec Spec;
        internal readonly TriasKit Kit;
        internal readonly Transform Root, Actors;
        internal readonly Material Dark, Steel, Glass, White, TeamNeutral;
        internal readonly Material[] Team = new Material[3];
        internal readonly Material[] TeamLight = new Material[3];
        internal readonly Transform[] Pickups, Zones, Flags, Cores, CoreShield, Turrets;
        internal readonly LineRenderer[] Links;
        internal readonly Transform DefendedCore;
        internal readonly Material Hostile;
        internal readonly List<Material> Paint = new List<Material>();
        internal const int Decor = 8;
        internal const int TerrainMask = ~(1 << Decor);
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Transform> spinning = new List<Transform>();
        readonly List<Transform> animals = new List<Transform>();
        readonly Material water;
        readonly bool island;
        readonly Color sunColor;
        readonly System.Random random = new System.Random(719);
        internal float Height(float x, float z) => Spec.terrain?.At(x, z) ?? 0;

        internal TriasWorld(ArenaSpec spec, Catalog catalog, string mode, Camera view)
        {
            Spec = spec;
            Root = Group(null, spec.label);
            Actors = Group(Root, "Piloti, vozidla a projektily");
            island = spec.id == "island";
            var fog = Catalog.Tint(island ? "#a6d8d9" : spec.id == "foundry" ? "#402a37" : spec.id == "basin" ? "#3c394b" : "#101827");
            sunColor = Catalog.Tint(island ? "#fff6d2" : spec.id == "foundry" ? "#ffb79f" : "#cee9ff");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = island ? 0.012f : spec.id == "basin" ? 0.012f : 0.009f;
            RenderSettings.fogColor = fog;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = island ? Catalog.Tint("#a6ceaf") : Catalog.Tint("#626f91");
            RenderSettings.ambientEquatorColor = Catalog.Tint("#424653");
            RenderSettings.ambientGroundColor = Catalog.Tint("#424448");
            view.backgroundColor = fog;
            view.clearFlags = CameraClearFlags.SolidColor;
            var lamp = Group(Root, "Key").gameObject.AddComponent<Light>();
            lamp.type = LightType.Directional;
            lamp.intensity = island ? 1.35f : 1.52f;
            lamp.color = sunColor;
            lamp.shadows = LightShadows.Soft;
            lamp.shadowStrength = 0.69f;
            lamp.transform.rotation = Quaternion.Euler(47, -31, 0);

            Dark = MaterialAt("#182332");
            Steel = MaterialAt("#8091a9", false, "armor");
            White = MaterialAt("#dce6db", false, "armor");
            Glass = MaterialAt("#183246", true);
            TeamNeutral = MaterialAt("#aac5d6", true);
            Hostile = MaterialAt("#ff5454", true);
            Kit = new TriasKit(this);
            water = MaterialAt("#278caa", false, "water");
            water.SetTextureScale("_MainTex", new Vector2(1.8f, 1.0f));
            water.SetFloat("_Glossiness", 0.73f);
            water.EnableKeyword("_EMISSION");
            water.SetColor("_EmissionColor", Catalog.Tint("#104854"));
            for (int i = 0; i < Team.Length; i++)
            {
                Team[i] = MaterialAt(catalog.teams[i].color);
                TeamLight[i] = MaterialAt(catalog.teams[i].color, true);
            }
            if (island) Meadow();
            else Station();

            foreach (var box in Spec.cover)
            {
                var home = Group(Root, box.nature ?? (box.gantry ? "Suspended gantry" : box.relay ? "Reactor plinth" : "Cover"),
                    new Vector3(box.x, box.bottom, box.z));
                home.localRotation = Quaternion.Euler(0, box.angle * Mathf.Rad2Deg, 0);
                float h = box.h - box.bottom;
                if (island) IslandCover(box, home, h);
                else
                {
                    var alloy = walls;
                    Part(home, alloy, Vector3.up * h / 2, new Vector3(box.w, h, box.d), PrimitiveType.Cube, true);
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Part(home, Dark, new Vector3(0, h - 0.1f, sign * (box.d * 0.5f + 0.023f)), new Vector3(box.w, 0.14f, 0.06f));
                        Part(home, box.sector >= 0 ? TeamLight[box.sector] : TeamNeutral,
                            new Vector3(0, h - 0.25f, sign * (box.d * 0.5f + 0.071f)), new Vector3(box.w * 0.7f, 0.046f, 0.022f));
                    }
                }
            }
            var gold = MaterialAt("#ffae62", true);
            foreach (var pad in Spec.launchPads)
            {
                Part(Root, Dark, pad.Position + Vector3.up * 0.035f, new Vector3(1.85f, 0.04f, 1.85f), PrimitiveType.Cylinder);
                Ring(Root, pad.Position + Vector3.up * 0.083f, 0.92f, gold, 0.055f);
                Ring(Root, pad.Position + Vector3.up * 0.087f, 0.55f, gold);
            }

            Pickups = new Transform[spec.pickups.Length];
            for (int i = 0; i < Pickups.Length; i++)
            {
                var item = spec.pickups[i];
                Part(Root, island ? White : Dark, item.Position + Vector3.up * 0.045f, new Vector3(0.96f, 0.048f, 0.96f), PrimitiveType.Cylinder);
                var mark = Group(Root, item.type, item.Position + Vector3.up * 0.64f);
                var choice = Catalog.Find(catalog.weapons, item.type) ?? Catalog.Find(catalog.powerups, item.type);
                var tint = MaterialAt(choice?.color ?? "#f6faab", true);
                if (Catalog.Find(catalog.weapons, item.type) != null) TriasModels.Gun(this, mark, item.type, tint);
                else
                {
                    Part(mark, tint, Vector3.zero, new Vector3(0.40f, 0.13f, 0.12f));
                    Part(mark, tint, Vector3.zero, new Vector3(0.13f, 0.40f, 0.12f));
                }
                Pickups[i] = mark;
                spinning.Add(mark);
            }

            Site[] control = mode == "siege" && spec.siege != null ? spec.siege.nodes : mode == "dom" ? spec.points : Array.Empty<Site>();
            Zones = new Transform[control.Length];
            for (int i = 0; i < control.Length; i++)
            {
                var site = control[i];
                Zones[i] = Ring(Root, site.Position + Vector3.up * 0.082f, site.radius > 0 ? site.radius : 3.05f, TeamNeutral, 0.105f).transform;
                Part(Zones[i], Steel, Vector3.up * 0.13f, new Vector3(0.35f, 0.18f, 0.35f), PrimitiveType.Cylinder);
            }
            CoreShield = mode == "siege" ? new Transform[3] : Array.Empty<Transform>();
            Cores = new Transform[CoreShield.Length];
            Links = mode == "siege" ? new LineRenderer[spec.siege.links.Length] : Array.Empty<LineRenderer>();
            for (int i = 0; i < CoreShield.Length; i++)
            {
                Vector3 at = spec.siege.cores[i].Position;
                var orb = Cores[i] = Group(Root, "Reactor core", at);
                Part(orb, Team[i], Vector3.zero, new Vector3(1.46f, 2.55f, 1.46f), PrimitiveType.Sphere);
                Part(orb, TeamLight[i], Vector3.zero, new Vector3(0.33f, 2.60f, 0.33f), PrimitiveType.Sphere);
                CoreShield[i] = Ring(Root, at, 1.42f, TeamLight[i], 0.10f).transform;
                var rim = Ring(orb, Vector3.zero, 0.89f, TeamNeutral);
                rim.transform.localRotation = Quaternion.Euler(38, 0, 21);
            }
            for (int i = 0; i < Links.Length; i++)
            {
                var link = spec.siege.links[i];
                Links[i] = Line(Root, spec.siege.nodes[link.a].Position + Vector3.up * 0.19f,
                    spec.siege.nodes[link.b].Position + Vector3.up * 0.19f, TeamNeutral, 0.056f);
            }
            if (mode == "horde")
            {
                Vector3 at = spec.bases[0].Position;
                Part(Root, Dark, at + Vector3.up * .20f, new Vector3(2.70f, .20f, 2.7f), PrimitiveType.Cylinder);
                DefendedCore = Group(Root, "Defended core", at + Vector3.up * .78f);
                Part(DefendedCore, Team[0], Vector3.zero, new Vector3(1.15f, 1.51f, 1.15f), PrimitiveType.Sphere);
                Part(DefendedCore, TeamLight[0], Vector3.zero, new Vector3(.41f, 1.56f, .41f), PrimitiveType.Sphere);
                var shell = Group(DefendedCore, "Core field");
                Ring(shell, Vector3.zero, 1.24f, TeamLight[0], .08f).transform.localRotation = Quaternion.Euler(24, 0, 11);
                Ring(shell, Vector3.zero, 1.30f, TeamNeutral, .05f).transform.localRotation = Quaternion.Euler(-24, 0, -11);
                spinning.Add(shell);
            }

            Flags = mode == "ctf" ? new Transform[3] : Array.Empty<Transform>();
            for (int i = 0; i < Flags.Length; i++)
            {
                Flags[i] = Group(Root, "CTF · " + catalog.teams[i].name, spec.bases[i].Position);
                Part(Flags[i], Steel, Vector3.up * 0.86f, new Vector3(0.065f, 1.72f, 0.065f));
                Part(Flags[i], TeamLight[i], new Vector3(0.42f, 1.38f, 0), new Vector3(0.82f, 0.48f, 0.043f));
            }

            Turrets = new Transform[spec.turrets.Length];
            for (int i = 0; i < Turrets.Length; i++)
            {
                var site = spec.turrets[i];
                Part(Root, Dark, site.Position + Vector3.up * 0.32f, new Vector3(1.1f, 0.33f, 1.1f), PrimitiveType.Cylinder);
                Turrets[i] = Group(Root, "Energetické hnízdo", site.Position + Vector3.up * 1.03f);
                Turrets[i].rotation = Quaternion.Euler(0, (site.yaw + Mathf.PI) * Mathf.Rad2Deg, 0);
                TriasModels.Gun(this, Turrets[i], "arc", gold);
            }
            foreach (var pad in spec.repairs ?? Array.Empty<Site>())
                Ring(Root, pad.Position + Vector3.up * 0.05f, pad.radius, MaterialAt("#a0ff96", true), 0.11f);
        }

        Material walls, earth, bark, grassCrown;
        void Station()
        {
            bool basin = Spec.id == "basin", foundry = Spec.id == "foundry";
            walls = MaterialAt(basin ? "#a99c87" : foundry ? "#b18877" : "#a9bdd4", false, "bulkhead");
            var deck = MaterialAt(basin ? "#b8a498" : foundry ? "#b67660" : "#8e9aad", false,
                basin ? "basin" : foundry ? "foundry-deck" : "deck");
            deck.mainTextureScale = Vector2.one * (basin ? 22 : 15);
            float d = Spec.length * 2;
            Part(Root, deck, new Vector3(0, -0.44f, 0), new Vector3(d, 0.88f, d), PrimitiveType.Cube, true);
            for (int i = 0; i < 4; i++)
            {
                var wall = Group(Root, "Perimeter");
                wall.rotation = Quaternion.Euler(0, 90 * i, 0);
                Part(wall, walls, new Vector3(0, 2.15f, Spec.length + 0.25f), new Vector3(d + 1, 4.3f, 0.50f), PrimitiveType.Cube, true);
                for (float x = -Spec.length + 4; x < Spec.length - 1; x += 7.8f)
                {
                    Part(wall, Dark, new Vector3(x, 2.82f, Spec.length - 0.11f), new Vector3(0.31f, 5.65f, 0.54f));
                    Part(wall, TeamNeutral, new Vector3(x + 1.4f, 3.05f, Spec.length - 0.03f), new Vector3(1.7f, 0.045f, 0.035f));
                }
            }
            Stars();
            if (basin)
            {
                var road = MaterialAt("#82707c");
                Ring(Root, Vector3.up * 0.02f, Spec.roadRadius, road, 7.6f, 128);
                Ring(Root, Vector3.up * 0.04f, Spec.roadRadius + 3.75f, Dark, 0.10f, 128);
                var horizon = MaterialAt("#a08a68");
                for (int i = 0; i < 20; i++)
                {
                    float a = i * 0.31f;
                    Part(Root, horizon, new Vector3(Mathf.Cos(a) * 81, -9, Mathf.Sin(a) * 81),
                        new Vector3(24, 22 + R(18), 24)).rotation = Quaternion.Euler(0, i * 23, i * 11);
                }
            }
            else
            {
                var hot = MaterialAt(foundry ? "#ff7138" : "#ff4b85", true);
                float y = foundry ? 8.3f : 9.2f;
                var globe = Part(Root, hot, new Vector3(0, y, 0), new Vector3(3.7f, 3.42f, 3.7f), PrimitiveType.Sphere);
                Transform reactor = Group(Root, "Ruptured reactor", globe.position);
                Ring(reactor, Vector3.zero, 3.15f, TeamNeutral, 0.056f).transform.rotation = Quaternion.Euler(11, 19, -23);
                Ring(reactor, Vector3.up * 0.24f, 2.63f, hot, 0.082f).transform.rotation = Quaternion.Euler(-27, 0, 28);
                Ring(reactor, Vector3.up * 0.11f, 3.08f, Dark, 0.17f).transform.rotation = Quaternion.Euler(39, 0, -37);
                spinning.Add(reactor);
                if (foundry)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var channel = Group(Root, "Molten channel");
                        channel.rotation = Quaternion.Euler(0, i * 120, 0);
                        Part(channel, hot, new Vector3(0, 0.018f, 19), new Vector3(2.3f, 0.022f, 23));
                        Part(channel, Dark, new Vector3(-1.20f, 0.023f, 19), new Vector3(0.18f, 0.029f, 23));
                        Part(channel, Dark, new Vector3(1.20f, 0.023f, 19), new Vector3(0.18f, 0.029f, 23));
                    }
                }
            }
        }

        void Stars()
        {
            var star = MaterialAt("#c1e5ff", true);
            var planet = Part(Root, MaterialAt("#465679", true), new Vector3(-76, 28, 54), Vector3.one * 43, PrimitiveType.Sphere);
            Ring(Root, planet.position, 28, MaterialAt("#657496", true), 1.7f, 96).transform.rotation = Quaternion.Euler(16, 16, 21);
            var dust = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < 230; i++)
            {
                float a = R(6.283f), h = 0.035f + R(1.35f);
                Vector3 center = new Vector3(Mathf.Sin(a) * Mathf.Cos(h), Mathf.Sin(h), Mathf.Cos(a) * Mathf.Cos(h)) * 103f;
                float size = 0.06f + R(0.17f);
                int j = dust.Count;
                Vector3 right = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));
                dust.Add(center - right * size); dust.Add(center + Vector3.up * size); dust.Add(center + right * size);
                triangles.Add(j); triangles.Add(j + 1); triangles.Add(j + 2);
                triangles.Add(j + 2); triangles.Add(j + 1); triangles.Add(j);
            }
            MeshAt("Star field", dust, triangles, null, star);
        }

        void Meadow()
        {
            earth = MaterialAt("#807262");
            bark = MaterialAt("#74563d");
            grassCrown = MaterialAt("#599b62");
            var ground = MaterialAt("#7d9d62", false, "meadow");
            ground.mainTextureScale = Vector2.one * 8;
            var hill = Spec.terrain;
            var vertices = new List<Vector3>(hill.heights.Length);
            var uv = new List<Vector2>(hill.heights.Length);
            var grid = new List<int>();
            for (int z = 0; z < hill.count; z++)
            for (int x = 0; x < hill.count; x++)
            {
                float px = hill.start + hill.step * x, pz = hill.start + hill.step * z;
                vertices.Add(new Vector3(px, hill.heights[z * hill.count + x], pz));
                uv.Add(new Vector2((float)x / hill.count, (float)z / hill.count));
                if (x == 0 || z == 0 || px * px + pz * pz > Spec.edgeRadius * Spec.edgeRadius) continue;
                if ((px - hill.step) * (px - hill.step) + (pz - hill.step) * (pz - hill.step) > Spec.edgeRadius * Spec.edgeRadius) continue;
                int i = z * hill.count + x;
                grid.AddRange(new[] { i - hill.count - 1, i - 1, i - hill.count, i - 1, i, i - hill.count });
            }
            MeshAt("Mossy hills", vertices, grid, uv, ground, true);
            var rim = new List<Vector3>();
            var rimIndices = new List<int>();
            float[] layers = {1.01f,0.98f,0.89f,0.72f,0.38f,0.01f};
            float[] depths = {0,-2.3f,-6,-12,-18,-23};
            for (int i = 0; i < layers.Length; i++)
            for (int j = 0; j < 144; j++)
            {
                float a = j * Mathf.PI / 72;
                float rad = Spec.edgeRadius * layers[i] + Mathf.Sin(a * 19 + i * .4f) * i * 0.10f;
                float x = Mathf.Sin(a) * rad, z = Mathf.Cos(a) * rad;
                rim.Add(new Vector3(x, depths[i] + Height(x, z) * layers[i], z));
                if (i == 0) continue;
                int v = i * 144 + j;
                int w = i * 144 + (j + 1) % 144;
                rimIndices.AddRange(new[] { v - 144, v, w, v - 144, w, w - 144 });
            }
            MeshAt("Cliff strata", rim, rimIndices, null, earth);
            WaterRibbon(Spec.river, 1.83f, false);
            WaterRibbon(Spec.creek, 0.49f, true);
            foreach (int x in new[] { -16, 0, 15 })
            {
                var point = Array.Find(Spec.river, p => Mathf.Approximately(p.x, x));
                if (point == null) continue;
                for (int k = -9; k <= 9; k++)
                    Part(Root, bark, new Vector3(x, 0.091f, point.z + k * 0.27f), new Vector3(2.58f, 0.085f, 0.24f));
            }
            foreach (var tip in new[] { Spec.river[0], Spec.river[Spec.river.Length - 1] })
            {
                float sign = Mathf.Sign(tip.x);
                var cascade = new[] { tip, new Site { x = tip.x + sign * 2.4f, y = -2.5f, z = tip.z },
                    new Site { x = tip.x + sign * 6.9f, y = -10, z = tip.z }, new Site { x = tip.x + sign * 11.5f, y = -22f, z = tip.z } };
                WaterRibbon(cascade, 1.65f, false);
            }
            var soil = MaterialAt("#b7956e", false, "basin");
            var trail = new List<Vector3>();
            var trailUv = new List<Vector2>();
            var trailFaces = new List<int>();
            float traveled = 0;
            for (int i = 1; i < Spec.trail.Length; i++)
            {
                Vector3 a = Spec.trail[i - 1].Position, b = Spec.trail[i].Position;
                Vector3 left = Vector3.Cross(Vector3.up, (b - a).normalized) * .62f;
                int sections = Mathf.CeilToInt(Vector3.Distance(a, b) / .20f);
                for (int t = 0; t <= sections; t++)
                {
                    Vector3 p = Vector3.Lerp(a, b, (float)t / sections);
                    int vertex = trail.Count;
                    for (int lateral = -1; lateral <= 1; lateral++)
                    {
                        Vector3 edge = p + lateral * left;
                        edge.y = Height(edge.x, edge.z) + .028f;
                        trail.Add(edge);
                        trailUv.Add(new Vector2((lateral + 1) * .5f, traveled + t * .17f));
                    }
                    if (t == 0) continue;
                    for (int strip = 0; strip < 2; strip++)
                    {
                        int v = vertex + strip;
                        trailFaces.AddRange(new[] { v - 3, v, v - 2, v - 2, v, v + 1 });
                    }
                }
                traveled += sections * .17f;
            }
            MeshAt("Ridge trail", trail, trailFaces, trailUv, soil);
            for (int i = 0; i < 46; i++)
            {
                float a = i * 2.399963f, rad = 13f + R(15.3f);
                float x = Mathf.Sin(a) * rad, z = Mathf.Cos(a) * rad;
                float y = Height(x, z);
                if (y < .35f || Array.Exists(Spec.cover, box => box.Overlaps(x, z, .4f))) continue;
                bool bloom = i % 3 == 0;
                Kit.Nature(Root, bloom ? "flower_yellowA" : "plant_bushDetailed",
                    new Vector3(.48f + R(.25f), bloom ? .33f : .44f, .48f + R(.31f)),
                    new Vector3(x, y - .06f, z), R(360f));
            }
            var milk = MaterialAt("#fff7db");
            var bill = MaterialAt("#ffc34a");
            for (int i = 0; i < 3; i++)
            {
                var duck = Group(Root, "River duck");
                Part(duck, milk, new Vector3(0, 0.15f, 0), new Vector3(0.64f, 0.39f, 0.79f), PrimitiveType.Sphere);
                Part(duck, milk, new Vector3(0, 0.36f, 0.3f), Vector3.one * 0.32f, PrimitiveType.Sphere);
                Part(duck, bill, new Vector3(0, 0.33f, 0.47f), new Vector3(0.15f, 0.09f, 0.23f), PrimitiveType.Sphere);
                animals.Add(duck);
            }
        }

        void IslandCover(BoxSpec box, Transform parent, float h)
        {
            var solid = Part(parent, Dark, Vector3.up * h / 2, new Vector3(box.w, h, box.d), PrimitiveType.Cube, true);
            solid.GetComponent<Renderer>().enabled = false;
            bool tree = box.nature == "tree";
            string asset = tree ? (box.sector == 1 ? "tree_oak" : "tree_pineRoundC")
                : (box.sector == 1 ? "rock_largeB" : "rock_largeD");
            float foot = tree ? box.ground - box.bottom - .16f : Mathf.Max(0, box.ground - box.bottom - .6f);
            Vector3 size = new Vector3(tree ? Mathf.Max(box.w * 1.18f, 2.6f) : box.w * 1.18f,
                h - foot, tree ? Mathf.Max(box.d * 1.18f, 2.6f) : box.d * 1.18f);
            if (Kit.Nature(parent, asset, size, Vector3.up * foot, R(360))) return;
            if (box.nature == "tree")
            {
                Part(parent, bark, Vector3.up * h * 0.48f, new Vector3(box.w * 0.7f, h * 0.50f, box.d * 0.75f), PrimitiveType.Cylinder);
                for (int i = 0; i < 4; i++)
                    Part(parent, grassCrown, new Vector3(R(1.4f) - .7f, h * (.75f + R(.12f)), R(1.4f) - .7f),
                         new Vector3(2.65f, 1.9f, 2.50f), PrimitiveType.Sphere);
            }
            else
            {
                Part(parent, earth, Vector3.up * h * 0.42f, new Vector3(box.w * 1.14f, h * 1.27f, box.d * 1.15f), PrimitiveType.Sphere);
                Part(parent, grassCrown, Vector3.up * h * 0.90f, new Vector3(box.w * 0.8f, h * 0.26f, box.d * 0.83f), PrimitiveType.Sphere);
            }
        }

        void WaterRibbon(Site[] sites, float width, bool creek)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uv = new List<Vector2>();
            for (int i = 0; i < sites.Length; i++)
            {
                Vector3 p = sites[i].Position + Vector3.up * 0.038f;
                Vector3 side = (creek ? Vector3.right : Vector3.forward) * width;
                verts.Add(p - side); verts.Add(p + side);
                uv.Add(new Vector2(0, i * 0.08f)); uv.Add(new Vector2(1, i * 0.08f));
                if (i == 0) continue;
                int idx = i * 2;
                tris.AddRange(new[] { idx - 2, idx - 1, idx, idx, idx - 1, idx + 1, idx, idx - 1, idx - 2, idx + 1, idx - 1, idx });
            }
            MeshAt(creek ? "Creek" : "River", verts, tris, uv, water, doubleSided: true);
        }

        void MeshAt(string label, List<Vector3> vertices, List<int> triangles, List<Vector2> uv, Material paint, bool solid = false, bool doubleSided = false)
        {
            var target = Group(Root, label).gameObject;
            var mesh = new Mesh { name = label };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            if (uv != null) mesh.SetUVs(0, uv);
            if (doubleSided)
            {
                var normals = new Vector3[vertices.Count];
                Array.Fill(normals, Vector3.up);
                mesh.normals = normals;
            }
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            target.AddComponent<MeshRenderer>().sharedMaterial = paint;
            if (solid) target.AddComponent<MeshCollider>().sharedMesh = mesh;
            else target.layer = Decor;
            meshes.Add(mesh);
        }

        internal void Animate(float time)
        {
            for (int i = 0; i < spinning.Count; i++)
            {
                var item = spinning[i];
                item.rotation = Quaternion.Euler(0, time * 29 + i * 22, item.name == "Ruptured reactor" ? time * 9 : 0);
            }
            for (int i = 0; i < Pickups.Length; i++)
                Pickups[i].localPosition = Spec.pickups[i].Position + Vector3.up * (0.61f + Mathf.Sin(time * 2.5f + i) * 0.11f);
            if (island)
            {
                water.mainTextureOffset = new Vector2(0, time * 0.067f % 1);
                for (int i = 0; i < animals.Count; i++)
                {
                    float x = 3.7f + Mathf.Sin(time * 0.40f + i * 0.23f) * 3f + i * 2f;
                    float z = 1.35f + Mathf.Sin(x * 0.15f) * 4.72f + Mathf.Sin(x * 0.33f) * 0.48f;
                    animals[i].position = new Vector3(x, Mathf.Sin(time * 3 + i) * 0.04f + 0.017f, z - 0.33f);
                    animals[i].rotation = Quaternion.Euler(0, Mathf.Cos(time * .4f + i * .23f) > 0 ? 66 : -114, 0);
                }
            }
        }

        float R(float max) => (float)random.NextDouble() * max;
        internal Material MaterialAt(string color, bool glow = false, string texture = null)
        {
            var mat = new Material(Shader.Find(glow ? "Unlit/Color" : "Standard")) { color = Catalog.Tint(color) };
            if (texture != null) mat.mainTexture = Resources.Load<Texture2D>("TRIAS/" + texture);
            if (!glow)
            {
                mat.SetFloat("_Metallic", island ? 0f : .22f);
                mat.SetFloat("_Glossiness", island ? .31f : .44f);
            }
            Paint.Add(mat);
            return mat;
        }

        internal static Transform Group(Transform parent, string label, Vector3 at = default)
        {
            var child = new GameObject(label).transform;
            child.SetParent(parent, false);
            child.localPosition = at;
            return child;
        }

        internal static Transform Part(Transform parent, Material paint, Vector3 at, Vector3 scale,
            PrimitiveType type = PrimitiveType.Cube, bool solid = false)
        {
            var item = GameObject.CreatePrimitive(type);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = at;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = paint;
            if (!solid)
            {
                var collider = item.GetComponent<Collider>();
                collider.enabled = false;
                Object.Destroy(collider);
                item.layer = Decor;
            }
            return item.transform;
        }

        internal static LineRenderer Line(Transform parent, Vector3 start, Vector3 end, Material tint, float width)
        {
            var line = Group(parent, "Light beam").gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.sharedMaterial = tint;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.positionCount = 2;
            line.SetPosition(0, start); line.SetPosition(1, end);
            line.widthMultiplier = width;
            return line;
        }

        static LineRenderer Ring(Transform parent, Vector3 at, float radius, Material tint, float width = .045f, int segments = 56)
        {
            var line = Line(parent, Vector3.zero, Vector3.one, tint, width);
            line.transform.localPosition = at;
            line.loop = true; line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2;
                line.SetPosition(i, new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius));
            }
            return line;
        }

        public void Dispose()
        {
            Object.Destroy(Root.gameObject);
            foreach (var material in Paint) Object.Destroy(material);
            foreach (var mesh in meshes) Object.Destroy(mesh);
        }
    }
}
