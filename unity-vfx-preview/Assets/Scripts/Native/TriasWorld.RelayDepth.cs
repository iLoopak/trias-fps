using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Material relayPane, relayDrain, relayPaintedSteel;
        Font relayStencilFont;
        BlenderParts relayLoadBearing;

        void ConfigureRelayDepthMaterials()
        {
            relayDrain = MapSurface("#303640", "brushed-deck", .46f, .26f);
            relayPaintedSteel = MapSurface("#ca763e", "refractory", .08f, .24f);
            relayStencilFont = Resources.Load<Font>("TRIAS/Fonts/BarlowSemiCondensed-Medium");

            var shader = Resources.Load<Shader>("TRIAS/RelayGlazing");
            if (shader == null || !shader.isSupported) return;
            relayPane = new Material(shader);
            Paint.Add(relayPane);
        }

        bool BuildRelayStructuralCover(BoxSpec box, Transform home, float height)
        {
            if (box.profile == "airlock") return false;
            string mount = box.gantry ? "ContinuousGantry" : box.relay || (box.profile == "pylon" || box.structure == "pylon") && height >= 2.6f
                ? "ContinuousPylon" : "ContinuousBulkhead";
            relayLoadBearing ??= new BlenderParts("TRIAS/RelayStructure/RelayStructure");
            Material Finish(string slot) => slot switch
            {
                "Ceramic" => Porcelain,
                "Secondary" => relayStructure,
                "Alloy" => Hardware,
                "Amber" => relayPaintedSteel,
                _ => Graphite
            };
            bool crosswise = mount == "ContinuousBulkhead" && box.w < box.d * .94f;
            if (crosswise)
            {
                home = Group(home, "Transverse load-bearing cover");
                home.localRotation = Quaternion.Euler(0, 90, 0);
            }
            var size = crosswise ? new Vector3(box.d, height, box.w) : new Vector3(box.w, height, box.d);
            return relayLoadBearing.Fit(mount, home, Vector3.up * height / 2, size, Finish);
        }

        void BuildInductionColumn(Transform mount, Material fallback)
        {
            Vector3 center = new Vector3(0, 3.05f, 0);
            var shader = Resources.Load<Shader>("TRIAS/RelayInduction");
            if (shader == null || !shader.isSupported)
            {
                Part(mount, reactorEnergy, center, new Vector3(.67f, 5.65f, .67f), PrimitiveType.Cylinder);
                Part(mount, fallback, center, new Vector3(.25f, 5.48f, .25f), PrimitiveType.Cylinder);
                return;
            }

            for (int layer = 0; layer < 2; layer++)
            {
                bool halo = layer == 1;
                var paint = new Material(shader);
                paint.SetColor("_Color", Catalog.Tint(halo ? "#ff812d" : "#ffe0a9"));
                paint.SetFloat("_Halo", halo ? 1 : 0);
                Paint.Add(paint);
                float radius = halo ? .74f : .20f;
                var plasma = Part(mount, paint, center, new Vector3(radius, 5.65f, radius), PrimitiveType.Cylinder);
                var renderer = plasma.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // The existing solid perimeter remains the collision source for the pressure glazing.
        void RelayPerimeter(Transform wall, Transform collisionWall, int side, float width)
        {
            if (relayPane == null || side > 1) return;
            collisionWall.GetComponent<Renderer>().enabled = false;
            float edge = Spec.length;
            float window = side == 0 ? -17 : 15, span = 19;
            float left = window - span * .5f, right = window + span * .5f;
            void SolidSection(float min, float max, float y, float height, Material material)
            {
                Part(wall, material, new Vector3((min + max) * .5f, y, edge + .25f), new Vector3(max - min, height, .50f));
            }
            SolidSection(-width * .5f, left, 2.15f, 4.3f, walls);
            SolidSection(right, width * .5f, 2.15f, 4.3f, walls);
            SolidSection(left, right, .34f, .68f, Graphite);
            SolidSection(left, right, 4.12f, .36f, Porcelain);
            var glazing = Part(wall, relayPane, new Vector3(window, 2.32f, edge + .24f), new Vector3(span, 3.32f, .06f));
            glazing.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i <= 4; i++)
            {
                float x = left + i * span / 4;
                Part(wall, relayDrain, new Vector3(x, 2.30f, edge), new Vector3(.18f, 3.42f, .53f));
                Part(wall, Hardware, new Vector3(x + .17f, .69f, edge - .19f), new Vector3(.49f, .15f, .25f));
            }
            Part(wall, Hardware, new Vector3(window, .78f, edge - .12f), new Vector3(span, .16f, .38f));
            Part(wall, SafetyAmber, new Vector3(window, .87f, edge - .28f), new Vector3(span - .18f, .047f, .10f));

            var external = Group(wall, "Suspended maintenance truss", new Vector3(window + (side == 0 ? .8f : -2), -3.6f, edge + 9.4f));
            Armor(external, Graphite, Vector3.zero, new Vector3(21f, 1.31f, 3.7f), true);
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 4.15f;
                Armor(external, i == 0 ? Insulation : Porcelain, new Vector3(x, .43f, -1.83f),
                    new Vector3(3.94f, .82f, .23f), true);
                Beam(external, Graphite, new Vector3(x - 1.6f, -3.6f, -.76f), new Vector3(x + 1.48f, -.40f, -.82f), .30f);
            }
            var hanging = Group(external, "Suspended radiator", new Vector3(6.4f, -5.9f, .18f));
            Armor(hanging, relayPaintedSteel, Vector3.zero, new Vector3(2.1f, 10.8f, 3.12f), false);
            Armor(hanging, relayDrain, new Vector3(0, -.23f, -1.56f), new Vector3(1.62f, 9.78f, .36f), false);
            for (int i = 0; i < 8; i++)
                Part(hanging, Hardware, new Vector3(0, .78f * i - 3.2f, -1.8f), new Vector3(1.93f, .12f, .29f));
        }

        void DressRelayMechanical(BoxSpec box, Transform panel, int face, float width, float depth, float h)
        {
            if (width < 2.4f || face % 2 != 0) return;
            bool near = box.relay || box.gantry || box.profile == "pylon";
            if (!near) return;
            Vector3 at = new Vector3(width * -.24f, box.gantry ? h * .41f : h * .19f, depth + .075f);
            var frame = Group(panel, "Ceramic fasteners and ventilation", at);
            Armor(frame, relayDrain, Vector3.zero, new Vector3(width * .41f, Mathf.Min(.30f, h * .44f), .15f), false);
            for (int i = 0; i < 4; i++)
                Part(frame, Hardware, new Vector3((i - 1.5f) * width * .074f, -.006f, .079f),
                    new Vector3(width * .050f, Mathf.Min(.14f, h * .22f), .045f));
            if (box.profile == "pylon" && h >= 3.3f && box.sector >= 0 && relayStencilFont != null && face == 0)
            {
                var stencil = Group(panel, "Cast casing stencil", new Vector3(width * -.17f, h * .79f, depth + .126f));
                stencil.localRotation = Quaternion.Euler(0, 180, 0);
                var text = stencil.gameObject.AddComponent<TextMesh>();
                stencil.gameObject.layer = Decor;
                text.font = relayStencilFont;
                text.text = $"R-{box.sector + 1:00}";
                text.fontSize = 72;
                text.characterSize = .115f;
                text.color = Catalog.Tint("#40434a");
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                var renderer = text.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = relayStencilFont.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (box.gantry)
            {
                float lane = width * .33f;
                Armor(panel, relayPaintedSteel, new Vector3(lane, h * .70f, depth + .163f),
                    new Vector3(width * .070f, h * .39f, .061f), false);
                Part(panel, Hardware, new Vector3(lane, h * .74f, depth + .197f),
                    new Vector3(width * .034f, .053f, .024f));
            }
        }

        void BuildRelayDeckJoints()
        {
            float edge = Spec.length;
            var groups = Group(Root, "Recessed relay service tracks");
            var deckBolts = Group(groups, "Inset steel");
            for (int i = -2; i <= 2; i++)
            {
                float z = i * 12.2f + 1.43f;
                Part(groups, relayDrain, new Vector3(0, .003f, z), new Vector3(edge * 2 - 2, .004f, .076f));
                if (i % 2 != 0) continue;
                for (int deckSide = -1; deckSide <= 1; deckSide += 2)
                {
                    float x = deckSide * (8.45f + Mathf.Abs(i) * 2.15f);
                    Part(groups, relayDrain, new Vector3(x, .007f, z + .33f), new Vector3(2.75f, .011f, .71f));
                    for (int tooth = 0; tooth < 10; tooth++)
                        Part(deckBolts, Hardware, new Vector3(x + (tooth - 4.5f) * .247f, .012f, z + .33f), new Vector3(.043f, .007f, .59f));
                    for (int marker = 0; marker < 3; marker++)
                    {
                        var chevron = Part(groups, relayPaintedSteel, new Vector3(x + 1.67f + marker * .19f, .011f, z + .32f),
                            new Vector3(.093f, .006f, .39f));
                        chevron.localRotation = Quaternion.Euler(0, 30, 0);
                    }
                }
            }

            // These rails stay flush with the deck and batch together across the arena.
            var rendererSet = groups.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in rendererSet) renderer.shadowCastingMode = ShadowCastingMode.Off;
            StaticBatchingUtility.Combine(groups.gameObject);
        }
    }
}
