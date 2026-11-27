using Trias.Native;
using UnityEngine;

namespace Trias.Preview
{
    public sealed class RelayPilot : MonoBehaviour
    {
        public const int MaxHealth = 210;
        const float BodyHeight = 1.78f;
        const float BodyRadius = 0.43f;
        const float Gravity = 19f;

        internal RelayMatch Match;
        internal CharacterController Body;
        internal Transform Art;
        internal Transform LeftLeg, RightLeg, Visor;
        internal int Team, Slot, Health, Deaths, WeaponIndex;
        readonly int[] ammo = new int[5], reserve = new int[5];
        internal Choice Weapon => Match.Arsenal[WeaponIndex];
        internal int Ammo => ammo[WeaponIndex];
        internal int Reserve => reserve[WeaponIndex];
        internal string Callsign;
        internal bool Human;
        internal float NextShot, ReloadAt, ReturnAt, ProtectedUntil;
        internal Vector3 Velocity;
        internal float Vertical, Pitch, Yaw, Kick, HitFlash, ConfirmFlash;
        bool airJump, grounded;
        float padLocked, launchUntil, step, seekAt;
        Vector3 waypoint;
        internal Vector3 DirectorGoal { get; private set; }
        internal bool DirectorContact { get; private set; }
        float memoryUntil, goalUntil, botReactionAt;
        RelayPilot botContact;
        Vector3 lastSeen;

        internal Vector3 Eye => transform.position + Vector3.up * 1.56f;
        Vector3 Aim => Quaternion.Euler(Pitch - (Human ? Kick * 0.52f : 0), Yaw, 0) * Vector3.forward;

        internal void Setup(RelayMatch match, int team, int slot, bool human)
        {
            Match = match;
            Team = team;
            Slot = slot;
            Human = human;
            Body = gameObject.AddComponent<CharacterController>();
            Body.height = BodyHeight;
            Body.radius = BodyRadius;
            Body.center = new Vector3(0, BodyHeight / 2, 0);
            Body.stepOffset = 0.27f;
            Body.skinWidth = 0.04f;
            Body.minMoveDistance = 0;
        }

        internal void Spawn(Vector3 position)
        {
            Body.enabled = false;
            transform.position = position + Vector3.up * 0.028f;
            Vector3 facing = -new Vector3(position.x, 0, position.z);
            Yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, Yaw, 0);
            Body.enabled = true;
            if (Art != null) Art.gameObject.SetActive(true);
            Health = MaxHealth;
            WeaponIndex = Human ? Match.StartingWeapon : 0;
            for (int i = 0; i < Match.Arsenal.Length; i++)
            {
                ammo[i] = Human || i == 0 ? Match.Arsenal[i].magazine : 0;
                reserve[i] = Human && i > 0 ? Match.Arsenal[i].magazine : 0;
            }
            Pitch = Vertical = Kick = HitFlash = ConfirmFlash = 0;
            Velocity = Vector3.zero;
            grounded = airJump = false;
            seekAt = padLocked = launchUntil = ReloadAt = 0;
            goalUntil = memoryUntil = -1;
            DirectorGoal = new Vector3(1024, 0, 1024);
            DirectorContact = false;
            botContact = null;
            ProtectedUntil = Time.time + 1.4f;
            NextShot = Time.time + 0.22f;
            if (Human) PositionCamera();
        }

        internal void Tick(float dt)
        {
            if (Health <= 0 || !Match.Running) return;
            Kick = Mathf.MoveTowards(Kick, 0, dt * (Weapon.id == "rift" ? 12 : 20));
            ConfirmFlash = Mathf.MoveTowards(ConfirmFlash, 0, dt * 5.7f);
            HitFlash = Mathf.MoveTowards(HitFlash, 0, dt * 2.5f);
            if (ReloadAt > 0 && Time.time >= ReloadAt)
            {
                int needed = Weapon.magazine - Ammo;
                int loaded = WeaponIndex == 0 ? needed : Mathf.Min(needed, Reserve);
                ammo[WeaponIndex] += loaded;
                if (WeaponIndex != 0) reserve[WeaponIndex] -= loaded;
                ReloadAt = 0;
                if (Human) Match.Effects.Reload(Eye, true);
            }
            if (Human) HumanInput(dt);
            else BotInput(dt);
        }

        void HumanInput(float dt)
        {
            Yaw = Mathf.Repeat(Yaw + Input.GetAxisRaw("Mouse X") * Match.lookSensitivity, 360);
            Pitch = Mathf.Clamp(Pitch - Input.GetAxisRaw("Mouse Y") * Match.lookSensitivity, -83, 83);
            for (int i = 0; i < Match.Arsenal.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i != WeaponIndex) Switch(i);
            if (Input.GetKeyDown(KeyCode.R)) Reload();
            var rotation = Quaternion.Euler(0, Yaw, 0);
            Vector3 wish = rotation * Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")), 1);
            bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Move(wish, sprint ? 10.1f : 7.55f, Input.GetKeyDown(KeyCode.Space), dt);
            PositionCamera();
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.F)) Fire(true);
            else if (Input.GetMouseButtonDown(0) ||
                ((Weapon.id == "pulse" || Weapon.id == "arc") && Input.GetMouseButton(0))) Fire();
        }

        void Switch(int index)
        {
            WeaponIndex = index;
            ReloadAt = 0;
            NextShot = Time.time + 0.22f;
        }

        void BotInput(float dt)
        {
            var enemy = Match.TargetFor(this, out bool visible);
            bool directed = Match.Directed;
            DirectorContact = visible;
            if (enemy == null && !directed) return;
            Vector3 target;
            if (!directed) target = enemy.transform.position;
            else if (visible)
            {
                target = lastSeen = enemy.transform.position;
                memoryUntil = Time.time + 2.4f;
            }
            else if (Time.time < memoryUntil && (lastSeen - transform.position).sqrMagnitude > 2.4f * 2.4f)
                target = lastSeen;
            else
            {
                if (Time.time >= goalUntil || (DirectorGoal - transform.position).sqrMagnitude < 8)
                {
                    DirectorGoal = Match.PatrolFor(this, DirectorGoal);
                    goalUntil = Time.time + 5.5f;
                    seekAt = -1;
                }
                target = DirectorGoal;
            }
            Vector3 toward = target - transform.position;
            Vector3 flat = new Vector3(toward.x, 0, toward.z);
            float distance = flat.magnitude;
            Vector3 direction = distance > 0.05f ? flat / distance : transform.forward;
            if (visible)
            {
                if (botContact != enemy)
                {
                    botContact = enemy;
                    botReactionAt = Time.time + .47f + (Slot % 4) * .06f;
                }
                Vector3 look = enemy.Eye - Eye - Vector3.up * 0.38f + Vector3.Cross(Vector3.up, direction) * Mathf.Sin(Time.time * 8 + Slot) * (0.48f + distance * 0.035f);
                float aimYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                float aimPitch = -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
                Yaw = Mathf.MoveTowardsAngle(Yaw, aimYaw, dt * 142);
                Pitch = Mathf.MoveTowardsAngle(Pitch, aimPitch, dt * 100);
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                var advance = distance > 10 ? direction * 0.82f : distance < 4 ? -direction * 0.42f : Vector3.zero;
                if (directed)
                {
                    float band = Match.PreferredRange(this);
                    bool withdraw = Match.Regrouping && Health < MaxHealth * .43f && distance < band + 2;
                    advance = withdraw ? -.67f * direction : direction * Mathf.Clamp((distance - band) * .18f, -.54f, .82f);
                }
                advance += side * Mathf.Sin(Time.time * 1.9f + Slot * 2 + Team) * 0.62f;
                Move(Vector3.ClampMagnitude(advance, 1), 6.1f, false, dt);
                if (Time.time >= botReactionAt && Mathf.Abs(Mathf.DeltaAngle(Yaw, aimYaw)) < 7 && Mathf.Abs(Mathf.DeltaAngle(Pitch, aimPitch)) < 6)
                    Fire();
            }
            else
            {
                botContact = null;
                Vector3 delta = waypoint - transform.position;
                delta.y = 0;
                if (Time.time >= seekAt || delta.sqrMagnitude < 0.45f)
                {
                    waypoint = Match.Route(transform.position, target);
                    seekAt = Time.time + 0.62f + Slot * 0.04f;
                    delta = waypoint - transform.position;
                    delta.y = 0;
                }
                if (delta.sqrMagnitude > 0.01f) Yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                Pitch = 0;
                Move(delta.normalized, 7.0f, false, dt);
            }
            if (Ammo == 0) Reload();
        }

        internal void Move(Vector3 wish, float speed, bool jump, float dt)
        {
            bool landed = grounded || Body.isGrounded;
            Vector3 pad = Vector3.zero;
            bool launched = Human && landed && Time.time >= padLocked && Match.LaunchAt(transform.position, out pad);
            if (launched)
            {
                Velocity = pad;
                Vertical = 12.35f;
                padLocked = Time.time + 0.65f;
                launchUntil = Time.time + 0.54f;
                airJump = true;
                Match.Effects.Launch(transform.position, Match.Layout.teams[Team].Tint);
            }
            else
            {
                float acceleration = landed ? 54f : wish.sqrMagnitude > 0 ? 15f : 4.2f;
                if (Time.time >= launchUntil || landed) Velocity = Vector3.MoveTowards(Velocity, wish * speed, acceleration * dt);
                if (jump && (landed || airJump))
                {
                    Vertical = landed ? 7.65f : 8.75f;
                    airJump = landed;
                    if (!landed && Human) Match.Effects.Launch(transform.position, Match.Layout.teams[Team].Tint);
                }
                else if (landed && Vertical < 0) Vertical = -2f;
            }
            Vertical -= Gravity * dt;
            CollisionFlags collision = Body.Move((Velocity + Vector3.up * Vertical) * dt);
            if ((collision & CollisionFlags.Above) != 0 && Vertical > 0) Vertical = 0;
            grounded = (collision & CollisionFlags.Below) != 0;
            if (grounded && Vertical < 0) Vertical = -2f;
            transform.rotation = Quaternion.Euler(0, Yaw, 0);
            step += Velocity.magnitude * dt * 1.85f;
            if (LeftLeg != null)
            {
                float swing = Mathf.Sin(step) * Mathf.Min(36f, Velocity.magnitude * 6f);
                LeftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
                RightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
                Visor.localRotation = Quaternion.Euler(Pitch * 0.55f, 0, 0);
            }
        }

        internal void PositionCamera()
        {
            if (!Human) return;
            Match.View.transform.SetPositionAndRotation(Eye, Quaternion.Euler(Pitch - Kick * 0.52f, Yaw, 0));
        }

        internal bool Fire(bool alternate = false)
        {
            if (Time.time < NextShot || ReloadAt > 0 || Health <= 0) return false;
            var spec = alternate ? Weapon.alt : Weapon.shot;
            if (Ammo < spec.Cost)
            {
                if (Ammo == 0) Reload();
                if (Human && alternate) Match.Effects.DryFire();
                return false;
            }
            ProtectedUntil = 0;
            ammo[WeaponIndex] -= spec.Cost;
            NextShot = Time.time + (Human ? spec.interval : 0.26f + Slot * 0.017f);
            Vector3 direction = Aim;
            Kick = Human ? (Weapon.id == "rift" ? 5.5f : 2.45f) * (alternate ? 1.4f : 1) : 0;
            Match.FireWeapon(this, Eye, direction, spec, alternate);
            return true;
        }

        internal void Reload()
        {
            if (Ammo >= Weapon.magazine || ReloadAt > 0 || (WeaponIndex > 0 && Reserve == 0)) return;
            ReloadAt = Time.time + Weapon.reload;
            if (Human) Match.Effects.Reload(Eye, false, Weapon.id);
        }

        internal bool Damage(RelayPilot attacker, int damage)
        {
            if (attacker == this || attacker.Team == Team || Health <= 0 || Time.time < ProtectedUntil) return false;
            Health = Mathf.Max(0, Health - damage);
            Match.DirectorHit(damage, Health == 0);
            HitFlash = 1f;
            if (attacker.Human) attacker.ConfirmFlash = Health == 0 ? 1.6f : 1f;
            if (Health != 0) return true;
            Deaths++;
            ReturnAt = Time.time + 5f;
            Body.enabled = false;
            if (Art != null) Art.gameObject.SetActive(false);
            Match.Eliminated(attacker, this);
            return true;
        }
    }
}
