using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Preview
{
    public static class BuildRelay
    {
        public const string Scene = "Assets/Scenes/Relay Station.unity";
        const string Art = "Assets/Relay/Materials";

        [MenuItem("TRIAS/Open Relay Station")]
        public static void Open()
        {
            if (File.Exists(Scene))
            {
                EditorSceneManager.OpenScene(Scene);
                return;
            }
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Relay/RelayLayout.json");
            var layout = JsonUtility.FromJson<RelayLayout>(layoutAsset.text);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.59f, 0.77f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.31f, 0.41f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.24f, 0.32f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.037f, 0.063f, 0.115f);
            RenderSettings.fogDensity = 0.012f;
            var key = new GameObject("Orbital key light", typeof(Light)).GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.80f, 0.90f, 1f);
            key.intensity = 1.65f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(53, -28, 0);

            var hullTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Relay/Bulkhead.jpg");
            var deckTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Relay/Deck.jpg");
            Material deck = Paint("Deck", new Color(0.58f, 0.69f, 0.81f), 0.25f, deckTexture, 15f);
            Material alloy = Paint("Hull plating", new Color(0.70f, 0.76f, 0.83f), 0.25f, hullTexture);
            Material trim = Paint("Titanium trims", new Color(0.32f, 0.42f, 0.53f), 0.45f);
            Material armor = Paint("Suit underlayer", new Color(0.085f, 0.12f, 0.18f), 0.34f);
            Material shell = Paint("Pilot ceramic", new Color(0.69f, 0.78f, 0.88f), 0.18f);
            Material silver = LightPaint("Neutral light", new Color(0.60f, 0.82f, 1f));
            Material gold = LightPaint("Launch light", new Color(1f, 0.58f, 0.22f));
            Material reactor = LightPaint("Reactor", new Color(1f, 0.27f, 0.57f));
            var paints = new Material[3];
            var glows = new Material[3];
            var shells = new Material[3];
            for (int i = 0; i < layout.teams.Length; i++)
            {
                var team = layout.teams[i];
                paints[i] = Paint(team.name + " armor", team.Tint, 0.32f);
                glows[i] = LightPaint(team.name + " circuit", team.Tint);
                shells[i] = i == 0 ? shell : Paint(team.name + " ceramic", Color.Lerp(new Color(0.65f, 0.70f, 0.78f), team.Tint, 0.28f), 0.21f);
            }

            Transform room = new GameObject("Relay Station · web collision layout").transform;
            float diameter = layout.length * 2;
            Block(room, "Main deck", deck, new Vector3(0, -0.41f, 0), new Vector3(diameter, 0.82f, diameter));
            for (int i = 0; i < 4; i++)
            {
                var wall = new GameObject("Perimeter " + (i + 1)).transform;
                wall.SetParent(room, false);
                wall.rotation = Quaternion.Euler(0, 90 * i, 0);
                Block(wall, "Bulkhead", alloy, new Vector3(0, 2.05f, layout.length + 0.25f), new Vector3(diameter + 0.6f, 4.1f, 0.50f));
                for (int section = -25; section <= 25; section += 5)
                {
                    Block(wall, "Reinforcement", trim, new Vector3(section, 2.35f, layout.length - 0.045f), new Vector3(0.26f, 4.4f, 0.18f), false);
                    Block(wall, "Perimeter light", silver, new Vector3(section + 1.15f, 2.9f, layout.length - 0.106f), new Vector3(1.5f, 0.055f, 0.03f), false);
                }
            }

            foreach (var box in layout.cover)
            {
                var chassis = new GameObject(box.relay ? "Central reactor plinth" : box.gantry ? "Suspended gantry" : box.terrace ? "Terrace" : box.profile).transform;
                chassis.SetParent(room, false);
                chassis.localPosition = new Vector3(box.x, box.bottom, box.z);
                chassis.localRotation = Quaternion.Euler(0, box.angle * Mathf.Rad2Deg, 0);
                float height = box.h - box.bottom;
                Material light = box.relay ? reactor : box.sector >= 0 ? glows[box.sector] : silver;
                Block(chassis, "Collidable plating", alloy, Vector3.up * height / 2, new Vector3(box.w, height, box.d));
                for (int side = -1; side <= 1; side += 2)
                {
                    Block(chassis, "Upper band", trim, new Vector3(0, height - 0.11f, side * (box.d * 0.5f + 0.028f)),
                        new Vector3(box.w * 0.92f, 0.13f, 0.057f), false);
                    Block(chassis, "Identification stripe", light, new Vector3(0, height - 0.24f, side * (box.d * 0.5f + 0.067f)),
                        new Vector3(box.w * 0.69f, 0.044f, 0.025f), false);
                }
            }

            for (int i = 0; i < layout.teams.Length; i++)
            {
                var team = layout.teams[i];
                var sector = new GameObject(team.name + " · transit lane").transform;
                sector.SetParent(room, false);
                sector.rotation = Quaternion.Euler(0, team.angle * Mathf.Rad2Deg, 0);
                for (int side = -1; side <= 1; side += 2)
                for (int segment = 0; segment < 6; segment++)
                {
                    Block(sector, "Lane marker", glows[i], new Vector3(side * 3.15f, 0.009f, 12f + segment * 2.68f),
                        new Vector3(0.05f, 0.011f, 1.70f), false);
                }
                Vector3 location = team.spawns[0].Position * 0.87f + Vector3.up * 4.8f;
                BuildPreview.Point(team.name + " color wash", location, team.Tint, 2.1f, 15f);
            }

            foreach (var pad in layout.pads)
            {
                Vector3 position = new Vector3(pad.x, 0.025f, pad.z);
                Block(room, "Jump pad deck", trim, position, new Vector3(1.92f, 0.06f, 1.92f), false, PrimitiveType.Cylinder);
                Ring(room, "Launch indicator", position + Vector3.up * 0.078f, 0.92f, 0.04f, gold);
                Ring(room, "Launch inner ring", position + Vector3.up * 0.081f, 0.56f, 0.024f, gold);
            }

            var core = Block(room, "Suspended reactor", reactor, new Vector3(0, 8.5f, 0), new Vector3(2.50f, 2.75f, 2.50f), false, PrimitiveType.Sphere);
            Ring(room, "Reactor outer collar", core.position, 3.3f, 0.18f, trim);
            Ring(room, "Reactor plasma ring", core.position + Vector3.up * 0.54f, 2.23f, 0.052f, reactor).rotation = Quaternion.Euler(17, 0, 21);
            Ring(room, "Reactor cold ring", core.position - Vector3.up * 0.38f, 2.62f, 0.054f, silver).rotation = Quaternion.Euler(-14, 0, -8);
            BuildPreview.Point("Reactor spill", core.position, new Color(1, 0.28f, 0.51f), 5.6f, 21f);

            var camera = new GameObject("First-person camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.045f;
            camera.farClipPlane = 156;

            var fx = new GameObject("Combat effects", typeof(RelayEffects)).GetComponent<RelayEffects>();
            Material dust = BuildPreview.ParticleMaterial();
            var tint = new Color(0.21f, 0.97f, 0.88f);
            fx.muzzle = BuildPreview.Emitter("Pulse muzzle", Vector3.zero, tint, dust, 0.19f, 1.8f, 0.14f, 0.15f);
            fx.impact = BuildPreview.Emitter("Impact sparks", Vector3.zero, tint, dust, 0.61f, 5.2f, 0.12f, 0.59f);
            fx.rift = BuildPreview.Emitter("Rift detonations", Vector3.zero, Color.magenta, dust, 0.82f, 6.6f, 0.24f, 0.41f);
            fx.arrival = BuildPreview.Emitter("Respawn columns", Vector3.zero, tint, dust, 0.76f, 1.8f, 0.15f, -0.19f);
            var ascent = fx.arrival.velocityOverLifetime;
            ascent.enabled = true;
            ascent.space = ParticleSystemSimulationSpace.World;
            ascent.x = new ParticleSystem.MinMaxCurve(0, 0);
            ascent.y = new ParticleSystem.MinMaxCurve(2.35f, 4.2f);
            ascent.z = new ParticleSystem.MinMaxCurve(0, 0);
            fx.tracerMaterial = MaterialAsset("Pulse tracer material", "Sprites/Default", Color.white);
            fx.orbMaterial = reactor;
            fx.haloMaterial = glows[0];
            fx.haloMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Preview Art/Respawn ring.asset");
            fx.flash = BuildPreview.Point("Combat lights", Vector3.zero, tint, 0, 8.5f);
            fx.listener = camera.transform;
            var match = new GameObject("Relay match", typeof(RelayMatch)).GetComponent<RelayMatch>();
            match.layoutJson = layoutAsset;
            match.viewCamera = camera;
            match.effects = fx;
            match.shell = shells;
            match.suit = armor;
            match.stripe = paints;
            match.glow = glows;
            EditorSceneManager.SaveScene(scene, Scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
            Selection.activeGameObject = match.gameObject;
        }

        public static void BuildMac()
        {
            Open();
            string location = Environment.GetEnvironmentVariable("TRIAS_RELAY_BUILD");
            if (string.IsNullOrWhiteSpace(location)) throw new InvalidOperationException("TRIAS_RELAY_BUILD must point to an app bundle.");
            PlayerSettings.companyName = "TRIAS";
            PlayerSettings.productName = "TRIAS Relay";
            PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.Standalone, 1);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene }, target = BuildTarget.StandaloneOSX, locationPathName = location,
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Relay macOS build failed: " + result.summary.result);
        }

        static Transform Block(Transform parent, string label, Material paint, Vector3 location, Vector3 scale, bool solid = true, PrimitiveType primitive = PrimitiveType.Cube)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = label;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = location;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = paint;
            if (!solid) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj.transform;
        }

        static Transform Ring(Transform parent, string name, Vector3 at, float radius, float width, Material paint)
        {
            var ring = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = at;
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.widthMultiplier = width;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.sharedMaterial = paint;
            ring.positionCount = 64;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = 2 * Mathf.PI * i / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius);
            }
            return ring.transform;
        }

        static Material MaterialAsset(string name, string shader, Color tint)
        {
            string path = Art + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(shader)) { name = name, color = tint };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material LightPaint(string name, Color tint) => MaterialAsset(name, "Unlit/Color", tint);

        static Material Paint(string name, Color tint, float metallic, Texture image = null, float repeat = 1f)
        {
            var material = MaterialAsset(name, "Standard", tint);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", 0.42f);
            material.mainTexture = image;
            material.mainTextureScale = Vector2.one * repeat;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
