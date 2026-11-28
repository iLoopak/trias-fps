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
        readonly Transform[] vanes;
        // Midpoints of the three authored arcs in chamber-local XY.
        static readonly Vector3[] RiftVaneRadii = {
            new(.485f,.875f,0), new(-.966f,-.259f,0), new(.643f,-.766f,0)
        };

        // Per-instrument handling temperament, so the five weapons do not share one motion profile.
        // RaiseTime: equip raise to rest; kept within .18-.26 s because the server allows fire .24 s after a
        // switch, so weight is expressed through the settle rather than a slow lift. Settle: damped pitch oscillation once at rest (degrees, rad/s, 1/s).
        // Sway: follow rate and amplitude of look sway. LandingWeight: gun dip on landing. Bob: stride scale.
        internal readonly struct Temper
        {
            internal readonly float RaiseTime, SettleDegrees, SettleRate, SettleDecay, SwayRate, SwayScale, LandingWeight, Bob;
            internal Temper(float raise, float settle, float rate, float decay, float swayRate, float swayScale, float landing, float bob)
            {
                RaiseTime = raise; SettleDegrees = settle; SettleRate = rate; SettleDecay = decay;
                SwayRate = swayRate; SwayScale = swayScale; LandingWeight = landing; Bob = bob;
            }
        }

        internal static Temper Temperament(string key) => key switch
        {
            // Heavy breaching shotgun: a weighty nod at rest, lags the view, sinks hardest on landing.
            "scatter" => new Temper(.25f, 3.4f, 11f, 6.0f, 11f, 1.15f, 1.35f, 1.2f),
            // Precision rifle: almost no settle, steady in sway and stride.
            "lance" => new Temper(.24f, 1.1f, 15f, 11f, 13f, .80f, .90f, .75f),
            // Volatile arc caster: snappy lift with a quick nervous buzz, reacts fast to the view.
            "arc" => new Temper(.18f, 2.6f, 24f, 9.0f, 21f, 1.10f, .90f, 1.05f),
            // Phase-stabilised rift: a controlled float into place with a long, low settle.
            "rift" => new Temper(.26f, 1.7f, 7f, 4.0f, 9f, .90f, 1.15f, .85f),
            // Service carbine: the reference.
            _ => new Temper(.20f, 2.0f, 18f, 9.0f, 17f, 1.00f, 1.00f, 1.00f),
        };

        internal static Vector3 SettleRotation(string key, float since)
        {
            if (since < 0) return Vector3.zero;
            var t = Temperament(key);
            float wave = Mathf.Exp(-t.SettleDecay * since) * Mathf.Sin(t.SettleRate * since);
            return new Vector3(t.SettleDegrees * wave, 0, -t.SettleDegrees * .35f * wave);
        }

        // Everything that moves the first-person grip, gathered so the client and the art review compose it
        // with the same code.
        internal struct View
        {
            internal Pose Reload;
            internal (Vector3 Offset, Vector3 Rotation) Presentation;
            internal Vector3 Settle;
            internal float SwayX, SwayY, InertiaX, Inertia, Stride, StrideSide, Landing, Travel, Lift, Twist, Retract, Idle, Motion;
        }

        internal static void Grip(in View v, out Vector3 position, out Quaternion rotation)
        {
            position = new Vector3(.365f + (v.SwayX * .0037f - v.InertiaX * .011f * v.Inertia) * v.Motion + v.StrideSide - v.Reload.Reach * .17f,
                -.305f + v.Reload.Reach * .137f - v.Reload.Dip - v.Landing * .35f + v.Stride + v.Idle,
                .60f - v.Travel - v.Retract + v.Reload.Reach * .082f) + v.Presentation.Offset;
            // A landing pitches the muzzle down after the body, so the weapon reads as carried mass.
            rotation = Quaternion.Euler(v.Reload.GunRotation + new Vector3(-v.Lift + v.SwayY * v.Motion + v.Landing * 24 * v.Motion,
                -4 + v.SwayX * v.Motion, v.Twist + v.InertiaX * v.Motion * 2 * v.Inertia) + v.Presentation.Rotation + v.Settle * v.Motion);
        }

        internal static float ServiceBeat(string key) => key switch
        {
            "scatter" => .63f, "lance" => .55f, "arc" => .66f, "rift" => .71f, _ => .59f,
        };

        static float Beat(float t, float begin, float opened, float closing, float end)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(begin, opened, t))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(closing, end, t)));

        internal static Pose At(string key, float t)
        {
            float hold = Beat(t, .08f, .29f, .76f, .97f);
            float open = Beat(t, .15f, .34f, .70f, .91f);
            float exchange = Beat(t, .32f, .47f, .55f, .74f);
            return key switch
            {
                "scatter" => new Pose(new Vector3(15,-4,18)*hold,.064f*hold,
                    new Vector3(0,.019f,.015f)*open,new Vector3(36,0,-12)*open,
                    new Vector3(-.14f,.028f,-.062f)*exchange,hold),
                "lance" => new Pose(new Vector3(7,-12,7)*hold,.025f*hold,
                    new Vector3(0,.013f,-.159f)*Beat(t,.06f,.32f,.62f,.91f),Vector3.zero,
                    new Vector3(-.037f,.031f,-.147f)*exchange,hold),
                "arc" => new Pose(new Vector3(8,-5,-25)*hold,.040f*hold,
                    new Vector3(-.116f,.006f,0)*open,new Vector3(0,-12,-25)*open,
                    new Vector3(-.157f,-.022f,.012f)*exchange,hold),
                "rift" => new Pose(new Vector3(10,9,23)*hold,.044f*hold,
                    new Vector3(-.053f,.037f,-.022f)*open,new Vector3(-5,12,66)*open,
                    new Vector3(-.139f,.105f,.006f)*exchange,hold),
                _ => new Pose(new Vector3(13,-7,-21)*hold,.040f*hold,
                    new Vector3(0,.012f,-.036f)*Beat(t,.60f,.70f,.75f,.91f),Vector3.zero,
                    new Vector3(-.055f,-.176f,-.01f)*exchange,hold),
            };
        }

        internal static (Vector3 Offset, Vector3 Rotation) Presentation(string key, float now, float equip,
            float speed, float movement, float landing, float motion)
        {
            float amount = Mathf.Clamp01(speed/9);
            float drop = equip*equip;
            return key switch
            {
                "scatter" => (new Vector3(-.032f,.018f,-.047f)+new Vector3(.052f*drop,-.19f*drop-.022f*amount,-.090f*drop),
                    new Vector3(28*drop+8*amount,-4*drop,16*drop+5*landing)*motion),
                "lance" => (new Vector3(-.035f,-.010f,-.056f)+new Vector3(.096f*drop,-.12f*drop,-.24f*drop),
                    new Vector3(17*drop+4*amount,-17*drop,-9*drop-3*movement)*motion),
                "arc" => (new Vector3(.011f,.027f,.009f)+new Vector3(-.137f*drop,-.10f*drop,-.076f*drop),
                    new Vector3(19*drop,-11*drop,32*drop+9*movement)*motion),
                "rift" => (new Vector3(.018f,-.018f,-.014f)+new Vector3(.034f*drop,-.145f*drop,-.035f*drop),
                    new Vector3(19*drop+3*amount,18*drop,-28*drop+5*movement+Mathf.Sin(now*2)*.75f)*motion),
                _ => (new Vector3(0,-.17f*drop,-.068f*drop),new Vector3(23*drop+4*amount,-3*drop,5*drop)*motion)
            };
        }

        internal WeaponHandling(TriasWorld world, Transform root, string key, Material accent, bool firstPerson, string kit = null, bool male = false)
        {
            this.key = key;
            chamber = root.Find(PhaseArmory.Chamber);
            if (chamber != null)
            {
                chamberAt = chamber.localPosition;
                if (key == "rift")
                    vanes = new[] { chamber.Find("Vane1"),chamber.Find("Vane2"),chamber.Find("Vane3") };
            }
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
        internal Vector3 SupportGrip(Pose pose) => Vector3.Lerp(key switch
        {
            "scatter" => new Vector3(-.117f,-.091f,.161f), "lance" => new Vector3(-.058f,-.039f,.190f),
            "arc" => new Vector3(-.071f,-.077f,.121f), "rift" => new Vector3(-.063f,-.070f,.103f),
            _ => new Vector3(-.061f,-.075f,.164f)
        },
            ServicePoint(key) + pose.PayloadTravel + new Vector3(-.039f,-.028f,0), pose.Reach);

        internal static Vector3 ActionPoint(string key) =>
            new Vector3(-.075f,key == "arc" ? -.018f : .07f,key == "lance" ? .19f : .12f);

        internal static Vector3 ServicePoint(string key) => key switch
        {
            "scatter" => new Vector3(-.093f,.040f,.11f), "lance" => new Vector3(-.108f,.091f,.16f),
            "arc" => new Vector3(-.107f,-.045f,.09f), "rift" => new Vector3(-.107f,.032f,.13f),
            _ => new Vector3(-.034f,-.115f,.080f),
        };

        internal void Update(Pose pose, float kick, float phaseCharge = 0, float motion = 1, bool alternate = false, int impulse = 0)
        {
            float sign = (impulse & 1) == 0 ? 1 : -1;
            float time = Time.unscaledTime;
            float stroke = Mathf.Clamp01(kick * 9f);
            Vector3 actionRecoil = key == "arc" ? new Vector3(sign*kick*.34f,0,-kick*.16f) :
                key == "scatter" ? new Vector3(0,kick*.18f,-kick*.49f) : Vector3.back*kick*.38f;
            action.localPosition = actionAt + pose.MechanismTravel + actionRecoil;
            action.localRotation = Quaternion.Euler(pose.MechanismRotation+Vector3.forward*(key=="arc"?sign*stroke*9f:0));
            payload.localPosition = payloadAt + pose.PayloadTravel;
            payload.localRotation = Quaternion.Euler(pose.MechanismRotation * .50f);
            if (chamber != null)
            {
                float open = pose.MechanismTravel.magnitude;
                var (shift, swivel) = key switch
                {
                    "scatter" => (new Vector3(0,-open*.78f,-kick*.25f),new Vector3(open*2100,-stroke*(alternate?1:3),0)),
                    "lance" => (new Vector3(0,0,-stroke*(alternate?.036f:.018f)+open*.14f),Vector3.zero),
                    "arc" => (new Vector3(stroke*sign*(alternate?.027f:.013f),0,-kick*.13f),new Vector3(0,0,stroke*sign*8f)),
                    "rift" => (new Vector3(Mathf.Sin(time*1.6f)*.006f*motion,.014f*Mathf.Sin(time*2.7f)*motion+open*.26f,
                        stroke*(alternate?.064f:.041f)+open*.44f),new Vector3(-stroke*9f,stroke*7f,
                        Mathf.Sin(time*1.75f)*6*motion+stroke*18+open*190)),
                    _ => (new Vector3(0,0,-stroke*(alternate?.036f:.026f)+phaseCharge*.019f),Vector3.zero)
                };
                chamber.SetLocalPositionAndRotation(chamberAt+shift,Quaternion.Euler(swivel));
                if (vanes != null)
                    for (int i=0;i<vanes.Length;i++)
                    {
                        if (vanes[i] == null) continue;
                        float drift = Mathf.Sin(time*2.35f+i*2.1f)*motion*(1-stroke);
                        // The field opens radially around its core on discharge.
                        // Its resting aperture remains visible with cosmetic motion disabled.
                        float spread = .019f + stroke*(alternate?.044f:.027f);
                        vanes[i].SetLocalPositionAndRotation(RiftVaneRadii[i]*spread +
                            new Vector3(0,drift*.004f,(i-1)*spread*.65f),Quaternion.Euler(0,0,drift*2));
                    }
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
