using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        void BuildWayfinding()
        {
            var backing = MaterialAt("#29363c", false, "bulkhead");
            string message = Spec.id switch
            {
                "relay" => "RELAY / TRANSFER",
                "foundry" => "VANTA / HOT ZONE",
                "basin" => "MERIDIAN / ORE LINE",
                _ => "KORDILERA / FIELD LAB"
            };
            var ink = MaterialAt(Spec.id == "foundry" ? "#e18957" : Spec.id == "basin" ? "#dbc099"
                : island ? "#e1c97e" : "#c5e0dc", true);
            var font = Resources.Load<Font>("TRIAS/Fonts/ChakraPetch-Bold");
            void Label(Transform face, string title, float y, float size)
            {
                if (font == null) return;
                var text = Group(face, title, new Vector3(0,y,-.148f)).gameObject.AddComponent<TextMesh>();
                text.gameObject.layer = Decor;
                text.font = font;
                text.text = title;
                text.fontSize = 64;
                text.characterSize = size;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = ink.color;
                var renderer = text.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            if (island)
            {
                var observatory = Group(Root, "LIMEN / abandoned weather relay", new Vector3(4 * Spec.IslandScale, -4.7f, 33.7f * Spec.IslandScale));
                var ceramic = MaterialAt("#8eaaa0", false, "armor");
                var steel = MaterialAt("#435a58", false, "bulkhead");
                for (int side = -1; side <= 1; side += 2)
                {
                    var pylon = Group(observatory, "Weather relay buttress", new Vector3(side*3.4f,5.4f,0));
                    pylon.localRotation = Quaternion.Euler(0,0,side*11);
                    Armor(pylon, steel, Vector3.zero, new Vector3(2.2f,13,2.38f), false);
                    Armor(pylon, ceramic, new Vector3(0,2.2f,-1.13f), new Vector3(1.35f,6.6f,.37f), false);
                    Armor(pylon, ink, new Vector3(0,3.8f,-1.36f), new Vector3(.20f,2.7f,.071f), false);
                }
                var crossbeam = Group(observatory, "Field relay bridge", new Vector3(0,12.05f,-.2f));
                Armor(crossbeam, ceramic, Vector3.zero, new Vector3(8.7f,1.8f,2.3f), false);
                Ring(crossbeam, new Vector3(0,2.0f,0), 2.3f, ceramic, .24f, 20).transform.localRotation = Quaternion.Euler(66,0,-12);
                Ring(crossbeam, new Vector3(0,2.0f,0), 1.53f, ink, .071f, 16).transform.localRotation = Quaternion.Euler(66,0,-12);
                var plate = Group(observatory, "Field lab identification", new Vector3(0,12.03f,-1.32f));
                Label(plate, message, 0, .149f);
                return;
            }

            for (int sector = 0; sector < 4; sector++)
            {
                var rotation = Quaternion.Euler(0, sector*90, 0);
                var board = Group(Root, "Station wayfinding", rotation * new Vector3(0,3.48f,Spec.length-.16f));
                board.localRotation = rotation;
                Armor(board, backing, Vector3.zero, new Vector3(9.2f,1.38f,.25f), false);
                Armor(board, ink, new Vector3(0,-.485f,-.127f), new Vector3(IsRelayArt ? 4.45f : 8.55f,.046f,.031f), false);
                string heading = IsRelayArt ? sector switch
                {
                    0 => "PHASE / TRANSFER",
                    1 => "RELAY / RADIATORS",
                    2 => "RELAY / SERVICE",
                    _ => "RELAY / HABITAT"
                } : message;
                Label(board, heading, .245f, .149f);
                Label(board, sector switch { 0 => "01 / NORTH", 1 => "02 / EAST", 2 => "03 / SOUTH", _ => "04 / WEST" }, -.245f, .067f);
            }

            for (int team = 0; team < Mathf.Min(TeamLight.Length, Spec.bases.Length); team++)
            {
                var at = Spec.bases[team].Position;
                Vector3 outward = new Vector3(at.x,0,at.z).normalized;
                // Keep all added fixtures behind the deployment area and outside the movement lane.
                float boundary = Spec.length / Mathf.Max(Mathf.Abs(outward.x), Mathf.Abs(outward.z));
                var mast = Group(Root, "Deployment transponder", outward*(boundary+.05f) + Vector3.up*5.2f);
                mast.LookAt(mast.position - outward);
                Armor(mast, backing, Vector3.zero, new Vector3(1.46f,1.92f,.27f), false);
                Stamp(mast, team, TeamLight[team], new Vector3(0,0,.148f), .76f);
                Armor(mast, TeamLight[team], new Vector3(0,-.66f,.151f), new Vector3(.95f,.045f,.021f), false);
            }
        }

        readonly List<ParticleSystem> boundaryVapor = new List<ParticleSystem>();
        float vaporIntensity = -1;

        void BuildBoundaryVapor()
        {
            if (!island && Spec.id != "foundry" && Spec.id != "basin") return;
            var shader = Resources.Load<Shader>("TRIAS/Vapor");
            if (shader == null || !shader.isSupported) return;
            var material = new Material(shader);
            Paint.Add(material);
            bool heat = Spec.id == "foundry";
            for (int i = 0; i < (island ? 2 : 3); i++)
            {
                Vector3 at = heat ? new Vector3((i - 1)*24, i == 1 ? 12 : 16, Spec.length + 10)
                    : new Vector3((i - 1)*27,3,Spec.length + 12);
                if (island)
                {
                    var outlet = Spec.river[i == 0 ? 0 : Spec.river.Length - 1];
                    at = outlet.Position + new Vector3(Mathf.Sign(outlet.x) * 4.1f, -5.7f, 0);
                }
                var plume = Group(Root,island ? "Waterfall spray" : heat ? "Cooling exhaust" : "Ore dust",at).gameObject.AddComponent<ParticleSystem>();
                plume.gameObject.layer = Decor;
                plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = plume.main;
                main.loop = true;
                main.startLifetime = heat ? 4.1f : 6f;
                main.startSpeed = 0;
                main.startRotation = new ParticleSystem.MinMaxCurve(-1f, 1f);
                main.startSize = new ParticleSystem.MinMaxCurve(heat ? 2f : 4f,heat ? 4.1f : 6.8f);
                main.startColor = island ? new Color(.83f,.93f,.94f,.29f) : heat ? new Color(.73f,.57f,.50f,.28f) : new Color(.77f,.60f,.39f,.20f);
                main.maxParticles = 8;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var shape = plume.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = .43f;
                var emission = plume.emission;
                emission.rateOverTime = 1.35f;
                var drift = plume.velocityOverLifetime;
                drift.enabled = true;
                drift.space = ParticleSystemSimulationSpace.World;
                drift.x = new ParticleSystem.MinMaxCurve(island ? -.7f : .4f, island ? .7f : 1.35f);
                drift.y = new ParticleSystem.MinMaxCurve(island ? -.80f : heat ? .81f : .08f, heat ? 1.7f : .26f);
                drift.z = new ParticleSystem.MinMaxCurve(.15f, .43f);
                var color = plume.colorOverLifetime;
                color.enabled = true;
                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                    new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.26f), new GradientAlphaKey(.54f,.65f), new GradientAlphaKey(0,1) });
                color.color = fade;
                var renderer = plume.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                boundaryVapor.Add(plume);
            }
        }

        void AnimateBoundaryVapor(float intensity)
        {
            if (Mathf.Approximately(vaporIntensity,intensity)) return;
            vaporIntensity = intensity;
            foreach (var plume in boundaryVapor)
            {
                var emission = plume.emission;
                emission.rateOverTime = intensity * 1.35f;
                if (intensity <= .01f) plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                else if (!plume.isPlaying) plume.Play();
            }
        }

        void BuildGrasses()
        {
            var vertices = new List<Vector3>();
            var faces = new List<int>();
            var uv = new List<Vector2>();
            for (int i = 0; i < 535; i++)
            {
                float angle = i * 2.399963f;
                float radius = (4 + R(23.8f)) * Spec.IslandScale;
                float x = Mathf.Cos(angle)*radius, z = Mathf.Sin(angle)*radius;
                float y = Height(x,z);
                if (y < .40f || Array.Exists(Spec.cover, box => box.Overlaps(x,z,.38f)) ||
                    Array.Exists(Spec.trail, marker => (marker.Position-new Vector3(x,0,z)).sqrMagnitude < 1.2f)) continue;
                float h = .07f + R(.11f);
                for (int blade = 0; blade < 4; blade++)
                {
                    float wind = angle + blade*2.399963f;
                    Vector3 side = new Vector3(Mathf.Cos(wind),0,Mathf.Sin(wind));
                    Vector3 baseAt = new Vector3(x,y-.009f,z) + side*R(.144f);
                    int n = vertices.Count;
                    vertices.Add(baseAt-side*.025f);
                    vertices.Add(baseAt+side*.025f);
                    vertices.Add(baseAt+side*.066f+Vector3.up*h);
                    uv.Add(new Vector2(0,0)); uv.Add(new Vector2(1,0)); uv.Add(new Vector2(.5f,1));
                    faces.AddRange(new[] { n,n+1,n+2,n+2,n+1,n });
                }
            }
            MeshAt("Low meadow grasses", vertices, faces, uv, MaterialAt("#848c52"), doubleSided:true);
        }
    }
}
