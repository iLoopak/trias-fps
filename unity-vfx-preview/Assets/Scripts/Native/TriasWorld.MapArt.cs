using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        BlenderParts mapProps;
        readonly Dictionary<string, Material> mapFinishes = new();

        bool MapProp(string profile, Transform parent, Vector3 center, Vector3 size)
        {
            string mesh = profile switch
            {
                "airlock" => "Airlock", "kiln" => "Kiln", "slag" => "Slag", "ore" => "Ore",
                "spruce" => "Spruce", "bridge" => "Bridge", "shelves" => "Shelves",
                "relay-bay" => "PressureBay", "foundry-bay" => "QuenchBay", "basin-bay" => "DrillBay",
                "splitrock" => "SplitRock", "granite" => "Granite", _ => null
            };
            if (mesh == null) return false;
            mapProps ??= new BlenderParts("TRIAS/MapProps/MapProps");
            return mapProps.Fit(mesh, parent, center, size, MapFinish);
        }

        Material MapFinish(string slot)
        {
            if (mapFinishes.TryGetValue(slot, out var cached)) return cached;
            Material material = slot switch
            {
                "Ceramic" => Porcelain, "Secondary" => Insulation, "Graphite" => Graphite,
                "Alloy" => MapSurface("#b7b9b3", "brushed-deck", .48f, .32f),
                "Amber" => SafetyAmber, "Heat" => MaterialAt("#df591e", true),
                "Mineral" => MineralSolid(),
                "Refractory" => MapSurface("#a08069", "refractory", 0, .13f),
                "DryStone" => MapSurface("#918e7f", "fractured-stone", 0, .11f),
                "DarkStone" => MapSurface("#666b5c", "fractured-stone", 0, .14f),
                "Moss" => MapSurface("#768855", "fractured-stone", 0, .08f),
                "Bark" => MapSurface("#5b4b37", "weathered-timber", 0, .12f),
                "Wood" => MapSurface("#b89b71", "weathered-timber", 0, .22f),
                "Leaf" => MaterialAt("#39604b"),
                "YoungLeaf" => MaterialAt("#59784f"), _ => Dark
            };
            mapFinishes.Add(slot, material);
            return material;
        }

        Material MapSurface(string tint, string texture, float metal, float smooth)
        {
            var surface = MaterialAt(tint, false, "SurfaceDetail/" + texture);
            surface.SetFloat("_Metallic", metal);
            surface.SetFloat("_Glossiness", smooth);
            return surface;
        }

        void DressRouteCover(BoxSpec box, Transform cover, float height)
        {
            if (box.profile != "pylon" || box.sector < 0 || height < 2.6f || box.d < 2.7f) return;
            float side = box.sector == 1 ? -1 : 1;
            var mount = Group(cover, "Route maintenance panel", new Vector3(side * (box.w * .5f + .04f), height * .48f, 0));
            mount.localRotation = Quaternion.Euler(0, side * 90f, 0);
            MapProp(Spec.id + "-bay", mount, Vector3.zero, new Vector3(box.d * .66f, height * .48f, .22f));
        }

        void DressServiceWall(Transform wall, int side, Material floor)
        {
            string profile = Spec.id + "-bay";
            bool heavy = Spec.id != "relay";
            float edge = Spec.length;
            for (int bay = 0; bay < 2; bay++)
            {
                float offset = (bay == 0 ? -.38f : .31f) * edge + (side % 2 == 0 ? -1.1f : .85f);
                var mount = Group(wall, "Perimeter service bay", new Vector3(offset, 1.94f, edge - .13f));
                mount.localRotation = Quaternion.Euler(0, 180, 0);
                if (!MapProp(profile, mount, Vector3.zero, new Vector3(heavy ? 4.85f : 4.05f, 2.67f, .42f))) continue;
                Part(wall, floor, new Vector3(offset, .009f, edge - 1.45f), new Vector3(heavy ? 5.3f : 4.7f, .017f, 2.55f));
                Part(wall, SafetyAmber, new Vector3(offset, .022f, edge - 2.74f), new Vector3(heavy ? 4.45f : 3.8f, .009f, .093f));
            }
        }
    }
}
