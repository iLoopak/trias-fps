using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal static class TriasMarkings
    {
        // The same strokes are stamped into suits, station hardware and the visor.
        internal static readonly Vector4[][] FactionStrokes =
        {
            new[] { new Vector4(-.72f,-.48f,-.36f,.48f), new Vector4(-.18f,-.48f,.18f,.48f), new Vector4(.36f,-.48f,.72f,.48f) },
            new[] { new Vector4(-.7f,.36f,0,-.18f), new Vector4(0,-.18f,.7f,.36f), new Vector4(-.49f,-.13f,0,-.52f), new Vector4(0,-.52f,.49f,-.13f) },
            new[] { new Vector4(0,.64f,.65f,0), new Vector4(.65f,0,0,-.64f), new Vector4(0,-.64f,-.65f,0), new Vector4(-.65f,0,0,.64f) }
        };

        internal static Mesh Emblem(int faction)
        {
            var vertices = new List<Vector3>();
            var faces = new List<int>();
            foreach (var stroke in FactionStrokes[faction])
            {
                var a = new Vector2(stroke.x, stroke.y);
                var b = new Vector2(stroke.z, stroke.w);
                var perpendicular = (b - a).normalized;
                perpendicular = new Vector2(-perpendicular.y, perpendicular.x) * (faction == 0 ? .112f : .087f);
                int i = vertices.Count;
                vertices.Add(a - perpendicular); vertices.Add(a + perpendicular);
                vertices.Add(b + perpendicular); vertices.Add(b - perpendicular);
                faces.AddRange(new[] { i, i+2, i+1, i, i+3, i+2 });
            }
            var mesh = new Mesh { name = "Faction stamp" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(faces, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        internal static Transform Model(Transform parent, string label, Mesh mesh, Material material,
            Vector3 at, Vector3 scale, bool shadows)
        {
            var part = TriasWorld.Group(parent, label, at);
            part.gameObject.layer = TriasWorld.Decor;
            part.localScale = scale;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return part;
        }

        internal static Mesh ArmorPlate(bool shell = false)
        {
            var outline = new[] { new Vector2(-.36f,-.5f), new Vector2(.36f,-.5f), new Vector2(.5f,-.33f),
                new Vector2(.5f,.33f), new Vector2(.36f,.5f), new Vector2(-.36f,.5f),
                new Vector2(-.5f,.33f), new Vector2(-.5f,-.33f) };
            if (shell)
                outline = new[] { new Vector2(-.23f,-.5f), new Vector2(.23f,-.5f), new Vector2(.38f,-.37f),
                    new Vector2(.48f,-.14f), new Vector2(.49f,.13f), new Vector2(.40f,.38f), new Vector2(.23f,.50f),
                    new Vector2(-.23f,.50f),new Vector2(-.40f,.38f),new Vector2(-.49f,.13f),
                    new Vector2(-.48f,-.14f),new Vector2(-.38f,-.37f) };
            var vertices = new List<Vector3>();
            var faces = new List<int>();
            var uv = new List<Vector2>();
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool cap = false)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                if (cap)
                {
                    uv.Add(new Vector2(a.x+.5f,a.y+.5f)); uv.Add(new Vector2(b.x+.5f,b.y+.5f));
                    uv.Add(new Vector2(c.x+.5f,c.y+.5f)); uv.Add(new Vector2(d.x+.5f,d.y+.5f));
                }
                else
                {
                    uv.Add(new Vector2(0,0)); uv.Add(new Vector2(1,0)); uv.Add(new Vector2(1,1)); uv.Add(new Vector2(0,1));
                }
                faces.AddRange(new[] { i, i+1, i+2 });
                if (d != a) faces.AddRange(new[] { i, i+2, i+3 });
            }
            Vector3 Point(int i, float radius, float z) => new Vector3(outline[i].x * radius, outline[i].y * radius, z);
            for (int i = 0; i < outline.Length; i++)
            {
                int j = (i + 1) % outline.Length;
                Face(Point(i,1,-.28f), Point(j,1,-.28f), Point(j,1,.28f), Point(i,1,.28f));
                Face(Point(i,1,.28f), Point(j,1,.28f), Point(j,.84f,.5f), Point(i,.84f,.5f));
                Face(Point(j,1,-.28f), Point(i,1,-.28f), Point(i,.84f,-.5f), Point(j,.84f,-.5f));
                Face(new Vector3(0,0,.5f), Point(i,.84f,.5f), Point(j,.84f,.5f), new Vector3(0,0,.5f), true);
                Face(new Vector3(0,0,-.5f), Point(j,.84f,-.5f), Point(i,.84f,-.5f), new Vector3(0,0,-.5f), true);
            }
            var mesh = new Mesh { name = "Chamfered armor plate" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0,uv);
            mesh.SetTriangles(faces,0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    internal sealed partial class TriasWorld
    {
        internal readonly Material Hardware, Graphite;
        readonly Mesh armorSection, ceramicShell;
        internal readonly Material[] FactionPaint = new Material[3];
        readonly Mesh[] emblems = new Mesh[3];

        internal Transform Armor(Transform root, Material paint, Vector3 at, Vector3 size, bool shadows = true) =>
            TriasMarkings.Model(root, "Machined plate", armorSection, paint, at, size, shadows);

        internal Transform Shell(Transform root, Material paint, Vector3 at, Vector3 size) =>
            TriasMarkings.Model(root, "Tapered ceramic shell", ceramicShell, paint, at, size, true);

        internal Transform Stamp(Transform root, int faction, Material paint, Vector3 at, float size)
        {
            if (emblems[faction] == null)
            {
                emblems[faction] = TriasMarkings.Emblem(faction);
                meshes.Add(emblems[faction]);
            }
            return TriasMarkings.Model(root, "Faction stamp", emblems[faction], paint, at, Vector3.one * size, false);
        }
    }
}
