using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed class WeaponHandling
    {
        internal readonly struct Pose
        {
            internal readonly Vector3 GunRotation, MechanismTravel, MechanismRotation, PayloadTravel;
            internal readonly float Dip, Reach;

            internal Pose(Vector3 gunRotation, float dip, Vector3 mechanismTravel, Vector3 mechanismRotation, Vector3 payloadTravel, float reach)
            {
                GunRotation = gunRotation; Dip = dip; MechanismTravel = mechanismTravel;
                MechanismRotation = mechanismRotation; PayloadTravel = payloadTravel; Reach = reach;
            }
        }

        readonly Transform chamber, action, payload, leftHand, rightHand, leftSleeve, rightSleeve;
        readonly Vector3 chamberAt, actionAt, payloadAt;
        readonly string key;

        internal static float ServiceBeat(string key) => key switch
        {
            "scatter" => .63f, "lance" => .55f, "arc" => .66f, "rift" => .71f, _ => .59f,
        };

        static float Beat(float t, float begin, float opened, float closing, float end)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(begin, opened, t))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(closing, end, t)));

        internal static Pose At(string key, float t)
        {
            float reach = Beat(t, .11f, .29f, .76f, .94f);
            float open = Beat(t, .16f, .34f, .66f, .86f);
            float exchange = Beat(t, .31f, .46f, .51f, .70f);
            return key switch
            {
                "scatter" => new Pose(new Vector3(24, -4, 24) * reach, .078f * reach,
                    new Vector3(0, -.015f, .036f) * open, new Vector3(0, 0, -72) * open,
                    new Vector3(-.12f, -.06f, 0) * exchange, reach),
                "lance" => new Pose(new Vector3(12, -9, 15) * reach, .044f * reach,
                    new Vector3(0, .026f, -.127f) * open, new Vector3(-14, 0, 0) * open,
                    new Vector3(-.09f, .05f, -.04f) * exchange, reach),
                "arc" => new Pose(new Vector3(19, -10, -27) * reach, .065f * reach,
                    new Vector3(-.088f, .015f, .016f) * open, new Vector3(0, -17, 0) * open,
                    new Vector3(-.077f, -.075f, .01f) * exchange, reach),
                "rift" => new Pose(new Vector3(26, 6, 32) * reach, .086f * reach,
                    new Vector3(-.038f, .05f, -.012f) * open, new Vector3(0, 12, 72) * open,
                    new Vector3(-.119f, .016f, .025f) * exchange, reach),
                _ => new Pose(new Vector3(13, -7, -21) * reach, .035f * reach,
                    new Vector3(0, .01f, -.042f) * open, Vector3.zero,
                    new Vector3(-.055f, -.176f, -.01f) * exchange, reach),
            };
        }

        internal WeaponHandling(TriasWorld world, Transform root, string key, Material accent, bool firstPerson, string kit = null, bool male = false)
        {
            this.key = key;
            chamber = root.Find(PhaseArmory.Chamber);
            if (chamber != null) chamberAt = chamber.localPosition;
            actionAt = ActionPoint(key);
            payloadAt = ServicePoint(key);
            action = root.Find(BlenderArmory.Action);
            payload = root.Find(BlenderArmory.Payload);
            if (action == null || payload == null)
            {
                action = TriasWorld.Group(root, key == "scatter" ? "Hinged breech" : key == "lance" ? "Bolt carriage"
                    : key == "arc" ? "Cell sled" : key == "rift" ? "Containment gate" : "Charging handle");
                payload = TriasWorld.Group(root, key == "pulse" ? "Magazine" : "Energy cartridge");
                Part(action, world.Steel, Vector3.zero, new Vector3(.057f,.032f,key == "lance" ? .18f : .10f));
                Part(action, accent, new Vector3(-.029f,.014f,0), new Vector3(.013f,.014f,.073f));
                if (key == "rift")
                    Part(payload, accent, Vector3.zero, new Vector3(.093f,.069f,.073f), PrimitiveType.Sphere);
                else if (key == "scatter")
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var shell = Part(payload, accent, new Vector3(0,i * .037f,0), new Vector3(.029f,.046f,.029f), PrimitiveType.Cylinder);
                        shell.localRotation = Quaternion.Euler(90, 0, 0);
                    }
                }
                else
                {
                    Part(payload, world.Dark, Vector3.zero, key == "pulse" ? new Vector3(.055f,.114f,.085f) : new Vector3(.037f,.047f,.104f));
                    Part(payload, accent, new Vector3(-.025f,-.015f,0), new Vector3(.012f,.026f,.061f));
                }
            }
            if (firstPerson)
            {
                Material gloveShell = kit == "wraith" ? world.Graphite : kit == "bulwark" ? world.Hardware : world.White;
                float gloveWidth = (kit == "bulwark" ? 1.12f : kit == "kestrel" ? .92f : 1f) * (male ? 1.05f : 1f);
                leftHand = Glove(world, root, accent, gloveShell, -1, gloveWidth, out leftSleeve);
                rightHand = Glove(world, root, accent, gloveShell, 1, gloveWidth, out rightSleeve);
            }
            Update(default, 0);
        }

        static Transform Part(Transform parent, Material material, Vector3 at, Vector3 size, PrimitiveType type = PrimitiveType.Cube)
        {
            var part = TriasWorld.Part(parent, material, at, size, type);
            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return part;
        }

        static Transform Glove(TriasWorld world, Transform root, Material paint, Material shell, int side, float width, out Transform sleeve)
        {
            var hand = TriasWorld.Group(root, side < 0 ? "Support glove" : "Grip glove");
            var palm = world.Sculpt(hand, PilotVolume.Glove, world.Dark, Vector3.zero, new Vector3(.085f,.102f,.063f));
            palm.localRotation = Quaternion.Euler(90,0,0);
            palm.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            hand.localScale = new Vector3(width,1,1);
            world.Armor(hand, shell, new Vector3(0,.027f,-.013f), new Vector3(.072f,.022f,.066f),false);
            world.Armor(hand, world.Hardware,new Vector3(0,.028f,.037f),new Vector3(.061f,.016f,.020f),false);
            Part(hand, paint, new Vector3(0,.038f,-.021f), new Vector3(.049f,.006f,.013f));
            for (int i = 0; i < 4; i++)
            {
                var finger = Part(hand, world.Dark, new Vector3((i - 1.5f)*.017f,-.011f,.034f), new Vector3(.019f,.027f,.045f), PrimitiveType.Sphere);
                finger.localRotation = Quaternion.Euler(28, 0, 0);
            }
            Part(hand, world.Dark, new Vector3(side * -.039f,-.008f,.013f), new Vector3(.031f,.033f,.060f), PrimitiveType.Sphere);
            sleeve = world.Sculpt(root,PilotVolume.Limb,world.Dark,Vector3.zero,Vector3.one);
            sleeve.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            world.Armor(sleeve, shell,new Vector3(0,.16f,.34f),new Vector3(.81f,.46f,.34f),false);
            world.Armor(sleeve, world.Graphite,new Vector3(0,-.16f,.28f),new Vector3(.79f,.24f,.36f),false);
            world.Armor(sleeve, paint,new Vector3(.21f,.14f,.54f),new Vector3(.15f,.29f,.07f),false);
            return hand;
        }

        internal static Vector3 TriggerGrip => new Vector3(.047f, -.094f, -.065f);
        internal Vector3 SupportGrip(Pose pose) => Vector3.Lerp(new Vector3(-.061f,-.075f,.164f),
            ServicePoint(key) + pose.PayloadTravel + new Vector3(-.039f,-.028f,0), pose.Reach);

        internal static Vector3 ActionPoint(string key) =>
            new Vector3(-.075f,key == "arc" ? -.018f : .07f,key == "lance" ? .19f : .12f);

        internal static Vector3 ServicePoint(string key) => key switch
        {
            "scatter" => new Vector3(-.093f,.040f,.11f), "lance" => new Vector3(-.108f,.091f,.16f),
            "arc" => new Vector3(-.107f,-.045f,.09f), "rift" => new Vector3(-.107f,.032f,.13f),
            _ => new Vector3(-.034f,-.115f,.080f),
        };

        internal void Update(Pose pose, float kick, float phaseCharge = 0)
        {
            action.localPosition = actionAt + pose.MechanismTravel - Vector3.forward * (kick * .38f);
            action.localRotation = Quaternion.Euler(pose.MechanismRotation);
            payload.localPosition = payloadAt + pose.PayloadTravel;
            payload.localRotation = Quaternion.Euler(pose.MechanismRotation * .50f);
            if (chamber != null)
            {
                chamber.localPosition = chamberAt + new Vector3(0, pose.Reach * .019f, pose.Reach * .069f - Mathf.Clamp(kick, 0, .045f) * .46f + phaseCharge*.019f);
                chamber.localRotation = Quaternion.Euler(-pose.Reach * 6f,0,pose.MechanismRotation.z*.12f);
            }
            if (leftHand == null) return;
            Vector3 support = SupportGrip(pose);
            leftHand.SetLocalPositionAndRotation(support, Quaternion.Euler(-22 + pose.Reach * 31, 14, 30 - pose.Reach * 42));
            rightHand.SetLocalPositionAndRotation(TriggerGrip, Quaternion.Euler(19, -17, -34));
            Sleeve(leftSleeve, support, new Vector3(-.30f,-.30f,-.26f));
            Sleeve(rightSleeve, TriggerGrip, new Vector3(.20f,-.27f,-.36f));
        }

        static void Sleeve(Transform sleeve, Vector3 wrist, Vector3 elbow)
        {
            Vector3 arm = wrist - elbow;
            sleeve.SetLocalPositionAndRotation((wrist + elbow) * .5f,
                Quaternion.FromToRotation(Vector3.up, arm));
            sleeve.localScale = new Vector3(.107f, arm.magnitude, .098f);
        }
    }

    internal sealed class PilotArm
    {
        readonly Transform upper, forearm, hand;
        Quaternion upperPose, forearmPose;
        bool posed;

        internal PilotArm(Transform upper, Transform forearm, Transform hand)
        {
            this.upper = upper; this.forearm = forearm; this.hand = hand;
        }

        internal void Restore()
        {
            if (!posed) return;
            upper.localRotation = upperPose;
            forearm.localRotation = forearmPose;
            posed = false;
        }

        internal void Reach(Vector3 target, Vector3 pole)
        {
            if (upper == null || forearm == null || hand == null) return;
            // The rig's parent scales the body during crouch and varies by pilot. Solve
            // in that parent's coordinates so rotating a bone preserves its link lengths.
            Transform frame = upper.parent;
            if (frame == null) return;
            Vector3 shoulder = upper.localPosition;
            Vector3 originalElbow = frame.InverseTransformPoint(forearm.position);
            Vector3 originalWrist = frame.InverseTransformPoint(hand.position);
            float a = Vector3.Distance(shoulder,originalElbow);
            float b = Vector3.Distance(originalElbow,originalWrist);
            if (a < .00001f || b < .00001f) return;
            Vector3 path = frame.InverseTransformPoint(target) - shoulder;
            if (path.sqrMagnitude < 1e-10f) return;
            Vector3 forward = path.normalized;
            float jointMargin = .004f*Mathf.Min(a,b);
            float distance = Mathf.Clamp(path.magnitude,Mathf.Abs(a-b)+jointMargin,a+b-jointMargin);
            float along = (a*a+distance*distance-b*b)/(2*distance);
            Vector3 bend = Vector3.ProjectOnPlane(frame.InverseTransformPoint(pole)-shoulder,forward).normalized;
            if (bend.sqrMagnitude < .001f) return;
            Vector3 elbow = shoulder+forward*along + bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 wrist = frame.TransformPoint(shoulder+forward*distance);
            upperPose = upper.localRotation;
            forearmPose = forearm.localRotation;
            posed = true;
            upper.localRotation = Quaternion.FromToRotation(originalElbow-shoulder,elbow-shoulder)*upperPose;
            Transform elbowFrame = forearm.parent;
            Vector3 current = elbowFrame.InverseTransformPoint(hand.position)-forearm.localPosition;
            Vector3 desired = elbowFrame.InverseTransformPoint(wrist)-forearm.localPosition;
            forearm.localRotation = Quaternion.FromToRotation(current,desired)*forearmPose;
        }
    }
}
