using UnityEngine;

namespace Trias.Preview
{
    public sealed class RelayPilot : MonoBehaviour
    {
        public const int MaxHealth = 150;
        public const int Magazine = 24;
        public const int TrainingRift = 3;
        const float BodyHeight = 1.78f;
        const float BodyRadius = 0.43f;
        const float Gravity = 19f;

        internal RelayMatch Match;
        internal CharacterController Body;
        internal Transform Art;
        internal Transform LeftLeg, RightLeg, Visor;
        internal int Team, Slot, Health, Ammo, RiftAmmo, Deaths;
        internal string Callsign;
        internal bool Human, Rift;
        internal float NextShot, ReloadAt, ReturnAt, ProtectedUntil;
        internal Vector3 Velocity;
        internal float Vertical, Pitch, Yaw, Kick, HitFlash, ConfirmFlash;
        bool airJump, grounded;
        float padLocked, launchUntil, step, seekAt;
        Vector3 waypoint;

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
            Ammo = Magazine;
            RiftAmmo = Human ? TrainingRift : 0;
            Rift = false;
            Pitch = Vertical = Kick = HitFlash = ConfirmFlash = 0;
            Velocity = Vector3.zero;
            grounded = airJump = false;
            seekAt = padLocked = launchUntil = ReloadAt = 0;
            ProtectedUntil = Time.time + 1.4f;
            NextShot = Time.time + 0.22f;
            if (Human) PositionCamera();
        }

        internal void Tick(float dt)
        {
            if (Health <= 0 || !Match.Running) return;
            Kick = Mathf.MoveTowards(Kick, 0, dt * (Rift ? 12 : 20));
            ConfirmFlash = Mathf.MoveTowards(ConfirmFlash, 0, dt * 5.7f);
            HitFlash = Mathf.MoveTowards(HitFlash, 0, dt * 2.5f);
            if (ReloadAt > 0 && Time.time >= ReloadAt)
            {
                Ammo = Magazine;
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
            if (Input.GetKeyDown(KeyCode.Alpha1) && Rift) Switch(false);
            if (Input.GetKeyDown(KeyCode.Alpha2) && !Rift && RiftAmmo > 0) Switch(true);
            if (!Rift && Input.GetKeyDown(KeyCode.R)) Reload();
            var rotation = Quaternion.Euler(0, Yaw, 0);
            Vector3 wish = rotation * Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")), 1);
            bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Move(wish, sprint ? 10.1f : 7.55f, Input.GetKeyDown(KeyCode.Space), dt);
            PositionCamera();
            if (Input.GetMouseButton(0)) Fire();
        }

        void Switch(bool rift)
        {
            Rift = rift;
            ReloadAt = 0;
            NextShot = Time.time + 0.22f;
        }

        void BotInput(float dt)
        {
            var enemy = Match.TargetFor(this, out bool visible);
            if (enemy == null) return;
            Vector3 target = enemy.transform.position;
            Vector3 toward = target - transform.position;
            Vector3 flat = new Vector3(toward.x, 0, toward.z);
            float distance = flat.magnitude;
            Vector3 direction = distance > 0.05f ? flat / distance : transform.forward;
            if (visible)
            {
                Vector3 look = enemy.Eye - Eye - Vector3.up * 0.38f + Vector3.Cross(Vector3.up, direction) * Mathf.Sin(Time.time * 9 + Slot) * (0.30f + distance * 0.022f);
                Yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                Pitch = -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                var advance = distance > 10 ? direction * 0.82f : distance < 4 ? -direction * 0.42f : Vector3.zero;
                advance += side * Mathf.Sin(Time.time * 1.9f + Slot * 2 + Team) * 0.62f;
                Move(Vector3.ClampMagnitude(advance, 1), 6.1f, false, dt);
                Fire();
            }
            else
            {
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

        internal bool Fire()
        {
            if (Time.time < NextShot || ReloadAt > 0 || Health <= 0) return false;
            if (!Rift && Ammo == 0)
            {
                Reload();
                return false;
            }
            if (Rift && RiftAmmo == 0) return false;
            ProtectedUntil = 0;
            Vector3 origin = Eye, aim = Aim;
            if (Rift)
            {
                RiftAmmo--;
                NextShot = Time.time + 0.72f;
                Kick = Human ? 5.5f : 0;
                Match.ShootRift(this, origin, aim);
            }
            else
            {
                Ammo--;
                NextShot = Time.time + (Human ? 0.145f : 0.26f + Slot * 0.017f);
                Kick = Human ? 2.45f : 0;
                Match.ShootPulse(this, origin, aim);
            }
            return true;
        }

        internal void Reload()
        {
            if (Ammo >= Magazine || ReloadAt > 0) return;
            ReloadAt = Time.time + 1.3f;
            if (Human) Match.Effects.Reload(Eye, false);
        }

        internal bool Damage(RelayPilot attacker, int damage)
        {
            if (attacker == this || attacker.Team == Team || Health <= 0 || Time.time < ProtectedUntil) return false;
            Health = Mathf.Max(0, Health - damage);
            HitFlash = 1f;
            if (attacker.Human) attacker.ConfirmFlash = Health == 0 ? 1.6f : 1f;
            if (Health != 0) return true;
            Deaths++;
            ReturnAt = Time.time + 2.8f;
            Body.enabled = false;
            if (Art != null) Art.gameObject.SetActive(false);
            Match.Eliminated(attacker, this);
            return true;
        }
    }
}
