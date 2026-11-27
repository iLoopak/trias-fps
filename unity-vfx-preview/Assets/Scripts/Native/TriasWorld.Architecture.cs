using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    // Construction language for arena architecture (see ART-DIRECTION.md, "Construction language").
    // After an arena is assembled, the shared mid-value finishes on architectural renderers are
    // replaced by TRIAS/Architecture materials that carry the Blender-baked plate atlas at metric
    // scale: ivory glazed ceramic plates, dark graphite load frames and ceramic deck tiles, each
    // separated by dark phase seams. Pilots, weapons, pickups and objective hardware keep their own
    // finishes; colliders and transforms are untouched.
    internal sealed partial class TriasWorld
    {
        Shader architectureShader;
        Texture2D wallData, wallNormal, deckData, deckNormal;
        readonly Dictionary<(Material, bool, bool), Material> architecture = new();
        readonly Dictionary<Material, ArchitectureFinish> architectureFinishes = new();

        sealed class ArchitectureFinish
        {
            internal bool Deck;
            internal string Color, Alt = "#9b8b77", Seam = "#121416";
            internal float Gloss = .55f, GlossVar = .16f, Metal, PlateVar = .12f, AltRate = .1f;
            internal float Scale = 4, Normal = 1, Edge = .35f, Grime = .25f, Bevel = .045f;
            // Seams take the plate's own darkened value unless SeamTone is 0, so large ceramic
            // fields read as one glazed mass with fine joints rather than a black-lined tile grid.
            internal float SeamStrength = 1, SeamTone, SideSeams = .12f;
        }

        bool PrepareArchitecture()
        {
            architectureShader = Resources.Load<Shader>("TRIAS/Architecture");
            if (architectureShader == null || !architectureShader.isSupported) return false;
            wallData = Resources.Load<Texture2D>("TRIAS/Architecture/plates-wall-data");
            wallNormal = Resources.Load<Texture2D>("TRIAS/Architecture/plates-wall-normal");
            deckData = Resources.Load<Texture2D>("TRIAS/Architecture/plates-deck-data");
            deckNormal = Resources.Load<Texture2D>("TRIAS/Architecture/plates-deck-normal");
            return wallData != null && wallNormal != null && deckData != null && deckNormal != null;
        }

        void ArchitectureFor(Material source, ArchitectureFinish finish)
        {
            if (source != null) architectureFinishes[source] = finish;
        }

        static string Hex(Material m) => "#" + ColorUtility.ToHtmlStringRGB(m.color);

        // Vanta and Meridian keep their own palette (taken from the arena's existing finishes) but
        // receive the same construction language as Relay: monolithic ceramic with plate-toned joints,
        // graphite load frames and machined deck alloy. Textured identity finishes (refractory,
        // Meridian's polished section, stone) are not in the map and stay untouched.
        void ArenaArchitecture()
        {
            bool foundry = Spec.id == "foundry";
            foreach (var source in new[] { Porcelain, coverPaint, Insulation })
                if (source != null)
                    ArchitectureFor(source, new ArchitectureFinish { Color = Hex(source), Alt = Hex(source), AltRate = 0,
                        Gloss = foundry ? .38f : .5f, GlossVar = .12f, PlateVar = .03f, Scale = 7.6f, Edge = .3f,
                        Grime = foundry ? .45f : .2f, SeamStrength = .55f, SeamTone = 1 });
            foreach (var source in new[] { Graphite, walls })
                if (source != null)
                    ArchitectureFor(source, new ArchitectureFinish { Color = Hex(source), Alt = Hex(source), AltRate = 0,
                        Seam = "#0b0c0d", Gloss = .32f, PlateVar = .12f, Scale = 6, Normal = .8f, Edge = .55f, Grime = 0,
                        SeamStrength = .7f, SeamTone = .6f });
            // Arena floor: cast slabs in the arena's own tone replace the stretched noise texture.
            // Meridian's untextured tint is far lighter than its old textured floor read; keep the dusk.
            string floor = Spec.id == "basin" ? "#7d6e66" : Hex(arenaDeck);
            if (arenaDeck != null)
                ArchitectureFor(arenaDeck, new ArchitectureFinish { Deck = true, Color = floor, Alt = floor, AltRate = .14f,
                    Gloss = foundry ? .3f : .38f, GlossVar = .22f, PlateVar = .07f, Scale = 6.4f, Edge = .4f, Grime = 0, Bevel = .03f,
                    SeamStrength = .75f, SeamTone = .65f });
            // Linked-route casting decks and their frames (brushed noise stretched across 30 m spans).
            if (routeTread != null && !island)
                ArchitectureFor(routeTread, new ArchitectureFinish { Deck = true, Color = foundry ? Hex(routeTread) : "#6a5d56",
                    Alt = foundry ? Hex(routeTread) : "#5e534d", AltRate = .18f,
                    Metal = .35f, Gloss = .4f, GlossVar = .22f, PlateVar = .08f, Scale = 4.8f, Edge = .45f, Grime = 0, Bevel = .025f,
                    SeamStrength = .8f, SeamTone = .6f });
            if (routeFrame != null && !island)
                ArchitectureFor(routeFrame, new ArchitectureFinish { Color = Hex(routeFrame), Alt = Hex(routeFrame), Seam = "#0b0c0d",
                    Metal = .3f, Gloss = .34f, PlateVar = .1f, Scale = 6, Normal = .8f, Edge = .55f, Grime = 0, SeamStrength = .7f, SeamTone = .6f });
            if (foundry)
            {
                // The speckled "refractory" brush texture read as cork at cover scale: replace it with
                // matte fired refractory plates in the same warm tone, grime settling toward the floor.
                var refractory = new ArchitectureFinish { Color = "#b09479", Alt = "#9c8068", AltRate = .12f, Gloss = .26f, GlossVar = .08f,
                    PlateVar = .06f, Scale = 5.2f, Edge = .45f, Grime = .6f, SeamStrength = .65f, SeamTone = .8f };
                if (environmentFinishes.TryGetValue("Ceramic", out var kitCeramic)) ArchitectureFor(kitCeramic, refractory);
                if (mapFinishes.TryGetValue("Refractory", out var propRefractory)) ArchitectureFor(propRefractory, refractory);
            }
            if (mapFinishes.TryGetValue("Alloy", out var kitAlloy))
                ArchitectureFor(kitAlloy, new ArchitectureFinish { Deck = true, Color = Hex(kitAlloy), Alt = Hex(kitAlloy), AltRate = .15f,
                    Metal = .55f, Gloss = .44f, GlossVar = .2f, PlateVar = .08f, Scale = 4.4f, Edge = .45f, Grime = 0, Bevel = .02f,
                    SeamStrength = .7f, SeamTone = .7f });
        }

        void ApplyArchitecture()
        {
            if (!PrepareArchitecture()) return;
            if (island)
            {
                // Kordilera stone: the fractured-stone map at 1:1 read as half-metre camouflage blotches.
                // At 3.2x it reads as grain; values move from olive toward warm weathered granite.
                foreach (var (slot, tint) in new[] { ("DryStone", "#9a968a"), ("DarkStone", "#6c6e66"), ("Moss", "#71824f") })
                    if (mapFinishes.TryGetValue(slot, out var stone))
                    {
                        stone.mainTextureScale *= 3.2f;
                        stone.color = Catalog.Tint(tint);
                    }
                if (environmentFinishes.TryGetValue("Secondary", out var shelf)) shelf.mainTextureScale *= 3.2f;
                return;
            }
            if (!IsRelayArt)
            {
                if (Spec.id != "foundry" && Spec.id != "basin") return;
                ArenaArchitecture();
                RemapArchitecture();
                return;
            }
            // Relay: bright glazed ceramic over very dark graphite, mid-dark deck tiles underfoot.
            // Identity comes from seams and phase plates, not from a checkerboard of plate values.
            // Monolith treatment (review r1): large plates (8.8 m period) with plate-toned joints. The 4 m
            // black-seam grid made every primitive read as a tiled box; phase plates stay the only accent.
            var ceramic = new ArchitectureFinish { Color = "#d4cdbf", Gloss = .66f, GlossVar = .1f, PlateVar = .022f, AltRate = .008f, Edge = .3f,
                Scale = 8.8f, SeamStrength = .55f, SeamTone = 1 };
            ArchitectureFor(Porcelain, ceramic);
            ArchitectureFor(coverPaint, ceramic);
            ArchitectureFor(Insulation, new ArchitectureFinish { Color = "#b4a690", Alt = "#d4cdbf", AltRate = .06f, Gloss = .42f,
                PlateVar = .05f, Scale = 7.2f, SeamStrength = .6f, SeamTone = 1 });
            var graphite = new ArchitectureFinish { Color = "#353a40", Alt = "#40454b", AltRate = .25f, Seam = "#0b0c0d",
                Gloss = .34f, PlateVar = .22f, Scale = 6, Normal = .8f, Edge = .55f, Grime = 0 };
            ArchitectureFor(Graphite, graphite);
            ArchitectureFor(shaftSteel, graphite);
            ArchitectureFor(walls, graphite);
            // Deck: larger cast slabs; value stays calm, per-slab smoothness varies so the sky
            // reflection breaks up across the floor and reads as worn glaze rather than flat paint.
            var deck = new ArchitectureFinish { Deck = true, Color = "#7a7c7c", Alt = "#686c6e", AltRate = .16f, Gloss = .42f, GlossVar = .26f,
                PlateVar = .08f, Scale = 5.2f, Edge = .5f, Grime = 0, Bevel = .03f, SeamStrength = .8f, SeamTone = .7f };
            ArchitectureFor(shaftFloor, deck);
            ArchitectureFor(stairTread, deck);
            ArchitectureFor(relayDrain, graphite);
            // Kit decks and machined alloy previously used a stretched brushed noise; give them metal deck plates.
            var alloyDeck = new ArchitectureFinish { Deck = true, Color = "#8b8e8f", Alt = "#6f7376", AltRate = .2f, Metal = .55f,
                Gloss = .46f, PlateVar = .1f, Scale = 3.2f, Edge = .45f, Grime = 0, Bevel = .02f };
            if (mapFinishes.TryGetValue("Alloy", out var kitAlloy)) ArchitectureFor(kitAlloy, alloyDeck);
            ArchitectureFor(shaftBridge, new ArchitectureFinish { Deck = true, Color = "#a59d8f", Alt = "#7a7c7c", AltRate = .18f,
                Gloss = .42f, PlateVar = .12f, Scale = 3.2f, Edge = .5f, Grime = 0, Bevel = .03f });

            RemapArchitecture();
        }

        void RemapArchitecture()
        {
            var skip = new HashSet<Transform>();
            if (Pickups != null) foreach (var p in Pickups) if (p != null) skip.Add(p);
            foreach (var renderer in Root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform.IsChildOf(Actors) || Skipped(renderer.transform, skip)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                bool box = mesh.name == "Cube" || mesh == armorSection;
                var extent = renderer.bounds.size;
                bool large = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z)) > 7.5f;
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !architectureFinishes.TryGetValue(materials[i], out var finish)) continue;
                    materials[i] = ArchitectureMaterial(materials[i], finish, box, large);
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        static bool Skipped(Transform t, HashSet<Transform> skip)
        {
            for (; t != null; t = t.parent) if (skip.Contains(t)) return true;
            return false;
        }

        Material ArchitectureMaterial(Material source, ArchitectureFinish f, bool box, bool large)
        {
            if (architecture.TryGetValue((source, box, large), out var made)) return made;
            made = new Material(architectureShader) { name = source.name + (box ? " · plates box" : " · plates"), enableInstancing = true };
            made.SetColor("_Color", Catalog.Tint(f.Color));
            made.SetColor("_AltColor", Catalog.Tint(f.Alt));
            made.SetColor("_SeamColor", Catalog.Tint(f.Seam));
            made.SetFloat("_AltRate", f.AltRate);
            made.SetTexture("_DataTex", f.Deck ? deckData : wallData);
            made.SetTexture("_NormalTex", f.Deck ? deckNormal : wallNormal);
            made.SetFloat("_Scale", f.Scale * (large ? 1.75f : 1));
            made.SetFloat("_NormalStrength", f.Normal);
            made.SetFloat("_Glossiness", f.Gloss);
            made.SetFloat("_GlossVar", f.GlossVar);
            made.SetFloat("_Metallic", f.Metal);
            made.SetFloat("_PlateVar", f.PlateVar);
            made.SetFloat("_EdgeWear", f.Edge);
            made.SetFloat("_Grime", f.Grime);
            made.SetFloat("_BoxEdge", box ? 1 : 0);
            made.SetFloat("_BevelWidth", f.Bevel);
            made.SetFloat("_SeamStrength", f.SeamStrength);
            made.SetFloat("_SeamTone", f.SeamTone);
            made.SetFloat("_SideSeams", f.Deck ? f.SideSeams : 1);
            Paint.Add(made);
            architecture[(source, box, large)] = made;
            return made;
        }
    }
}
