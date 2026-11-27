using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Trias.Native
{
    internal enum PilotVolume { Helmet, Torso, Limb, Boot, Glove }

    internal sealed partial class TriasWorld
    {
        readonly Dictionary<PilotVolume, Mesh> pilotSurfaces = new();
        readonly Dictionary<(string kit, bool male, int faction, int part, int slot), Mesh> pilotBatches = new();

        // Finish slots stay distinct for the Echo shader, while same-finish pieces share a draw per joint.
        internal void BatchPilotJoint(Transform joint, string kit, bool male, int faction, int part)
        {
            var groups = joint.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => renderer.enabled)
                .GroupBy(renderer => (renderer.sharedMaterial, renderer.shadowCastingMode));
            int slot = 0;
            foreach (var group in groups)
            {
                var pieces = group.ToArray();
                var key = (kit ?? "vector", male, faction, part, slot++);
                if (pieces.Length < 2) continue;
                if (!pilotBatches.TryGetValue(key, out var surface))
                {
                    var matrix = joint.worldToLocalMatrix;
                    var instances = new CombineInstance[pieces.Length];
                    for (int i = 0; i < pieces.Length; i++)
                    {
                        instances[i].mesh = pieces[i].GetComponent<MeshFilter>().sharedMesh;
                        instances[i].transform = matrix * pieces[i].transform.localToWorldMatrix;
                    }
                    surface = new Mesh { name = $"Pilot {kit} joint {part} slot {key.Item5}" };
                    surface.CombineMeshes(instances, true, true);
                    meshes.Add(surface);
                    pilotBatches.Add(key, surface);
                }
                TriasMarkings.Model(joint, "Finished joint", surface, group.Key.sharedMaterial, Vector3.zero, Vector3.one,
                    group.Key.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On);
                foreach (var piece in pieces)
                {
                    piece.enabled = false;
                    Object.Destroy(piece);
                    Object.Destroy(piece.GetComponent<MeshFilter>());
                }
            }
        }

        internal Transform Sculpt(Transform parent, PilotVolume form, Material finish, Vector3 position, Vector3 size)
        {
            if (!pilotSurfaces.TryGetValue(form, out var surface))
            {
                surface = PilotSculpt.Create(form);
                pilotSurfaces.Add(form, surface);
                meshes.Add(surface);
            }
            return TriasMarkings.Model(parent, form.ToString(), surface, finish, position, size, true);
        }
    }

    internal static class PilotSculpt
    {
        // Ring records give height, width, depth and forward offset within a unit volume.
        internal static Mesh Create(PilotVolume form)
        {
            var profile = form switch
            {
                PilotVolume.Helmet => new[]
                {
                    new Vector4(-.50f,.17f,.20f,.02f),new Vector4(-.45f,.30f,.35f,.04f),
                    new Vector4(-.31f,.43f,.46f,.036f),new Vector4(-.09f,.48f,.49f,0),
                    new Vector4(.14f,.50f,.49f,-.020f),new Vector4(.34f,.43f,.43f,-.04f),
                    new Vector4(.46f,.30f,.31f,-.04f),new Vector4(.50f,.15f,.15f,-.04f)
                },
                PilotVolume.Torso => new[]
                {
                    new Vector4(-.50f,.28f,.37f,-.045f),new Vector4(-.39f,.39f,.44f,-.050f),
                    new Vector4(-.24f,.37f,.46f,-.025f),new Vector4(.06f,.43f,.50f,0),
                    new Vector4(.27f,.50f,.48f,-.05f),new Vector4(.37f,.48f,.39f,-.06f),
                    new Vector4(.50f,.30f,.29f,-.05f)
                },
                PilotVolume.Boot => new[]
                {
                    new Vector4(-.50f,.45f,.50f,.04f),new Vector4(-.39f,.48f,.50f,.04f),
                    new Vector4(-.28f,.50f,.48f,.03f),new Vector4(-.05f,.48f,.45f,.01f),
                    new Vector4(.23f,.40f,.38f,-.057f),new Vector4(.41f,.36f,.25f,-.143f),
                    new Vector4(.50f,.31f,.21f,-.151f)
                },
                PilotVolume.Glove => new[]
                {
                    new Vector4(-.50f,.21f,.34f,.09f),new Vector4(-.32f,.39f,.49f,.08f),
                    new Vector4(.03f,.49f,.49f,.007f),new Vector4(.34f,.46f,.42f,0),
                    new Vector4(.50f,.35f,.34f,-.038f)
                },
                _ => new[]
                {
                    new Vector4(-.50f,.29f,.30f,0),new Vector4(-.43f,.34f,.34f,0),
                    new Vector4(-.21f,.41f,.42f,.02f),new Vector4(.11f,.50f,.50f,.015f),
                    new Vector4(.32f,.47f,.44f,-.016f),new Vector4(.44f,.38f,.34f,-.012f),
                    new Vector4(.50f,.32f,.28f,0)
                }
            };
            const int sides = 16;
            var vertices = new List<Vector3>(profile.Length*sides+2);
            var triangles = new List<int>((profile.Length-1)*sides*6+sides*6);
            var uv = new List<Vector2>(vertices.Capacity);
            float corner = form == PilotVolume.Boot || form == PilotVolume.Helmet ? .71f : .89f;
            float Round(float p) => Mathf.Sign(p)*Mathf.Pow(Mathf.Abs(p),corner);
            for (int ring = 0; ring < profile.Length; ring++)
            {
                Vector4 band = profile[ring];
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    float x = Round(Mathf.Sin(angle)), z = Round(Mathf.Cos(angle));
                    vertices.Add(new Vector3(x*band.y,band.x,z*band.z+band.w));
                    uv.Add(new Vector2(x*.49f+.5f,band.x+.5f));
                    if (ring == 0) continue;
                    int a = (ring-1)*sides + i, b = (ring-1)*sides + (i+1)%sides;
                    triangles.Add(a); triangles.Add(b); triangles.Add(b+sides);
                    triangles.Add(a); triangles.Add(b+sides); triangles.Add(a+sides);
                }
            }
            int cap = vertices.Count, last = (profile.Length-1)*sides;
            vertices.Add(new Vector3(0,profile[0].x,profile[0].w));
            vertices.Add(new Vector3(0,profile[profile.Length-1].x,profile[profile.Length-1].w));
            uv.Add(Vector2.one*.5f); uv.Add(Vector2.one*.5f);
            for (int i = 0; i < sides; i++)
            {
                int next = (i+1)%sides;
                triangles.Add(cap); triangles.Add(next); triangles.Add(i);
                triangles.Add(cap+1); triangles.Add(last+i); triangles.Add(last+next);
            }
            var mesh = new Mesh { name = $"Pilot {form} hull" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
