#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Trias.Native
{
    // Editor-only art review fixture. Builds production arenas through TriasWorld, holds fixed
    // eye-height poses with the first-person weapon and production pilots, and writes HUD-free PNGs.
    // Started by Trias.Preview.ArtReview.Run in batch mode; see ART-DIRECTION.md (Art review loop).
    public sealed class ArtReviewProbe : MonoBehaviour
    {
        internal sealed class Shot
        {
            internal string Key, Arena, Mode = "tdm", Weapon = "pulse";
            internal Func<TriasWorld, Vector3> Eye, Aim;
            internal (string Kit, int Team, Func<TriasWorld, Vector3> At, float Yaw)[] Pilots = Array.Empty<(string, int, Func<TriasWorld, Vector3>, float)>();
            internal bool Weaponless;
            // Spawns review props (Horde bodies, vehicles) under the given parent.
            internal Action<TriasWorld, Transform> Extras;
            internal float Fov = 70;
            // Fires a gameplay event through the shipped RelayEffects and captures it EventAge seconds later.
            internal Action<Trias.Preview.RelayEffects, TriasWorld> Event;
            internal float EventAge;
            // "equip" or "land": renders the first-person timeline through WeaponHandling.Grip at fixed times.
            internal string Motion;
        }

        static readonly float[] EquipFrames = { 0, .06f, .12f, .18f, .24f, .32f, .45f, .7f };
        static readonly float[] LandFrames = { 0, .03f, .06f, .1f, .16f, .26f, .4f };

        // Set by Trias.Preview.ArtReview before the probe starts; null leaves event shots out.
        public static Trias.Preview.RelayEffects Effects;
        static readonly Vector3 EventSpot = new Vector3(-.6f, 6.6f, -9.2f);
        static Shot EventShot(string key, float age, Action<Trias.Preview.RelayEffects, TriasWorld> fire) => new Shot
        {
            Key = key, Arena = "relay", Eye = P(1.4f, 8.18f, -14.6f), Aim = P(-.6f, 7.4f, -9.2f), Fov = 60, Event = fire, EventAge = age,
        };

        static Func<TriasWorld, Vector3> P(float x, float y, float z) => _ => new Vector3(x, y, z);
        static Func<TriasWorld, Vector3> G(float x, float z, float lift) => w => new Vector3(x, w.Height(x, z) + lift, z);

        internal static readonly Shot[] Shots =
        {
            new Shot { Key = "relay-transfer", Arena = "relay", Eye = P(1.7f, 8.18f, -15.8f), Aim = P(.3f, 7.3f, -5.7f),
                Pilots = new[] { ("vector", 1, P(-.6f, 6.6f, -9.2f), 160f) } },
            // Character lineup on the transfer deck: Vektor, Bašta, Přízrak, Poštolka in three factions.
            new Shot { Key = "relay-pilots", Arena = "relay", Weaponless = true, Fov = 38, Eye = P(-.3f, 7.75f, -15.9f), Aim = P(-.3f, 7.4f, -10.5f),
                Pilots = new[] { ("vector", 0, P(-2.4f, 6.6f, -11f), 195f), ("bulwark", 1, P(-.95f, 6.6f, -11f), 185f),
                    ("wraith", 2, P(.45f, 6.6f, -11f), 175f), ("kestrel", 0, P(1.85f, 6.6f, -11f), 165f) } },
            // Horde lineup on the transfer deck: runner, crusher, spitter, thrower.
            new Shot { Key = "relay-horde", Arena = "relay", Weaponless = true, Fov = 38, Eye = P(-.3f, 7.75f, -15.9f), Aim = P(-.3f, 7.3f, -10.5f),
                Extras = (w, home) =>
                {
                    string[] kinds = { "runner", "crusher", "spitter", "thrower" };
                    for (int i = 0; i < kinds.Length; i++)
                    {
                        var at = TriasWorld.Group(home, kinds[i]);
                        at.SetPositionAndRotation(new Vector3(-2.5f + i * 1.45f, 6.6f, -11f), Quaternion.Euler(0, 180 + (i - 1.5f) * 12, 0));
                        new HordeBody(w, at, kinds[i], i).Pose(40, 2.2f, .3f + i * .2f, i, 0);
                    }
                } },
            // Vehicles: skimmer and heavy bastion, three-quarter view across the deck.
            new Shot { Key = "relay-vehicles", Arena = "relay", Weaponless = true, Fov = 50, Eye = P(4.5f, 8.6f, -18.5f), Aim = P(-.5f, 7.2f, -10.5f),
                Extras = (w, home) =>
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var v = TriasModels.Vehicle(w, i == 1, i, out _);
                        v.SetParent(home, false);
                        v.SetPositionAndRotation(new Vector3(i == 0 ? 2.2f : -3.4f, 6.6f, i == 0 ? -12.6f : -9.8f), Quaternion.Euler(0, i == 0 ? 215 : 160, 0));
                    }
                } },
            // Equipment stations: the two nearest pickups to the transfer deck, framed at eye height.
            new Shot { Key = "relay-pickup-a", Arena = "relay", Weaponless = true, Fov = 34,
                Eye = w => w.Pickups[0].position + new Vector3(1.6f, .75f, -1.6f), Aim = w => w.Pickups[0].position + Vector3.up * .85f },
            new Shot { Key = "relay-pickup-b", Arena = "relay", Weaponless = true, Fov = 34,
                Eye = w => w.Pickups[w.Pickups.Length - 1].position + new Vector3(1.6f, .75f, -1.6f), Aim = w => w.Pickups[w.Pickups.Length - 1].position + Vector3.up * .85f },
            new Shot { Key = "relay-gallery", Arena = "relay", Weapon = "rift", Eye = P(7.5f, 4.88f, -14.75f), Aim = P(-.2f, 4.2f, -1.95f),
                Pilots = new[] { ("kestrel", 2, P(1.9f, 3.3f, -9.4f), 200f) } },
            new Shot { Key = "relay-deck", Arena = "relay", Weapon = "scatter", Eye = P(-13.5f, 1.58f, -20.5f), Aim = P(-4f, 2.2f, -9f) },
            new Shot { Key = "vanta-approach", Arena = "foundry", Weapon = "arc", Eye = P(-10, 1.58f, -24.5f), Aim = P(-6, 1.5f, -16),
                Pilots = new[] { ("bulwark", 0, P(-7.3f, 0, -15.8f), 200f) } },
            new Shot { Key = "vanta-works", Arena = "foundry", Weapon = "scatter", Eye = P(5, 1.58f, -22), Aim = P(0, 7, 30) },
            new Shot { Key = "vanta-terrace", Arena = "foundry", Weapon = "pulse", Eye = P(1.5f, 1.58f, -9.5f), Aim = P(-1.5f, .8f, -2) },
            new Shot { Key = "meridian-terrace", Arena = "basin", Weapon = "arc", Eye = P(-3, 1.58f, -15), Aim = P(0, .9f, -6.4f) },
            new Shot { Key = "meridian-approach", Arena = "basin", Weapon = "lance", Eye = P(-8.8f, 1.58f, -37.4f), Aim = P(-5.3f, 1.4f, -31) },
            EventShot("fx-elimination", .14f, (fx, w) => fx.Elimination(EventSpot, w.TeamLight[1].color)),
            EventShot("fx-elimination-mid", .3f, (fx, w) => fx.Elimination(EventSpot, w.TeamLight[1].color)),
            EventShot("fx-elimination-late", .55f, (fx, w) => fx.Elimination(EventSpot, w.TeamLight[1].color)),
            EventShot("fx-respawn", .22f, (fx, w) => fx.Reentry(EventSpot, w.TeamLight[0].color, false, 0)),
            EventShot("fx-dodge", .1f, (fx, w) => fx.Motion(EventSpot, w.TeamLight[2].color, MotionCue.Dodge, false, 1, 2)),
            EventShot("fx-echo-rupture-late", .36f, (fx, w) => fx.Phase(EventSpot, w.TeamLight[0].color, "rupture", false, 1)),
            EventShot("fx-echo-rupture", .16f, (fx, w) => fx.Phase(EventSpot, w.TeamLight[0].color, "rupture", false, 1)),
            new Shot { Key = "motion-pulse-equip", Arena = "relay", Weapon = "pulse", Motion = "equip", Eye = P(-13.5f, 1.58f, -20.5f), Aim = P(-4f, 1.4f, -9f) },
            new Shot { Key = "motion-scatter-equip", Arena = "relay", Weapon = "scatter", Motion = "equip", Eye = P(-13.5f, 1.58f, -20.5f), Aim = P(-4f, 1.4f, -9f) },
            new Shot { Key = "motion-lance-equip", Arena = "relay", Weapon = "lance", Motion = "equip", Eye = P(-13.5f, 1.58f, -20.5f), Aim = P(-4f, 1.4f, -9f) },
            new Shot { Key = "motion-arc-equip", Arena = "relay", Weapon = "arc", Motion = "equip", Eye = P(-13.5f, 1.58f, -20.5f), Aim = P(-4f, 1.4f, -9f) },
            new Shot { Key = "motion-rift-equip", Arena = "relay", Weapon = "rift", Motion = "equip", Eye = P(-13.5f, 1.58f, -20.5f), Aim = P(-4f, 1.4f, -9f) },
            new Shot { Key = "motion-scatter-land", Arena = "relay", Weapon = "scatter", Motion = "land", Eye = P(-13.5f, 1.58f, -20.5f), Aim = P(-4f, 1.4f, -9f) },
            new Shot { Key = "motion-lance-land", Arena = "relay", Weapon = "lance", Motion = "land", Eye = P(-13.5f, 1.58f, -20.5f), Aim = P(-4f, 1.4f, -9f) },
            new Shot { Key = "kordilera-crossing", Arena = "island", Mode = "ctf", Eye = G(27.5f, 11.3f, 1.58f), Aim = G(23.4f, 6.6f, .68f) },
        };

        Camera view;
        VisorComposite visor;
        TriasWorld world;
        Catalog catalog;
        string output;

        // TRIAS_ART_TUNE="key=1.2;amb=.5;exp=.9;ao=.8;con=1.1" multiplies key/ambient and overrides grade.
        void Tune()
        {
            var text = Environment.GetEnvironmentVariable("TRIAS_ART_TUNE");
            if (string.IsNullOrEmpty(text)) return;
            var v = text.Split(';').Where(p => p.Contains('=')).ToDictionary(p => p.Split('=')[0].Trim(), p => float.Parse(p.Split('=')[1], CultureInfo.InvariantCulture));
            if (v.TryGetValue("key", out var key))
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional) light.intensity *= key;
            if (v.TryGetValue("amb", out var amb))
            {
                RenderSettings.ambientSkyColor *= amb; RenderSettings.ambientEquatorColor *= amb; RenderSettings.ambientGroundColor *= amb;
            }
            if (v.TryGetValue("refl", out var refl)) RenderSettings.reflectionIntensity *= refl;
            if (v.TryGetValue("sat", out var sat))
            {
                Color Desaturate(Color c) { float l = c.grayscale; return Color.Lerp(new Color(l, l, l), c, sat); }
                RenderSettings.ambientSkyColor = Desaturate(RenderSettings.ambientSkyColor);
                RenderSettings.ambientEquatorColor = Desaturate(RenderSettings.ambientEquatorColor);
                RenderSettings.ambientGroundColor = Desaturate(RenderSettings.ambientGroundColor);
            }
            // Architecture surface experiments: ascale (period multiplier), seam, stone (seam tone), pvar, wear.
            var seen = new HashSet<Material>();
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            foreach (var m in renderer.sharedMaterials)
            {
                if (m == null || m.shader.name != "TRIAS/Architecture" || !seen.Add(m)) continue;
                if (v.TryGetValue("ascale", out var s)) m.SetFloat("_Scale", m.GetFloat("_Scale") * s);
                if (v.TryGetValue("seam", out var seam)) m.SetFloat("_SeamStrength", seam);
                if (v.TryGetValue("stone", out var tone)) m.SetFloat("_SeamTone", tone);
                if (v.TryGetValue("pvar", out var pv)) m.SetFloat("_PlateVar", m.GetFloat("_PlateVar") * pv);
                if (v.TryGetValue("arate", out var ar)) m.SetFloat("_AltRate", m.GetFloat("_AltRate") * ar);
                if (v.TryGetValue("nrm", out var nr)) m.SetFloat("_NormalStrength", m.GetFloat("_NormalStrength") * nr);
            }
            visor.Tune(v.TryGetValue("exp", out var e) ? e : 0, v.TryGetValue("con", out var c) ? c : 0, v.TryGetValue("ao", out var a) ? a : -1);
        }

        IEnumerator Start()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            output = Environment.GetEnvironmentVariable("TRIAS_ART_OUT");
            string only = Environment.GetEnvironmentVariable("TRIAS_ART_ONLY");
            int width = int.TryParse(Environment.GetEnvironmentVariable("TRIAS_ART_WIDTH"), out var w) ? w : 1600;
            int height = width * 9 / 16;
            Directory.CreateDirectory(output);
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            QualitySettings.vSyncCount = 0;
            catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TRIAS/World").text);

            view = gameObject.AddComponent<Camera>();
            view.tag = "MainCamera";
            view.renderingPath = RenderingPath.Forward;
            view.nearClipPlane = .037f;
            view.farClipPlane = 235;
            view.allowMSAA = true;
            visor = gameObject.AddComponent<VisorComposite>();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var log = new List<string>();

            foreach (var group in Shots.Where(s => string.IsNullOrEmpty(only) || only.Split(',').Any(o => s.Key.StartsWith(o) || s.Arena == o))
                         .GroupBy(s => s.Arena + "|" + s.Mode))
            {
                var first = group.First();
                world?.Dispose();
                yield return null;
                view.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                world = new TriasWorld(catalog.maps.Single(m => m.id == first.Arena), catalog, first.Mode, view);
                visor.InMatch = true;
                visor.Grade(first.Arena);
                Tune();
                yield return null;
                foreach (var shot in group)
                {
                    var props = new List<GameObject>();
                    Transform grip = null;
                    WeaponHandling hands = null;
                    var eye = shot.Eye(world);
                    view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(shot.Aim(world) - eye));
                    view.fieldOfView = shot.Fov;
                    if (!shot.Weaponless)
                    {
                        grip = TriasWorld.Group(view.transform, "First person arms");
                        grip.localPosition = new Vector3(.365f, -.305f, .60f);
                        grip.localRotation = Quaternion.Euler(0, -4, 0);
                        var art = TriasWorld.Group(grip, shot.Weapon);
                        var accent = world.MaterialAt(Catalog.Find(catalog.weapons, shot.Weapon)?.color ?? "#b9fff1", true);
                        TriasModels.Gun(world, art, shot.Weapon, accent);
                        hands = new WeaponHandling(world, art, shot.Weapon, accent, true, "vector", false);
                        hands.Update(WeaponHandling.At(shot.Weapon, 0), 0);
                        props.Add(grip.gameObject);
                    }
                    if (shot.Extras != null)
                    {
                        var home = TriasWorld.Group(world.Actors, "Review extras " + shot.Key);
                        shot.Extras(world, home);
                        props.Add(home.gameObject);
                    }
                    foreach (var pilot in shot.Pilots)
                    {
                        var root = TriasWorld.Group(world.Actors, "Review pilot " + pilot.Kit);
                        root.SetPositionAndRotation(pilot.At(world), Quaternion.Euler(0, pilot.Yaw, 0));
                        world.Kit.Pilot(root, pilot.Kit, world.TeamLight[pilot.Team], pilot.Team, pilot.Kit == "vector" || pilot.Kit == "bulwark", out var rig);
                        var gun = TriasWorld.Group(root, "Pulse", new Vector3(.17f, 1.36f, .34f));
                        gun.localScale = Vector3.one * .9f;
                        TriasModels.Gun(world, gun, "pulse", world.TeamLight[pilot.Team]);
                        if (rig != null && rig["idle"] != null) { var s = rig["idle"]; s.enabled = true; s.weight = 1; s.time = .4f; rig.Sample(); s.enabled = false; }
                        props.Add(root.gameObject);
                    }
                    for (int frame = 0; frame < 24; frame++)
                    {
                        world.Animate(40 + frame / 60f, 1, 0, eye, 1, 1, 40 + frame / 60f, 719);
                        yield return null;
                    }
                    if (shot.Event != null)
                    {
                        if (Effects == null) log.Add($"{shot.Key}: event skipped (no effects)");
                        else
                        {
                            Effects.listener = view.transform;
                            Effects.ClearTransient();
                            shot.Event(Effects, world);
                            float fired = Time.time;
                            while (Time.time - fired < shot.EventAge)
                            {
                                world.Animate(41, 1, 0, eye, 1, 1, 41, 719);
                                yield return null;
                            }
                        }
                    }
                    if (shot.Motion != null && grip != null)
                    {
                        // Simulate the client's equip or landing timeline at 240 Hz and render the listed instants.
                        var temper = WeaponHandling.Temperament(shot.Weapon);
                        bool landing = shot.Motion == "land";
                        float[] instants = landing ? LandFrames : EquipFrames;
                        float equip = landing ? 0 : 1, settledAt = -1, step = landing ? .1f : 0, stepVelocity = 0, gun = 0, gunVelocity = 0;
                        const float dt = 1f / 240;
                        var curve = new List<string> { "t,equip,landing,pitch,y" };
                        int next = 0;
                        for (int tick = 0; next < instants.Length; tick++)
                        {
                            float t = tick * dt;
                            if (!landing)
                            {
                                float was = equip;
                                equip = Mathf.MoveTowards(equip, 0, dt / temper.RaiseTime);
                                if (was > 0 && equip <= 0) settledAt = t;
                            }
                            step = Mathf.SmoothDamp(step, 0, ref stepVelocity, .15f, Mathf.Infinity, dt);
                            gun = Mathf.SmoothDamp(gun, step * temper.LandingWeight, ref gunVelocity, .055f, Mathf.Infinity, dt);
                            var v = new WeaponHandling.View
                            {
                                Reload = WeaponHandling.At(shot.Weapon, 0), Motion = 1, Inertia = 1, Landing = gun,
                                Presentation = WeaponHandling.Presentation(shot.Weapon, t, equip, 0, 0, step, 1),
                                Settle = settledAt >= 0 ? WeaponHandling.SettleRotation(shot.Weapon, t - settledAt) : Vector3.zero,
                            };
                            WeaponHandling.Grip(v, out var at, out var turn);
                            if (tick % 4 == 0)
                                curve.Add(string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F3},{2:F4},{3:F2},{4:F4}",
                                    t, equip, gun, Mathf.DeltaAngle(0, turn.eulerAngles.x), at.y));
                            if (t + dt * .5f < instants[next]) continue;
                            grip.SetLocalPositionAndRotation(at, turn);
                            hands?.Update(WeaponHandling.At(shot.Weapon, 0), 0);
                            view.targetTexture = target;
                            view.Render();
                            view.targetTexture = null;
                            RenderTexture.active = target;
                            var still = new Texture2D(width, height, TextureFormat.RGB24, false);
                            still.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                            RenderTexture.active = null;
                            File.WriteAllBytes(Path.Combine(output, $"{shot.Key}-{next}.png"), still.EncodeToPNG());
                            Destroy(still);
                            next++;
                        }
                        File.WriteAllLines(Path.Combine(output, shot.Key + ".csv"), curve);
                    }
                    // TRIAS_ART_PICK=1 logs the nearest renderer (by bounds) under a 5x4 screen grid; =2 also admits large distant structures.
                    string pickMode = Environment.GetEnvironmentVariable("TRIAS_ART_PICK");
                    if (pickMode == "1" || pickMode == "2")
                    {
                        var all = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                        for (int gy = 0; gy < 4; gy++)
                        for (int gx = 0; gx < 5; gx++)
                        {
                            var ray = view.ViewportPointToRay(new Vector3(.1f + gx * .2f, .12f + gy * .25f, 0));
                            MeshRenderer best = null; float near = float.MaxValue;
                            foreach (var r in all)
                                if (r.enabled && r.bounds.IntersectRay(ray, out var d) && d < near && d > .3f && (r.bounds.size.magnitude < 60 || gy == 0 || pickMode == "2")) { near = d; best = r; }
                            if (best != null)
                            {
                                string path = best.name; for (var t = best.transform.parent; t != null && path.Length < 160; t = t.parent) path = t.name + "/" + path;
                                log.Add($"  pick {shot.Key} ({gx},{gy}) d={near:F1} {path} mat={best.sharedMaterial?.name} scale={(best.sharedMaterial != null && best.sharedMaterial.HasProperty("_Scale") ? best.sharedMaterial.GetFloat("_Scale") : -1):F1} col={(best.sharedMaterial != null && best.sharedMaterial.HasProperty("_Color") ? ColorUtility.ToHtmlStringRGB(best.sharedMaterial.color) : "-")} mesh={(best.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null ? mf.sharedMesh.name : "-")} bounds={best.bounds.center:F1}/{best.bounds.size:F1}");
                            }
                        }
                    }
                    var started = System.Diagnostics.Stopwatch.StartNew();
                    view.targetTexture = target;
                    view.Render();
                    view.targetTexture = null;
                    double ms = started.Elapsed.TotalMilliseconds;
                    RenderTexture.active = target;
                    var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(output, shot.Key + ".png"), image.EncodeToPNG());
                    Destroy(image);
                    // TRIAS_ART_PERF=<frames> times repeated renders of the same view (editor CPU submission time of
                    // Camera.Render including the visor composite; not GPU time) and reads the render counters.
                    string perfFrames = Environment.GetEnvironmentVariable("TRIAS_ART_PERF");
                    string perf = "";
                    if (int.TryParse(perfFrames, out int frames) && frames > 0)
                    {
                        using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                        using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                        using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                        bool wasEnabled = view.enabled;
                        view.enabled = false;
                        var times = new List<double>();
                        long drawSum = 0, passSum = 0, triSum = 0; int counted = 0;
                        for (int f = 0; f < frames + 10; f++)
                        {
                            var clock = System.Diagnostics.Stopwatch.StartNew();
                            view.targetTexture = target;
                            view.Render();
                            view.targetTexture = null;
                            double each = clock.Elapsed.TotalMilliseconds;
                            yield return null;
                            if (f < 10) continue;
                            times.Add(each);
                            if (draws.LastValue > 0) { drawSum += draws.LastValue; passSum += setPass.LastValue; triSum += triangles.LastValue; counted++; }
                        }
                        view.enabled = wasEnabled;
                        times.Sort();
                        perf = $" perf_median_ms={times[times.Count / 2]:F2} perf_p95_ms={times[(int)(times.Count * .95f)]:F2}" +
                               (counted > 0 ? $" draws={drawSum / counted} setpass={passSum / counted} triangles={triSum / counted}" : " counters=unavailable");
                    }
                    int renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.enabled && r.gameObject.activeInHierarchy);
                    log.Add($"{shot.Key}: renderers={renderers} render_ms={ms:F1} source={VisorComposite.LastSourceFormat}{perf}");
                    foreach (var p in props) Destroy(p);
                    yield return null;
                }
            }
            world?.Dispose();
            File.WriteAllLines(Path.Combine(output, "review.txt"), log.Prepend($"{SystemInfo.graphicsDeviceName} {Application.unityVersion} {QualitySettings.activeColorSpace} {width}x{height}"));
            Debug.Log("ART REVIEW COMPLETE");
            UnityEditor.EditorApplication.Exit(0);
        }
    }
}
#endif
