using Trias.Native;
using UnityEditor;
using UnityEngine;

namespace Trias.Preview
{
    [InitializeOnLoad]
    internal static class OpenGameFeelLab
    {
        const string OpenPending = "TRIAS.GameFeelLab.OpenPending";

        static OpenGameFeelLab() => EditorApplication.playModeStateChanged += HandlePlayMode;

        [MenuItem("TRIAS/Play Game Feel Lab")]
        static void Open()
        {
            if (EditorApplication.isPlaying)
            {
                var running = Object.FindFirstObjectByType<TriasClient>();
                if (running != null) running.JoinFeelLab();
                return;
            }
            BuildTrias.Open();
            SessionState.SetBool(OpenPending, true);
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            EditorApplication.EnterPlaymode();
        }

        static void HandlePlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(OpenPending, false)) return;
            SessionState.EraseBool(OpenPending);
            var client = Object.FindFirstObjectByType<TriasClient>();
            if (client != null) client.JoinFeelLab();
        }
    }
}
