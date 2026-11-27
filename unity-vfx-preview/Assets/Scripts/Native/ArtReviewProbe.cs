#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
            internal float Fov = 70;
        }

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
            new Shot { Key = "relay-gallery", Arena = "relay", Weapon = "rift", Eye = P(7.5f, 4.88f, -14.75f), Aim = P(-.2f, 4.2f, -1.95f),
                Pilots = new[] { ("kestrel", 2, P(1.9f, 3.3f, -9.4f), 200f) } },
            new Shot { Key = "relay-deck", Arena = "relay", Weapon = "scatter", Eye = P(-13.5f, 1.58f, -20.5f), Aim = P(-4f, 2.2f, -9f) },
            new Shot { Key = "vanta-approach", Arena = "foundry", Weapon = "arc", Eye = P(-10, 1.58f, -24.5f), Aim = P(-6, 1.5f, -16),
                Pilots = new[] { ("bulwark", 0, P(-7.3f, 0, -15.8f), 200f) } },
            new Shot { Key = "meridian-approach", Arena = "basin", Weapon = "lance", Eye = P(-8.8f, 1.58f, -37.4f), Aim = P(-5.3f, 1.4f, -31) },
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
                        var hands = new WeaponHandling(world, art, shot.Weapon, accent, true, "vector", false);
                        hands.Update(WeaponHandling.At(shot.Weapon, 0), 0);
                        props.Add(grip.gameObject);
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
                    int renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.enabled && r.gameObject.activeInHierarchy);
                    log.Add($"{shot.Key}: renderers={renderers} render_ms={ms:F1} source={VisorComposite.LastSourceFormat}");
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
