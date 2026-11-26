using System;
using System.IO;
using Trias.Native;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Trias.Preview
{
    public static class BuildTrias
    {
        public const string Scene = "Assets/Scenes/TRIAS.unity";

        [MenuItem("TRIAS/Open full game")]
        public static void Open()
        {
            if (File.Exists(Scene))
            {
                EditorSceneManager.OpenScene(Scene);
                return;
            }
            var arena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("TRIAS camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = .037f;
            camera.farClipPlane = 235f;
            camera.fieldOfView = 67f;

            var fx = new GameObject("TRIAS effects", typeof(RelayEffects)).GetComponent<RelayEffects>();
            Material dust = BuildPreview.ParticleMaterial();
            var tint = new Color(.27f,.95f,.90f);
            fx.muzzle = BuildPreview.Emitter("Muzzle", Vector3.zero, tint, dust, .18f, 2.6f, .12f, .15f);
            fx.impact = BuildPreview.Emitter("Impact", Vector3.zero, tint, dust, .50f, 4.5f, .13f, .62f);
            fx.rift = BuildPreview.Emitter("Rift and reactor collapse", Vector3.zero, Color.magenta, dust, .83f, 6.7f, .24f, .38f);
            fx.arrival = BuildPreview.Emitter("Spawn and pickup", Vector3.zero, tint, dust, .75f, 2.3f, .16f, -.20f);
            var velocity = fx.arrival.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0,0);
            velocity.y = new ParticleSystem.MinMaxCurve(2f,4.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(0,0);
            fx.tracerMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Relay/Materials/Pulse tracer material.mat");
            fx.orbMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Relay/Materials/Reactor.mat");
            fx.haloMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Preview Art/Teal tubes.mat");
            fx.haloMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Preview Art/Respawn ring.asset");
            fx.flash = BuildPreview.Point("Combat lighting", Vector3.zero, tint, 0, 8.8f);
            fx.listener = camera.transform;

            var client = new GameObject("TRIAS client", typeof(TriasClient)).GetComponent<TriasClient>();
            client.view = camera;
            client.effects = fx;
            EditorSceneManager.SaveScene(arena, Scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true), new EditorBuildSettingsScene(BuildRelay.Scene, true) };
            Selection.activeGameObject = client.gameObject;
        }

        public static void BuildMac()
        {
            PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.Standalone, 1);
            Build(BuildTarget.StandaloneOSX);
        }

        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64);
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64);

        static void Build(BuildTarget target)
        {
            Open();
            string output = Environment.GetEnvironmentVariable("TRIAS_NATIVE_BUILD");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Set TRIAS_NATIVE_BUILD to the platform-specific output path.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Install Unity Standalone Build Support for " + target);
            PlayerSettings.companyName = "TRIAS";
            PlayerSettings.productName = "TRIAS";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 810;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene, BuildRelay.Scene }, target = target, locationPathName = output,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("TRIAS build failed: " + report.summary.result);
        }
    }
}
