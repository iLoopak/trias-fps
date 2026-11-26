using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Trias.Native
{
    internal sealed partial class TriasWorld : IDisposable
    {
        internal readonly ArenaSpec Spec;
        internal readonly string Mode;
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
        readonly bool island, ribbonShader;
        float waterEffects = -1, waterWind = -1;
        readonly Color sunColor;
        readonly System.Random random = new System.Random(719);
        internal float Height(float x, float z) => Spec.terrain?.At(x, z) ?? 0;

        internal TriasWorld(ArenaSpec spec, Catalog catalog, string mode, Camera view, bool hangar = false)
        {
            Spec = spec;
            IsRelayArt = spec.id == "relay" && !hangar;
            Mode = mode;
            Root = Group(null, hangar ? "TRIAS orbital hangar" : spec.label);
            Actors = Group(Root, "Piloti, vozidla a projektily");
            island = spec.id == "island";
            var fog = Catalog.Tint(island ? "#a6d8d9" : spec.id == "foundry" ? "#402a37" : spec.id == "basin" ? "#3c394b" : "#101827");
            sunColor = Catalog.Tint(island ? "#fff6d2" : spec.id == "foundry" ? "#ffb79f" : spec.id == "relay" ? "#ffe0b0" : "#f4e3d3");
            RenderSettings.fog = !hangar;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = island || spec.id == "basin" ? .012f : spec.id == "relay" ? .0055f : .009f;
            RenderSettings.fogColor = fog;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Catalog.Tint(spec.id switch
            {
                "foundry" => "#827281", "basin" => "#99938a", "island" => "#b1d1b3", _ => "#778ca8"
            });
            RenderSettings.ambientEquatorColor = Catalog.Tint(spec.id switch
            {
                "foundry" => "#513f45", "basin" => "#625750", "island" => "#5c6952", _ => "#45545c"
            });
            RenderSettings.ambientGroundColor = Catalog.Tint(island ? "#585747" : "#43464a");
            view.backgroundColor = fog;
            ConfigureSky(view);
            var lamp = Group(Root, "Key").gameObject.AddComponent<Light>();
            lamp.type = LightType.Directional;
            lamp.intensity = island ? 1.34f : spec.id == "basin" ? 1.37f : 1.43f;
            lamp.color = sunColor;
            lamp.shadows = hangar ? LightShadows.None : LightShadows.Soft;
            lamp.shadowStrength = island ? .66f : .63f;
            lamp.transform.rotation = island ? Quaternion.LookRotation(-IslandSun) : Quaternion.Euler(Spec.id == "relay" ? 37 : 47, -31, 0);

            if (IsRelayArt) ConfigureRelayLight(lamp, view);

            Porcelain = CeramicFinish("#eddbba", .03f, .29f);
            Insulation = CeramicFinish("#ae8f72", .06f, .22f);
            Porcelain.mainTexture = Insulation.mainTexture = Resources.Load<Texture2D>("TRIAS/ceramic-grain");
            SafetyAmber = CeramicFinish("#cb6921", .10f, .34f);
            PilotVisor = CeramicFinish("#142731", .49f, .64f);
            Dark = MaterialAt("#222631");
            Steel = MaterialAt("#8091a9", false, "armor");
            White = Porcelain;
            Glass = MaterialAt("#183246", true);
            TeamNeutral = MaterialAt("#aac5d6", true);
            Hostile = MaterialAt("#ff5454", true);
            Hardware = MaterialAt("#a8aeac", false, "armor");
            Hardware.SetFloat("_Metallic", .41f);
            Hardware.SetFloat("_Glossiness", .32f);
            Graphite = MaterialAt("#303f49", false, "armor");
            Graphite.SetFloat("_Metallic", .17f);
            Graphite.SetFloat("_Glossiness", .24f);
            armorSection = TriasMarkings.ArmorPlate();
            meshes.Add(armorSection);
            ceramicShell = TriasMarkings.ArmorPlate(true);
            if (IsRelayArt) ConfigureRelaySurfaces();
            meshes.Add(ceramicShell);
            Kit = new TriasKit(this);
            if (island)
            {
                var shader = Resources.Load<Shader>("TRIAS/IslandWater");
                ribbonShader = shader != null && shader.isSupported;
                if (ribbonShader)
                {
                    water = new Material(shader);
                    water.mainTexture = Resources.Load<Texture2D>("TRIAS/water");
                    Paint.Add(water);
                }
                else
                {
                    water = MaterialAt("#278caa", false, "water");
                    water.SetFloat("_Glossiness", .57f);
                }
            }
            for (int i = 0; i < Team.Length; i++)
            {
                Team[i] = MaterialAt(catalog.teams[i].color);
                TeamLight[i] = MaterialAt(catalog.teams[i].color, true);
                FactionPaint[i] = MaterialAt(i switch { 0 => "#269fbd", 1 => "#de6e29", _ => "#9860e2" }, false, "armor");
                FactionPaint[i].SetFloat("_Glossiness", .23f);
                FactionPaint[i].EnableKeyword("_EMISSION");
                FactionPaint[i].SetColor("_EmissionColor", FactionPaint[i].color * .19f);
            }
            if (hangar)
            {
                BuildHangar();
                return;
            }
            if (island) Meadow();
            else
            {
                Station();
                BuildOrbitalTraffic();
            }

            BuildWayfinding();
            BuildBoundaryVapor();
            foreach (var box in Spec.cover)
            {
                var home = Group(Root, box.nature ?? (box.gantry ? "Suspended gantry" : box.relay ? "Reactor plinth" : "Cover"),
                    new Vector3(box.x, box.bottom, box.z));
                home.localRotation = Quaternion.Euler(0, box.angle * Mathf.Rad2Deg, 0);
                float h = box.h - box.bottom;
                if (island) IslandCover(box, home, h);
                else
                {
                    var alloy = IsRelayArt ? (box.gantry ? Graphite : box.relay ? Hardware : walls)
                        : box.gantry || box.relay || box.sector >= 0 ? coverPaint : walls;
                    Part(home, alloy, Vector3.up * h / 2, new Vector3(box.w, h, box.d), PrimitiveType.Cube, true);
                    if (Spec.id == "relay")
                    {
                        DressRelayCover(box, home, h);
                        continue;
                    }
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Part(home, Dark, new Vector3(0, h - 0.1f, sign * (box.d * 0.5f + 0.023f)), new Vector3(box.w, 0.14f, 0.06f));
                        Part(home, box.sector >= 0 ? TeamLight[box.sector] : TeamNeutral,
                            new Vector3(0, h - 0.25f, sign * (box.d * 0.5f + 0.071f)), new Vector3(box.w * 0.7f, 0.046f, 0.022f));
                    }
                }
            }
            if (IsRelayArt) BuildRelayContacts();
            var gold = MaterialAt("#ffae62", true);
            foreach (var pad in Spec.launchPads)
            {
                Part(Root, Dark, pad.Position + Vector3.up * 0.035f, new Vector3(1.85f, 0.04f, 1.85f), PrimitiveType.Cylinder);
                Ring(Root, pad.Position + Vector3.up * 0.083f, 0.92f, gold, 0.055f);
                Ring(Root, pad.Position + Vector3.up * 0.087f, 0.55f, gold);
            }

            Pickups = new Transform[spec.pickups.Length];
            pickupMotion = new Transform[spec.pickups.Length];
            for (int i = 0; i < Pickups.Length; i++)
            {
                var item = spec.pickups[i];
                Part(Root, island ? White : Dark, item.Position + Vector3.up * 0.045f, new Vector3(0.96f, 0.048f, 0.96f), PrimitiveType.Cylinder);
                var availability = Group(Root, item.type, item.Position);
                var mark = Group(availability, "Pickup marker", Vector3.up * .64f);
                var choice = Catalog.Find(catalog.weapons, item.type) ?? Catalog.Find(catalog.powerups, item.type);
                var tint = MaterialAt(choice?.color ?? "#f6faab", true);
                if (Catalog.Find(catalog.weapons, item.type) != null) TriasModels.Gun(this, mark, item.type, tint);
                else
                {
                    Part(mark, tint, Vector3.zero, new Vector3(0.40f, 0.13f, 0.12f));
                    Part(mark, tint, Vector3.zero, new Vector3(0.13f, 0.40f, 0.12f));
                }
                Ring(availability, Vector3.up * .084f, .46f, tint, .030f, 24);
                Pickups[i] = availability;
                pickupMotion[i] = mark;
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

        Material walls, coverPaint, earth, bark, grassCrown;
        readonly Transform[] pickupMotion;
        void Station()
        {
            bool basin = Spec.id == "basin", foundry = Spec.id == "foundry";
            walls = MaterialAt(basin ? "#a99c87" : foundry ? "#866453" : "#929397", false, "bulkhead");
            coverPaint = MaterialAt(basin ? "#ebd3b3" : foundry ? "#be9e82" : "#eadbc6", false, "panel-" + Spec.id);
            coverPaint.SetFloat("_Metallic", .08f);
            coverPaint.SetFloat("_Glossiness", foundry ? .27f : .33f);
            var deck = MaterialAt(basin ? "#b8a498" : foundry ? "#b67660" : "#828c9d", false,
                basin ? "basin" : foundry ? "foundry-deck" : "deck");
            deck.mainTextureScale = Vector2.one * (basin ? 22 : 15);
            float d = Spec.length * 2;
            Part(Root, deck, new Vector3(0, -0.44f, 0), new Vector3(d, 0.88f, d), PrimitiveType.Cube, true);
            for (int i = 0; i < 4; i++)
            {
                var wall = Group(Root, "Perimeter");
                wall.rotation = Quaternion.Euler(0, 90 * i, 0);
                Part(wall, walls, new Vector3(0, 2.15f, Spec.length + 0.25f), new Vector3(d + 1, 4.3f, 0.50f), PrimitiveType.Cube, true);
                for (float x = -Spec.length + 4; x < Spec.length - 1; x += IsRelayArt ? 12.8f : 7.8f)
                {
                    Part(wall, Dark, new Vector3(x, 2.82f, Spec.length - 0.11f), new Vector3(0.31f, 5.65f, 0.54f));
                    if (IsRelayArt)
                        Armor(wall, Hardware, new Vector3(x + 1.5f, 3.05f, Spec.length - .04f),new Vector3(2.4f,.12f,.09f),false);
                    else
                        Part(wall, TeamNeutral, new Vector3(x + 1.4f, 3.05f, Spec.length - 0.03f), new Vector3(1.7f, 0.045f, 0.035f));
                }
            }
            if (!IsRelayArt) BuildInfrastructure();
            BuildStationDistrict();
            if (basin)
            {
                BuildMiningRigs();
                var road = MaterialAt("#82707c");
                Ring(Root, Vector3.up * 0.02f, Spec.roadRadius, road, 7.6f, 128);
                Ring(Root, Vector3.up * 0.04f, Spec.roadRadius + 3.75f, Dark, 0.10f, 128);
                var horizon = MaterialAt("#947757");
                var mineral = MaterialAt("#b48b7c", false, "armor");
                var fracture = MaterialAt("#715c66", false, "armor");
                for (int i = 0; i < 20; i++)
                {
                    float a = i * 0.31f;
                    Part(Root, horizon, new Vector3(Mathf.Cos(a) * 81, -9, Mathf.Sin(a) * 81),
                        new Vector3(24, 22 + R(18), 24)).rotation = Quaternion.Euler(0, i * 23, i * 11);
                    if (i % 3 != 1) continue;
                    var vein = Group(Root, "Exposed mineral seam", new Vector3(Mathf.Cos(a)*79,2.4f,Mathf.Sin(a)*79));
                    vein.localRotation = Quaternion.Euler(0,-a*Mathf.Rad2Deg + 36f,0);
                    for (int spike = -1; spike <= 1; spike++)
                    {
                        var shard = Armor(vein, spike == 0 ? mineral : fracture, new Vector3(spike*3.1f, 4 - Mathf.Abs(spike)*1.5f, 0),
                            new Vector3(3.2f, spike == 0 ? 13 : 7.5f, 3.1f), false);
                        shard.localRotation = Quaternion.Euler(9,spike*48,spike*21);
                    }
                }
            }
            else
            {
                if (foundry)
                {
                    BuildCrucible();
                    BuildFoundryStacks();
                    var hot = MaterialAt("#ff7138", true);
                    for (int i = 0; i < 3; i++)
                    {
                        var channel = Group(Root, "Molten channel");
                        channel.rotation = Quaternion.Euler(0, i * 120, 0);
                        Part(channel, hot, new Vector3(0, 0.018f, 19), new Vector3(2.3f, 0.022f, 23));
                        Part(channel, Dark, new Vector3(-1.20f, 0.023f, 19), new Vector3(0.18f, 0.029f, 23));
                        Part(channel, Dark, new Vector3(1.20f, 0.023f, 19), new Vector3(0.18f, 0.029f, 23));
                    }
                }
                else
                {
                    BuildReactor();
                    BuildRelayHorizon();
                }
            }
        }

        void Meadow()
        {
            earth = MaterialAt("#807262");
            bark = MaterialAt("#74563d");
            grassCrown = MaterialAt("#599b62");
            var ground = MaterialAt("#dedcc2", false, "meadow-macro");
            ground.SetFloat("_Glossiness", .13f);
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
            int midstream = Spec.river.Length / 2;
            var upstream = new Site[midstream + 1];
            var downstream = new Site[Spec.river.Length - midstream];
            Array.Copy(Spec.river, upstream, upstream.Length);
            Array.Copy(Spec.river, midstream, downstream, 0, downstream.Length);
            Array.Reverse(upstream);
            WaterRibbon(upstream, 1.83f, false);
            WaterRibbon(downstream, 1.83f, false);
            WaterRibbon(Spec.creek, 0.49f, true);
            foreach (int x in new[] { -16, 0, 15 })
            {
                var point = Array.Find(Spec.river, p => Mathf.Approximately(p.x, x));
                if (point == null) continue;
                if (x != 0)
                {
                    var sluice = Group(Root, "Ceramic water regulation sill", new Vector3(x,0,point.z));
                    for (int bank = -1; bank <= 1; bank += 2)
                    {
                        var abutment = Group(sluice, "Weathered flow deflector", new Vector3(0, Height(x, point.z+bank*2.7f)-.15f, bank*2.7f));
                        abutment.localRotation = Quaternion.Euler(0, bank*23, 0);
                        Armor(abutment, Insulation, new Vector3(0,.47f,0),new Vector3(.80f,1.08f,1.10f),false);
                        Armor(abutment, Porcelain, new Vector3(0,1.12f,0),new Vector3(.66f,.30f,.93f),false);
                        Armor(abutment, Graphite, new Vector3(0,.66f,-bank*.56f),new Vector3(.37f,.16f,.09f),false);
                    }
                }
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
            BuildGrasses();
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
            var verts = new List<Vector3>(sites.Length * 2);
            var tris = new List<int>();
            var uv = new List<Vector2>(sites.Length * 2);
            float traveled = 0;
            for (int i = 0; i < sites.Length; i++)
            {
                Vector3 p = sites[i].Position + Vector3.up * .038f;
                Vector3 side = (creek ? Vector3.right : Vector3.forward) * width;
                if (i > 0) traveled += Vector3.Distance(sites[i - 1].Position, sites[i].Position);
                verts.Add(p - side); verts.Add(p + side);
                uv.Add(new Vector2(0, traveled)); uv.Add(new Vector2(1, traveled));
                if (i == 0) continue;
                int idx = i * 2;
                tris.AddRange(new[] { idx - 2, idx - 1, idx, idx, idx - 1, idx + 1 });
                if (!ribbonShader) tris.AddRange(new[] { idx, idx - 1, idx - 2, idx + 1, idx - 1, idx });
            }
            MeshAt(creek ? "Creek" : "River", verts, tris, uv, water, doubleSided: !ribbonShader);
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
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paint;
            if (paint == water) renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (solid) target.AddComponent<MeshCollider>().sharedMesh = mesh;
            else target.layer = Decor;
            meshes.Add(mesh);
        }

        internal void Animate(float time, float effects = 1, float arenaPulse = 0)
        {
            AnimateBoundaryVapor(effects);
            AnimateLandmark(time, arenaPulse);
            AnimateOrbitalTraffic(time, arenaPulse);
            AnimateStationDistrict(time);
            for (int i = 0; i < spinning.Count; i++)
            {
                var item = spinning[i];
                item.rotation = Quaternion.Euler(0, time * 29 + i * 22, item.name == "Ruptured reactor" ? time * 9 : 0);
            }
            for (int i = 0; i < pickupMotion.Length; i++)
                if (Pickups[i].gameObject.activeSelf)
                    pickupMotion[i].localPosition = Vector3.up * (.61f + Mathf.Sin(time * 2.5f + i) * .11f);
            if (island)
            {
                if (ribbonShader && !Mathf.Approximately(waterEffects, effects))
                {
                    waterEffects = effects;
                    water.SetFloat("_Effects", effects);
                }
                if (ribbonShader)
                {
                    if (!Mathf.Approximately(waterWind, arenaPulse))
                    {
                        waterWind = arenaPulse;
                        water.SetFloat("_WindGust", arenaPulse);
                    }
                }
                else water.mainTextureOffset = new Vector2(0, -time * .12f * effects % 1);
                for (int i = 0; i < animals.Count; i++)
                {
                    float x = 3.7f + Mathf.Sin(time * 0.40f + i * 0.23f) * 3f + i * 2f;
                    float z = 1.35f + Mathf.Sin(x * 0.15f) * 4.72f + Mathf.Sin(x * 0.33f) * 0.48f;
                    animals[i].position = new Vector3(x, Mathf.Sin(time * 3 + i) * (0.04f + .015f * arenaPulse) + 0.017f, z - 0.33f);
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
            mat.enableInstancing = !glow;
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

        internal static LineRenderer Ring(Transform parent, Vector3 at, float radius, Material tint, float width = .045f, int segments = 56)
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
            if (IsRelayArt && RenderSettings.sun == relaySun) RestoreRelayLight();
            if (orbitalSky != null && RenderSettings.skybox == orbitalSky) RenderSettings.skybox = null;
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
                Object.Destroy(Root.gameObject);
            }
            foreach (var material in Paint) Object.Destroy(material);
            foreach (var mesh in meshes) Object.Destroy(mesh);
        }
    }
}
