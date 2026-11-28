using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine.Profiling;
using UnityEngine;

namespace Trias.Native
{
    // Unattended playtest diagnostics, enabled only by command-line flags:
    //   -trias-frame-log <file>   appends frame-time percentiles every 10 s while in a match
    //   -trias-capture <dir>      saves a gameplay screenshot every 8 s while in a match
    //   -trias-quit-after <s>     quits the player after that many seconds
    //   -trias-uncapped           disables vsync and the frame cap so the log shows real frame cost
    //   -trias-no-capture         overrides -trias-capture (screenshots stall the main thread ~150 ms)
    //   -trias-autopilot          while alive, sprints, weaves, turns, jumps and dodges through the normal input
    //                             path, so movement, Momentum Chain and first-person motion are in the measured load
    // Used with tools/playtest/host-room.mjs and -trias-invite; normal play is unaffected.
    public sealed partial class TriasClient
    {
        string frameLogPath, capturePath;
        float remoteError, remoteShown, remoteServer, localError, lastShownSpeed = -1; int remoteSamples, localSamples;
        Vector3 lastDisplay;
        float quitAfter = -1, nextFrameReport, nextCapture;
        readonly List<float> frameSamples = new(1200);
        int captureIndex, unfocusedFrames, spikeFrames, spikesWithGc, collections, lastGcCount = -1;
        bool uncapped, autopilot;
        // Managed allocation attribution: heap growth per frame section, summed over the report window.
        readonly Dictionary<string, long> allocatedBy = new();
        readonly Dictionary<string, double> slowestBy = new();
        readonly System.Diagnostics.Stopwatch sectionClock = new();
        long allocationMark;

        long AllocMark() => frameLogPath != null ? Profiler.GetMonoUsedSizeLong() : 0;

        void AllocNote(string section, long mark)
        {
            if (frameLogPath == null) return;
            long grown = Profiler.GetMonoUsedSizeLong() - mark;
            if (grown > 0) allocatedBy[section] = (allocatedBy.TryGetValue(section, out var sum) ? sum : 0) + grown;
        }

        void NoteParse(long grown)
        {
            if (grown > 0) allocatedBy["parse"] = (allocatedBy.TryGetValue("parse", out var sum) ? sum : 0) + grown;
        }

        void AllocationBegin()
        {
            if (frameLogPath == null) return;
            allocationMark = Profiler.GetMonoUsedSizeLong();
            sectionClock.Restart();
        }

        void AllocationEnd(string section)
        {
            if (frameLogPath == null) return;
            double ms = sectionClock.Elapsed.TotalMilliseconds;
            if (!slowestBy.TryGetValue(section, out var slowest) || ms > slowest) slowestBy[section] = ms;
            long grown = Profiler.GetMonoUsedSizeLong() - allocationMark;
            if (grown > 0) allocatedBy[section] = (allocatedBy.TryGetValue(section, out var sum) ? sum : 0) + grown;
        }
        float autopilotTurn;
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
            if (Array.IndexOf(args, "-trias-autopilot") >= 0) autopilot = true;
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
                // Hitch attribution: frames over 25 ms, and how many of them coincide with a garbage collection.
                int gc = GC.CollectionCount(0);
                bool collected = lastGcCount >= 0 && gc != lastGcCount;
                if (collected) collections++;
                if (Time.unscaledDeltaTime > .025f) { spikeFrames++; if (collected) spikesWithGc++; }
                lastGcCount = gc;
                // Own view smoothness: frame-to-frame change of the camera's ground speed while running.
                float shown = new Vector2(display.x - lastDisplay.x, display.z - lastDisplay.z).magnitude / Mathf.Max(1e-4f, Time.unscaledDeltaTime);
                float running = new Vector2(own[21].Float, own[23].Float).magnitude;
                if (own[6].Int > 0 && (own[2].Int & 2) != 0 && running > 3 && running < 14 && lastShownSpeed >= 0)
                {
                    localError += Mathf.Abs(shown - lastShownSpeed) / running;
                    localSamples++;
                }
                lastShownSpeed = shown; lastDisplay = display;
                // Remote smoothness: how far each running pilot's on-screen speed strays from the server's.
                foreach (var actor in pilots.Values)
                    if (actor.Row != null && actor.Row[6].Int > 0 && (actor.Row[2].Int & 2) != 0 && actor.ServerSpeed > 3 && actor.ServerSpeed < 14)
                    {
                        remoteError += Mathf.Abs(actor.ShownSpeed - actor.PreviousShownSpeed) / actor.ServerSpeed;
                        remoteShown += actor.ShownSpeed; remoteServer += actor.ServerSpeed;
                        remoteSamples++;
                    }
                if (now >= nextFrameReport) FlushFrames(now);
            }
            if (capturePath != null && now >= nextCapture)
            {
                nextCapture = now + 8;
                StartCoroutine(CaptureFrame(Path.Combine(capturePath, $"{world.Spec.id}-{captureIndex++:D2}.png")));
            }
        }

        // Scripted movement for unattended playtests; it uses the same command bits as the keyboard.
        void Autopilot(ref int bits, ref float horizontal, ref float vertical)
        {
            if (!autopilot || own == null || own[6].Int <= 0 || state["winner"]["label"].Text != null) return;
            float t = Time.unscaledTime;
            vertical = 1;
            horizontal = Mathf.Sin(t * 1.3f) * .7f;
            bits |= 1 << 1;                                   // sprint
            if (Mathf.Repeat(t, 2.6f) < Motor.Tick) bits |= 1 << 5;          // jump press
            if (Mathf.Repeat(t + 1.1f, 4.3f) < Motor.Tick) bits |= 1 << 6;   // dodge
            // Wander: a slow sweep plus a sharp turn every few seconds keeps the pilot off walls.
            if (Mathf.Repeat(t, 3.7f) < Motor.Tick) autopilotTurn = 70 + Mathf.Repeat(t * 37, 110);
            float turn = Mathf.MoveTowards(0, autopilotTurn, 120 * Motor.Tick);
            autopilotTurn -= turn;
            yaw = Mathf.Repeat(yaw + turn + Mathf.Sin(t * .7f) * .6f, 360);
            pitch = Mathf.Lerp(pitch, 4, .1f);
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
                "{0} frames={1} median_ms={2:F2} p95_ms={3:F2} p99_ms={4:F2} max_ms={5:F2} main_ms={6:F2} unfocused={7} renderers={8} gc_mb={9:F0} spikes25={12} spikes_gc={13} gc_runs={14} local_judder={18:F1}% remote_judder={15:F1}% remote_speed={16:F1}/{17:F1} {10}x{11}\n",
                world.Spec.id, frameSamples.Count, At(.5f), At(.95f), At(.99f), frameSamples[^1], main, unfocusedFrames,
                renderers, gcMemory.Valid ? gcMemory.LastValue / 1048576.0 : -1, Screen.width, Screen.height,
                spikeFrames, spikesWithGc, collections, remoteSamples > 0 ? remoteError / remoteSamples * 100 : -1,
                remoteShown / Mathf.Max(1, remoteSamples), remoteServer / Mathf.Max(1, remoteSamples), localSamples > 0 ? localError / localSamples * 100 : -1));
            remoteError = remoteShown = remoteServer = localError = 0; remoteSamples = localSamples = 0;
            var parts = new List<string>();
            foreach (var pair in allocatedBy) parts.Add($"{pair.Key}={pair.Value / 10240}");
            File.AppendAllText(frameLogPath, "  alloc_kb_per_s " + string.Join(" ", parts) + "\n");
            parts.Clear();
            foreach (var pair in slowestBy) parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}={1:F1}", pair.Key, pair.Value));
            File.AppendAllText(frameLogPath, "  slowest_ms " + string.Join(" ", parts) + "\n");
            allocatedBy.Clear();
            slowestBy.Clear();
            frameSamples.Clear();
            unfocusedFrames = spikeFrames = spikesWithGc = collections = 0;
        }
    }
}
