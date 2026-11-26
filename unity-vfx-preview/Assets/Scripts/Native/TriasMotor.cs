using UnityEngine;

namespace Trias.Native
{
    internal struct Command
    {
        internal int Seq, Bits;
        internal float Horizontal, Vertical, Yaw, Pitch;
        internal string Weapon;
        internal bool Has(int flag) => (Bits & (1 << flag)) != 0;
    }

    internal struct Motor
    {
        internal const float Tick = 1f / 30f;
        const float Radius = 0.43f;
        internal Vector3 Position, Velocity;
        internal float DodgeCooldown, DodgeTime, PadCooldown;
        internal bool Grounded, AirJump, AirDodge, Crouch;

        internal void Read(WireValue p)
        {
            int bits = p[2].Int;
            Position = new Vector3(p[18].Float, p[19].Float, p[20].Float);
            Velocity = new Vector3(p[21].Float, p[22].Float, p[23].Float);
            DodgeCooldown = p[26].Float;
            DodgeTime = p[27].Float;
            PadCooldown = p[28].Float;
            Grounded = (bits & 2) != 0;
            AirJump = (bits & 4) != 0;
            AirDodge = (bits & 8) != 0;
            Crouch = (bits & 16) != 0;
        }

        internal void Step(Command input, ArenaSpec map)
        {
            DodgeCooldown = Mathf.Max(0, DodgeCooldown - Tick);
            DodgeTime = Mathf.Max(0, DodgeTime - Tick);
            PadCooldown = Mathf.Max(0, PadCooldown - Tick);
            Crouch = input.Has(0) || Crouch && Blocked(map, Position.x, Position.z, Position.y, 1.78f);
            float height = Crouch ? 1.12f : 1.78f;
            float cos = Mathf.Cos(input.Yaw), sin = Mathf.Sin(input.Yaw);
            Vector2 axis = new Vector2(input.Horizontal, input.Vertical);
            float length = axis.magnitude;
            var wish = new Vector2(axis.x * cos - axis.y * sin, -axis.x * sin - axis.y * cos) / Mathf.Max(1, length);
            PadSpec launch = null;
            if (Grounded && !Crouch && PadCooldown <= 0)
                foreach (var pad in map.launchPads)
                    if (Mathf.Abs(pad.y - Position.y) < 0.04f && new Vector2(pad.x - Position.x, pad.z - Position.z).sqrMagnitude < pad.radius * pad.radius)
                    { launch = pad; break; }
            if (launch != null)
            {
                Velocity = new Vector3(launch.vx, 12.35f, launch.vz);
                Grounded = false;
                AirJump = AirDodge = true;
                PadCooldown = 0.45f;
            }
            else if (input.Has(5) && (Grounded || AirJump))
            {
                Velocity.y = (Grounded ? 7.65f : 8.75f) * (Crouch ? 0.69f : 1f);
                AirJump = Grounded;
                Grounded = false;
            }
            if (launch == null && input.Has(6) && !Crouch && DodgeCooldown <= 0 && (Grounded || AirDodge))
            {
                Vector2 slide = length > 0 ? wish.normalized : new Vector2(-sin, -cos);
                Velocity = new Vector3(slide.x * 18.3f, Grounded ? 3.25f : Mathf.Max(Velocity.y, 1.9f), slide.y * 18.3f);
                DodgeCooldown = 0.96f;
                DodgeTime = 0.22f;
                Grounded = AirDodge = false;
            }
            if (launch == null && DodgeTime <= 0)
            {
                float speed = Crouch ? 4.25f : input.Has(1) ? 10.1f : 7.55f;
                if (map.id == "island" && Grounded && Shallow(Position)) speed *= 0.72f;
                float acceleration = Grounded ? (length > 0 ? 70 : 64) : (length > 0 ? 20 : 4.2f);
                Vector2 adjusted = Vector2.MoveTowards(new Vector2(Velocity.x, Velocity.z), wish * speed, acceleration * Tick);
                Velocity.x = adjusted.x;
                Velocity.z = adjusted.y;
            }
            Velocity.y -= 19 * Tick;
            float old = Position.y, next = old + Velocity.y * Tick;
            if (Velocity.y > 0)
                foreach (var box in map.cover)
                    if (old + height <= box.bottom + 0.0001f && next + height > box.bottom && box.Overlaps(Position.x, Position.z, Radius))
                    { next = box.bottom - height; Velocity.y = 0; }

            bool slope = map.terrain != null && Grounded;
            float moveY = Mathf.Max(old, next);
            float x = Position.x + Velocity.x * Tick, z = Position.z + Velocity.z * Tick;
            if (StepAllowed(map, x, Position.z, moveY, height, slope)) Position.x = x;
            else Velocity.x = 0;
            if (StepAllowed(map, Position.x, z, moveY, height, slope)) Position.z = z;
            else Velocity.z = 0;
            float ground = -1;
            if (Velocity.y <= 0)
            {
                ground = map.terrain?.At(Position.x, Position.z) ?? 0;
                foreach (var box in map.cover)
                    if (old + 0.0001f >= box.h && box.h > ground && box.Overlaps(Position.x, Position.z, Radius))
                        ground = box.h;
            }
            Grounded = next <= ground || slope && ground >= old - 0.35f;
            Position.y = Grounded ? ground : next;
            if (Grounded)
            {
                Velocity.y = 0;
                AirJump = AirDodge = true;
            }
        }

        static bool StepAllowed(ArenaSpec map, float x, float z, float y, float h, bool slope)
        {
            float ground = map.terrain?.At(x, z) ?? 0;
            return (map.terrain == null || slope || ground <= y + 0.0001f) && !Blocked(map, x, z, Mathf.Max(y, ground), h);
        }

        static bool Blocked(ArenaSpec map, float x, float z, float y, float h)
        {
            if (map.edgeRadius > 0 ? x * x + z * z > (map.edgeRadius - Radius) * (map.edgeRadius - Radius)
                : Mathf.Abs(x) > map.length - Radius || Mathf.Abs(z) > map.length - Radius) return true;
            foreach (var box in map.cover)
                if (y < box.h - 0.0001f && y + h > box.bottom + 0.0001f && box.Overlaps(x, z, Radius)) return true;
            return false;
        }

        static bool Shallow(Vector3 position)
        {
            float x = position.x, z = position.z;
            if (z >= -19 && z <= -2.2f && Mathf.Abs(x - (-6.7f + Mathf.Sin((z + 19) * 0.32f) * 1.24f)) < 0.53f) return true;
            return Mathf.Abs(z - (1.35f + Mathf.Sin(x * 0.15f) * 4.72f + Mathf.Sin(x * 0.33f) * 0.48f)) < 1.74f
                && Mathf.Abs(x + 16) > 1.35f && Mathf.Abs(x) > 1.35f && Mathf.Abs(x - 15) > 1.35f;
        }
    }
}
