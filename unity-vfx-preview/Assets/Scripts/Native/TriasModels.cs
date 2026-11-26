using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    internal static class TriasModels
    {
        internal static void Gun(TriasWorld w, Transform root, string weapon, Material accent)
        {
            if (w.Kit.Gun(root, weapon, accent)) return;
            bool scatter = weapon == "scatter", lance = weapon == "lance", rift = weapon == "rift", arc = weapon == "arc";
            float width = scatter || rift ? 0.24f : 0.19f;
            float length = lance ? 1.02f : arc ? 0.59f : rift ? 0.83f : 0.74f;
            TriasWorld.Part(root, w.Dark, new Vector3(0, 0, length * 0.17f), new Vector3(width, 0.16f, length * 0.72f));
            TriasWorld.Part(root, w.Steel, new Vector3(0, .077f, .09f), new Vector3(width * .81f, .046f, .28f));
            TriasWorld.Part(root, w.Dark, new Vector3(0, .109f, .105f), new Vector3(width * .44f, .024f, .35f));
            TriasWorld.Part(root, w.Dark, new Vector3(0, -0.126f, 0.07f), new Vector3(0.12f, 0.20f, 0.13f)).localRotation = Quaternion.Euler(-11, 0, 0);
            if (!scatter && !rift)
            {
                var barrel = TriasWorld.Part(root, w.Dark, new Vector3(0, .024f, length * .59f), new Vector3(.081f, length * .13f, .081f), PrimitiveType.Cylinder);
                barrel.localRotation = Quaternion.Euler(90, 0, 0);
                TriasWorld.Part(root, accent, new Vector3(0, .025f, length * .66f), new Vector3(.062f,.055f,.018f));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                TriasWorld.Part(root, w.Steel, new Vector3(side * width * 0.44f, 0.025f, length * 0.40f), new Vector3(.041f, .105f, length * .43f));
                TriasWorld.Part(root, accent, new Vector3(side * width * 0.52f, 0.042f, length * 0.33f), new Vector3(.018f, .025f, length * .35f));
                for (int slot = 0; slot < 3; slot++)
                {
                    var vent = TriasWorld.Part(root, w.Dark, new Vector3(side * width * .54f, -.008f, length * (.27f + .096f * slot)), new Vector3(.016f, .034f, .022f));
                    vent.localRotation = Quaternion.Euler(-19, 0, 0);
                }
                if (scatter || rift)
                    TriasWorld.Part(root, w.Dark, new Vector3(side * .065f, .025f, length * .56f), new Vector3(.085f, .073f, .19f));
                if (arc || rift)
                    TriasWorld.Part(root, accent, new Vector3(side * width * .56f, -.045f, .12f), new Vector3(.029f, .035f, .17f));
            }
            if (lance) TriasWorld.Part(root, w.Glass, new Vector3(0, .152f, .15f),
                new Vector3(.11f, .12f, .12f), PrimitiveType.Cylinder).localRotation = Quaternion.Euler(90, 0, 0);
            else TriasWorld.Part(root, w.Dark, new Vector3(0, .14f, length * .30f), new Vector3(.058f, .049f, .083f));
        }

        internal static Transform Vehicle(TriasWorld w, bool heavy, int team, out Transform turret)
        {
            Transform root = TriasWorld.Group(w.Actors, heavy ? "Bastion" : "Skimmer");
            float width = heavy ? 2.80f : 2.00f, length = heavy ? 4.68f : 3.25f;
            TriasWorld.Part(root, w.Dark, new Vector3(0, .49f, 0), new Vector3(width, .55f, length));
            TriasWorld.Part(root, w.Team[team], new Vector3(0, .93f, -0.28f), new Vector3(width * .74f, .66f, length * .8f));
            TriasWorld.Part(root, w.Glass, new Vector3(0, 1.21f, length * .14f), new Vector3(width * .48f, .34f, length * .34f), PrimitiveType.Sphere);
            for (int side = -1; side <= 1; side += 2)
            {
                TriasWorld.Part(root, w.TeamLight[team], new Vector3(side * width * .49f, .58f, -length * .39f), new Vector3(.17f,.16f,.35f));
                for (int i = -1; i <= 1; i++)
                {
                    var part = TriasWorld.Part(root, w.Dark, new Vector3(side * width * .47f, .31f, i * length * .36f),
                        new Vector3(heavy ? .82f : .56f, .24f, heavy ? .82f : .56f), PrimitiveType.Cylinder);
                    part.localRotation = Quaternion.Euler(0, 0, 90);
                }
            }
            turret = TriasWorld.Group(root, "Weapon housing", Vector3.up * (heavy ? 1.72f : 1.43f));
            turret.localScale = Vector3.one * (heavy ? 1.94f : 1.52f);
            Gun(w, turret, heavy ? "rift" : "arc", w.TeamLight[team]);
            return root;
        }
    }

    internal sealed class Avatar
    {
        internal readonly Transform Root;
        readonly Transform body, head, left, right, weapon;
        readonly Animation locomotion;
        readonly AnimationState runCycle;
        readonly PilotArm leftArm, rightArm;
        readonly Transform hips, spine;
        Quaternion hipsPose, spinePose;
        bool pelvisPosed;
        string currentMotion;
        float weaponKick, flinch, stance = 1, yLean, zLean, previousTick;
        float gait, paceShown, reloadPose, reloadSpan, impactPose, turnPose, gaitYaw, frameSpan = .043f;
        WeaponHandling.Pose handlingPose;
        readonly Dictionary<string, Transform> guns = new Dictionary<string, Transform>();
        readonly Dictionary<string, WeaponHandling> actions = new Dictionary<string, WeaponHandling>();
        readonly TriasWorld world;
        readonly Material accent;
        internal WireValue Row;
        internal Vector3 From, Position, Previous, Desired;
        internal float FromYaw, Yaw, DesiredYaw, Got;
        internal string Id, Weapon;
        internal int Spawn = -1;

        internal Avatar(TriasWorld w, string id, WireValue player, Material shade)
        {
            world = w; Id = id; accent = shade;
            Root = TriasWorld.Group(w.Actors, player[1].Text);
            body = TriasWorld.Group(Root, "Armor");
            string kit = player[5].Text;
            float width = kit == "bulwark" ? .64f : kit == "wraith" ? .42f : .53f;
            var shell = kit == "wraith" ? w.Dark : kit == "kestrel" ? w.Steel : w.White;
            var pilot = w.Kit.Pilot(body, kit, out locomotion);
            weapon = TriasWorld.Group(body, "Weapon", new Vector3(.18f,1.14f,.36f));
            weapon.localScale = Vector3.one * .62f;
            if (pilot != null)
            {
                var rig = pilot.GetComponentsInChildren<Transform>();
                Transform Joint(string name) => System.Array.Find(rig, joint => joint.name == name);
                leftArm = new PilotArm(Joint("LeftArm"), Joint("LeftForeArm"), Joint("LeftHand"));
                rightArm = new PilotArm(Joint("RightArm"), Joint("RightForeArm"), Joint("RightHand"));
                hips = Joint("Hips");
                spine = Joint("Spine");
                runCycle = locomotion["run"];
                if (runCycle != null) runCycle.speed = 0;
                BuildSuit(w, shell, kit, width);
                return;
            }
            TriasWorld.Part(body, w.Dark, new Vector3(0, 1.06f, 0), new Vector3(width,.58f,.32f));
            TriasWorld.Part(body, accent, new Vector3(0, 1.16f, .145f), new Vector3(width * .83f,.28f,.13f));
            TriasWorld.Part(body, shell, new Vector3(0, 1.09f, -.18f), new Vector3(width*.75f,.34f,.13f));
            TriasWorld.Part(body, accent, new Vector3(0, 1.10f, -.252f), new Vector3(.038f,.25f,.029f));
            head = TriasWorld.Group(body,"Helmet", Vector3.up * 1.45f);
            TriasWorld.Part(head, shell, new Vector3(0,.13f,0), new Vector3(.34f,.34f,.31f),
                kit == "bulwark" || kit == "wraith" ? PrimitiveType.Cube : PrimitiveType.Sphere);
            TriasWorld.Part(head, w.Glass, new Vector3(0,.16f,.15f),new Vector3(.29f,.09f,.054f));
            TriasWorld.Part(head, accent, new Vector3(0,.159f,.185f),new Vector3(.235f,.028f,.019f));
            if (kit == "wraith" || kit == "kestrel")
                TriasWorld.Part(head, w.Steel, new Vector3(.13f,.33f,-.04f),new Vector3(.032f,.23f,.032f));
            for (int side = -1; side <= 1; side += 2)
            {
                TriasWorld.Part(body, accent, new Vector3(side*width*.52f,1.33f,0),new Vector3(.20f,.17f,.27f));
                TriasWorld.Part(body, w.Dark, new Vector3(side * width * .47f,1.15f,.13f),new Vector3(.15f,.13f,.37f));
            }
            left = Leg(-1);
            right = Leg(1);
        }

        void BuildSuit(TriasWorld w, Material shell, string kit, float width)
        {
            Transform pack = TriasWorld.Group(body, "Life support", new Vector3(0, 1.13f, -.20f));
            TriasWorld.Part(pack, shell, Vector3.zero, new Vector3(width * .54f, .36f, .17f));
            TriasWorld.Part(pack, accent, new Vector3(0, .008f, -.091f), new Vector3(.039f, .24f, .024f));
            float plateWidth = kit == "bulwark" ? .235f : .128f;
            for (int side = -1; side <= 1; side += 2)
            {
                var pad = TriasWorld.Part(body, shell, new Vector3(side * width * .43f, 1.35f, -.01f),
                    new Vector3(plateWidth, .13f, .23f), PrimitiveType.Sphere);
                pad.localRotation = Quaternion.Euler(0, 0, -side * 23f);
                TriasWorld.Part(body, accent, new Vector3(side * width * .45f, 1.36f, .086f),
                    new Vector3(plateWidth * .74f, .040f, .031f));
            }
            if (kit == "wraith" || kit == "kestrel")
                TriasWorld.Part(pack, shell, new Vector3(.096f,.28f,0),new Vector3(.022f,.35f,.022f)).localRotation = Quaternion.Euler(0, 0, -13);
            if (kit == "bulwark")
            {
                TriasWorld.Part(body, shell, new Vector3(0, 1.20f, .15f), new Vector3(.37f, .27f, .14f));
                for (int side = -1; side <= 1; side += 2)
                {
                    var heavy = TriasWorld.Part(body, shell, new Vector3(side * .32f, 1.42f, -.018f), new Vector3(.245f, .20f, .29f));
                    heavy.localRotation = Quaternion.Euler(0, 0, side * 14);
                    TriasWorld.Part(body, accent, new Vector3(side * .34f, 1.46f, .133f), new Vector3(.115f, .036f, .023f));
                }
            }
            else if (kit == "wraith")
            {
                var scanner = TriasWorld.Part(pack, w.Dark, new Vector3(-.14f,.22f,-.06f), new Vector3(.18f,.31f,.14f));
                scanner.localRotation = Quaternion.Euler(8, 0, 19);
                TriasWorld.Part(scanner, accent, new Vector3(0,.24f,-.55f), new Vector3(.54f,.11f,.12f));
            }
            else if (kit == "kestrel")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var fin = TriasWorld.Part(pack, w.Steel, new Vector3(side * .22f,.11f,-.064f), new Vector3(.095f,.39f,.19f));
                    fin.localRotation = Quaternion.Euler(-11, 0, side * -21);
                    TriasWorld.Part(pack, accent, new Vector3(side * .21f,.03f,-.168f), new Vector3(.044f,.095f,.025f));
                }
            }
            else
            {
                TriasWorld.Part(body, shell, new Vector3(-.21f,1.13f,.135f), new Vector3(.17f,.25f,.14f));
                TriasWorld.Part(body, accent, new Vector3(-.22f,1.15f,.209f), new Vector3(.10f,.028f,.024f));
            }
        }

        Transform Leg(int side)
        {
            var pivot = TriasWorld.Group(body, "Hip", new Vector3(side*.145f,.86f,0));
            TriasWorld.Part(pivot, world.Dark, new Vector3(0,-.31f,0), new Vector3(.179f,.59f,.17f));
            TriasWorld.Part(pivot, accent, new Vector3(0,-.54f,.052f), new Vector3(.187f,.245f,.147f));
            TriasWorld.Part(pivot, world.Steel, new Vector3(0,-.76f,.045f), new Vector3(.195f,.17f,.28f));
            return pivot;
        }

        internal void Accept(WireValue player, float now)
        {
            var pos = new Vector3(player[18].Float, player[19].Float, player[20].Float);
            var yaw = (player[24].Float + Mathf.PI) * Mathf.Rad2Deg;
            if (Spawn != player[13].Int)
            {
                Position = pos; Yaw = yaw;
                Spawn = player[13].Int;
                impactPose = reloadPose = gait = paceShown = yLean = zLean = turnPose = gaitYaw = weaponKick = flinch = 0;
                currentMotion = null;
                stance = (player[2].Int & 16) != 0 ? .65f : 1;
            }
            else if (Row != null)
            {
                if ((Row[2].Int & 2) == 0 && (player[2].Int & 2) != 0 && Row[22].Float < -4)
                    impactPose = Mathf.Clamp01(-Row[22].Float / 13);
                float packetSpan = Mathf.Clamp(now - Got, .025f, .067f);
                frameSpan = Mathf.Lerp(frameSpan, packetSpan + .007f, .22f);
                turnPose = Mathf.Lerp(turnPose, Mathf.Clamp(Mathf.DeltaAngle(DesiredYaw, yaw) * .32f, -12, 12), .45f);
            }
            From = Position; FromYaw = Yaw;
            Previous = Desired;
            Desired = pos; DesiredYaw = yaw;
            Got = now;
            if ((player[2].Int & 64) != 0 && (Row == null || (Row[2].Int & 64) == 0))
                reloadSpan = Mathf.Max(.1f, player[30].Float);
            Row = player;
            string key = player[9].Text ?? "pulse";
            if (key != Weapon)
            {
                if (Weapon != null) guns[Weapon].gameObject.SetActive(false);
                if (!guns.TryGetValue(key, out var art))
                {
                    art = TriasWorld.Group(weapon, key);
                    TriasModels.Gun(world, art, key, accent);
                    actions[key] = new WeaponHandling(world, art, key, accent, false);
                    guns.Add(key, art);
                }
                art.gameObject.SetActive(true);
                Weapon = key;
            }
        }

        internal void Tick(float now)
        {
            leftArm?.Restore();
            rightArm?.Restore();
            if (pelvisPosed)
            {
                hips.localRotation = hipsPose;
                spine.localRotation = spinePose;
                pelvisPosed = false;
            }
            var row = Row;
            float dt = Mathf.Clamp(now - previousTick, 0, .05f);
            previousTick = now;
            float t = Mathf.Clamp01((now - Got) / frameSpan);
            Vector3 old = Position;
            Position = Vector3.Lerp(From, Desired, t);
            Yaw = Mathf.LerpAngle(FromYaw, DesiredYaw, t);
            Root.SetPositionAndRotation(Position, Quaternion.Euler(0, Yaw, 0));
            int bits = row[2].Int;
            float blend = 1 - Mathf.Exp(-dt * 16);
            stance = Mathf.Lerp(stance, (bits & 16) != 0 ? .65f : 1, blend);
            impactPose *= Mathf.Exp(-dt * 13);
            body.localScale = new Vector3(1 + .028f * impactPose, stance - .069f * impactPose, 1);
            Vector3 lateral = Root.InverseTransformDirection(new Vector3(row[21].Float, 0, row[23].Float));
            bool dodge = row[27].Float > .02f;
            zLean = Mathf.Lerp(zLean, Mathf.Clamp(-lateral.x * (dodge ? 1.6f : .62f), -18, 18), blend);
            yLean = Mathf.Lerp(yLean, Mathf.Clamp(lateral.z * .6f - row[22].Float * .43f, -11, 11), blend);
            flinch *= Mathf.Exp(-dt * 21);
            turnPose *= Mathf.Exp(-dt * 9);
            body.localRotation = Quaternion.Euler(yLean - flinch + 8 * impactPose, turnPose, zLean);
            float pace = new Vector2(row[21].Float,row[23].Float).magnitude;
            paceShown = Mathf.Lerp(paceShown, pace, 1 - Mathf.Exp(-dt * 20));
            bool running = paceShown > (currentMotion == "run" ? .40f : .68f);
            bool grounded = (bits & 2) != 0;
            bool backward = lateral.z < -pace * .25f;
            float direction = Mathf.Atan2(lateral.x, lateral.z) * Mathf.Rad2Deg;
            if (backward) direction = Mathf.DeltaAngle(180, direction);
            gaitYaw = Mathf.Lerp(gaitYaw, grounded && running && !dodge ? Mathf.Clamp(direction, -52, 52) : 0, blend);
            if (grounded && running && !dodge)
            {
                float distance = new Vector2(Position.x - old.x, Position.z - old.z).magnitude;
                gait = Mathf.Repeat(gait + Mathf.Min(distance, 10.6f * dt) / (stance < .8f ? 1.8f : 3.2f) * (backward ? -1 : 1), 1);
            }
            if (locomotion != null)
            {
                string next = !grounded ? "jump" : running ? "run" : "idle";
                if (currentMotion != next && locomotion[next] != null)
                {
                    locomotion.CrossFade(next, next == "jump" ? .07f : .13f);
                    currentMotion = next;
                }
                if (runCycle != null) runCycle.normalizedTime = gait;
            }
            else
            {
                head.localRotation = Quaternion.Euler(-row[25].Float * 27f,0,0);
                float cycle = Mathf.Sin(gait * 2 * Mathf.PI + row[4].Float * 1.53f);
                float angle = !grounded ? 18 : cycle * Mathf.Min(35, paceShown * 5.3f);
                left.localRotation = Quaternion.Euler(angle,0,0);
                right.localRotation = Quaternion.Euler(-angle,0,0);
            }
            weaponKick *= Mathf.Exp(-dt * 19);
            float reloading = (bits & 64) != 0
                ? 1 - Mathf.Max(0, row[30].Float - (now - Got)) / Mathf.Max(.1f, reloadSpan) : 0;
            reloadPose = (bits & 64) != 0 ? Mathf.Max(reloadPose, reloading) : 0;
            handlingPose = WeaponHandling.At(Weapon, reloadPose);
            Vector3 geometry = handlingPose.GunRotation * .72f;
            weapon.localPosition = new Vector3(.095f, 1.13f - handlingPose.Dip * .75f, .30f - weaponKick * .004f);
            weapon.localRotation = Quaternion.Euler(geometry + new Vector3(-row[25].Float * 43 - weaponKick, 0, 0));
            if (actions.TryGetValue(Weapon, out var mechanism)) mechanism.Update(handlingPose, weaponKick * .007f);
        }

        internal void PlaceHands()
        {
            if (leftArm == null || Weapon == null || !guns.TryGetValue(Weapon, out var gun)) return;
            if (hips != null && spine != null && Mathf.Abs(gaitYaw) > .05f)
            {
                hipsPose = hips.localRotation;
                spinePose = spine.localRotation;
                pelvisPosed = true;
                Quaternion yaw = Quaternion.AngleAxis(gaitYaw, Root.up);
                hips.rotation = yaw * hips.rotation;
                spine.rotation = Quaternion.Inverse(yaw) * spine.rotation;
            }
            rightArm.Reach(gun.TransformPoint(WeaponHandling.TriggerGrip), body.TransformPoint(.46f,.92f,-.15f));
            leftArm.Reach(gun.TransformPoint(actions[Weapon].SupportGrip(handlingPose)), body.TransformPoint(-.46f, .94f, -.06f));
        }

        internal void Fire(string key) => weaponKick = key == "rift" || key == "scatter" ? 11 : key == "arc" ? 4 : 7;
        internal void React(bool shieldBreak) => flinch = Mathf.Max(flinch, shieldBreak ? 7 : 3.2f);
    }

    internal sealed class FallenPilot
    {
        internal readonly Transform Root;
        internal readonly Color Tint;
        internal readonly bool Visible;
        internal Vector3 Landing => feet;
        Vector3 feet, velocity;
        readonly float yaw;
        float tilt, settled;

        internal FallenPilot(TriasWorld world, WireValue body, Material paint, Color tint)
        {
            feet = new Vector3(body["x"].Float, body["y"].Float, body["z"].Float);
            yaw = (body["yaw"].Float + Mathf.PI) * Mathf.Rad2Deg;
            Root = TriasWorld.Group(world.Actors, "Fallen pilot", feet);
            Root.rotation = Quaternion.Euler(0, yaw, 0);
            Tint = tint;
            var suit = world.Kit.Pilot(Root, body["character"].Text ?? "vector", out var motion);
            if (suit == null) { Object.Destroy(Root.gameObject); return; }
            Visible = true;
            if (motion["jump"] != null)
                motion["jump"].clip.SampleAnimation(motion.gameObject, motion["jump"].length * .30f);
            TriasWorld.Part(Root, paint, new Vector3(0, 1.27f, -.23f), new Vector3(.23f, .085f, .042f));
            Vector3 from = new Vector3(body["from"][0].Float, body["from"][1].Float, body["from"][2].Float);
            var push = Vector3.ProjectOnPlane(feet - from, Vector3.up).normalized;
            velocity = Vector3.ClampMagnitude(new Vector3(body["vx"].Float * .36f, 0, body["vz"].Float * .36f) + push * 2.8f, 5);
            velocity.y = Mathf.Clamp(body["vy"].Float * .4f + 1.5f, -8f, 5f);
        }

        internal bool Step(TriasWorld world, float delta)
        {
            float dt = Mathf.Min(delta, .05f);
            velocity.y -= 17.5f * dt;
            Vector3 next = feet + velocity * dt;
            Vector3 direction = new Vector3(velocity.x, 0, velocity.z);
            if (direction.sqrMagnitude > .01f && Physics.Raycast(feet + Vector3.up * .45f, direction.normalized,
                direction.magnitude * dt + .22f, TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
            {
                next.x = feet.x; next.z = feet.z;
                velocity.x = velocity.z = 0;
            }
            float floor = world.Height(next.x, next.z);
            if (Physics.Raycast(new Vector3(next.x, feet.y + .10f, next.z), Vector3.down, out var hit,
                Mathf.Max(.18f, feet.y - next.y + .18f), TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
                floor = Mathf.Max(floor, hit.point.y);
            if (next.y <= floor)
            {
                next.y = floor;
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, dt * 25);
                velocity.y = 0;
                settled += dt;
            }
            else settled = 0;
            feet = next;
            tilt = Mathf.MoveTowards(tilt, 87, dt * (settled > 0 ? 310 : 190));
            Root.SetPositionAndRotation(feet + Vector3.up * (.23f * tilt / 87), Quaternion.Euler(-tilt, yaw, 16 * tilt / 87));
            return settled > .75f;
        }
    }
}
