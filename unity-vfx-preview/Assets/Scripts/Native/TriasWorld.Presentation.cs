using UnityEngine;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Material orbitalSky, reactorEnergy;
        Transform reactorRotor;
        Light reactorLight;
        Color reactorTint;
        float reactorShock;

        void ConfigureSky(Camera view)
        {
            var shader = Resources.Load<Shader>("TRIAS/OrbitalSky");
            if (shader == null || !shader.isSupported) return;
            orbitalSky = new Material(shader);
            Paint.Add(orbitalSky);
            string horizon = Spec.id switch
            {
                "foundry" => "#4c2b32",
                "basin" => "#594441",
                "island" => "#b0d6cf",
                _ => "#30455c"
            };
            orbitalSky.SetColor("_Zenith", Catalog.Tint(island ? "#517eab" : "#0c1428"));
            orbitalSky.SetColor("_Horizon", Catalog.Tint(horizon));
            orbitalSky.SetColor("_LightColor", Catalog.Tint(island ? "#fff2cb" : "#f6b57b"));
            orbitalSky.SetColor("_Body", Catalog.Tint(Spec.id == "basin" ? "#594543" : "#385064"));
            orbitalSky.SetVector("_Planet", new Vector4(-.78f, .31f, .53f, .32f));
            orbitalSky.SetFloat("_Daylight", island ? 1 : 0);
            RenderSettings.skybox = orbitalSky;
            view.clearFlags = CameraClearFlags.Skybox;
        }

        void BuildInfrastructure()
        {
            float edge = Spec.length;
            var copper = MaterialAt(Spec.id == "foundry" ? "#b46044" : "#987660", false, "armor");
            var signal = MaterialAt("#d99655", true);
            for (int side = 0; side < 4; side++)
            {
                var bank = Group(Root, "Exterior radiator bank");
                bank.rotation = Quaternion.Euler(0, side * 90, 0);
                for (int i = -2; i <= 2; i++)
                {
                    if (i == 0) continue;
                    float x = i * edge * .38f;
                    Vector3 mount = new Vector3(x, 5.3f, edge + 2.7f);
                    Beam(bank, Dark, mount - Vector3.up * 4.2f, mount + new Vector3(0, 5.5f, 4), .71f);
                    var panel = Part(bank, copper, mount + new Vector3(0, 2.5f, 2.0f), new Vector3(3.9f, 4.7f, .32f));
                    panel.rotation = bank.rotation * Quaternion.Euler(-28, 0, i * 11);
                    for (int fin = 0; fin < 3; fin++)
                        Part(panel, Dark, new Vector3((fin - 1) * .30f, 0, -.57f), new Vector3(.088f, .91f, .27f));
                    Part(bank, signal, mount + new Vector3(0, 5.4f, 3.85f), new Vector3(.23f, .28f, .23f));
                }
                for (int i = -2; i <= 2; i++)
                {
                    float x = i * edge * .32f;
                    Part(bank, copper, new Vector3(x, .012f, edge - .83f), new Vector3(edge * .085f, .025f, .16f));
                }
            }
        }

        void BuildReactor(bool foundry)
        {
            var mount = Group(Root, foundry ? "Tavicí komora" : "Orbitální relé", new Vector3(0, foundry ? 8.5f : 9.4f, 0));
            reactorTint = Catalog.Tint(foundry ? "#ff743f" : "#ffab68");
            reactorEnergy = MaterialAt(foundry ? "#ff743f" : "#ffab68", true);
            var shell = MaterialAt(foundry ? "#745052" : "#637988", false, "bulkhead");
            var pale = MaterialAt("#f4bb7d", true);
            var baseGlow = MaterialAt(foundry ? "#ff6836" : "#6abbbd", true);
            reactorRotor = Group(mount, "Reactor rotor");
            var crystal = Part(reactorRotor, reactorEnergy, Vector3.zero, new Vector3(1.94f, 2.35f, 1.94f));
            crystal.localRotation = Quaternion.Euler(36, 43, 28);
            for (int i = 0; i < 3; i++)
            {
                var arm = Group(mount, "Containment arm");
                arm.localRotation = Quaternion.Euler(0, i * 120, 0);
                Beam(arm, shell, new Vector3(0, -3.5f, 4.9f), new Vector3(0, -.65f, 3.15f), .9f);
                Beam(arm, Dark, new Vector3(0, -.65f, 3.15f), new Vector3(0, 2.3f, 2.45f), 1.12f);
                Beam(arm, baseGlow, new Vector3(-.54f, -.65f, 3.22f), new Vector3(-.54f, 2.2f, 2.50f), .09f);
                Beam(arm, shell, new Vector3(0, 2.3f, 2.45f), new Vector3(0, 2.7f, 1.5f), .64f);
                Part(arm, pale, new Vector3(0, 2.7f, 1.5f), new Vector3(.81f,.17f,.33f));
                Beam(arm, shell, new Vector3(0, -3.1f, 4.9f), new Vector3(0, 3.9f, 20), .24f);
                Beam(arm, Dark, new Vector3(.42f, -3.1f, 4.9f), new Vector3(.42f, 4.1f, 20), .13f);
                Part(arm, pale, new Vector3(0, 3.9f, 20), new Vector3(.47f, .34f, 1.25f));
                var ring = Ring(reactorRotor, Vector3.zero, 2.12f + i * .17f, i == 0 ? pale : reactorEnergy, .075f, 44);
                ring.transform.localRotation = Quaternion.Euler(30 + i * 44, i * 40, 23);
            }
            reactorLight = Group(mount, "Reactor spill", Vector3.down * .85f).gameObject.AddComponent<Light>();
            reactorLight.type = LightType.Point;
            reactorLight.color = reactorTint;
            reactorLight.range = 17;
            reactorLight.intensity = 2.05f;
        }

        static void Beam(Transform root, Material material, Vector3 start, Vector3 end, float width)
        {
            var d = end - start;
            var rail = Part(root, material, (start + end) * .5f, new Vector3(width, width, d.magnitude));
            rail.localRotation = Quaternion.LookRotation(d);
        }

        internal void SurgeLandmark() => reactorShock = 1;

        void AnimateLandmark(float time)
        {
            if (reactorRotor == null) return;
            reactorShock = Mathf.MoveTowards(reactorShock, 0, Time.unscaledDeltaTime * .63f);
            float pulse = .5f + .5f * Mathf.Sin(time * 2.3f);
            reactorRotor.localRotation = Quaternion.Euler(12 + Mathf.Sin(time) * 11, time * 31, -19);
            reactorEnergy.color = Color.Lerp(reactorTint, new Color(1f,.87f,.59f), pulse * .38f + reactorShock * .44f);
            reactorLight.intensity = 1.6f + pulse * .85f + reactorShock * 3.25f;
        }
    }
}
