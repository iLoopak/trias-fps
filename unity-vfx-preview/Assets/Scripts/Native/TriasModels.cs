using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    internal static class TriasModels
    {
        internal static void Gun(TriasWorld world, Transform root, string weapon, Material accent)
            => PhaseArmory.Build(world, root, weapon, accent);

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
        readonly HordeBody infected;
        readonly Animation locomotion;
        readonly AnimationState runCycle;
        readonly PilotArm leftArm, rightArm;
        readonly Transform hips, spine;
        Quaternion hipsPose, spinePose;
        bool pelvisPosed;
        string currentMotion;
        bool alternateImpulse;
        int impulse;
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
            body = TriasWorld.Group(Root, "Body");
            if (player[31].Text is string hordeType && player[9].Text == null)
            {
                infected = new HordeBody(w, body, hordeType, player[4].Int);
                return;
            }
            string kit = player[5].Text;
            float width = kit == "bulwark" ? .64f : kit == "wraith" ? .42f : .53f;
            var shell = kit == "wraith" ? w.Dark : kit == "kestrel" ? w.Steel : w.White;
            int faction = w.Mode != "ffa" && player[31].Text == null ? player[3].Int : -1;
            if (faction >= 0) shell = w.FactionPaint[faction];
            var pilot = w.Kit.Pilot(body, kit, accent, faction, player[32].Text == "male", out locomotion);
            // Carried at a shouldered ready height and near true size for adult-proportioned pilots.
            weapon = TriasWorld.Group(body, "Weapon", new Vector3(.17f,1.36f,.34f));
            weapon.localScale = Vector3.one * .9f;
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
            if (infected != null) return;
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
            int bits = row[2].Int;
            float missing = Mathf.Clamp(now - Got - frameSpan, 0, .045f);
            if ((bits & 2) != 0 && row[27].Float <= .02f && missing > 0 && row[6].Int > 0)
            {
                var drift = Vector3.ClampMagnitude(new Vector3(row[21].Float, 0, row[23].Float) * missing, .38f);
                if (drift.sqrMagnitude > .0004f &&
                    !Physics.Linecast(Desired + Vector3.up * .7f, Desired + drift + Vector3.up * .7f,
                        TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
                    Position += drift;
            }
            Yaw = Mathf.LerpAngle(FromYaw, DesiredYaw, t);
            Root.SetPositionAndRotation(Position, Quaternion.Euler(0, Yaw, 0));
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
            if (infected != null)
            {
                infected.Pose(now, paceShown, gait, row[4].Int, flinch);
                return;
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
            if (actions.TryGetValue(Weapon, out var mechanism)) mechanism.Update(handlingPose, weaponKick * .007f, alternate:alternateImpulse, impulse:impulse);
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

        internal void Windup(float now, float duration) => infected?.Windup(now, duration);
        internal void Fire(string key, bool alternate = false)
        {
            alternateImpulse=alternate;
            impulse++;
            weaponKick = key switch { "rift" => 11, "scatter" => 12, "arc" => 4, "lance" => 7, _ => 6 };
        }
        internal void React(bool shieldBreak) => flinch = Mathf.Max(flinch, shieldBreak ? 7 : 3.2f);

        internal void BoostPose(MotionCue motion)
        {
            if (motion != MotionCue.Double && motion != MotionCue.Launch) return;
            var jump = locomotion == null ? null : locomotion["jump"];
            if (currentMotion == "jump" && jump != null) jump.normalizedTime = .12f;
        }
    }

    internal sealed class FallenPilot
    {
        internal readonly Transform Root;
        internal readonly Color Tint;
        internal readonly bool Visible;
        internal Vector3 Landing => feet;
        internal readonly bool Local,HordeEnemy;
        internal readonly int Team;
        Vector3 feet, velocity;
        readonly float yaw;
        float tilt, settled;

        internal FallenPilot(TriasWorld world, WireValue body, Material paint, Color tint,bool local)
        {
            feet = new Vector3(body["x"].Float, body["y"].Float, body["z"].Float);
            yaw = (body["yaw"].Float + Mathf.PI) * Mathf.Rad2Deg;
            Root = TriasWorld.Group(world.Actors, "Fallen pilot", feet);
            Root.rotation = Quaternion.Euler(0, yaw, 0);
            Tint = tint;
            Local = local;
            Team = body["team"].Int;
            HordeEnemy = body["hordeType"].Text != null;
            if (HordeEnemy && body["weapon"].Text == null)
            {
                var infected = new HordeBody(world, Root, body["hordeType"].Text, body["slot"].Int);
                infected.Pose(0, 0, 0, body["slot"].Int, 19);
            }
            else
            {
                int faction = world.Mode == "ffa" || HordeEnemy ? -1 : body["team"].Int;
                var suit = world.Kit.Pilot(Root, body["character"].Text ?? "vector", paint, faction, body["frame"].Text == "male", out var motion);
                if (suit == null) { Object.Destroy(Root.gameObject); return; }
                if (motion["jump"] != null)
                    motion["jump"].clip.SampleAnimation(motion.gameObject, motion["jump"].length * .30f);
            }
            Visible = true;
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
