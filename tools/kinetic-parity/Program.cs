// Reads cases.json from run.mjs: every step gives a wire row (as the server packs it), a command and the
// state shared/rules.js reached from that row. The Motor reads the same row, takes the same step, and any
// difference beyond float precision is reported.
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Trias.Native;
using UnityEngine;

static class Program
{
    // First-person weapon sway and inertia at several frame rates: the per-frame formulas the client used
    // before (old) against KineticView (new). Prints peak values; new must agree within 5% across rates.
    // Model of TriasClient's own-camera path on flat ground, using the real Motor: frames at a fixed rate,
    // commands on the 30 Hz send schedule (SendInput), the view built as before (a line from the previous step,
    // then a 60/s exponential follow) or as now (from the last step toward the next one with the keys held
    // this frame). Measures keydown to 1 cm and to 10 cm of camera travel, and jump press to 1 cm of rise.
    static void Latency()
    {
        var map = new ArenaSpec { id = "lab", length = 400, cover = new BoxSpec[0], launchPads = new PadSpec[0] };
        foreach (float fps in new[] { 60f, 144f })
        {
            var results = new System.Collections.Generic.List<float>[6];
            for (int i = 0; i < 6; i++) results[i] = new System.Collections.Generic.List<float>();
            for (int trial = 0; trial < 40; trial++)
            {
                float press = .5f + trial / 40f * Motor.Tick;
                for (int jump = 0; jump < 2; jump++)
                {
                    float dt = 1 / fps, sendAt = 0, stepAt = -1, display = 0;
                    var predicted = new Motor { Grounded = true, AirJump = true, AirDodge = true };
                    Vector3 stepFrom = predicted.Position;
                    Motor peek = predicted;
                    float[] reached = { -1, -1, -1, -1 };
                    bool queued = false, sentJump = false;
                    for (float now = 0; now < 1.2f; now += dt)
                    {
                        bool down = now >= press;
                        if (down && !sentJump) queued = true;
                        Command Keys(bool withButtons) => new Command
                        {
                            Vertical = jump == 0 && down ? 1 : 0,
                            Bits = withButtons && jump == 1 && queued ? 1 << 5 : 0,
                        };
                        if (now >= sendAt)
                        {
                            float scheduled = Math.Min(sendAt, now);
                            float frac = stepAt < 0 ? 1 : Math.Clamp((scheduled - stepAt) / Motor.Tick, 0, 1);
                            var oldLine = stepAt < 0 ? predicted.Position : Lerp(stepFrom, predicted.Position, frac);
                            stepFrom = oldLine;
                            stepAt = scheduled;
                            var command = Keys(true);
                            if (command.Has(5)) { sentJump = true; queued = false; }
                            predicted.Step(command, map);
                            sendAt = Math.Max(sendAt + Motor.Tick, now - Motor.Tick * .6f);
                        }
                        float f = Math.Clamp((now - stepAt) / Motor.Tick, 0, 1);
                        var oldView = Lerp(stepFrom, predicted.Position, f);
                        peek = predicted;
                        peek.Step(Keys(true), map);
                        var newView = Lerp(predicted.Position, peek.Position, f);
                        display += ((jump == 0 ? -oldView.z : oldView.y) - display) * (1 - Mathf.Exp(-dt * 60));
                        float shownOld = display, shownNew = jump == 0 ? -newView.z : newView.y;
                        if (reached[0] < 0 && shownOld >= .01f) reached[0] = now - press;
                        if (reached[1] < 0 && shownNew >= .01f) reached[1] = now - press;
                        if (reached[2] < 0 && shownOld >= .10f) reached[2] = now - press;
                        if (reached[3] < 0 && shownNew >= .10f) reached[3] = now - press;
                    }
                    if (jump == 0) { results[0].Add(reached[0]); results[1].Add(reached[1]); results[2].Add(reached[2]); results[3].Add(reached[3]); }
                    else { results[4].Add(reached[0]); results[5].Add(reached[1]); }
                }
            }
            string Stat(System.Collections.Generic.List<float> list)
            {
                list.Sort();
                return FormattableString.Invariant($"median {list[list.Count / 2] * 1000:F0} ms, worst {list[^1] * 1000:F0} ms");
            }
            Console.WriteLine($"LATENCY {fps} fps  move 1 cm  old {Stat(results[0])}  new {Stat(results[1])}");
            Console.WriteLine($"LATENCY {fps} fps  move 10 cm old {Stat(results[2])}  new {Stat(results[3])}");
            Console.WriteLine($"LATENCY {fps} fps  jump 1 cm  old {Stat(results[4])}  new {Stat(results[5])}");
        }
    }

    static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

    static bool Views()
    {
        float[] rates = { 30, 60, 144, 240 };
        var oldSway = new float[rates.Length]; var newSway = new float[rates.Length];
        var oldInertia = new float[rates.Length]; var newInertia = new float[rates.Length];
        for (int r = 0; r < rates.Length; r++)
        {
            float dt = 1 / rates[r], a = 0, b = 0;
            // A 0.4 s turn at 240 deg/s, Pulse temperament (sway rate 17, scale 1).
            for (float t = 0; t < .4f; t += dt)
            {
                float degrees = 240 * dt;
                a = Mathf.Lerp(a, Mathf.Clamp(degrees * -.48f, -5.5f, 5.5f), 1 - Mathf.Exp(-dt * 17));
                b = KineticView.Sway(b, degrees, dt, -.48f, 5.5f, 17);
                oldSway[r] = Math.Max(oldSway[r], Math.Abs(a)); newSway[r] = Math.Max(newSway[r], Math.Abs(b));
            }
            // Sprint start from rest: the Motor's 30 Hz velocity steps (70 m/s^2 toward 10.1 m/s).
            Vector3 lagOld = Vector3.zero, lagNew = Vector3.zero, last = Vector3.zero;
            for (float t = 0; t < .5f; t += dt)
            {
                int step = (int)(t / (1f / 30)), next = step + 1;
                float speed = Math.Min(10.1f, step * 70f / 30), upcoming = Math.Min(10.1f, next * 70f / 30);
                var velocity = new Vector3(speed, 0, 0);
                lagOld = Vector3.Lerp(lagOld, Vector3.ClampMagnitude(velocity - last, 3), 1 - Mathf.Exp(-dt * 17));
                last = velocity;
                lagNew = KineticView.Inertia(lagNew, new Vector3((upcoming - speed) * 30, 0, 0), dt, 17);
                oldInertia[r] = Math.Max(oldInertia[r], lagOld.magnitude); newInertia[r] = Math.Max(newInertia[r], lagNew.magnitude);
            }
        }
        bool ok = true;
        for (int r = 0; r < rates.Length; r++)
        {
            Console.WriteLine(FormattableString.Invariant(
                $"VIEW {rates[r],3} fps  sway old={oldSway[r]:F3} new={newSway[r]:F3}   inertia old={oldInertia[r]:F3} new={newInertia[r]:F3}"));
            ok &= Math.Abs(newSway[r] / newSway[1] - 1) < .05f && Math.Abs(newInertia[r] / newInertia[1] - 1) < .05f;
        }
        Console.WriteLine("VIEW frame-rate independent: " + (ok ? "yes" : "NO"));
        return ok;
    }

    static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var options = new JsonSerializerOptions { IncludeFields = true };
        using var document = JsonDocument.Parse(File.ReadAllText(args[0]));
        var arenas = document.RootElement.GetProperty("arenas");
        int steps = 0, failures = 0;
        foreach (var scenario in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            string name = scenario.GetProperty("name").GetString();
            var map = arenas.GetProperty(scenario.GetProperty("arena").GetString()).Deserialize<ArenaSpec>(options);
            map.cover ??= new BoxSpec[0];
            map.launchPads ??= new PadSpec[0];
            // Unity's JsonUtility fills a missing serializable class field with an empty instance; do the same.
            map.terrain ??= new HeightData();
            int index = 0;
            foreach (var step in scenario.GetProperty("steps").EnumerateArray())
            {
                var motor = new Motor();
                motor.Read(WireValue.Parse(step.GetProperty("row").GetRawText()));
                var c = step.GetProperty("command");
                var command = new Command
                {
                    Bits = c.GetProperty("bits").GetInt32(), Horizontal = c.GetProperty("x").GetSingle(),
                    Vertical = c.GetProperty("z").GetSingle(), Yaw = c.GetProperty("yaw").GetSingle(),
                };
                var cue = motor.Step(command, map);
                var e = step.GetProperty("expected");
                string problem = null;
                void Near(string key, float actual, float tolerance)
                {
                    float wanted = e.GetProperty(key).GetSingle();
                    if (problem == null && Math.Abs(actual - wanted) > tolerance) problem = $"{key} {actual:F5} != {wanted:F5}";
                }
                void Same(string key, object actual)
                {
                    string wanted = e.GetProperty(key).ToString().ToLowerInvariant();
                    if (problem == null && actual.ToString().ToLowerInvariant() != wanted) problem = $"{key} {actual} != {wanted}";
                }
                Near("x", motor.Position.x, 2e-3f); Near("y", motor.Position.y, 2e-3f); Near("z", motor.Position.z, 2e-3f);
                Near("vx", motor.Velocity.x, 5e-3f); Near("vy", motor.Velocity.y, 5e-3f); Near("vz", motor.Velocity.z, 5e-3f);
                Near("dodgeCooldown", motor.DodgeCooldown, 1e-4f); Near("dodgeTime", motor.DodgeTime, 1e-4f);
                Same("grounded", motor.Grounded); Same("airJump", motor.AirJump); Same("airDodge", motor.AirDodge); Same("crouch", motor.Crouch);
                Same("jumpBuffer", motor.JumpBuffer); Same("dodgeBuffer", motor.DodgeBuffer); Same("coyote", motor.Coyote);
                string signal = e.GetProperty("signal").GetString() ?? "";
                string expectedCue = signal switch { "jump" => "Jump", "double" => "Double", "dodge" => "Dodge", "launch" => "Launch", _ => "None" };
                if (problem == null && cue.ToString() != expectedCue) problem = $"cue {cue} != {expectedCue}";
                steps++;
                if (problem != null && failures++ < 12) Console.WriteLine($"MISMATCH {name} step {index}: {problem}");
                index++;
            }
        }
        Console.WriteLine($"PARITY steps={steps} mismatches={failures}");
        bool views = Views();
        Latency();
        return failures == 0 && views ? 0 : 1;
    }
}
