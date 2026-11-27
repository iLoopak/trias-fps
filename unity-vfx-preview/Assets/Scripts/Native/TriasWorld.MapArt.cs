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
                "Alloy" => Hardware, "Amber" => SafetyAmber, "Heat" => MaterialAt("#df591e", true),
                "DryStone" => MaterialAt("#777667"), "DarkStone" => MaterialAt("#51564e"),
                "Moss" => MaterialAt("#65713f"), "Bark" => MaterialAt("#493d2d"),
                "Wood" => MaterialAt("#937751"), "Leaf" => MaterialAt("#39604b"),
                "YoungLeaf" => MaterialAt("#59784f"), _ => Dark
            };
            mapFinishes.Add(slot, material);
            return material;
        }
    }
}
