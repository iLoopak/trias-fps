using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed class BlenderParts
    {
        readonly Dictionary<string, Part> parts = new();

        internal BlenderParts(string resource)
        {
            var asset = Resources.Load<GameObject>(resource);
            if (asset == null) return;
            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !filter.name.EndsWith("_Mount", StringComparison.Ordinal)) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                var finishes = Array.ConvertAll(renderer.sharedMaterials,m => m != null ? m.name : "");
                if (filter.sharedMesh.subMeshCount == finishes.Length)
                    parts[filter.name[..^6]] = new Part(filter.sharedMesh,finishes);
            }
        }

        internal bool Has(string key) => parts.ContainsKey(key);

        internal void Bind(string key, Transform target, Func<string,Material> finish, bool shadows)
        {
            var part = parts[key];
            target.gameObject.layer = TriasWorld.Decor;
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
            var renderer = target.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = Array.ConvertAll(part.Finishes,finish.Invoke);
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        internal bool Fit(string key, Transform parent, Vector3 center, Vector3 size, Func<string,Material> finish)
        {
            if (!parts.TryGetValue(key, out var part)) return false;
            Bounds bounds = part.Mesh.bounds;
            var target = TriasWorld.Group(parent, key);
            target.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            target.localPosition = center - Vector3.Scale(bounds.center, target.localScale);
            Bind(key, target, finish, true);
            return true;
        }

        readonly struct Part
        {
            internal readonly Mesh Mesh;
            internal readonly string[] Finishes;
            internal Part(Mesh mesh, string[] finishes) { Mesh = mesh; Finishes = finishes; }
        }
    }

    internal sealed class BlenderArmory
    {
        internal const string Action = "Blender action";
        internal const string Payload = "Blender payload";
        readonly BlenderParts parts = new("TRIAS/Armory/Arsenal");

        internal bool Build(TriasWorld world, Transform root, string key, Material accent)
        {
            return Build(root, key, slot => slot switch
            {
                "Ceramic" => world.Porcelain,
                "Secondary" => world.Insulation,
                "Graphite" => world.Graphite,
                "Alloy" => world.Hardware,
                "Visor" => world.PilotVisor,
                "Amber" => world.SafetyAmber,
                _ => slot.EndsWith("Glow", StringComparison.Ordinal) ? accent : world.Dark,
            });
        }

        internal bool Build(Transform root, string key, Func<string, Material> finish)
        {
            string part = key + "_";
            if (!parts.Has(part+"Receiver") || !parts.Has(part+"Chamber") ||
                !parts.Has(part+"Action") || !parts.Has(part+"Payload")) return false;

            void Add(string subPart,string label,Vector3 position)
            {
                var joint = TriasWorld.Group(root,label,position);
                parts.Bind(part+subPart,joint,finish,false);
            }

            Add("Receiver","Blender receiver",Vector3.zero);
            Add("Chamber",PhaseArmory.Chamber,new Vector3(0,.026f,PhaseArmory.Muzzle(key) - (key == "pulse" ? .098f : .096f)));
            Add("Action",Action,WeaponHandling.ActionPoint(key));
            Add("Payload",Payload,WeaponHandling.ServicePoint(key));
            return true;
        }
    }
}
