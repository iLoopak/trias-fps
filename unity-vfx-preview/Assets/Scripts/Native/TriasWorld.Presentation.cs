using UnityEngine;
using System.Collections.Generic;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Material orbitalSky, reactorEnergy;
        Transform reactorRotor;
        Light reactorLight;
        Color reactorTint;
        float reactorShock;
        internal static readonly Vector3 IslandSun = new Vector3(-.9f, .19f, .41f).normalized;
        readonly List<Transform> excavators = new List<Transform>();

        void ConfigureSky(Camera view)
        {
            var shader = Resources.Load<Shader>("TRIAS/OrbitalSky");
            if (shader == null || !shader.isSupported) return;
            orbitalSky = new Material(shader);
            Paint.Add(orbitalSky);
            var sky = Spec.id switch
            {
                "foundry" => (1f, "#190f1c", "#4c2b32", "#ff9966", "#9a513e", new Vector4(-.42f, .36f, 1, .235f)),
                "basin" => (2f, "#14212a", "#4f493e", "#d7c99d", "#7a927e", new Vector4(-.92f, .63f, -.38f, .225f)),
                "island" => (0f, "#517eab", "#b0d6cf", "#fff2cb", "#385064", Vector4.zero),
                _ => (3f, "#142035", "#6a849b", "#fff0d4", "#29739a", new Vector4(.61f,.49f,1,.46f))
            };
            var (scene, zenith, horizon, light, body, planet) = sky;
            orbitalSky.SetFloat("_SkyScene", scene);
            orbitalSky.SetColor("_Zenith", Catalog.Tint(zenith));
            orbitalSky.SetColor("_Horizon", Catalog.Tint(horizon));
            orbitalSky.SetColor("_LightColor", Catalog.Tint(light));
            orbitalSky.SetColor("_Body", Catalog.Tint(body));
            orbitalSky.SetVector("_Planet", planet);
            if (island) orbitalSky.SetVector("_SunDirection", IslandSun);
            if (IsRelayArt)
            {
                orbitalSky.SetVector("_SunDirection", RelayStar);
                var cloudDeck = Resources.Load<Texture2D>("TRIAS/relay-cloud-deck");
                if (cloudDeck != null)
                {
                    orbitalSky.SetTexture("_RelayCloudDeck", cloudDeck);
                    orbitalSky.SetFloat("_RelayCloudDeckActive", 1);
                }
            }
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
                for (int i = -2; side != 0 && i <= 2; i++)
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

        void BuildMiningRigs()
        {
            var rust = MaterialAt("#936858", false, "armor");
            var marker = MaterialAt("#ffae63", true);
            for (int i = 0; i < 3; i++)
            {
                var mast = Group(Root, "Mining rig");
                mast.rotation = Quaternion.Euler(0, i * 120 + 27, 0);
                float edge = Spec.length + 5.5f;
                Vector3 bearing = new Vector3(0, 13.6f, edge);
                Beam(mast, rust, new Vector3(-4, -.6f, edge), bearing, .93f);
                Beam(mast, rust, new Vector3(4, -.6f, edge), bearing, .93f);
                Beam(mast, Dark, new Vector3(-2.7f, 4.5f, edge), new Vector3(2.7f, 4.5f, edge), .52f);
                var pivot = Group(mast, "Excavator arm", bearing);
                Beam(pivot, Dark, new Vector3(-7, 0, 0), new Vector3(10.4f, 0, 0), .72f);
                var teeth = Group(pivot, "Separated core sampler", new Vector3(8.5f,-1.2f,0));
                teeth.localRotation = Quaternion.Euler(0,0,-12);
                for (int section = 0; section < 3; section++)
                {
                    var jaw = Group(teeth, "Excavator jaw");
                    jaw.localRotation = Quaternion.Euler(0, section*120,0);
                    CeramicArc(jaw, Insulation, Vector3.up*.27f, new Vector3(2.4f,1.14f,2.4f), 91);
                    CeramicArc(jaw, rust, Vector3.down*.68f, new Vector3(2.15f,.76f,2.15f), 78);
                }
                Part(teeth, Graphite, Vector3.up*.28f, new Vector3(1.05f,1.12f,1.05f), PrimitiveType.Cylinder);
                Armor(pivot, Porcelain, new Vector3(-2.2f,.34f,0),new Vector3(4.5f,.77f,.87f),false);
                Armor(pivot, SafetyAmber, new Vector3(2.3f,.32f,0),new Vector3(1.1f,.45f,.87f),false);
                Part(pivot, marker, new Vector3(-6.5f,.18f,0), new Vector3(.31f,.25f,.31f));
                excavators.Add(pivot);
            }
        }

        void BuildReactor()
        {
            var mount = Group(Root, "Orbitální relé", new Vector3(0, Spec.Shaft ? 16.1f : 10.1f, 0));
            reactorTint = Catalog.Tint("#ffb566");
            reactorEnergy = MaterialAt("#ffb566", true);
            var filament = MaterialAt("#fff2cc", true);
            reactorRotor = Group(mount, "Axial induction column");
            BuildInductionColumn(mount, filament);
            for (int level = 0; level < 3; level++)
            {
                float y = level == 0 ? -3.1f : level == 1 ? 2.45f : 7.8f;
                var stage = Group(mount, "Floating induction stage", new Vector3(0,y,0));
                stage.localRotation = level == 0 ? Quaternion.Euler(4,-11,-7)
                    : Quaternion.Euler(level == 1 ? 24 : -25, 16 + level * 25, level == 1 ? -16 : 12);
                float radius = level == 0 ? 4.75f : level == 1 ? 7.4f : 9.55f;
                for (int side = 0; side < 3; side++)
                {
                    var shell = Group(stage, "Independently suspended ring section");
                    shell.localRotation = Quaternion.Euler(0, 120 * side, 0);
                    CeramicArc(shell, Graphite, new Vector3(0,-.52f,0), new Vector3(radius*.997f,.61f,radius*.997f), 83);
                    CeramicArc(shell, Porcelain, new Vector3(0,.32f,0), new Vector3(radius,.94f,radius), 91);
                    CeramicArc(shell, relayStructure, new Vector3(0,-.07f,0), new Vector3(radius*1.008f,.19f,radius*1.008f), 71);
                    CeramicArc(shell, reactorEnergy, new Vector3(0,-.35f,0), new Vector3(radius*1.001f,.085f,radius*1.001f), 29);
                    for (int rib = -2; rib <= 2; rib++)
                    {
                        var joint = Group(shell, "Reactor ring service joint");
                        joint.localRotation = Quaternion.Euler(0, rib * 16, 0);
                        Armor(joint, relayStructure, new Vector3(0, .20f, radius + .019f),
                            new Vector3(.28f, .91f, .15f), false);
                        if (rib % 2 == 0)
                            Armor(joint, Insulation, new Vector3(.36f, -.29f, radius + .035f),
                                new Vector3(.46f, .25f, .16f), false);
                    }
                }
            }
            for (int i = 0; i < 3; i++)
            {
                var foot = Group(mount, "Anchor pedestal");
                foot.localRotation = Quaternion.Euler(0, i * 120 + 20, 0);
                Beam(foot, Graphite, new Vector3(0,-7.3f,6.6f),new Vector3(0,-2.8f,3.8f),1.25f);
                Armor(foot, Porcelain, new Vector3(0,-4.76f,5.75f),new Vector3(1.34f,3.25f,.37f), false).localRotation = Quaternion.Euler(-28,0,0);
                Armor(foot, SafetyAmber, new Vector3(0,-4.82f,5.98f),new Vector3(.35f,1.22f,.13f), false).localRotation = Quaternion.Euler(-28,0,0);
                var inner = Group(reactorRotor, "Suspended core shell");
                inner.localRotation = Quaternion.Euler(0, 120 * i, 0);
                CeramicArc(inner, Graphite, Vector3.zero, new Vector3(1.8f,1.7f,1.8f), 73);
                CeramicArc(inner, Porcelain, Vector3.up*.51f, new Vector3(1.86f,.5f,1.86f), 70);
            }
            SetReactorLight(mount);
        }

        void SetReactorLight(Transform mount)
        {
            reactorLight = Group(mount, "Reactor spill", Vector3.down * .85f).gameObject.AddComponent<Light>();
            reactorLight.type = LightType.Point;
            reactorLight.color = reactorTint;
            reactorLight.range = IsRelayArt ? 13 : 17;
            reactorLight.intensity = IsRelayArt ? .58f : 2.05f;
        }

        static void Beam(Transform root, Material material, Vector3 start, Vector3 end, float width)
        {
            var d = end - start;
            var rail = Part(root, material, (start + end) * .5f, new Vector3(width, width, d.magnitude));
            rail.localRotation = Quaternion.LookRotation(d);
        }

        internal void SurgeLandmark() => reactorShock = 1;

        void AnimateLandmark(float time, float arenaPulse)
        {
            for (int i = 0; i < excavators.Count; i++)
                excavators[i].localRotation = Quaternion.Euler(0, i * 14, Mathf.Sin(time * .44f + i * 1.7f) * (6.2f + 3.2f * arenaPulse));
            if (reactorRotor == null) return;
            reactorShock = Mathf.MoveTowards(reactorShock, 0, Time.unscaledDeltaTime * .63f);
            float pulse = .5f + .5f * Mathf.Sin(time * 2.3f);
            reactorRotor.localRotation = Quaternion.Euler(12 + Mathf.Sin(time) * 11, time * 31, -19);
            reactorEnergy.color = Color.Lerp(reactorTint, new Color(1f,.87f,.59f), pulse * .38f + reactorShock * .44f + arenaPulse * .20f);
            reactorLight.intensity = IsRelayArt
                ? .46f + pulse * .14f + reactorShock * .65f + arenaPulse * .25f
                : 1.6f + pulse * .85f + reactorShock * 3.25f + arenaPulse * .62f;
        }
    }
}
