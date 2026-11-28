using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Trias.Preview
{
    // Batch check for the ElevenLabs sound set: every manifest clip imports, loads and level-matches.
    // Unity -batchmode -quit -projectPath unity-vfx-preview -executeMethod Trias.Preview.AudioReview.Run
    public static class AudioReview
    {
        public static void Run()
        {
            var manifest = File.ReadAllText(Path.Combine(Application.dataPath, "../../tools/audio/sfx-manifest.json"));
            var ids = System.Text.RegularExpressions.Regex.Matches(manifest, "\"([a-z0-9-]+)\": \\{ \"d\"")
                .Select(m => m.Groups[1].Value).ToArray();
            int missing = 0;
            foreach (var id in ids)
            {
                var clip = Resources.Load<AudioClip>("TRIAS/Audio/Sfx/" + id);
                if (clip == null) { missing++; Debug.Log($"AUDIO MISSING {id}"); continue; }
                AudioClip reference = null;
                if (!id.StartsWith("amb-"))
                {
                    var tone = new float[4800];
                    for (int i = 0; i < tone.Length; i++) tone[i] = .3f * Mathf.Sin(i * .05f) * Mathf.Exp(-i / 1200f);
                    reference = AudioClip.Create("reference", tone.Length, 1, 24000, false);
                    reference.SetData(tone, 0);
                }
                var matched = RelayEffects.Sfx(id, reference);
                Debug.Log($"AUDIO {id} {clip.length:F2}s ch={clip.channels} load={clip.loadType} ok={matched == clip}");
            }
            Debug.Log($"AUDIO REVIEW COMPLETE {ids.Length} clips, {missing} missing");
        }
    }
}
