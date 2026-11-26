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
        readonly Dictionary<string, Material> suits = new Dictionary<string, Material>();
        readonly Dictionary<string, Material> landscape = new Dictionary<string, Material>();
        readonly Material blaster;
        readonly AnimationClip idle, run, jump;

        internal TriasKit(TriasWorld world)
        {
            blaster = world.MaterialAt("#cedaed");
            blaster.mainTexture = Resources.Load<Texture2D>(Directory + "Weapons/colormap");
            blaster.SetFloat("_Glossiness", .56f);
            blaster.SetFloat("_Metallic", .30f);
            var pilotKeys = new[] { "vector", "bulwark", "wraith", "kestrel" };
            var paint = new[] { "#abc5d7", "#d3ac85", "#837b9c", "#97bb95" };
            var skin = Resources.Load<Texture2D>(Directory + "Pilots/cyborgFemaleA");
            for (int i = 0; i < pilotKeys.Length; i++)
            {
                var suit = world.MaterialAt(paint[i]);
                suit.mainTexture = skin;
                suits.Add(pilotKeys[i], suit);
            }
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

        internal bool Gun(Transform parent, string weapon, Material accent)
        {
            (string file, float length) = weapon switch
            {
                "scatter" => ("blaster-l", .65f),
                "lance" => ("blaster-f", .94f),
                "arc" => ("blaster-p", .66f),
                "rift" => ("blaster-a", .86f),
                _ => ("blaster-d", .72f)
            };
            var item = Place(parent, "Weapons/" + file, Quaternion.Euler(0, 180, 0), out var bounds);
            if (item == null) return false;
            float scale = length / Mathf.Max(.001f, bounds.size.z);
            item.localScale = Vector3.one * scale;
            item.localPosition = new Vector3(-bounds.center.x * scale, -bounds.center.y * scale - .021f, length * .12f - bounds.center.z * scale);
            foreach (var renderer in item.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = blaster;
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = TriasWorld.Part(parent, accent, new Vector3(side * (bounds.extents.x * scale + .002f), .02f, .12f),
                    new Vector3(.009f, .012f, length * .24f));
                rail.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return true;
        }

        internal bool Nature(Transform parent, string name, Vector3 size, Vector3 at, float yaw = 0)
        {
            var item = Place(parent, "Nature/" + name, Quaternion.Euler(0, yaw, 0), out var bounds);
            if (item == null) return false;
            Vector3 scale = new Vector3(size.x / Mathf.Max(.001f, bounds.size.x),
                size.y / Mathf.Max(.001f, bounds.size.y), size.z / Mathf.Max(.001f, bounds.size.z));
            item.localScale = scale;
            item.localPosition = at - Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
            foreach (var renderer in item.GetComponentsInChildren<Renderer>())
            {
                var surfaces = renderer.sharedMaterials;
                for (int i = 0; i < surfaces.Length; i++)
                    if (surfaces[i] != null && landscape.TryGetValue(surfaces[i].name, out var material)) surfaces[i] = material;
                renderer.sharedMaterials = surfaces;
            }
            return true;
        }

        internal Transform Pilot(Transform parent, string kit, out Animation motion)
        {
            motion = null;
            var item = Place(parent, "Pilots/characterMedium", Quaternion.identity, out _);
            if (item == null) return null;
            var rig = item.GetChild(0).gameObject;
            var renderers = item.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) { Object.Destroy(item.gameObject); return null; }
            if (idle != null) idle.SampleAnimation(rig, 0);
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = suits.TryGetValue(kit, out var paint) ? paint : suits["vector"];
                renderer.updateWhenOffscreen = true;
            }
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
            float scale = height * .92f / (top - bottom);
            item.localScale = new Vector3(scale * (kit == "bulwark" ? 1.22f : kit == "wraith" ? .90f : 1f), scale, scale);
            item.localPosition = Vector3.down * bottom * scale;
            var legacy = rig.GetComponent<Animation>();
            motion = legacy != null ? legacy : rig.AddComponent<Animation>();
            motion.playAutomatically = false;
            if (idle != null) { motion.AddClip(idle, "idle"); motion["idle"].wrapMode = WrapMode.Loop; }
            if (run != null) { motion.AddClip(run, "run"); motion["run"].wrapMode = WrapMode.Loop; }
            if (jump != null) { motion.AddClip(jump, "jump"); motion["jump"].wrapMode = WrapMode.ClampForever; }
            return item;
        }
    }
}
