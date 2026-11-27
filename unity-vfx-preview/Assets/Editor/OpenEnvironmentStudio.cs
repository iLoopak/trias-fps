using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Trias.Native;

namespace Trias.Preview
{
    public static class OpenEnvironmentStudio
    {
        public const string ScenePath = "Assets/Scenes/TRIAS Environment Studio.unity";

        [MenuItem("TRIAS/Open environment kit studio")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                EditorSceneManager.OpenScene(ScenePath);
            else Create();
        }

        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Environment review camera", typeof(Camera), typeof(EnvironmentStudio)).GetComponent<Camera>();
            camera.fieldOfView = 52;
            camera.transform.position = new Vector3(8.7f, 7.5f, -16);
            camera.transform.LookAt(new Vector3(.2f, .90f, 1.1f));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
