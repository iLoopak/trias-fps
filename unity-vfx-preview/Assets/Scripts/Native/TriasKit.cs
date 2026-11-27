using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Trias.Native
{
    internal sealed class TriasKit
    {
        const string Directory = "TRIAS/Kit/";
        readonly Dictionary<string, GameObject> meshes = new Dictionary<string, GameObject>();
        readonly TriasWorld world;
        readonly Dictionary<string, Material> landscape = new Dictionary<string, Material>();
        readonly Material crateFinish;
        readonly AnimationClip idle, run, jump;
        VektorSuit vektor;

        internal TriasKit(TriasWorld world)
        {
            this.world = world;
            crateFinish = world.MaterialAt("#cedaed");
            crateFinish.mainTexture = Resources.Load<Texture2D>(Directory + "Weapons/colormap");
            crateFinish.SetFloat("_Glossiness", .56f);
            crateFinish.SetFloat("_Metallic", .30f);
            idle = Clip("idle"); run = Clip("run"); jump = Clip("jump");
            landscape["leafsGreen"] = world.MaterialAt("#528b50");
            landscape["leafsDark"] = world.MaterialAt("#326e4e");
            landscape["woodBark"] = world.MaterialAt("#735542");
            landscape["woodBarkDark"] = landscape["woodBark"];
            landscape["grass"] = world.MaterialAt("#70994c");
            landscape["dirt"] = world.MaterialAt("#6d6c62");
            landscape["_defaultMat"] = world.MaterialAt("#858472");
            landscape["colorYellow"] = world.MaterialAt("#e6bd65");
        }

        static AnimationClip Clip(string key)
        {
            var clips = Resources.LoadAll<AnimationClip>(Directory + "Pilots/" + key);
            return Array.Find(clips, c => c.name.EndsWith(key, StringComparison.OrdinalIgnoreCase));
        }

        Transform Place(Transform parent, string resource, Quaternion rotation, out Bounds bounds)
        {
            bounds = default;
            if (!meshes.TryGetValue(resource, out var model))
            {
                model = Resources.Load<GameObject>(Directory + resource);
                meshes.Add(resource, model);
            }
            if (model == null) return null;
            var item = TriasWorld.Group(parent, model.name);
            var art = Object.Instantiate(model, item, false);
            art.transform.localRotation = rotation * art.transform.localRotation;
            bool hasBounds = false;
            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.layer = TriasWorld.Decor;
                var box = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = box.center + Vector3.Scale(box.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = item.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (hasBounds) bounds.Encapsulate(point);
                    else { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                }
            }
            if (hasBounds) return item;
            Object.Destroy(item.gameObject);
            return null;
        }

        internal bool Nature(Transform parent, string name, Vector3 size, Vector3 at, float yaw = 0)
        {
            var item = Sized(parent, "Nature/" + name, size, at, yaw);
            if (item == null) return false;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>())
            {
                var surfaces = renderer.sharedMaterials;
                for (int i = 0; i < surfaces.Length; i++)
                    if (surfaces[i] != null && landscape.TryGetValue(surfaces[i].name, out var material)) surfaces[i] = material;
                renderer.sharedMaterials = surfaces;
            }
            return true;
        }

        internal bool SupplyCrate(Transform parent, bool wide, Vector3 size)
        {
            var item = Sized(parent, wide ? "Weapons/crate-wide" : "Weapons/crate-medium", size, Vector3.zero, 0);
            if (item == null) return false;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = crateFinish;
            return true;
        }

        internal bool Spacecraft(Transform parent, string file, float length, Material hull,
            Material armor, Material glass, Material markings)
        {
            var item = Place(parent, "Space/" + file, Quaternion.identity, out var bounds);
            if (item == null) return false;
            float scale = length / Mathf.Max(.001f, bounds.size.z);
            item.localScale = Vector3.one * scale;
            item.localPosition = -bounds.center * scale;
            PaintSpaceModel(item, (hull, armor, glass, markings));
            return true;
        }

        internal bool SpaceFixture(Transform parent, string file, Vector3 size, Vector3 at, float yaw,
            (Material Hull, Material Frame, Material Glass, Material Trim) paint)
        {
            var item = Sized(parent, "Space/" + file, size, at, yaw);
            if (item == null) return false;
            PaintSpaceModel(item, paint);
            return true;
        }

        static void PaintSpaceModel(Transform item, (Material Hull, Material Frame, Material Glass, Material Trim) paint)
        {
            foreach (var renderer in item.GetComponentsInChildren<Renderer>())
            {
                var surfaces = renderer.sharedMaterials;
                for (int i = 0; i < surfaces.Length; i++)
                    surfaces[i] = surfaces[i]?.name switch
                    {
                        "metalDark" => paint.Frame,
                        "dark" => paint.Glass,
                        "metalRed" => paint.Trim,
                        _ => paint.Hull
                    };
                renderer.sharedMaterials = surfaces;
            }
        }

        Transform Sized(Transform parent, string resource, Vector3 size, Vector3 at, float yaw)
        {
            var item = Place(parent, resource, Quaternion.Euler(0, yaw, 0), out var bounds);
            if (item == null) return null;
            Vector3 scale = new Vector3(size.x / Mathf.Max(.001f, bounds.size.x),
                size.y / Mathf.Max(.001f, bounds.size.y), size.z / Mathf.Max(.001f, bounds.size.z));
            item.localScale = scale;
            item.localPosition = at - Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
            return item;
        }

        internal Transform Pilot(Transform parent, string kit, Material accent, int faction, bool male, out Animation motion)
        {
            motion = null;
            var item = Place(parent, "Pilots/characterMedium", Quaternion.identity, out _);
            if (item == null) return null;
            var rig = item.GetChild(0).gameObject;
            var renderers = item.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) { Object.Destroy(item.gameObject); return null; }
            if (idle != null) idle.SampleAnimation(rig, 0);
            // The imported mesh and skeleton use different unit transforms. Size the
            // posed character from feet to head in the wrapper's coordinate space.
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (var joint in rig.GetComponentsInChildren<Transform>())
            {
                float y = item.InverseTransformPoint(joint.position).y;
                if (joint.name == "Head_end") top = y;
                if (joint.name == "LeftToes_end" || joint.name == "RightToes_end") bottom = Mathf.Min(bottom, y);
            }
            if (top <= bottom) { Object.Destroy(item.gameObject); return null; }
            float height = kit == "bulwark" ? 1.77f : kit == "wraith" ? 1.62f : 1.68f;
            float scale = height * (male ? .963f : .92f) / (top - bottom);
            item.localScale = new Vector3(scale * (kit == "bulwark" ? 1.23f : kit == "wraith" ? .87f : 1f) * (male ? 1.045f : 1f), scale, scale);
            item.localPosition = Vector3.down * bottom * scale;
            foreach (var renderer in renderers) renderer.enabled = false;
            if (kit != "vector" || !(vektor ??= new VektorSuit()).Build(world,parent,rig.transform,accent,faction,male))
                PilotSuit.Build(world, parent, rig.transform, kit, accent, faction, male);
            var legacy = rig.GetComponent<Animation>();
            motion = legacy != null ? legacy : rig.AddComponent<Animation>();
            motion.playAutomatically = false;
            motion.cullingType = AnimationCullingType.AlwaysAnimate;
            if (idle != null) { motion.AddClip(idle, "idle"); motion["idle"].wrapMode = WrapMode.Loop; }
            if (run != null) { motion.AddClip(run, "run"); motion["run"].wrapMode = WrapMode.Loop; }
            if (jump != null) { motion.AddClip(jump, "jump"); motion["jump"].wrapMode = WrapMode.ClampForever; }
            return item;
        }
    }
}
