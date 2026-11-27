using UnityEditor;

namespace Trias.Preview
{
    public static class EnvironmentReviewBuild
    {
        public static void Build()
        {
            PlayerSettings.enableFrameTimingStats = true;
            BuildTrias.BuildMac();
        }
    }
}
