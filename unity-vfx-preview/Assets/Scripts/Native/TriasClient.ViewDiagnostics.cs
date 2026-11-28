using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Trias.Native
{
    // Visual stability probes for unattended playtests. All of them are off unless a flag is given:
    //   -trias-sweep              repeats a fixed 24 s camera sequence while alive (still, rapid yaw, strafe
    //                             with fire, rapid pitch, tracking with strafe and fire, sprint/jump/dodge) and
    //                             writes per-phase crosshair and camera statistics into the frame log
    //   -trias-clean-view         diagnostic baseline: no cosmetic camera motion, weapon sway or inertia,
    //                             no dynamic FOV and no atmosphere (bloom, fog, shafts); occlusion stays
    //   -trias-fps <n>            disables vsync and caps the frame rate at n
    //   -trias-burst <dir>        saves 8 consecutive full frames from the middle of selected sweep phases
    public sealed partial class TriasClient
    {
        bool sweep, cleanView, sweepTrigger;
        int fpsTarget = -1;
        string burstPath, sweepPhase = "play";
        float sweepStarted = -1, sweepYaw, sweepPitch;

        sealed class PhaseStats
        {
            internal readonly List<float> Offset = new(), Jump = new(), Lag = new();
            internal float Roll, FovMin = 999, FovMax, ReticleRange;
            internal int Frames;
        }

        readonly Dictionary<string, PhaseStats> phaseStats = new();
        Vector2 lastReticle = new(float.NaN, float.NaN);
        int corrections; float correctionSum, correctionMax;

        // Burst capture of consecutive frames: read back into preallocated textures and encode afterwards,
        // so the captured frames are as close as practical to uninterrupted play.
        readonly HashSet<string> burstTaken = new();
        Texture2D[] burstFrames;
        readonly float[] burstDt = new float[8];
        readonly Vector2[] burstReticle = new Vector2[8];
        int burstIndex = -1; string burstName;

        void InitViewDiagnostics(string[] args)
        {
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-trias-fps" && int.TryParse(args[i + 1], out var fps)) fpsTarget = fps;
                else if (args[i] == "-trias-burst") burstPath = args[i + 1];
            }
            if (Array.IndexOf(args, "-trias-sweep") >= 0) sweep = true;
            if (Array.IndexOf(args, "-trias-clean-view") >= 0) cleanView = true;
            if (burstPath != null) Directory.CreateDirectory(burstPath);
        }

        void ApplyFrameTarget()
        {
            if (fpsTarget <= 0) return;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fpsTarget;
        }

        // Runs at the start of every Update, before input is read, so the sweep turns the view every frame
        // exactly like a mouse would (the movement autopilot only changes yaw on 30 Hz input steps).
        void DriveSweep(float now, float dt)
        {
            sweepTrigger = false;
            if (!sweep || own == null || world == null) { sweepPhase = "play"; return; }
            if (own[6].Int <= 0 || state["winner"]["label"].Text != null) { sweepStarted = -1; sweepPhase = "dead"; return; }
            if (!captured && Application.isFocused) Capture(true);
            if (sweepStarted < 0) { sweepStarted = now; sweepYaw = yaw; sweepPitch = 2; }
            float t = now - sweepStarted, local = Mathf.Repeat(t, 4);
            int slot = (int)(Mathf.Repeat(t, 24) / 4);
            sweepPhase = slot switch { 0 => "still", 1 => "rotate", 2 => "strafe", 3 => "pitch", 4 => "track", _ => "run" };
            // Sinusoids start and end each phase at the base pose, so phase changes add no extra turn.
            float wave = Mathf.Sin(local * Mathf.PI * 2 * .75f);
            switch (sweepPhase)
            {
                case "rotate": yaw = Mathf.Repeat(sweepYaw + 80 * wave, 360); pitch = sweepPitch; break;     // peak ~377 deg/s
                case "pitch": yaw = sweepYaw; pitch = sweepPitch + 32 * Mathf.Sin(local * Mathf.PI * 2 * .5f); break;
                case "track": yaw = Mathf.Repeat(sweepYaw + 30 * Mathf.Sin(local * Mathf.PI * 2 * .5f), 360); pitch = sweepPitch; sweepTrigger = true; break;
                case "strafe": yaw = sweepYaw; pitch = sweepPitch; sweepTrigger = true; break;
                case "run": break;
                default: yaw = sweepYaw; pitch = sweepPitch; break;
            }
        }

        // Movement for the sweep phases, through the same command bits as the keyboard.
        bool SweepInput(ref int bits, ref float horizontal, ref float vertical)
        {
            if (!sweep || sweepPhase == "play" || sweepPhase == "dead") return false;
            float t = Time.unscaledTime - sweepStarted;
            horizontal = vertical = 0;
            bool left = Mathf.Repeat(t, 1.2f) < .6f;
            if (sweepPhase == "strafe" || sweepPhase == "track") horizontal = left ? 1 : -1;
            if (sweepPhase != "run") return true;
            vertical = 1;
            horizontal = Mathf.Sin(t * 2.1f) * .8f;
            bits |= 1 << 1;
            if (Mathf.Repeat(t, 1.9f) < Motor.Tick) bits |= 1 << 5;
            if (Mathf.Repeat(t + .9f, 2.7f) < Motor.Tick) bits |= 1 << 6;
            return true;
        }

        PhaseStats Phase()
        {
            if (!phaseStats.TryGetValue(sweepPhase, out var stats)) phaseStats[sweepPhase] = stats = new PhaseStats();
            return stats;
        }

        // Called from the reticle painter on Repaint with the crosshair centre in GUI units.
        void NoteReticle(Vector2 drawn, float w, float h)
        {
            if (frameLogPath == null) return;
            float scale = Screen.height / h;
            var stats = Phase();
            stats.Frames++;
            stats.Offset.Add((drawn - new Vector2(w / 2, h / 2)).magnitude * scale);
            if (!float.IsNaN(lastReticle.x)) stats.Jump.Add((drawn - lastReticle).magnitude * scale);
            lastReticle = drawn;
            Vector3 eyeAhead = predicted.Position - display;
            stats.Lag.Add(new Vector2(eyeAhead.x, eyeAhead.z).magnitude * 100);
            float roll = Mathf.Abs(Mathf.DeltaAngle(0, view.transform.eulerAngles.z));
            stats.Roll = Mathf.Max(stats.Roll, roll);
            stats.FovMin = Mathf.Min(stats.FovMin, view.fieldOfView);
            stats.FovMax = Mathf.Max(stats.FovMax, view.fieldOfView);
            stats.ReticleRange += Vector3.Distance(view.transform.position, reticleWorld);
            if (burstIndex >= 0 && burstIndex < burstReticle.Length) burstReticle[burstIndex] = (drawn - new Vector2(w / 2, h / 2)) * scale;
        }

        void NoteCorrection(float metres)
        {
            if (frameLogPath == null || metres <= .001f) return;
            corrections++;
            correctionSum += metres;
            correctionMax = Mathf.Max(correctionMax, metres);
        }

        void FlushViewDiagnostics()
        {
            if (frameLogPath == null) return;
            static float P(List<float> list, float q)
            {
                if (list.Count == 0) return 0;
                list.Sort();
                return list[Mathf.Min(list.Count - 1, (int)(q * list.Count))];
            }
            foreach (var pair in phaseStats)
            {
                var s = pair.Value;
                if (s.Frames < 10) continue;
                float mean = 0; foreach (var v in s.Offset) mean += v; mean /= s.Offset.Count;
                File.AppendAllText(frameLogPath, string.Format(CultureInfo.InvariantCulture,
                    "  view {0} frames={1} reticle_off_px mean={2:F1} p95={3:F1} max={4:F1} reticle_jump_px p50={5:F1} p95={6:F1} max={7:F1} eye_ahead_cm p50={8:F1} p95={9:F1} roll_max={10:F2} fov={11:F1}-{12:F1} aim_m={13:F1}\n",
                    pair.Key, s.Frames, mean, P(s.Offset, .95f), P(s.Offset, 1), P(s.Jump, .5f), P(s.Jump, .95f), P(s.Jump, 1),
                    P(s.Lag, .5f), P(s.Lag, .95f), s.Roll, s.FovMin, s.FovMax, s.ReticleRange / s.Frames));
            }
            phaseStats.Clear();
            lastReticle = new Vector2(float.NaN, float.NaN);
            File.AppendAllText(frameLogPath, string.Format(CultureInfo.InvariantCulture,
                "  corrections count={0} mean_cm={1:F1} max_cm={2:F1} clean_view={3} fps_target={4} vsync={5} msaa={6} quality={7}\n",
                corrections, corrections > 0 ? correctionSum / corrections * 100 : 0, correctionMax * 100, cleanView ? 1 : 0,
                fpsTarget, QualitySettings.vSyncCount, QualitySettings.antiAliasing, QualitySettings.names[QualitySettings.GetQualityLevel()]));
            corrections = 0; correctionSum = correctionMax = 0;
        }

        void UpdateBurst(float now)
        {
            if (burstPath == null || !sweep || burstIndex >= 0) return;
            if (sweepPhase != "rotate" && sweepPhase != "strafe" && sweepPhase != "track") return;
            if (burstTaken.Contains(sweepPhase) || now - sweepStarted < 30) return;
            float local = Mathf.Repeat(now - sweepStarted, 4);
            if (local < 1.6f || local > 2.4f) return;
            burstTaken.Add(sweepPhase);
            burstName = sweepPhase;
            StartCoroutine(Burst());
        }

        System.Collections.IEnumerator Burst()
        {
            int width = Mathf.Min(Screen.width, 960), height = Mathf.Min(Screen.height, 540);
            if (burstFrames == null)
            {
                burstFrames = new Texture2D[8];
                for (int i = 0; i < burstFrames.Length; i++) burstFrames[i] = new Texture2D(width, height, TextureFormat.RGB24, false);
            }
            var region = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            for (burstIndex = 0; burstIndex < burstFrames.Length; burstIndex++)
            {
                yield return new WaitForEndOfFrame();
                burstFrames[burstIndex].ReadPixels(region, 0, 0, false);
                burstDt[burstIndex] = Time.unscaledDeltaTime * 1000;
            }
            burstIndex = -1;
            var notes = new List<string>();
            for (int i = 0; i < burstFrames.Length; i++)
            {
                burstFrames[i].Apply(false);
                File.WriteAllBytes(Path.Combine(burstPath, $"{world.Spec.id}-{burstName}-{i}.png"), burstFrames[i].EncodeToPNG());
                notes.Add(string.Format(CultureInfo.InvariantCulture, "{0} dt_ms={1:F1} reticle_dx={2:F1} reticle_dy={3:F1}", i, burstDt[i], burstReticle[i].x, burstReticle[i].y));
            }
            File.WriteAllLines(Path.Combine(burstPath, $"{world.Spec.id}-{burstName}.txt"), notes);
        }
    }
}
