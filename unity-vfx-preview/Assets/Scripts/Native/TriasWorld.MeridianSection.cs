using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Material meridianSection;

        Material MeridianSection()
        {
            if (meridianSection != null) return meridianSection;
            meridianSection = MaterialAt("#ffffff", false, "meridian-agate");
            meridianSection.SetFloat("_Glossiness", .49f);
            meridianSection.SetFloat("_Metallic", .08f);
            return meridianSection;
        }

        Material mineralSolid;

        // The anomalous mineral as a solid (TRIAS/MineralSolid): banding follows world position, so the
        // stone held in machinery shows one continuous body across faces and neighbouring parts.
        Material MineralSolid()
        {
            if (mineralSolid != null) return mineralSolid;
            var shader = Resources.Load<Shader>("TRIAS/MineralSolid");
            var palette = Resources.Load<Texture2D>("TRIAS/meridian-agate");
            if (shader == null || !shader.isSupported || palette == null) return mineralSolid = MeridianSection();
            mineralSolid = new Material(shader) { name = "Anomalous mineral solid" };
            mineralSolid.SetTexture("_Palette", palette);
            Paint.Add(mineralSolid);
            return mineralSolid;
        }

        // Meridian's floor shows one polished agate vein surfacing in open ground: lens-shaped cuts laid
        // along a single fault axis through the basin, each 2.6-4.4 m long, offset to alternate sides of the
        // axis. UVs follow position along the axis, so the banding reads as the same vein between windows.
        // Only where the whole lens lies on clear base floor (no cover, no step); 2 cm above the collider.
        void BuildMeridianFloorSeam()
        {
            if (Spec.id != "basin") return;
            Physics.SyncTransforms();
            Vector2 axis = new Vector2(.78f, .63f).normalized, across = new Vector2(-axis.y, axis.x);
            var points = new List<Vector3>();
            var uv = new List<Vector2>();
            var faces = new List<int>();
            int windows = 0;
            bool Floor(Vector2 at, out float y)
            {
                y = 0;
                if (!Physics.Raycast(new Vector3(at.x, 6, at.y), Vector3.down, out var hit, 9, TerrainMask, QueryTriggerInteraction.Ignore)
                    || hit.normal.y < .97f || Mathf.Abs(hit.point.y) > .2f) return false;
                y = hit.point.y;
                return !Array.Exists(Spec.cover, box => box.Overlaps(at.x, at.y, .5f));
            }
            for (float t = -48; t <= 48 && windows < 12; t += 3.1f)
            {
                int k = Mathf.RoundToInt(t * 10);
                float length = 2.6f + Mathf.Abs(k * 7 % 10) * .18f, width = 1.0f + Mathf.Abs(k * 3 % 7) * .09f;
                float lateral = (Mathf.Abs(k) % 2 == 0 ? 1 : -1) * (.6f + Mathf.Abs(k * 11 % 5) * .25f);
                Vector2 centre = axis * t + across * lateral;
                bool clear = Floor(centre, out float y);
                for (int c = 0; c < 4 && clear; c++)
                    clear = Floor(centre + axis * ((c < 2 ? -.5f : .5f) * length) + across * ((c % 2 == 0 ? -.5f : .5f) * width), out _);
                if (!clear) continue;
                windows++;
                int start = points.Count;
                const int steps = 8;
                for (int i = 0; i <= steps; i++)
                {
                    float f = (float)i / steps, half = Mathf.Sin(f * Mathf.PI) * width * .5f + .02f;
                    Vector2 at = centre + axis * ((f - .5f) * length);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 edge = at + across * (side * half);
                        points.Add(new Vector3(edge.x, y + .02f, edge.y));
                        // Banding runs across the vein, keyed to the position along the fault axis.
                        uv.Add(new Vector2(Vector2.Dot(edge, axis) * .06f, .5f + Vector2.Dot(edge, across) * .18f));
                    }
                    if (i == 0) continue;
                    int a = start + (i - 1) * 2, b = start + i * 2;
                    // Clockwise from above, so the inlay faces up in Unity.
                    faces.Add(a); faces.Add(b + 1); faces.Add(b);
                    faces.Add(a); faces.Add(a + 1); faces.Add(b + 1);
                }
            }
            if (faces.Count == 0) return;
            // A floor cut is honed, not polished: lower gloss and a deeper tint keep it from mirroring the
            // pale sky at grazing angles, and a graphite rim (built first, slightly wider) defines its edge.
            var honed = MaterialAt("#b49a86", false, "meridian-agate");
            honed.SetFloat("_Glossiness", .18f);
            honed.SetFloat("_Metallic", 0);
            var rim = new List<Vector3>(points.Count);
            for (int i = 0; i < points.Count; i += 2)
            {
                Vector3 a = points[i], b = points[i + 1], mid = (a + b) * .5f, spread = (b - a) * .5f;
                float grow = spread.magnitude + .07f;
                spread = spread.sqrMagnitude > 1e-6f ? spread.normalized * grow : spread;
                rim.Add(mid - spread - Vector3.up * .003f);
                rim.Add(mid + spread - Vector3.up * .003f);
            }
            MeshAt("Meridian floor vein rim", rim, faces, uv, MapSurface("#2b2a31", "fractured-stone", 0, .2f));
            MeshAt("Meridian floor vein", points, faces, uv, honed);
        }

        // Four pages from one geological section. UVs refer to its position before
        // separation so the inclusion can be followed across the missing intervals.
        void BuildMeridianSection()
        {
            var crown = Group(industry, "The Unbound Section", new Vector3(20, 9, 73));
            crown.localRotation = Quaternion.Euler(-6, -18, -12);
            float[] edges = { -31, -16, -3, 11, 29 };
            float[] tops = { 8, 29, 38, 25, 10 };
            float[] feet = { 0, -4, -1, 2, 5 };
            var rind = MapSurface("#675451", "fractured-stone", 0, .15f);
            for (int i = 0; i < edges.Length - 1; i++)
            {
                var page = Group(crown, "Suspended core section", new Vector3(i * 3.6f, i * 2.8f, i * -2.6f));
                page.localRotation = Quaternion.Euler(0, (i - 1) * 8, 0);
                float l = edges[i], r = edges[i + 1];
                var front = new[] {
                    new Vector3(l+2, feet[i], 0), new Vector3(l, feet[i]+7, 0),
                    new Vector3(l+4, tops[i]-2, 0), new Vector3(l+7, tops[i]+1, 0),
                    new Vector3(r+2, tops[i+1], 0), new Vector3(r+4, tops[i+1]-4, 0),
                    new Vector3(r, feet[i+1]+2, 0), new Vector3(r-4, feet[i+1]-1, 0)
                };
                var vertices = new List<Vector3>();
                var uv = new List<Vector2>();
                var cut = new List<int>();
                var crust = new List<int>();
                void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> indices)
                {
                    int n = vertices.Count;
                    vertices.AddRange(new[] {a,b,c,d});
                    foreach (var v in new[] {a,b,c,d})
                        uv.Add(new Vector2((v.x+31)/65, (v.y+4)/40));
                    indices.AddRange(new[] {n,n+1,n+2,n,n+2,n+3});
                }
                Vector3 depth = new Vector3(-2.6f, 1, 7.2f);
                Vector3 center = Vector3.zero;
                foreach (var point in front) center += point;
                center /= front.Length;
                void Cap(Vector3 offset, bool reverse)
                {
                    int first = vertices.Count;
                    foreach (var point in front)
                    {
                        Vector3 v = point + offset;
                        vertices.Add(v);
                        uv.Add(new Vector2((point.x+31)/65, (point.y+4)/40));
                    }
                    for (int j = 1; j < front.Length-1; j++)
                        cut.AddRange(new[] {first,first+j+(reverse?1:0),first+j+(reverse?0:1)});
                }
                Cap(Vector3.zero, false);
                Cap(depth, true);
                for (int j = 0; j < front.Length; j++)
                {
                    int k = (j+1)%front.Length;
                    Vector3 shoulderJ = center + (front[j]-center)*1.055f + depth*.18f;
                    Vector3 shoulderK = center + (front[k]-center)*1.055f + depth*.18f;
                    Quad(front[k],front[j],shoulderJ,shoulderK,crust);
                    Quad(shoulderK,shoulderJ,front[j]+depth,front[k]+depth,crust);
                }
                var mesh = new Mesh { name = "Meridian polished cut", subMeshCount = 2 };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0,uv);
                mesh.SetTriangles(cut,0);
                mesh.SetTriangles(crust,1);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                meshes.Add(mesh);
                page.gameObject.layer = Decor;
                page.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var skin = page.gameObject.AddComponent<MeshRenderer>();
                skin.sharedMaterials = new[] { MeridianSection(), rind };
                skin.shadowCastingMode = ShadowCastingMode.Off;
                skin.receiveShadows = false;
                plantMotion.Add(new PlantMotion(page, new Vector3(i*.85f,i*.65f,0), Vector3.up, 0, i*1.8f));
            }
        }
    }
}
