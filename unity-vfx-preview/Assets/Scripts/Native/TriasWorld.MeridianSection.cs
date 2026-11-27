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
