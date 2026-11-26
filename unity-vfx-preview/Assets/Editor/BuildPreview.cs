using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Preview
{
    public static class BuildPreview
    {
        const string Scene = "Assets/Scenes/Trias VFX Lab.unity";
        const string Art = "Assets/Preview Art";
        static readonly Color Teal = new Color(0.18f, 0.92f, 0.87f);
        static readonly Color Amber = new Color(1f, 0.62f, 0.24f);
        static readonly Color Violet = new Color(0.68f, 0.39f, 1f);

        [MenuItem("TRIAS/Open VFX Lab")]
        public static void Open()
        {
            if (File.Exists(Scene))
            {
                EditorSceneManager.OpenScene(Scene);
                return;
            }

            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(Art);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.21f, 0.24f, 0.30f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.042f, 0.058f, 0.085f);
            RenderSettings.fogDensity = 0.031f;

            Material deck = Surface("Graphite deck", new Color(0.135f, 0.162f, 0.22f), 0.54f);
            Material trim = Surface("Brushed titanium", new Color(0.32f, 0.38f, 0.42f), 0.77f);
            Material dark = Surface("Reactor casing", new Color(0.09f, 0.10f, 0.16f), 0.55f);
            Material teal = Glow("Teal tubes", Teal);
            Material amber = Glow("Amber tubes", Amber);
            Material violet = Glow("Violet tubes", Violet);
            Material particle = ParticleMaterial();

            Geometry("Deck", deck, new Vector3(0, -0.38f, 3), new Vector3(22, 0.7f, 27));
            for (int i = -9; i <= 9; i += 3)
            {
                Geometry("Panel seam", dark, new Vector3(i, -0.025f, 3), new Vector3(0.08f, 0.014f, 24));
            }
            for (int i = -9; i <= 9; i += 3)
            {
                Geometry("Horizon rib", trim, new Vector3(i, 3.3f, 11.2f), new Vector3(0.62f, 6.7f, 0.58f));
                Geometry("Rib lamp", i % 2 == 0 ? violet : teal, new Vector3(i, 4.15f, 10.87f), new Vector3(0.11f, 2.32f, 0.038f));
            }
            Geometry("Bulkhead", dark, new Vector3(0, 2.8f, 11.6f), new Vector3(22, 5.6f, 0.7f));
            for (int i = 0; i < 8; i++)
            {
                Geometry("Landing stripe", amber, new Vector3(-5.6f + i * 1.6f, 0.005f, -0.3f),
                    new Vector3(0.61f, 0.017f, 0.047f));
            }

            var camera = new GameObject("Effects camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.fieldOfView = 48f;
            camera.transform.position = new Vector3(2.85f, 3.55f, -6.85f);
            camera.transform.LookAt(new Vector3(0.0f, 1.12f, 3.35f));

            var daylight = new GameObject("Overhead softbox", typeof(Light)).GetComponent<Light>();
            daylight.type = LightType.Directional;
            daylight.color = new Color(0.82f, 0.88f, 1f);
            daylight.intensity = 1.4f;
            daylight.shadows = LightShadows.Soft;
            daylight.transform.rotation = Quaternion.Euler(43, -36, 0);
            Point("Rim left", new Vector3(-6, 2.7f, 4), Teal, 3.15f, 9);
            Point("Rim right", new Vector3(6, 3.3f, 5), Violet, 3.35f, 8);

            Vector3 emitter = new Vector3(-4.6f, 1.26f, 1.3f);
            Vector3 target = new Vector3(-1.3f, 1.43f, 5.35f);
            Vector3 charge = new Vector3(1.4f, 0.45f, 4.1f);
            Vector3 arrival = new Vector3(5.0f, 0.12f, 4.0f);
            Geometry("Pulse pedestal", trim, emitter + Vector3.down * 0.61f, new Vector3(0.85f, 1.22f, 0.85f));
            var barrel = Geometry("Pulse emitter", dark, emitter, new Vector3(0.36f, 0.23f, 1.03f));
            barrel.transform.LookAt(target);
            Geometry("Impact target", trim, target + Vector3.forward * 0.20f, new Vector3(0.76f, 1.07f, 0.27f));
            Geometry("Target marker", amber, target, new Vector3(0.30f, 0.32f, 0.042f));
            Geometry("Rift cradle", dark, charge - Vector3.up * 0.31f, new Vector3(1.62f, 0.30f, 1.64f));
            Geometry("Rift coil", violet, charge, new Vector3(0.35f, 0.35f, 0.35f), PrimitiveType.Sphere);
            Geometry("Respawn deck", trim, arrival - Vector3.up * 0.066f, new Vector3(2.1f, 0.06f, 2.1f), PrimitiveType.Cylinder);
            for (int i = 0; i < 8; i++)
            {
                float angle = Mathf.PI * i / 4;
                var tick = Geometry("Respawn marker", teal, arrival + new Vector3(Mathf.Sin(angle), 0.02f, Mathf.Cos(angle)) * 0.98f,
                    new Vector3(0.35f, 0.022f, 0.09f));
                tick.transform.rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
            }
            Transform lift = new GameObject("Respawn pulse pivot").transform;
            lift.position = arrival;
            Transform halo = Ring(teal);
            halo.SetParent(lift, false);

            var shot = new GameObject("Pulse tracer", typeof(LineRenderer)).GetComponent<LineRenderer>();
            shot.sharedMaterial = amber;
            shot.positionCount = 2;
            shot.SetPositions(new[] { emitter, target });
            shot.numCapVertices = 4;
            shot.shadowCastingMode = ShadowCastingMode.Off;

            var preview = new GameObject("Effects controls", typeof(EffectPreview)).GetComponent<EffectPreview>();
            preview.previewCamera = camera;
            preview.tracer = shot;
            preview.muzzle = Emitter("Pulse flash", emitter, Amber, particle, 0.20f, 3.1f, 0.14f, 0.25f);
            preview.impact = Emitter("Armor sparks", target, Amber, particle, 0.65f, 4.8f, 0.088f, 0.8f);
            preview.rift = Emitter("Rift explosion", charge, Violet, particle, 0.85f, 5.7f, 0.18f, 0.58f);
            preview.respawn = Emitter("Respawn motes", arrival, Teal, particle, 0.76f, 1.9f, 0.16f, -0.16f);
            var ascent = preview.respawn.velocityOverLifetime;
            ascent.enabled = true;
            ascent.space = ParticleSystemSimulationSpace.World;
            ascent.x = new ParticleSystem.MinMaxCurve(0, 0);
            ascent.y = new ParticleSystem.MinMaxCurve(2.35f, 4.0f);
            ascent.z = new ParticleSystem.MinMaxCurve(0, 0);
            preview.burstLight = Point("Impact light", target, Amber, 0, 7);
            preview.returnLight = Point("Respawn light", arrival + Vector3.up, Teal, 0, 6);
            preview.returnHalo = halo;
            EditorSceneManager.SaveScene(scene, Scene);
            Selection.activeGameObject = preview.gameObject;
        }

        internal static ParticleSystem Emitter(string name, Vector3 position, Color tint, Material material, float lifetime, float speed, float size, float gravity)
        {
            var particles = new GameObject(name, typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            particles.transform.position = position;
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.44f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.38f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(tint, Color.Lerp(tint, Color.white, 0.8f));
            main.gravityModifier = gravity;
            main.maxParticles = 144;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.085f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.25f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.0f;
            renderer.velocityScale = 0.19f;
            return particles;
        }

        static GameObject Geometry(string name, Material material, Vector3 position, Vector3 scale, PrimitiveType primitive = PrimitiveType.Cube)
        {
            var item = GameObject.CreatePrimitive(primitive);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        internal static Transform Ring(Material material)
        {
            const string path = Art + "/Respawn ring.asset";
            const int count = 60;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                var vertices = new Vector3[(count + 1) * 2];
                var triangles = new int[count * 6];
                for (int i = 0; i <= count; i++)
                {
                    float angle = 2 * Mathf.PI * i / count;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    int vertex = 2 * i;
                    vertices[vertex] = direction * 0.91f;
                    vertices[vertex + 1] = direction;
                    if (i == count) continue;
                    int face = i * 6;
                    triangles[face] = vertex;
                    triangles[face + 1] = vertex + 2;
                    triangles[face + 2] = vertex + 1;
                    triangles[face + 3] = vertex + 1;
                    triangles[face + 4] = vertex + 2;
                    triangles[face + 5] = vertex + 3;
                }
                mesh = new Mesh { name = "Respawn ring", vertices = vertices, triangles = triangles };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            var ring = new GameObject("Respawn pulse", typeof(MeshFilter), typeof(MeshRenderer));
            ring.GetComponent<MeshFilter>().sharedMesh = mesh;
            ring.GetComponent<MeshRenderer>().sharedMaterial = material;
            return ring.transform;
        }

        internal static Light Point(string name, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
            light.type = LightType.Point;
            light.transform.position = position;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        static Material Surface(string name, Color color, float metallic)
        {
            var material = new Material(Shader.Find("Standard")) { color = color, name = name };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", 0.53f);
            return Save(material);
        }

        static Material Glow(string name, Color color)
        {
            return Save(new Material(Shader.Find("Unlit/Color")) { color = color, name = name });
        }

        static Material Save(Material material)
        {
            string path = Art + "/" + material.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                Object.DestroyImmediate(material);
                return existing;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static Material ParticleMaterial()
        {
            string path = Art + "/Soft spark.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - radius), 2f)));
                }
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"))
            {
                name = "Additive sparks",
                mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path)
            };
            return Save(material);
        }
    }
}
