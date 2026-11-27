using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed class VektorSuit
    {
        const string Resource = "TRIAS/Vektor/VektorSuit";
        readonly Dictionary<string, (Mesh Mesh, string[] Finishes)> parts = new();
        readonly Material[] factionFinishes = new Material[3];

        internal VektorSuit()
        {
            var asset = Resources.Load<GameObject>(Resource);
            if (asset == null) return;
            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !filter.name.EndsWith("_Mount", System.StringComparison.Ordinal)) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                var finishes = System.Array.ConvertAll(renderer.sharedMaterials, m => m != null ? m.name : "");
                if (filter.sharedMesh.subMeshCount == finishes.Length)
                    parts[filter.name[..^6]] = (filter.sharedMesh, finishes);
            }
        }

        internal bool Build(TriasWorld world, Transform root, Transform rig, Material accent, int faction, bool male)
        {
            if (parts.Count != 17) return false;
            var joints = new Dictionary<string, Transform>();
            foreach (var joint in rig.GetComponentsInChildren<Transform>())
                joints[joint.name] = joint;

            var mounts = new List<(string Part, Transform Bone, Vector3 At, Quaternion Rotation, Vector3 Scale)>(17);
            Vector3 Joint(string name) => root.InverseTransformPoint(joints[name].position);
            float width = male ? 1f : .91f;
            float bodyHeight = male ? 1f : .95f;
            Vector3 bodyScale = new Vector3(width,bodyHeight,bodyHeight);
            Material paint = faction >= 0 ? FactionFinish(world, faction) : world.SafetyAmber;
            Material Finish(string slot) => slot switch
            {
                "Ceramic" => world.Porcelain,
                "Secondary" => world.Insulation,
                "Graphite" => world.Graphite,
                "Alloy" => world.Hardware,
                "Visor" => world.PilotVisor,
                "Faction" => paint,
                "Signal" => accent,
                "Amber" => world.SafetyAmber,
                _ => world.Dark,
            };

            bool Has(string name) => parts.ContainsKey(name);
            if (!Has("Helmet") || !Has("Torso") || !Has("Pelvis") ||
                !joints.ContainsKey("Head") || !joints.ContainsKey("Spine") || !joints.ContainsKey("Hips")) return false;
            mounts.Add(("Helmet", joints["Head"],Joint("Head")+new Vector3(0,male ? .179f : .162f,-.044f),Quaternion.identity,bodyScale));
            mounts.Add(("Torso", joints["Spine"],Joint("Spine")+new Vector3(0,.27f,.005f),Quaternion.identity,bodyScale));
            mounts.Add(("Pelvis", joints["Hips"],Joint("Hips")+Vector3.up*.015f,Quaternion.identity,bodyScale));

            foreach (string side in new[] {"Left","Right"})
            {
                string arm = side+"Arm", fore = side+"ForeArm", hand = side+"Hand";
                string thigh = side+"UpLeg", knee = side+"Leg", foot = side+"Foot";
                if (!joints.ContainsKey(arm) || !joints.ContainsKey(fore) || !joints.ContainsKey(hand) ||
                    !joints.ContainsKey(thigh) || !joints.ContainsKey(knee) || !joints.ContainsKey(foot) ||
                    !Has(side+"Upper") || !Has(side+"Shoulder") || !Has(side+"Forearm") || !Has(side+"Hand") ||
                    !Has(side+"Thigh") || !Has(side+"Shin") || !Has(side+"Boot")) return false;

                Limb(side+"Upper",arm,fore,.290f);
                Limb(side+"Forearm",fore,hand,.276f);
                Limb(side+"Thigh",thigh,knee,.353f);
                Limb(side+"Shin",knee,foot,.365f);
                var a = Joint(arm);
                float sign = a.x < Joint("Spine").x ? -1 : 1;
                var shoulder = Vector3.Lerp(a,Joint(fore),.13f) + new Vector3(sign*.067f,.037f,-.017f);
                mounts.Add((side+"Shoulder",joints[arm],shoulder,Quaternion.Euler(-9,0,-sign*13),bodyScale));
                mounts.Add((side+"Hand",joints[hand],Joint(hand),Quaternion.identity,bodyScale));
                mounts.Add((side+"Boot",joints[foot],Joint(foot)+new Vector3(0,-.015f,.041f),Quaternion.identity,bodyScale));
            }

            foreach (var mount in mounts)
            {
                var joint = TriasWorld.Group(root,mount.Part+" · Blender",mount.At);
                joint.localRotation = mount.Rotation;
                joint.localScale = mount.Scale;
                joint.SetParent(mount.Bone,true);
                joint.gameObject.layer = TriasWorld.Decor;
                var (mesh, finishes) = parts[mount.Part];
                joint.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = joint.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = System.Array.ConvertAll(finishes,Finish);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                if (faction >= 0 && mount.Part == "Torso")
                {
                    world.Stamp(joint,faction,accent,new Vector3(-.083f,.028f,.162f),.084f);
                    world.Stamp(joint,faction,accent,new Vector3(0,.089f,-.277f),.063f)
                        .localRotation = Quaternion.Euler(0,180,0);
                }
            }
            return true;

            void Limb(string part, string start, string end, float authoredLength)
            {
                Vector3 a = Joint(start), b = Joint(end);
                Vector3 axis = a-b;
                Vector3 up = axis.normalized;
                Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward,up);
                if (forward.sqrMagnitude < .04f) forward = Vector3.ProjectOnPlane(Vector3.right,up);
                mounts.Add((part,joints[start],(a+b)*.5f,Quaternion.LookRotation(forward,up),
                    new Vector3(width,axis.magnitude/authoredLength,bodyHeight)));
            }
        }
        Material FactionFinish(TriasWorld world, int faction)
        {
            if (factionFinishes[faction] != null) return factionFinishes[faction];
            var finish = new Material(world.FactionPaint[faction]) { name = "Vektor faction armor" };
            Color baseColor = finish.color;
            // Keep the large marks identifiable against shaded graphite without a player outline.
            finish.SetColor("_EmissionColor", baseColor * (faction == 2 ? .42f : .33f));
            world.Paint.Add(finish);
            return factionFinishes[faction] = finish;
        }
    }
}
