using System;
using UnityEngine;

namespace Trias.Native
{
    internal static class PilotSuit
    {
        internal static void Build(TriasWorld world, Transform root, Transform rig, string kit, Material accent, int faction, bool male)
        {
            var bones = rig.GetComponentsInChildren<Transform>();
            Transform Bone(string name) => Array.Find(bones, bone => bone.name == name);
            Transform Anchor(string label, Transform bone, Vector3 at, Quaternion rotation)
            {
                var joint = TriasWorld.Group(root, label, at);
                joint.localRotation = rotation;
                if (bone != null) joint.SetParent(bone, true);
                return joint;
            }
            Vector3 Joint(Transform bone) => root.InverseTransformPoint(bone.position);
            bool heavy = kit == "bulwark", scout = kit == "kestrel", phase = kit == "wraith";
            float breadth = (heavy ? .51f : scout ? .32f : phase ? .35f : .40f) * (male ? 1.10f : .96f);
            float limbBuild = male ? 1.08f : .96f;
            Material shell = heavy ? world.Hardware : phase ? world.Graphite : world.White;
            var head = Bone("Head");
            var neck = Bone("Neck");
            var spine = Bone("Spine");
            var hips = Bone("Hips");
            if (head == null || spine == null || hips == null) return;

            var body = Anchor("Chest and spine", spine, Joint(spine) + new Vector3(0,.27f,.005f), Quaternion.identity);
            world.Armor(body, world.Dark, new Vector3(0,-.045f,0), new Vector3(breadth*.92f,.45f,.23f));
            world.Armor(body, shell, new Vector3(0,.085f,.087f), new Vector3(breadth,.25f,.19f));
            world.Armor(body, world.Graphite, new Vector3(0,-.14f,.067f), new Vector3(breadth*.76f,.19f,.14f));
            world.Armor(body, world.Dark, new Vector3(0,-.247f,.025f),new Vector3(breadth*.60f,.135f,.16f));
            var pack = world.Armor(body, world.Graphite, new Vector3(0,.045f,-.163f),
                new Vector3(breadth*.78f,heavy ? .44f : .37f,.18f));
            world.Armor(body, shell, new Vector3(0,.05f,-.237f), new Vector3(breadth*.67f,heavy ? .36f : .30f,.065f));
            var belt = Anchor("Belt and hip protection", hips, Joint(hips), Quaternion.identity);
            world.Armor(belt, world.Graphite, Vector3.zero, new Vector3(breadth*(male ? .74f : .91f),.15f,male ? .225f : .26f));
            if (neck != null)
                world.Armor(body, world.Dark, Joint(neck) - root.InverseTransformPoint(body.position), new Vector3(male ? .147f : .123f,.15f,.155f));
            var helmet = Anchor("Sealed combat helmet", head, Joint(head) + new Vector3(0,male ? .219f : .184f,-.052f), Quaternion.identity);
            float helmetWidth = (heavy ? .249f : phase ? .194f : scout ? .208f : .225f) * (male ? 1.08f : .96f);
            var shellAt = new Vector3(0,0,scout ? .014f : 0);
            var headPlate = world.Armor(helmet, shell, shellAt,
                new Vector3(helmetWidth,male ? .251f : .226f,scout ? .271f : .228f));
            headPlate.localRotation = Quaternion.Euler(scout ? 19 : phase ? -6 : -2,0,phase ? 3 : 0);
            world.Armor(helmet, world.Graphite, new Vector3(0,-.139f,-.005f), new Vector3(heavy ? .178f : .119f,.13f,.154f));
            if (heavy)
            {
                world.Armor(helmet, world.Hardware, new Vector3(0,.082f,.063f), new Vector3(.291f,.084f,.189f));
                world.Armor(helmet, world.Graphite, new Vector3(0,-.070f,.082f), new Vector3(.249f,.084f,.163f));
                world.Armor(helmet, world.Glass, new Vector3(0,.013f,.129f), new Vector3(.174f,.064f,.052f));
                for (int i = -1; i <= 1; i++)
                    world.Armor(helmet, accent, new Vector3(i*.047f,.012f,.156f), new Vector3(.030f,.012f,.008f), false);
            }
            else if (scout)
            {
                world.Armor(helmet, shell, new Vector3(0,-.045f,.142f), new Vector3(.149f,.104f,.145f)).localRotation = Quaternion.Euler(22,0,0);
                for (int side = -1; side <= 1; side += 2)
                {
                    var eye = TriasWorld.Group(helmet,"Swept flight visor");
                    eye.SetLocalPositionAndRotation(new Vector3(side*.065f,.042f,.142f),Quaternion.Euler(9,side*33,-side*21));
                    world.Armor(eye, world.Glass, Vector3.zero, new Vector3(.127f,.060f,.061f));
                    world.Armor(eye, accent, new Vector3(0,.011f,.034f), new Vector3(.091f,.012f,.006f), false);
                }
            }
            else if (phase)
            {
                world.Armor(helmet, world.Graphite, new Vector3(0,-.023f,.108f), new Vector3(.15f,.163f,.055f));
                world.Armor(helmet, accent, new Vector3(.049f,.018f,.145f), new Vector3(.018f,.105f,.009f), false);
                world.Armor(helmet, world.Hardware, new Vector3(-.14f,.011f,.028f), new Vector3(.087f,.168f,.254f)).localRotation = Quaternion.Euler(-11,0,14);
            }
            else
            {
                world.Armor(helmet, world.Graphite, new Vector3(0,-.058f,.090f), new Vector3(helmetWidth*(male ? .92f : .75f),.081f,.118f));
                world.Armor(helmet, world.Glass, new Vector3(0,.038f,.112f), new Vector3(helmetWidth*.94f,.077f,.061f)).localRotation = Quaternion.Euler(0,0,-8);
                world.Armor(helmet, accent, new Vector3(.025f,.040f,.145f), new Vector3(helmetWidth*.63f,.011f,.006f), false).localRotation = Quaternion.Euler(0,0,-8);
            }

            for (int sign = -1; sign <= 1; sign += 2)
            {
                string side = sign == -1 ? "Left" : "Right";
                var shoulder = Bone(side + "Arm");
                var elbow = Bone(side + "ForeArm");
                var wrist = Bone(side + "Hand");
                var thigh = Bone(side + "UpLeg");
                var knee = Bone(side + "Leg");
                var ankle = Bone(side + "Foot");
                if (shoulder != null && elbow != null && wrist != null)
                {
                    Limb(shoulder, elbow, shell, heavy ? .161f : .122f);
                    Limb(elbow, wrist, world.Graphite, heavy ? .138f : .108f);
                    var hand = Anchor("Armored glove", wrist, Joint(wrist), Quaternion.identity);
                    world.Armor(hand, world.Dark, new Vector3(0,-.008f,.025f), new Vector3(.092f,.081f,.136f));
                    world.Armor(hand, shell, new Vector3(0,.027f,.026f), new Vector3(.071f,.031f,.082f));
                    var pauldron = Anchor("Articulated pauldron", shoulder,
                        Vector3.Lerp(Joint(shoulder),Joint(elbow),.10f) + new Vector3(sign*.034f,.020f,-.010f), Quaternion.identity);
                    world.Armor(pauldron, shell, Vector3.zero,
                        new Vector3(heavy ? .23f : scout ? .135f : .17f,heavy ? .18f : .13f,heavy ? .28f : .19f));
                    world.Armor(pauldron, accent, new Vector3(0,.01f,.104f),
                        new Vector3(heavy ? .107f : .076f,.027f,.022f), false);
                }
                if (thigh != null && knee != null && ankle != null)
                {
                    Limb(thigh, knee, world.Graphite, heavy ? .174f : .145f);
                    Limb(knee, ankle, shell, heavy ? .16f : .124f);
                    var boot = Anchor("Ceramic boot", ankle, Joint(ankle) + new Vector3(0,-.014f,.053f), Quaternion.identity);
                    world.Armor(boot, world.Dark, Vector3.zero, new Vector3(.142f,.104f,.241f));
                }
                if (heavy)
                {
                    world.Armor(body, world.Graphite, new Vector3(sign*.235f,.10f,.045f), new Vector3(.176f,.23f,.27f));
                    world.Armor(body, world.Hardware, new Vector3(sign*.248f,.14f,.151f), new Vector3(.14f,.118f,.091f));
                }
                if (heavy || scout)
                {
                    var module = world.Armor(body, heavy ? world.Hardware : world.White,
                        new Vector3(sign * breadth * .39f, .062f,-.193f),
                        new Vector3(heavy ? .16f : .091f, scout ? .47f : .36f, .153f));
                    module.localRotation = Quaternion.Euler(scout ? -14 : 0,0,scout ? -sign * 24 : -sign * 6);
                    world.Armor(body, accent, new Vector3(sign*breadth*.39f,.007f,-.273f),
                        new Vector3(.054f,.13f,.018f), false);
                }
            }

            if (phase)
            {
                var antenna = world.Armor(body, world.Hardware, new Vector3(-.143f,.31f,-.16f),new Vector3(.083f,.38f,.085f));
                antenna.localRotation = Quaternion.Euler(-14,0,16);
            }
            else if (!heavy && !scout)
            {
                world.Armor(body, world.Hardware, new Vector3(-.202f,-.031f,-.177f),new Vector3(.10f,.29f,.182f));
                world.Armor(body, world.Graphite, new Vector3(.18f,.051f,.08f),new Vector3(.123f,.20f,.17f));
            }
            if (faction >= 0)
            {
                var backing = world.FactionPaint[faction];
                world.Armor(body, backing, new Vector3(-breadth*.19f,.126f,.173f), new Vector3(.156f,.094f,.037f));
                world.Armor(body, backing, new Vector3(0,.077f,-.265f), new Vector3(.172f,.154f,.038f));
                world.Stamp(body, faction, accent, new Vector3(-breadth*.19f,.126f,.194f), .077f);
                world.Stamp(body, faction, accent, new Vector3(0,.082f,-.286f), .105f).localRotation = Quaternion.Euler(0,180,0);
            }
            else
                world.Armor(pack, accent, new Vector3(0,0,-.57f), new Vector3(.54f,.17f,.07f), false);

            void Limb(Transform start, Transform end, Material metal, float width)
            {
                Vector3 a = Joint(start), b = Joint(end), axis = b - a;
                var limb = Anchor("Plated limb", start, (a + b)*.5f, Quaternion.FromToRotation(Vector3.up,axis));
                world.Armor(limb, metal, Vector3.zero, new Vector3(width*limbBuild,axis.magnitude+.057f,width*.96f*limbBuild));
            }
        }
    }
}
