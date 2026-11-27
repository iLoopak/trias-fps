using UnityEngine;

namespace Trias.Native
{
    internal sealed class HordeBody
    {
        readonly Transform torso, head, jaw;
        readonly Transform[] shoulders = new Transform[2], elbows = new Transform[2];
        readonly Transform[] hips = new Transform[2], knees = new Transform[2];
        readonly string kind;
        float attackAt, attackDuration;

        internal HordeBody(TriasWorld world, Transform parent, string variant, int slot)
        {
            kind = variant;
            bool heavy = kind == "crusher", spitter = kind == "spitter", thrower = kind == "thrower";
            float breadth = heavy ? .68f : thrower ? .54f : spitter ? .42f : .45f;
            float tall = heavy || thrower ? 1f : .94f;
            var paint = world.InfectedMaterials;
            Material skin = paint[(slot + (heavy ? 1 : 0)) % 2];
            Material cloth = paint[2 + (slot % 2)];
            Material dark = paint[4], infected = paint[5], bone = paint[6], spit = paint[7];
            float girth = heavy ? 1.28f : 1;
            var frame = TriasWorld.Group(parent, "Unarmed infected");
            frame.localScale = new Vector3(1, tall, 1);
            torso = TriasWorld.Group(frame, "Hunched torso", Vector3.up * .86f);
            TriasWorld.Part(torso, cloth, new Vector3(0, .13f, -.01f), new Vector3(breadth, .37f, .29f), PrimitiveType.Sphere);
            TriasWorld.Part(torso, skin, new Vector3(.02f, .34f, .048f), new Vector3(breadth, .47f, .35f), PrimitiveType.Sphere);
            TriasWorld.Part(torso, dark, new Vector3(-.084f, .31f, .182f), new Vector3(.18f * girth, .27f, .051f)).localRotation = Quaternion.Euler(0, 0, -21);
            for (int rib = 0; rib < 3; rib++)
                TriasWorld.Part(torso, cloth, new Vector3(.07f, .24f + rib * .052f, .204f), new Vector3(.14f*girth, .017f, .022f)).localRotation = Quaternion.Euler(0, 4, -(rib+1)*9);

            head = TriasWorld.Group(torso, "Forward neck", new Vector3(.013f, .58f, .155f));
            TriasWorld.Part(head, skin, new Vector3(0,.098f,0), new Vector3(.25f,.31f,.24f)*girth, PrimitiveType.Sphere);
            TriasWorld.Part(head, skin, new Vector3(-.011f,.069f,.116f)*girth,
                new Vector3(.061f,.081f,.077f)*girth, PrimitiveType.Sphere);
            for (int socket = 0; socket < 2; socket++)
            {
                float side = socket * 2 - 1;
                TriasWorld.Part(head, dark, new Vector3(side*.061f,.114f,.100f)*girth,
                    new Vector3(.071f,.050f,.050f)*girth, PrimitiveType.Sphere);
                TriasWorld.Part(head, skin, new Vector3(side*.072f,.079f,.103f)*girth,
                    new Vector3(.102f,.063f,.066f)*girth, PrimitiveType.Sphere);
            }
            jaw = TriasWorld.Group(head, "Slack jaw", new Vector3(0,.018f,.04f));
            TriasWorld.Part(jaw, dark, Vector3.forward * .055f, new Vector3(.147f,.061f,.081f)*girth, PrimitiveType.Sphere);
            TriasWorld.Part(jaw, skin, new Vector3(0,-.039f,.059f), new Vector3(.175f,.062f,.135f)*girth, PrimitiveType.Sphere);
            for (int side = 0; side < 2; side++)
                TriasWorld.Part(head, spitter ? spit : infected, new Vector3((side * 2 - 1)*.056f,.108f,.123f)*girth,
                    new Vector3(.017f,.015f,.014f), PrimitiveType.Sphere);
            if (spitter)
                TriasWorld.Part(head, spit, new Vector3(.023f,-.104f,.072f), new Vector3(.133f,.164f,.12f), PrimitiveType.Sphere);
            if (heavy || thrower)
                for (int shard = 0; shard < 3; shard++)
                    TriasWorld.Part(torso, heavy ? bone : infected, new Vector3(-.12f+shard*.104f,.51f,-.159f),
                        new Vector3(.078f,.20f + (shard % 2)*.10f,.092f)).localRotation = Quaternion.Euler(-28, 0, (shard-1)*21);

            for (int i = 0; i < 2; i++)
            {
                float side = i * 2 - 1;
                float arm = (thrower && side > 0 ? 1.32f : girth);
                shoulders[i] = TriasWorld.Group(torso, "Shoulder", new Vector3(side*breadth*.48f,.42f,0));
                TriasWorld.Part(shoulders[i], i == slot % 2 ? cloth : skin, new Vector3(0,-.026f,0),
                    new Vector3(.184f,.17f,.193f)*arm, PrimitiveType.Sphere);
                TriasWorld.Part(shoulders[i], skin, new Vector3(side*.035f,-.16f,0),
                    new Vector3(.15f,.34f,.16f)*arm, PrimitiveType.Sphere);
                elbows[i] = TriasWorld.Group(shoulders[i], "Elbow", new Vector3(side*.059f,-.27f*arm,0));
                TriasWorld.Part(elbows[i], skin, Vector3.zero, new Vector3(.126f,.15f,.13f)*arm, PrimitiveType.Sphere);
                TriasWorld.Part(elbows[i], skin, Vector3.down*.14f*arm, new Vector3(.12f,.29f,.126f)*arm, PrimitiveType.Sphere);
                TriasWorld.Part(elbows[i], heavy && i == 1 ? bone : dark, new Vector3(0,-.27f*arm,.027f),
                    new Vector3(.136f,.15f,.09f)*arm, PrimitiveType.Sphere);
                for (int finger = -1; finger < 2; finger++)
                    TriasWorld.Part(elbows[i], skin, new Vector3(finger*.039f,-.34f,.038f)*arm,
                        new Vector3(.024f,.104f,.023f)*arm).localRotation = Quaternion.Euler(24, 0, finger*8);

                hips[i] = TriasWorld.Group(frame, "Hip", new Vector3(side*breadth*.25f,.88f,0));
                TriasWorld.Part(hips[i], cloth, Vector3.down*.19f, new Vector3(.19f,.44f,.24f)*girth, PrimitiveType.Sphere);
                knees[i] = TriasWorld.Group(hips[i], "Knee", Vector3.down*.41f);
                TriasWorld.Part(knees[i], cloth, Vector3.zero, new Vector3(.147f,.157f,.17f)*girth, PrimitiveType.Sphere);
                TriasWorld.Part(knees[i], i == slot % 2 ? skin : cloth, Vector3.down*.19f,
                    new Vector3(.14f,.38f,.16f)*girth, PrimitiveType.Sphere);
                TriasWorld.Part(knees[i], dark, new Vector3(0,-.37f,.051f), new Vector3(.15f,.11f,.26f)*girth, PrimitiveType.Sphere);
            }
        }

        internal void Windup(float now, float duration)
        {
            attackAt = now;
            attackDuration = Mathf.Max(.1f, duration);
        }

        internal void Pose(float now, float pace, float gait, int slot, float stun)
        {
            float phase = gait * Mathf.PI * 2;
            float moving = Mathf.Clamp01(pace / 4.5f);
            float sway = Mathf.Sin(phase + slot*.51f);
            float elapsed = now - attackAt;
            float wind = attackDuration > 0 && elapsed < attackDuration ? Mathf.SmoothStep(0, 1, elapsed / attackDuration) : 0;
            float strike = attackDuration > 0 && elapsed >= attackDuration ? Mathf.Max(0,1 - (elapsed-attackDuration)/.22f) : 0;
            bool ranged = kind is "spitter" or "thrower";
            torso.localRotation = Quaternion.Euler((kind == "crusher" ? 11 : 19) + sway*5*moving - wind*19 + strike*18 - stun,
                sway*7*moving, sway*6*moving);
            head.localRotation = Quaternion.Euler(-12 + (kind == "spitter" ? -wind*24 + strike*9 : wind*7), sway * 5, -9);
            jaw.localRotation = Quaternion.Euler(12 + (ranged ? 28 : 18) * Mathf.Max(wind,strike), 0, 0);
            for (int i = 0; i < 2; i++)
            {
                float side = i * 2 - 1;
                float step = Mathf.Sin(phase + i * Mathf.PI) * moving;
                hips[i].localRotation = Quaternion.Euler(step * (i == slot % 2 ? 31 : 48), 0, side * 4);
                knees[i].localRotation = Quaternion.Euler(Mathf.Max(0,-step)*34, 0, 0);
                float swing = kind == "thrower" ? (i == 1 ? -138 : -34) : kind == "spitter" ? -24 : -75;
                shoulders[i].localRotation = Quaternion.Euler(-23 - step*19 + wind*swing - strike*60, 0, side*(14 + wind*7));
                elbows[i].localRotation = Quaternion.Euler(-19 - wind*38 + strike*13, 0, side*4);
            }
        }
    }

    internal sealed partial class TriasWorld
    {
        Material[] infectedMaterials;
        internal Material[] InfectedMaterials => infectedMaterials ??= new[]
        {
            MapSurface("#8b8b68", "refractory", 0, .17f), MapSurface("#a09675", "refractory", 0, .16f),
            MapSurface("#484b46", "brushed-deck", 0, .13f), MapSurface("#726650", "refractory", 0, .12f),
            MaterialAt("#342b28"), MaterialAt("#b97a5f", true), MaterialAt("#b2a585"), MaterialAt("#889653", true)
        };
    }
}
