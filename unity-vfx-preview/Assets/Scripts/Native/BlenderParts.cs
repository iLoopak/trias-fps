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

        internal bool FitEnvironment(string key, Transform parent, Vector3 center, Vector3 size,
            Func<string, Material> finish, bool shadows, bool lod, float lodScreenHeight)
        {
            if (!parts.TryGetValue(key, out var part)) return false;
            var target = TriasWorld.Group(parent, key);
            Bounds bounds = part.Mesh.bounds;
            target.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            target.localPosition = center - Vector3.Scale(bounds.center, target.localScale);
            Bind(key, target, finish, shadows);

            string reduced = key + "_LOD1";
            if (!lod || !parts.ContainsKey(reduced)) return true;
            var simpler = TriasWorld.Group(target, reduced);
            // Keep the high-detail scale and pivot so the trunk and main plates do not move.
            Bind(reduced, simpler, finish, shadows);
            var levels = target.gameObject.AddComponent<LODGroup>();
            levels.SetLODs(new[]
            {
                new LOD(lodScreenHeight, new Renderer[] { target.GetComponent<MeshRenderer>() }),
                new LOD(0, new Renderer[] { simpler.GetComponent<MeshRenderer>() })
            });
            levels.localReferencePoint = bounds.center;
            levels.size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            return true;
        }

        internal bool FitEnvironmentRun(string[] keys, Transform parent, float length, float height, float thickness,
            bool alongDepth, Func<string, Material> finish, List<Mesh> ownedMeshes, float lodScreenHeight)
        {
            if (keys.Length < 2) return false;
            foreach (string key in keys)
                if (!parts.TryGetValue(key, out var high) || !high.Mesh.isReadable ||
                    !parts.TryGetValue(key + "_LOD1", out var low) || !low.Mesh.isReadable)
                    return false;

            float pitch = length / keys.Length;
            Vector3 size = alongDepth ? new Vector3(thickness,height,pitch) : new Vector3(pitch,height,thickness);
            var batches = new []
            {
                new Dictionary<Material,List<CombineInstance>>(),
                new Dictionary<Material,List<CombineInstance>>()
            };
            float lodSize = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                var high = parts[keys[i]];
                float offset = -length*.5f + pitch*(i+.5f);
                var center = Vector3.up * height*.5f + (alongDepth ? Vector3.forward : Vector3.right)*offset;
                Bounds bounds = high.Mesh.bounds;
                var scale = new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
                var matrix = Matrix4x4.TRS(center-Vector3.Scale(bounds.center,scale),Quaternion.identity,scale);
                lodSize = Mathf.Max(lodSize,Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)*
                    Mathf.Max(scale.x,scale.y,scale.z));
                Collect(high,batches[0],matrix);
                Collect(parts[keys[i]+"_LOD1"],batches[1],matrix);
            }

            var levels = parent.gameObject.AddComponent<LODGroup>();
            levels.SetLODs(new[]
            {
                new LOD(lodScreenHeight,new Renderer[] { MountBatch("Combined LOD0",batches[0]) }),
                new LOD(0,new Renderer[] { MountBatch("Combined LOD1",batches[1]) })
            });
            levels.localReferencePoint = Vector3.up*height*.5f;
            levels.size = lodSize;
            return true;

            void Collect(Part part, Dictionary<Material,List<CombineInstance>> batch, Matrix4x4 matrix)
            {
                for (int submesh = 0; submesh < part.Finishes.Length; submesh++)
                {
                    Material material = finish(part.Finishes[submesh]);
                    if (!batch.TryGetValue(material, out var instances))
                        batch.Add(material,instances = new List<CombineInstance>());
                    instances.Add(new CombineInstance { mesh = part.Mesh, subMeshIndex = submesh, transform = matrix });
                }
            }

            MeshRenderer MountBatch(string name, Dictionary<Material,List<CombineInstance>> batch)
            {
                var materials = new Material[batch.Count];
                var pieces = new CombineInstance[batch.Count];
                int i = 0;
                foreach (var entry in batch)
                {
                    var strip = new Mesh { indexFormat = IndexFormat.UInt32 };
                    strip.CombineMeshes(entry.Value.ToArray(),true,true);
                    materials[i] = entry.Key;
                    pieces[i++] = new CombineInstance { mesh = strip };
                }

                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pieces,false,false);
                foreach (var piece in pieces) UnityEngine.Object.Destroy(piece.mesh);
                mesh.UploadMeshData(true);
                ownedMeshes.Add(mesh);
                var model = TriasWorld.Group(parent,name);
                model.gameObject.layer = TriasWorld.Decor;
                model.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = model.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                return renderer;
            }
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
