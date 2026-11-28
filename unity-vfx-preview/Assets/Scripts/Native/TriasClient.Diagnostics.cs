using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace Trias.Native
{
    // Unattended playtest diagnostics, enabled only by command-line flags:
    //   -trias-frame-log <file>   appends frame-time percentiles every 10 s while in a match
    //   -trias-capture <dir>      saves a gameplay screenshot every 8 s while in a match
    //   -trias-quit-after <s>     quits the player after that many seconds
    //   -trias-uncapped           disables vsync and the frame cap so the log shows real frame cost
    //   -trias-no-capture         overrides -trias-capture (screenshots stall the main thread ~150 ms)
    // Used with tools/playtest/host-room.mjs and -trias-invite; normal play is unaffected.
    public sealed partial class TriasClient
    {
        string frameLogPath, capturePath;
        float quitAfter = -1, nextFrameReport, nextCapture;
        readonly List<float> frameSamples = new(1200);
        int captureIndex, unfocusedFrames;
        bool uncapped;
        ProfilerRecorder mainThread, gcMemory;

        void InitDiagnostics()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-trias-frame-log") frameLogPath = args[i + 1];
                else if (args[i] == "-trias-capture") capturePath = args[i + 1];
                else if (args[i] == "-trias-quit-after" && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) quitAfter = s;
            }
            if (Array.IndexOf(args, "-trias-uncapped") >= 0) uncapped = true;
            if (Array.IndexOf(args, "-trias-no-capture") >= 0) capturePath = null;
            if (capturePath != null) Directory.CreateDirectory(capturePath);
            // An unattended player is rarely the focused window; keep simulating and rendering anyway.
            if (frameLogPath != null || capturePath != null || quitAfter > 0) Application.runInBackground = true;
            if (frameLogPath != null)
            {
                mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
                gcMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            }
        }

        void UpdateDiagnostics(float now)
        {
            if (quitAfter > 0 && now > quitAfter) { FlushFrames(now); Application.Quit(); return; }
            // Re-applied every frame because the client's own settings may restore vsync after loading.
            if (uncapped) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            if (world == null || own == null) { frameSamples.Clear(); nextFrameReport = now + 10; nextCapture = now + 6; return; }
            if (frameLogPath != null)
            {
                frameSamples.Add(Time.unscaledDeltaTime * 1000);
                if (!Application.isFocused) unfocusedFrames++;
                if (now >= nextFrameReport) FlushFrames(now);
            }
            if (capturePath != null && now >= nextCapture)
            {
                nextCapture = now + 8;
                StartCoroutine(CaptureFrame(Path.Combine(capturePath, $"{world.Spec.id}-{captureIndex++:D2}.png")));
            }
        }

        // The Screen Capture module is not in this project, so read the finished frame back directly.
        System.Collections.IEnumerator CaptureFrame(string path)
        {
            yield return new WaitForEndOfFrame();
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            image.Apply(false);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Destroy(image);
        }

        void FlushFrames(float now)
        {
            nextFrameReport = now + 10;
            if (frameLogPath == null || frameSamples.Count < 30 || world == null) { frameSamples.Clear(); return; }
            frameSamples.Sort();
            float At(float q) => frameSamples[Mathf.Min(frameSamples.Count - 1, (int)(q * frameSamples.Count))];
            // Main-thread time separates real work from OS throttling of an unfocused window.
            double main = mainThread.Valid && mainThread.LastValue > 0 ? mainThread.LastValue * 1e-6 : -1;
            int renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
            File.AppendAllText(frameLogPath, string.Format(CultureInfo.InvariantCulture,
                "{0} frames={1} median_ms={2:F2} p95_ms={3:F2} p99_ms={4:F2} max_ms={5:F2} main_ms={6:F2} unfocused={7} renderers={8} gc_mb={9:F0} {10}x{11}\n",
                world.Spec.id, frameSamples.Count, At(.5f), At(.95f), At(.99f), frameSamples[^1], main, unfocusedFrames,
                renderers, gcMemory.Valid ? gcMemory.LastValue / 1048576.0 : -1, Screen.width, Screen.height));
            frameSamples.Clear();
            unfocusedFrames = 0;
        }
    }
}
