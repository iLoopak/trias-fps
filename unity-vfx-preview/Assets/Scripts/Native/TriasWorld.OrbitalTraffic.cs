using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Transform orbitalCarrier, orbitalFlight;
        Vector3 carrierAnchor, flightAnchor;
        float flightStart, flightPhase;

        void BuildOrbitalTraffic()
        {
            var backdrop = Group(Root, "Orbital traffic");
            bool foundry = Spec.id == "foundry", basin = Spec.id == "basin";
            var hull = MaterialAt(basin ? "#927d77" : foundry ? "#a78780" : "#8799b0");
            var armor = MaterialAt(basin ? "#55474b" : foundry ? "#5f4651" : "#425269");
            var glass = MaterialAt("#212d40");
            var markings = MaterialAt(basin ? "#b78e58" : foundry ? "#c88152" : "#aeb9be");
            var beacon = MaterialAt(foundry ? "#f4a867" : basin ? "#e5a865" : "#92d5e5", true);
            var exhaust = MaterialAt(foundry ? "#9c6257" : basin ? "#a17155" : "#547b96", true);
            foreach (var surface in new[] { hull, armor, markings })
            {
                surface.EnableKeyword("_EMISSION");
                surface.SetColor("_EmissionColor", surface.color * .47f);
            }

            if (!basin)
            {
                carrierAnchor = foundry ? new Vector3(55, 38, 119) : new Vector3(55, 48, 110);
                var carrier = Group(backdrop, foundry ? "Foundry smelter tender" : "Relay carrier", carrierAnchor);
                float length = foundry ? 37 : 49;
                if (Kit.Spacecraft(carrier, "craft_cargoB", length, hull, armor, glass, markings))
                {
                    orbitalCarrier = carrier;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side * length * .24f;
                        Vector3 start = new Vector3(x, length * .105f, -length * .32f);
                        Vector3 end = new Vector3(x * .82f, length * .105f, length * .35f);
                        Beam(carrier, armor, start, end, length * .024f);
                        for (int bay = 0; bay < 6; bay++)
                        {
                            var at = Vector3.Lerp(start, end, (bay + 1) / 7f);
                            Part(carrier, markings, at, new Vector3(length * .039f, length * .058f, length * .034f));
                            Part(carrier, beacon, new Vector3(at.x * 1.30f, -length * .08f, at.z),
                                new Vector3(length * .015f, length * .019f, length * .032f));
                        }
                        if (foundry)
                            Part(carrier, armor, new Vector3(x, -length * .14f, length * .15f),
                                new Vector3(length * .15f, length * .14f, length * .34f), PrimitiveType.Cylinder);
                        Part(carrier, exhaust, new Vector3(x * .86f, -.06f * length, -.51f * length),
                            new Vector3(length * .069f, length * .035f, length * .030f));
                    }
                    Beam(carrier, armor, new Vector3(0, length * .08f, length * .11f),
                        new Vector3(0, length * .27f, length * .09f), length * .034f);
                    Beam(carrier, hull, new Vector3(-length * .13f, length * .26f, length * .09f),
                        new Vector3(length * .13f, length * .26f, length * .09f), length * .020f);
                    Part(carrier, beacon, new Vector3(0, length * .28f, length * .09f),
                        new Vector3(length * .019f, length * .016f, length * .019f));
                }
                else Object.Destroy(carrier.gameObject);
            }

            flightAnchor = basin ? new Vector3(-4, 36, 122) : foundry ? new Vector3(12, 29, 106) : new Vector3(12, 38, 96);
            var formation = Group(backdrop, basin ? "Mining convoy" : "Interceptor patrol", flightAnchor);
            int count = basin ? 3 : 2;
            float craftLength = basin ? 7.0f : 4.5f;
            for (int i = 0; i < count; i++)
            {
                var offset = new Vector3(i == 0 ? 0 : i == 1 ? -7 : 7, -i * 1.25f, -i * (basin ? 10 : 5.5f));
                var ship = Group(formation, basin ? "Mining shuttle" : "Interceptor", offset);
                if (!Kit.Spacecraft(ship, basin ? "craft_miner" : "craft_speederD", craftLength, hull, armor, glass, markings))
                {
                    Object.Destroy(ship.gameObject);
                    continue;
                }
                orbitalFlight = formation;
                var rear = new Vector3(0, -.11f * craftLength, -.53f * craftLength);
                Part(ship, beacon, rear, new Vector3(.27f * craftLength, .095f * craftLength, .08f * craftLength));
                var wash = Line(ship, rear - Vector3.forward * .18f, rear - Vector3.forward * (craftLength * .8f),
                    exhaust, craftLength * .076f);
                wash.endWidth = 0;
            }
            if (orbitalFlight == null) Object.Destroy(formation.gameObject);
            foreach (var renderer in backdrop.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            flightStart = Time.unscaledTime;
            flightPhase = foundry ? 2.5f : 2.95f;
            AnimateOrbitalTraffic(flightStart);
        }

        void AnimateOrbitalTraffic(float time)
        {
            float age = time - flightStart;
            if (orbitalCarrier != null)
            {
                orbitalCarrier.localPosition = carrierAnchor + new Vector3(Mathf.Sin(age * .095f) * 10,
                    Mathf.Sin(age * .18f) * 2.2f, Mathf.Cos(age * .095f) * 4);
                orbitalCarrier.localRotation = Quaternion.Euler(4, 57 + Mathf.Sin(age * .11f) * 9, -7);
            }
            if (orbitalFlight == null) return;
            float angle = age * (Spec.id == "basin" ? .15f : .25f) + flightPhase;
            float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
            orbitalFlight.localPosition = flightAnchor + new Vector3(s * 57, Mathf.Sin(angle * 2) * 4, c * 13);
            var tangent = new Vector3(c * 57, Mathf.Cos(angle * 2) * 8, -s * 13);
            orbitalFlight.localRotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0, 0, 14 * s);
        }
    }
}
