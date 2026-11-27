using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Trias.Preview
{
    // Batch-mode entry for the HUD-free art review fixture (Assets/Scripts/Native/ArtReviewProbe.cs).
    // Unity -batchmode -projectPath unity-vfx-preview -executeMethod Trias.Preview.ArtReview.Run
    // with TRIAS_ART_OUT=<dir> and optional TRIAS_ART_ONLY=relay,basin and TRIAS_ART_WIDTH=1600.
    // Do not pass -quit: the probe exits the editor after the last capture.
    public static class ArtReview
    {
        const string Pending = "trias.artReview.pending";

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            SessionState.SetBool(Pending, true);
            EditorApplication.playModeStateChanged += Started;
            EditorApplication.isPlaying = true;
        }

        static void Started(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            new GameObject("Art review eye").AddComponent<Trias.Native.ArtReviewProbe>();
        }
    }
}
