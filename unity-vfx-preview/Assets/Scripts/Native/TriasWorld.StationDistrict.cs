using UnityEngine;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Transform antenna, oreShuttle;
        float stationStart;

        void BuildStationDistrict()
        {
            bool foundry = Spec.id == "foundry", basin = Spec.id == "basin";
            var district = Group(Root, "Station district", new Vector3(0, 0, Spec.length));
            var paint = (
                Hull: MaterialAt(foundry ? "#8e6660" : basin ? "#a09686" : "#8b9fb2", false, "bulkhead"),
                Frame: MaterialAt(foundry ? "#4c3439" : basin ? "#62575a" : "#4b6175", false, "armor"),
                Glass: MaterialAt("#253342"),
                Trim: MaterialAt(foundry ? "#ae704b" : basin ? "#a67a50" : "#7e9daa"));
            var lamp = MaterialAt(foundry ? "#e99657" : basin ? "#edd5a2" : "#b0ddeb", true);
            stationStart = Time.unscaledTime;

            if (basin)
            {
                var railroad = Group(district, "Ore monorail", new Vector3(0, 0, 9));
                for (int i = -4; i <= 4; i++)
                {
                    float x = i * 12;
                    Kit.SpaceFixture(railroad, "monorail_trackStraight", new Vector3(12.05f, 1, 2.4f), new Vector3(x, 8, 0), 90, paint);
                    Kit.SpaceFixture(railroad, "monorail_trackSupport", new Vector3(2.25f, 8.1f, 2.6f), new Vector3(x, 0, 0), 0, paint);
                    Part(railroad, lamp, new Vector3(x, 8.55f, -1.22f), new Vector3(.44f, .17f, .11f));
                }
                for (int side = -1; side <= 1; side += 2)
                    Kit.SpaceFixture(railroad, "hangar_largeA", new Vector3(14, 13, 7), new Vector3(side * 48, 0, 0), 90, paint);
                oreShuttle = Group(railroad, "Ore shuttle", new Vector3(-35, 9, 0));
                for (int i = 0; i < 3; i++)
                {
                    float x = i * 4.8f;
                    Kit.SpaceFixture(oreShuttle, "monorail_trainCargo", new Vector3(4.6f, 2.45f, 2.1f), new Vector3(x, 0, 0), 90, paint);
                    Part(oreShuttle, lamp, new Vector3(x, .54f, -1.04f), new Vector3(.62f, .16f, .08f));
                }
            }
            else if (foundry)
            {
                var processing = Group(district, "Foundry processing yard");
                for (int side = -1; side <= 1; side += 2)
                {
                    var refinery = Group(processing, "Cooling and power", new Vector3(side * 20, 0, 8));
                    Part(refinery, paint.Frame, new Vector3(0, 1.55f, 0), new Vector3(13, 3.1f, 9));
                    Kit.SpaceFixture(refinery, "machine_generatorLarge", new Vector3(8.4f, 5.4f, 7.0f), new Vector3(-side * 1.2f, 3.1f, 0), 0, paint);
                    Kit.SpaceFixture(refinery, "chimney_detailed", new Vector3(2.6f, 13.5f, 2.6f), new Vector3(side * 4.8f, 3.1f, 1.9f), 0, paint);
                    Kit.SpaceFixture(refinery, "pipe_straight", new Vector3(13.6f, 1.45f, 1.45f), new Vector3(0, 5.0f, -3.9f), 90, paint);
                    Ring(refinery, new Vector3(side * 4.8f, 16.50f, 1.9f), .43f, lamp, .10f, 20);
                    for (int i = -2; i <= 2; i++)
                        Part(refinery, lamp, new Vector3(i * 1.52f, 4.3f, -3.94f), new Vector3(.67f, .15f, .13f));
                }
            }
            else
            {
                var service = Group(district, "Relay maintenance docks");
                Kit.SpaceFixture(service, "hangar_largeA", new Vector3(19, 10.2f, 11), new Vector3(18, .1f, 10), 0, paint);
                Kit.SpaceFixture(service, "hangar_largeA", new Vector3(12, 7.8f, 9), new Vector3(3.5f, .1f, 12.7f), 0, paint);
                Part(service, paint.Frame, new Vector3(13, -.10f, 11), new Vector3(35, .85f, 15));
                for (int i = 0; i < 7; i++)
                    Part(service, lamp, new Vector3(1 + i * 4.1f, 4.9f, 4.30f), new Vector3(1.03f, .16f, .16f));
                Kit.SpaceFixture(service, "machine_generatorLarge", new Vector3(7.3f, 5.5f, 7.3f), new Vector3(-21, 0, 10), 0, paint);
                antenna = Group(service, "Tracking dish", new Vector3(-21, 5.35f, 10));
                Kit.SpaceFixture(antenna, "satelliteDish_large", new Vector3(10.8f, 10.8f, 8), Vector3.zero, 180, paint);
            }
        }

        void AnimateStationDistrict(float time)
        {
            float age = time - stationStart;
            if (antenna != null)
                antenna.localRotation = Quaternion.Euler(0, 22 * Mathf.Sin(age * .16f), 0);
            if (oreShuttle != null)
            {
                var at = oreShuttle.localPosition;
                at.x = -5 - 37 * Mathf.Cos(age * .12f + .40f);
                oreShuttle.localPosition = at;
            }
        }
    }
}
