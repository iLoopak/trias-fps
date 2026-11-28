using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Trias.Native;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Trias.Preview
{
    // Regression checks for first-person visual stability, run in batch mode:
    //   Unity -batchmode -quit -projectPath unity-vfx-preview -executeMethod Trias.Preview.ViewStabilityCheck.Run
    // Exits with 1 when a check fails. They exercise ViewComposer, the code the match camera runs.
    public static class ViewStabilityCheck
    {
        static readonly List<string> report = new();
        static int failures;

        public static void Run()
        {
            report.Clear(); failures = 0;
            Guard(FrameRateIndependentView);
            Guard(Reconciliation);
            Guard(NoAccumulation);
            Guard(Presets);
            Guard(FrameRateIndependentWeapon);
            Guard(ReticleCentre);
            Guard(CameraOwnership);
            Guard(SceneAudit);
            Guard(NoTemporalHistory);
            foreach (var line in report) Debug.Log("[view-check] " + line);
            Debug.Log($"[view-check] {(failures == 0 ? "PASS" : "FAIL")} ({failures} failed)");
            string output = Environment.GetEnvironmentVariable("TRIAS_VIEW_CHECK_REPORT");
            if (!string.IsNullOrEmpty(output)) File.WriteAllLines(output, report.Append(failures == 0 ? "PASS" : $"FAIL {failures}"));
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void Guard(Action check)
        {
            try { check(); }
            catch (Exception error) { Fail(check.Method.Name + " threw " + error.Message); }
        }

        static void Check(bool ok, string what)
        {
            report.Add((ok ? "ok   " : "FAIL ") + what);
            if (!ok) failures++;
        }

        static void Fail(string what) => Check(false, what);
        static string F(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

        // Mirrors TriasClient's input schedule: steps at 30 Hz on `sendAt`, the view on the step line.
        sealed class Sim
        {
            internal Vector3 Predicted, StepFrom, Correction, Display;
            internal float StepAt = -1, SendAt;
            internal Vector3 Velocity;

            internal void Frame(float now, float dt, bool legacy)
            {
                while (now >= SendAt)
                {
                    float scheduled = Mathf.Min(SendAt, now);
                    StepFrom = ViewComposer.Stepped(StepFrom, Predicted, StepAt, scheduled);
                    StepAt = scheduled;
                    Predicted += Velocity * Motor.Tick;
                    SendAt = Mathf.Max(SendAt + Motor.Tick, now - Motor.Tick * .6f);
                }
                Correction = ViewComposer.FadeCorrection(Correction, dt);
                var stepped = ViewComposer.Stepped(StepFrom, Predicted, StepAt, now);
                // legacy: the second exponential chase removed in this pass.
                Display = legacy ? Vector3.Lerp(Display, stepped, 1 - Mathf.Exp(-dt * 60)) : stepped + Correction;
            }

            internal void Correct(Vector3 shift)
            {
                Predicted += shift;
                StepFrom += shift;
                Correction = ViewComposer.AddCorrection(Correction, shift);
            }
        }

        static float[] Frames(float fps, float jitter, float seconds, int seed)
        {
            var random = new System.Random(seed);
            var list = new List<float>();
            for (float t = 0; t < seconds;)
            {
                float dt = 1 / fps * (1 + (float)(random.NextDouble() * 2 - 1) * jitter);
                list.Add(dt); t += dt;
            }
            return list.ToArray();
        }

        // Judder = mean frame-to-frame change of the shown speed relative to the true speed (as the playtest log).
        static (float judder, float lagMax) Strafe(float fps, float jitter, bool legacy)
        {
            var sim = new Sim { Velocity = new Vector3(7.55f, 0, 0) };
            float now = 0, last = -1, error = 0, lagMax = 0; int samples = 0;
            var frames = Frames(fps, jitter, 4, 11);
            Vector3 previous = Vector3.zero;
            for (int i = 0; i < frames.Length; i++)
            {
                now += frames[i];
                sim.Frame(now, frames[i], legacy);
                if (now < 1) { previous = sim.Display; continue; }
                float shown = (sim.Display - previous).magnitude / frames[i];
                if (last >= 0) { error += Mathf.Abs(shown - last) / 7.55f; samples++; }
                last = shown; previous = sim.Display;
                lagMax = Mathf.Max(lagMax, (now * 7.55f) - sim.Display.x);
            }
            return (error / Mathf.Max(1, samples) * 100, lagMax);
        }

        static void FrameRateIndependentView()
        {
            foreach (float fps in new[] { 60f, 75f, 120f, 144f, 240f })
            {
                var now = Strafe(fps, .15f, false);
                var old = Strafe(fps, .15f, true);
                report.Add($"     strafe {fps} fps ±15%: judder {F(now.judder)}% (with the removed second chase {F(old.judder)}%), lag behind the continuous path {F(now.lagMax * 100)} cm (with the second chase {F(old.lagMax * 100)} cm)");
                Check(now.judder < 1.5f, $"own view moves evenly at {fps} fps with frame-time jitter (judder {F(now.judder)}% < 1.5%)");
                // At most one input step of render interpolation behind the true path, plus the send schedule.
                Check(now.lagMax <= 7.55f * Motor.Tick * 1.75f, $"own view lags at most ~1 step at {fps} fps ({F(now.lagMax * 100)} cm)");
            }
            var slow = Strafe(30, 0, false);
            report.Add($"     strafe 30 fps steady: judder {F(slow.judder)}%");
        }

        static void Reconciliation()
        {
            var sim = new Sim { Velocity = new Vector3(7.55f, 0, 0) };
            float now = 0, dt = 1 / 60f, worst = 0;
            Vector3 previous = Vector3.zero;
            for (int i = 0; i < 240; i++)
            {
                now += dt;
                if (i == 90) sim.Correct(new Vector3(0, 0, .10f));
                sim.Frame(now, dt, false);
                if (i > 60) worst = Mathf.Max(worst, Mathf.Abs((sim.Display - previous).z));
                previous = sim.Display;
            }
            Check(worst < .10f * .25f, $"a 10 cm correction is spread over frames (largest per-frame sideways step {F(worst * 100)} cm < 2.5 cm)");
            Check(sim.Correction.magnitude < .001f, $"the correction is gone within 2.5 s ({F(sim.Correction.magnitude * 1000)} mm)");
            Check(Mathf.Abs(sim.Display.z - .10f) < .001f, "after the fade the view sits exactly on the corrected prediction");
            var snap = new Sim();
            snap.Correct(new Vector3(3, 0, 0));
            Check(snap.Correction == Vector3.zero, "a 3 m correction (knockback, pad) is shown at once, not smoothed");
        }

        static void NoAccumulation()
        {
            var random = new System.Random(5);
            var tuning = ViewComposer.For(ViewComposer.Style.Cinematic);
            float fov = 70;
            Vector3 correction = Vector3.zero;
            for (int i = 0; i < 20000; i++)
            {
                float dt = .004f + (float)random.NextDouble() * .03f;
                var velocity = new Vector3((float)random.NextDouble() * 20 - 10, 0, (float)random.NextDouble() * 20 - 10);
                fov = ViewComposer.FollowFov(fov, ViewComposer.Fov(tuning, 70, velocity.magnitude, (float)random.NextDouble(), (float)random.NextDouble() * 1.7f, 1, false), dt);
                if (i % 7 == 0) correction = ViewComposer.AddCorrection(correction, new Vector3((float)random.NextDouble() - .5f, 0, 0) * .2f);
                correction = ViewComposer.FadeCorrection(correction, dt);
            }
            for (int i = 0; i < 240; i++)
            {
                fov = ViewComposer.FollowFov(fov, ViewComposer.Fov(tuning, 70, 0, 0, 0, 1, false), 1 / 60f);
                correction = ViewComposer.FadeCorrection(correction, 1 / 60f);
            }
            Check(Mathf.Abs(fov - 70) < .01f, $"FOV returns to the base value at rest ({F(fov)})");
            Check(correction.magnitude < 1e-4f, "reconciliation offset returns to zero at rest");
            Check(ViewComposer.Roll(tuning, Vector3.zero, Quaternion.identity, 1, false) == 0, "no roll at rest");
        }

        static void Presets()
        {
            var competitive = ViewComposer.For(ViewComposer.Style.Competitive);
            var cinematic = ViewComposer.For(ViewComposer.Style.Cinematic);
            var aim = Quaternion.identity;
            Check(ViewComposer.Roll(competitive, new Vector3(10, 0, 0), aim, 1, false) == 0, "competitive: no strafe roll");
            float span = ViewComposer.Fov(competitive, 70, 10.1f, 1, 1.7f, 1, false) - 70;
            Check(span <= 2.5f, $"competitive: sprint + dodge + recoil widen the FOV by at most 2.5° ({F(span)}°)");
            Check(ViewComposer.Fov(competitive, 70, 0, 0, 1.7f, 1, false) == 70, "competitive: recoil does not pump the FOV");
            Check(competitive.HeadBob <= .5f, "competitive: head bob at most half");
            Check(Mathf.Abs(ViewComposer.Roll(cinematic, new Vector3(10, 0, 0), aim, 1, false)) > 1, "cinematic keeps the strafe roll");
            Check(ViewComposer.Fov(cinematic, 70, 10.1f, 1, 1.7f, 1, false) - 70 > 6, "cinematic keeps the full FOV motion");
            Check(ViewComposer.Fov(cinematic, 70, 10.1f, 1, 1.7f, 0, false) == 70, "camera motion 0 removes every FOV change");
        }

        static void FrameRateIndependentWeapon()
        {
            float turn = 300; // deg/s
            var rates = new[] { 30f, 60f, 144f, 240f }.Select(fps => ViewComposer.TurnRate(0, turn / fps, 1 / fps)).ToArray();
            Check(rates.Max() - rates.Min() < .001f, $"weapon sway input is the same at 30-240 fps ({F(rates.Min())}-{F(rates.Max())})");
            var accel = new[] { 30f, 60f, 144f, 240f }.Select(fps => ViewComposer.StepAcceleration(new Vector3(30 / fps, 0, 0), Vector3.zero, 1 / fps).x).ToArray();
            Check(accel.Max() - accel.Min() < .001f, $"weapon inertia input is the same at 30-240 fps ({F(accel.Min())}-{F(accel.Max())})");
        }

        static void ReticleCentre()
        {
            foreach (var (width, height) in new[] { (1600, 900), (2560, 1664), (1920, 1080), (1440, 810), (3456, 2234) })
            {
                float scale = Mathf.Min(width / 1280f, height / 720f), w = width / scale, h = height / scale;
                var point = ViewComposer.Reticle(w, h, scale) * scale;
                bool whole = Mathf.Abs(point.x - Mathf.Round(point.x)) < .01f && Mathf.Abs(point.y - Mathf.Round(point.y)) < .01f;
                bool centred = Mathf.Abs(point.x - width / 2f) <= .5f && Mathf.Abs(point.y - height / 2f) <= .5f;
                Check(whole && centred, $"crosshair on a whole pixel at the centre of {width}x{height} ({F(point.x)}, {F(point.y)})");
            }
            string visor = File.ReadAllText("Assets/Scripts/Native/TriasClient.Visor.cs");
            var painter = Regex.Match(visor, @"void PaintReticle\(.*?\n        }\n", RegexOptions.Singleline).Value;
            Check(painter.Contains("ViewComposer.Reticle(") && !painter.Contains("WorldToViewportPoint") && !painter.Contains("WorldToScreenPoint"),
                "the crosshair is not projected from a world point");
        }

        // Writers of the match camera's pose and FOV. A new writer must be added here deliberately.
        static readonly Dictionary<string, int> CameraWriters = new()
        {
            ["TriasClient.Feel.cs"] = 2,          // UpdateFeel: pose and FOV of the live first-person view
            ["TriasClient.cs"] = 3,               // Chase position + LookAt (vehicle, spectator, replay) and the replay LookAt
            ["TriasClient.Lobby.cs"] = 3,         // hangar camera in the lobby
            ["TriasClient.Presentation.cs"] = 1,  // FOV reset to the base value on deployment
        };

        static void CameraOwnership()
        {
            var writer = new Regex(@"view\.(fieldOfView\s*=[^=]|transform\.(position|rotation|localPosition|localRotation)\s*=[^=]|transform\.(SetPositionAndRotation|LookAt|Rotate|Translate)\s*\()");
            var found = new Dictionary<string, int>();
            foreach (var path in Directory.GetFiles("Assets/Scripts/Native", "*.cs"))
            {
                int count = 0;
                foreach (var line in File.ReadAllLines(path))
                    if (!line.TrimStart().StartsWith("//") && writer.IsMatch(line)) count++;
                if (count > 0) found[Path.GetFileName(path)] = count;
            }
            foreach (var pair in found)
            {
                if (pair.Key == "ArtReviewProbe.cs") continue; // editor review fixture, its own camera
                CameraWriters.TryGetValue(pair.Key, out var allowed);
                Check(pair.Value <= allowed, $"camera writers in {pair.Key}: {pair.Value} (allowed {allowed})");
            }
            string feel = File.ReadAllText("Assets/Scripts/Native/TriasClient.Feel.cs");
            Check(!Regex.IsMatch(feel, @"view\.transform\.(position|rotation|localPosition|localRotation)\s*\+="), "no camera offset is accumulated onto the previous frame's transform");
            string client = File.ReadAllText("Assets/Scripts/Native/TriasClient.cs");
            Check(Regex.Matches(client, @"display\s*=\s*Vector3\.Lerp\(display,\s*(Stepped|predicted\.Position)").Count <= 1,
                "the own view is not smoothed a second time on top of the step interpolation (only the unpredicted vehicle view chases)");
        }

        static void SceneAudit()
        {
            if (!File.Exists(BuildTrias.Scene)) { Fail("full game scene missing: " + BuildTrias.Scene); return; }
            var scene = EditorSceneManager.OpenScene(BuildTrias.Scene, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var cameras = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToArray();
            var listeners = roots.SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).ToArray();
            var clients = roots.SelectMany(r => r.GetComponentsInChildren<TriasClient>(true)).ToArray();
            Check(cameras.Length == 1, $"one camera renders the match ({cameras.Length})");
            Check(listeners.Length == 1, $"one audio listener ({listeners.Length})");
            Check(clients.Length == 1, $"one client draws the HUD ({clients.Length})");
            var painters = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(b => b != null && b.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) != null)
                .Select(b => b.GetType().Name).ToArray();
            Check(painters.Length == 1 && painters[0] == nameof(TriasClient), $"only TriasClient paints IMGUI ({string.Join(",", painters)})");
            var images = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(b => b != null && b.GetType().GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) != null).ToArray();
            Check(images.Length == 0, $"no image effect is baked into the scene ({images.Length}); VisorComposite is the only one, added at run time");
        }

        static void NoTemporalHistory()
        {
            var fields = typeof(VisorComposite).GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => typeof(Texture).IsAssignableFrom(f.FieldType)).Select(f => f.Name).ToArray();
            Check(fields.Length == 0, $"the visor compositor keeps no texture between frames, so nothing is accumulated ({string.Join(",", fields)})");
            string shader = File.ReadAllText("Assets/Resources/TRIAS/VisorComposite.shader");
            Check(!Regex.IsMatch(shader, @"_History|_Previous|_Prev|MotionVector|_CameraMotionVectorsTexture"), "the visor shader reads no history or motion vectors");
            string visor = File.ReadAllText("Assets/Scripts/Native/VisorComposite.cs");
            Check(!visor.Contains("MotionVectors"), "no motion vectors are requested");
            int temporal = QualitySettings.names.Length;
            report.Add($"     quality levels: {string.Join(", ", QualitySettings.names)} (Built-in RP: MSAA only, no TAA)");
            Check(temporal > 0, "quality levels present");
        }
    }
}
