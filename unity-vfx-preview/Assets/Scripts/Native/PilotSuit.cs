using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    internal static class PilotSuit
    {
        internal static void Build(TriasWorld world, Transform root, Transform rig, string kit, Material accent, int faction, bool male)
        {
            var bones = rig.GetComponentsInChildren<Transform>();
            var joints = new List<Transform>(24);
            Transform Bone(string name) => Array.Find(bones, bone => bone.name == name);
            Transform Anchor(string label, Transform bone, Vector3 at, Quaternion rotation)
            {
                var joint = TriasWorld.Group(root, label, at);
                joint.localRotation = rotation;
                if (bone != null) joint.SetParent(bone, true);
                joints.Add(joint);
                return joint;
            }
            Vector3 Joint(Transform bone) => root.InverseTransformPoint(bone.position);
            bool heavy = kit == "bulwark", scout = kit == "kestrel", phase = kit == "wraith";
            float breadth = (heavy ? .51f : scout ? .32f : phase ? .35f : .40f) * (male ? 1.10f : .96f);
            float limbBuild = male ? 1.08f : .96f;
            Material shell = world.Porcelain;
            Material secondary = heavy ? world.Insulation : phase ? world.Graphite : world.Hardware;
            Material teamPaint = faction >= 0 ? world.FactionPaint[faction] : world.SafetyAmber;
            var head = Bone("Head");
            var neck = Bone("Neck");
            var spine = Bone("Spine");
            var hips = Bone("Hips");
            if (head == null || spine == null || hips == null) return;

            var body = Anchor("Chest and spine", spine, Joint(spine) + new Vector3(0,.27f,.005f), Quaternion.identity);
            world.Sculpt(body, PilotVolume.Torso, world.Dark, new Vector3(0,-.065f,.003f), new Vector3(breadth*.96f,.52f,.255f));
            for (int side = -1; side <= 1; side += 2)
            {
                float offset = phase && side == 1 ? .031f : 0;
                var section = world.Shell(body, shell, new Vector3(side*breadth*.265f,.091f+offset,.131f),
                    new Vector3(breadth*.442f,heavy ? .263f : .221f,.12f));
                section.localRotation = Quaternion.Euler(-7,side*11,-side*(scout ? 21 : 8));
            }
            world.Armor(body, teamPaint, new Vector3(0,-.124f,.144f),new Vector3(breadth*.78f,.085f,.065f));
            for (int side = -1; side <= 1; side += 2)
            {
                var muscle = world.Sculpt(body, PilotVolume.Limb, world.Graphite, new Vector3(side*breadth*.36f,-.147f,-.027f),
                    new Vector3(.093f,.236f,.139f));
                muscle.localRotation = Quaternion.Euler(0,0,side*(heavy ? 12 : 23));
                world.Armor(body, secondary, new Vector3(side*breadth*.25f,-.205f,.116f),
                    new Vector3(breadth*.29f,.081f,.052f)).localRotation = Quaternion.Euler(0,side*27,side*12);
            }
            world.Armor(body, world.Graphite, new Vector3(0,-.14f,.067f), new Vector3(breadth*.76f,.19f,.14f));
            world.Armor(body, world.Dark, new Vector3(0,-.247f,.025f),new Vector3(breadth*.60f,.135f,.16f));
            var pack = world.Armor(body, world.Graphite, new Vector3(0,.045f,-.163f),
                new Vector3(breadth*.78f,heavy ? .44f : .37f,.18f));
            world.Armor(body, secondary, new Vector3(0,.05f,-.237f), new Vector3(breadth*.67f,heavy ? .36f : .30f,.065f));
            var belt = Anchor("Belt and hip protection", hips, Joint(hips), Quaternion.identity);
            world.Armor(belt, world.Graphite, Vector3.zero, new Vector3(breadth*(male ? .74f : .91f),.15f,male ? .225f : .26f));
            if (neck != null)
                world.Armor(body, world.Dark, Joint(neck) - root.InverseTransformPoint(body.position), new Vector3(male ? .147f : .123f,.15f,.155f));
            var helmet = Anchor("Sealed combat helmet", head, Joint(head) + new Vector3(0,male ? .198f : .175f,-.043f), Quaternion.identity);
            float helmetWidth = (heavy ? .249f : phase ? .194f : scout ? .208f : .225f) * (male ? 1.08f : .96f);
            var shellAt = new Vector3(0,0,scout ? .014f : 0);
            var headPlate = world.Sculpt(helmet, PilotVolume.Helmet, shell, shellAt,
                new Vector3(helmetWidth,male ? .233f : .219f,scout ? .271f : .241f));
            headPlate.localRotation = Quaternion.Euler(scout ? 19 : phase ? -6 : -2,0,phase ? 3 : 0);
            world.Sculpt(helmet, PilotVolume.Limb, world.Graphite, new Vector3(0,-.132f,-.013f),
                new Vector3(heavy ? .163f : .119f,.136f,.147f));
            for (int side = -1; side <= 1; side += 2)
            {
                var cheek = world.Sculpt(helmet,PilotVolume.Limb,world.Graphite,new Vector3(side*helmetWidth*.43f,-.047f,.045f),
                    new Vector3(.065f,.125f,.151f));
                cheek.localRotation = Quaternion.Euler(-14,side*28,-side*24);
                world.Armor(cheek,secondary,new Vector3(side*.47f,.13f,.043f),new Vector3(.30f,.52f,.67f));
                var sensor = world.Sculpt(helmet,PilotVolume.Limb,world.Hardware,new Vector3(helmetWidth*.49f*side,.027f,-.033f),
                    new Vector3(.056f,.092f,.085f));
                sensor.localRotation = Quaternion.Euler(0,0,side*72);
                world.Armor(helmet, teamPaint, new Vector3(helmetWidth*.51f*side,.076f,-.034f),
                    new Vector3(.014f,.067f,.065f),false);
            }
            if (heavy)
            {
                world.Armor(helmet, world.Hardware, new Vector3(0,.082f,.063f), new Vector3(.291f,.084f,.189f));
                world.Armor(helmet, world.Graphite, new Vector3(0,-.070f,.082f), new Vector3(.249f,.084f,.163f));
                world.Armor(helmet, world.PilotVisor, new Vector3(0,.013f,.129f), new Vector3(.174f,.064f,.052f));
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
                    world.Armor(eye, world.PilotVisor, Vector3.zero, new Vector3(.127f,.060f,.061f));
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
                world.Sculpt(helmet, PilotVolume.Helmet, world.Graphite, new Vector3(0,-.060f,.094f),
                    new Vector3(helmetWidth*(male ? .91f : .83f),.110f,.132f));
                world.Sculpt(helmet, PilotVolume.Helmet, world.PilotVisor, new Vector3(0,.032f,.114f), new Vector3(helmetWidth,.079f,.064f)).localRotation = Quaternion.Euler(0,0,-8);
                world.Armor(helmet, accent, new Vector3(.025f,.040f,.145f), new Vector3(helmetWidth*.63f,.011f,.006f), false).localRotation = Quaternion.Euler(0,0,-8);
            }
            if (faction == 1)
                world.Armor(helmet, teamPaint, new Vector3(0,.092f,-.025f),new Vector3(helmetWidth*1.23f,.102f,.24f));

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
                    Limb(shoulder, elbow, world.Dark, heavy ? .161f : .122f);
                    Limb(elbow, wrist, world.Graphite, heavy ? .138f : .108f);
                    if (world.Spec.id == "relay" && kit == "vector")
                    {
                        Vector3 axis = Joint(elbow) - Joint(wrist);
                        var forearm = Anchor("Vektor · layered forearm",elbow,
                            Vector3.Lerp(Joint(elbow),Joint(wrist),.46f) + new Vector3(sign*.034f,.009f,.022f),
                            Quaternion.FromToRotation(Vector3.up,axis));
                        world.Shell(forearm,world.Porcelain,Vector3.zero,new Vector3(.076f,axis.magnitude*.67f,.047f));
                        world.Armor(forearm,world.Hardware,new Vector3(0,-.043f,.018f),new Vector3(.065f,.037f,.022f),false);
                    }
                    var hand = Anchor("Armored glove", wrist, Joint(wrist), Quaternion.identity);
                    world.Sculpt(hand, PilotVolume.Glove, world.Dark, new Vector3(0,-.008f,.025f), new Vector3(.091f,.074f,.135f));
                    world.Armor(hand, shell, new Vector3(0,.027f,.026f), new Vector3(.071f,.029f,.077f));
                    world.Armor(hand, secondary,new Vector3(0,.020f,.079f),new Vector3(.074f,.015f,.024f));
                    var cuff = Anchor("Stacked wrist cuff", elbow,Vector3.Lerp(Joint(elbow),Joint(wrist),.77f),
                        Quaternion.FromToRotation(Vector3.up,Joint(elbow)-Joint(wrist)));
                    world.Sculpt(cuff,PilotVolume.Limb,world.Graphite,Vector3.zero,new Vector3(.113f,.103f,.106f));
                    world.Shell(cuff,side == "Right" ? teamPaint : secondary,
                        new Vector3(sign*.025f,.010f,.042f),new Vector3(.094f,.073f,.044f));
                    var pauldron = Anchor("Articulated pauldron", shoulder,
                        Vector3.Lerp(Joint(shoulder),Joint(elbow),.10f) + new Vector3(sign*(heavy ? .106f : .081f),.048f,-.019f), Quaternion.identity);
                    var plate = world.Armor(pauldron, teamPaint, Vector3.zero,
                        new Vector3((heavy ? .23f : scout ? .135f : .17f) + (faction == 1 ? .044f : 0),
                            (heavy ? .18f : .13f) + (faction == 2 ? .055f : 0),heavy ? .28f : .19f));
                    plate.localRotation = Quaternion.Euler(scout ? -23 : -9,0, faction == 2 ? sign*27 : -sign*17);
                    world.Shell(plate, shell, new Vector3(0,.36f,-.04f),new Vector3(.82f,.42f,.85f));
                    world.Shell(plate, secondary,new Vector3(sign*.31f,-.36f,.032f),new Vector3(.56f,.29f,.78f));
                    world.Armor(plate,world.Dark,new Vector3(-sign*.38f,.22f,-.12f),new Vector3(.22f,.33f,.49f), false);
                    world.Armor(pauldron, accent, new Vector3(0,.01f,.104f),
                        new Vector3(heavy ? .107f : .076f,.027f,.022f), false);
                }
                if (thigh != null && knee != null && ankle != null)
                {
                    Limb(thigh, knee, world.Dark, heavy ? .174f : .145f);
                    var thighGuard = Anchor("Femoral plate",thigh,Vector3.Lerp(Joint(thigh),Joint(knee),.39f)
                        + new Vector3(sign*.038f,.012f,.070f),Quaternion.FromToRotation(Vector3.up,Joint(thigh)-Joint(knee)));
                    world.Shell(thighGuard,secondary,Vector3.zero,new Vector3(heavy ? .164f : .128f,.247f,.085f));
                    world.Armor(thighGuard,teamPaint,new Vector3(sign*.038f,.069f,.043f),new Vector3(.047f,.068f,.022f),false);
                    var joint = Anchor("Knee shock mount",knee,Joint(knee) + new Vector3(0,-.016f,.040f),Quaternion.identity);
                    world.Sculpt(joint,PilotVolume.Helmet,world.Graphite,Vector3.zero,new Vector3(.126f,.107f,.121f));
                    world.Shell(joint,heavy ? shell : secondary,new Vector3(0,.008f,.042f),new Vector3(.09f,.073f,.069f));
                    Limb(knee, ankle, world.Dark, heavy ? .142f : .112f);
                    var shin = Anchor("Suspended shin guard",knee,Vector3.Lerp(Joint(knee),Joint(ankle),.49f) + new Vector3(sign*.016f,0,.055f),
                        Quaternion.FromToRotation(Vector3.up,Joint(knee)-Joint(ankle)));
                    world.Shell(shin,shell,Vector3.zero,new Vector3(heavy ? .177f : .139f,.267f,.097f));
                    world.Shell(shin,teamPaint,new Vector3(sign*.026f,.054f,.055f),new Vector3(.040f,.075f,.024f));
                    var boot = Anchor("Ceramic boot", ankle, Joint(ankle) + new Vector3(0,-.014f,.053f), Quaternion.identity);
                    world.Sculpt(boot,PilotVolume.Boot,world.Dark,Vector3.zero,new Vector3(heavy ? .167f : .138f,.146f,.264f));
                    world.Shell(boot,shell,new Vector3(0,.026f,.073f),new Vector3(.132f,.069f,.135f));
                    world.Armor(boot,secondary,new Vector3(0,-.027f,-.073f),new Vector3(.114f,.074f,.10f));
                    world.Armor(boot,teamPaint,new Vector3(0,-.005f,-.123f),new Vector3(.082f,.025f,.018f),false);
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
                if (faction == 0)
                {
                    var vane = world.Armor(body, teamPaint, new Vector3(sign * breadth * .43f,.14f,-.219f),new Vector3(.074f,.31f,.128f));
                    vane.localRotation = Quaternion.Euler(17,0,-sign * 24);
                }
                else if (faction == 1)
                    world.Armor(body, teamPaint, new Vector3(sign * breadth * .41f,.058f,-.208f),new Vector3(.103f,.22f,.161f));
            }

            if (heavy)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var strut = world.Sculpt(body,PilotVolume.Limb,world.Hardware,new Vector3(side*.22f,-.069f,-.143f),
                        new Vector3(.093f,.305f,.091f));
                    strut.localRotation = Quaternion.Euler(12,0,side*19);
                    world.Armor(strut,world.Graphite,new Vector3(0,.29f,0),new Vector3(1f,.30f,.98f));
                }
            }
            else if (scout)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var stabilizer = world.Shell(body, shell, new Vector3(side*breadth*.53f,.195f,-.26f),
                        new Vector3(.076f,.356f,.141f));
                    stabilizer.localRotation = Quaternion.Euler(-33,0,side*29);
                    world.Shell(stabilizer,secondary,new Vector3(0,-.31f,-.45f),new Vector3(.77f,.41f,.59f));
                }
            }
            if (phase)
            {
                var antenna = world.Armor(body, world.Hardware, new Vector3(-.143f,.31f,-.16f),new Vector3(.083f,.38f,.085f));
                antenna.localRotation = Quaternion.Euler(-14,0,16);
                world.Armor(antenna,teamPaint,new Vector3(0,.28f,.42f),new Vector3(.43f,.27f,.26f),false);
                world.Shell(body, shell,new Vector3(.17f,-.089f,-.215f),new Vector3(.085f,.239f,.113f))
                    .localRotation = Quaternion.Euler(-14,0,-18);
            }
            else if (!heavy && !scout)
            {
                world.Armor(body, world.Hardware, new Vector3(-.202f,-.031f,-.177f),new Vector3(.10f,.29f,.182f));
                world.Shell(body,shell,new Vector3(-.251f,-.013f,-.172f),new Vector3(.027f,.206f,.129f));
                world.Armor(body,teamPaint,new Vector3(-.258f,.02f,-.176f),new Vector3(.022f,.036f,.105f),false);
                world.Armor(body, world.Graphite, new Vector3(.18f,.051f,.08f),new Vector3(.123f,.20f,.17f));
            }
            if (faction >= 0)
            {
                if (faction == 2)
                    world.Armor(body, teamPaint, new Vector3(0,.209f,-.237f),new Vector3(.104f,.43f,.131f)).localRotation = Quaternion.Euler(-19,0,11);
                world.Armor(body, world.Graphite, new Vector3(-breadth*.19f,.125f,.183f), new Vector3(.177f,.130f,.038f));
                world.Armor(body, world.Graphite, new Vector3(0,.060f,-.305f), new Vector3(.194f,.174f,.036f));
                world.Stamp(body, faction, accent, new Vector3(-breadth*.19f,.125f,.205f), .109f);
                world.Stamp(body, faction, accent, new Vector3(0,.060f,-.326f), .137f).localRotation = Quaternion.Euler(0,180,0);
            }
            else
                world.Armor(pack, accent, new Vector3(0,0,-.57f), new Vector3(.54f,.17f,.07f), false);

            if (world.Spec.id == "relay" && kit == "vector")
            {
                world.Sculpt(body,PilotVolume.Limb,world.Graphite,new Vector3(0,.189f,.062f),new Vector3(breadth*.73f,.115f,.158f));
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var rail = world.Shell(body,world.Hardware,new Vector3(sign*breadth*.28f,.190f,.121f),
                        new Vector3(.042f,.11f,.058f));
                    rail.localRotation = Quaternion.Euler(8,sign*26,-sign*18);
                    var lower = world.Shell(body,world.Porcelain,new Vector3(sign*breadth*.29f,-.215f,.126f),
                        new Vector3(.072f,.095f,.052f));
                    lower.localRotation = Quaternion.Euler(-9,sign*19,sign*17);
                }
            }

            for (int i = 0; i < joints.Count; i++)
                world.BatchPilotJoint(joints[i], kit, male, faction, i);

            void Limb(Transform start, Transform end, Material metal, float width)
            {
                Vector3 a = Joint(start), b = Joint(end), axis = b - a;
                var limb = Anchor("Plated limb", start, (a + b)*.5f, Quaternion.FromToRotation(Vector3.up,axis));
                world.Sculpt(limb, PilotVolume.Limb,metal,Vector3.zero,
                    new Vector3(width*limbBuild,axis.magnitude*.97f,width*.96f*limbBuild));
            }
        }
    }
}
