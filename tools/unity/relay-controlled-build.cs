using System;
using Trias.Native;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Trias.Preview
{
    public static class RelayControlledBuild
    {
        public static void Build()
        {
            const string scenePath="Assets/Scenes/RelayControlledProfile.unity";
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Relay controlled profile",typeof(RelayControlledProbe));
            EditorSceneManager.SaveScene(scene,scenePath);
            PlayerSettings.enableFrameTimingStats=true;
            PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.Standalone,1);
            string path=Environment.GetEnvironmentVariable("TRIAS_NATIVE_BUILD");
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Set TRIAS_NATIVE_BUILD.");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{scenePath,BuildTrias.Scene,BuildRelay.Scene},target=BuildTarget.StandaloneOSX,locationPathName=path
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Relay profile build failed: "+result.summary.result);
        }
    }
}
