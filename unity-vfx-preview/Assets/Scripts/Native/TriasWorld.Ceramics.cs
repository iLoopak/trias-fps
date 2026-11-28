using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        internal readonly Material Porcelain, Insulation, SafetyAmber, PilotVisor;
        readonly Dictionary<int, Mesh> ceramicArcs = new();

        Material CeramicFinish(string color, float metallic, float smoothness)
        {
            var finish = MaterialAt(color);
            finish.SetFloat("_Metallic", metallic);
            finish.SetFloat("_Glossiness", smoothness);
            return finish;
        }

        void DressRelayCover(BoxSpec box, Transform home, float height)
        {
            bool ribbed = box.relay || height >= 3.3f;
            bool low = height < 1.8f;
            for (int face = 0; face < 4; face++)
            {
                bool end = face % 2 != 0;
                float width = end ? box.d : box.w;
                float depth = (end ? box.w : box.d)*.5f;
                var skin = Group(home, "Replaceable layered fascia");
                skin.localRotation = Quaternion.Euler(0,face*90,0);
                DressRelayMechanical(box, skin, face, width, depth, height);
                if (box.gantry)
                {
                    Armor(skin, Graphite, new Vector3(0,height*.22f,depth+.025f),new Vector3(width*.98f,height*.53f,.12f),false);
                    float gap = Mathf.Min(.055f, width * .028f);
                    int cells = end ? 2 : width >= 7 ? 4 : 3;
                    for (int tile = 0; tile < cells; tile++)
                    {
                        float span = width / cells;
                        float x = (tile + .5f) * span - width * .5f;
                        var paint = (tile + face) % 4 == 3 ? Insulation : Porcelain;
                        Armor(skin, paint, new Vector3(x, height * .73f, depth + .077f),
                            new Vector3(span - gap * 2, height * .48f, .14f), true);
                        Armor(skin, tile % 2 == 0 ? relayPaintedSteel : relayStructure,
                            new Vector3(x - span * .19f, height * .41f, depth + .12f),
                            new Vector3(span * .51f, height * .116f, .079f), false);
                    }
                    continue;
                }
                if (end || ribbed)
                {
                    Armor(skin, Graphite,new Vector3(0,height*.27f,depth+.025f),new Vector3(width*.93f,height*.45f,.13f),false);
                    if (low || end && !ribbed) continue;
                }
                var panel = Armor(skin, Porcelain,new Vector3((ribbed ? -.073f : .07f)*width,height*(low ? .78f : .65f),depth+.041f),
                    new Vector3(width*(ribbed ? .78f : .84f),height*(low ? .34f : .62f),.116f),false);
                panel.localRotation = Quaternion.Euler(0,0,ribbed ? 0f : face == 0 ? 2f : -2f);
                if (low || end) continue;
                if (box.profile == "pylon")
                {
                    float x = width * .34f;
                    var service = Armor(skin, relayPaintedSteel, new Vector3(x, height * .72f, depth + .13f),
                        new Vector3(width * .138f, height * .39f, .102f), true);
                    service.localRotation = Quaternion.Euler(0, 0, -4);
                    Part(skin, relayDrain, new Vector3(x, height * .62f, depth + .181f),
                        new Vector3(width * .074f, .034f, .036f));
                }
                Armor(skin, box.sector >= 0 ? FactionPaint[box.sector] : SafetyAmber,
                    new Vector3(-width*.28f,height*.39f,depth+.093f),new Vector3(width*.22f,.078f,.044f),false);
                if (!ribbed) continue;
                var bearing = Armor(skin, relayStructure, new Vector3(width*.38f,height*.48f,depth+.065f),
                    new Vector3(width*.125f,height*.76f,.19f),false);
                bearing.localRotation = Quaternion.Euler(0,0,9);
            }
            if (!box.gantry) return;
            for (int i = -1; i <= 1; i++)
            {
                Armor(home, Graphite,new Vector3(i*box.w*.33f,-.020f,0),new Vector3(box.w*.295f,.11f,box.d*.89f),false);
                Armor(home, relayStructure,new Vector3(i*box.w*.33f,-.081f,0),new Vector3(box.w*.063f,.056f,box.d*.86f),false);
            }
        }

        // An elevated deck slab with open floor beneath shows its structure: shallow transverse ribs at a
        // 2.4 m pitch, two edge girders and flush strip lights between the ribs. Nothing hangs more than
        // .18 m below the collider, so the ceiling height players read stays honest.
        Material soffitLamp;

        void DressRelaySoffit(BoxSpec box, Transform home)
        {
            float thickness = box.h - box.bottom;
            if (box.bottom < 1.9f || thickness > 1.5f || box.w * box.d < 20) return;
            foreach (var other in Spec.cover)
            {
                if (other == box || other.h > box.bottom + .05f || other.h < box.bottom - 2.4f) continue;
                if (box.Overlaps(other.x, other.z, Mathf.Max(other.w, other.d) * .5f)) return;
            }
            bool alongWidth = box.w >= box.d;
            float length = alongWidth ? box.w : box.d, span = alongWidth ? box.d : box.w;
            soffitLamp ??= MaterialAt("#cbd9dc", true);
            var soffit = Group(home, "Deck soffit structure");
            if (!alongWidth) soffit.localRotation = Quaternion.Euler(0, 90, 0);
            int ribs = Mathf.Max(2, Mathf.RoundToInt(length / 2.4f));
            for (int i = 0; i <= ribs; i++)
            {
                float x = Mathf.Lerp(-length * .5f + .2f, length * .5f - .2f, (float)i / ribs);
                Part(soffit, Graphite, new Vector3(x, -.07f, 0), new Vector3(.16f, .14f, span - .3f));
                if (i < ribs && i % 2 == 0)
                    Part(soffit, soffitLamp, new Vector3(x + length / ribs * .5f, -.006f, 0), new Vector3(.08f, .01f, span * .55f));
            }
            for (int side = -1; side <= 1; side += 2)
                Part(soffit, relayDrain, new Vector3(0, -.09f, side * (span * .5f - .22f)), new Vector3(length - .1f, .18f, .26f));
        }

        void BuildRelayHorizon()
        {
            var distantHull = CeramicFinish("#556173", .12f, .32f);
            var distantFrame = CeramicFinish("#414b5b", .18f, .28f);
            var distantInsulation = CeramicFinish("#7b8290", .04f, .30f);
            var signal = MaterialAt("#837b69", true);

            // The structure reads as one broken orbital collar with gaps for the planet and star.
            var collar = Group(Root, "Outer orbital assembly", new Vector3(-21,-3,62));
            collar.localRotation = Quaternion.Euler(-32,-18,-10);
            for (int sector = 0; sector < 3; sector++)
            {
                var span = Group(collar, "Orbital collar bay");
                span.localRotation = Quaternion.Euler(0, sector*111 + 42, 0);
                CeramicArc(span,distantFrame,Vector3.down*2,new Vector3(61,5.9f,58),63);
                CeramicArc(span,distantHull,Vector3.up*2,new Vector3(61.7f,3.8f,58.4f),66);
                CeramicArc(span,distantInsulation,Vector3.up*4.5f,new Vector3(62.1f,1.08f,59),59);
                CeramicArc(span,signal,Vector3.up*.29f,new Vector3(62.3f,.14f,59.2f),51);
            }
            var foundation = Group(Root, "Relay habitat support pylons");
            for (int side = 0; side < 4; side++)
            {
                var perimeter = Group(foundation, "External foundation");
                perimeter.localRotation = Quaternion.Euler(0,side*90,0);
                float edge = Spec.length;
                for (int column = 0; column < 2; column++)
                {
                    float x = column == 0 ? -edge*.73f : edge*.57f;
                    float z = edge + 7.7f + column*4.1f;
                    float height = side % 2 == column ? 24f : 17f;
                    var shaft = Group(perimeter,"Habitat pier",new Vector3(x,0,z));
                    shaft.localRotation = Quaternion.Euler(-5, column == 0 ? 10 : -11,column == 0 ? -8 : 9);
                    // The Blender pier carries the habitat on a bearing beam; boxes remain the fallback.
                    if (Megastructure("HabitatPier", shaft, Vector3.zero, column == 0 ? 6 : -9, height / 30f,
                            slot => slot switch
                            {
                                "Shell" => distantHull, "Trim" => distantInsulation, "Signal" or "Heat" => signal,
                                _ => distantFrame,
                            }))
                        continue;
                    Armor(shaft, distantFrame, new Vector3(0,-2.4f,0),new Vector3(5.2f,height,5.9f),false);
                    Armor(shaft, distantHull, new Vector3(-.2f,height*.31f,-2.63f),new Vector3(3.9f,height*.52f,1.62f),false);
                    Armor(shaft, distantInsulation, new Vector3(-.35f,height*.51f,-2.63f),new Vector3(3.35f,1.48f,1.82f),false);
                    Armor(shaft, distantFrame,new Vector3(1.7f,height*.37f,-.6f),new Vector3(.74f,height*.69f,4.35f),false);
                    // Intermediate floors split the exterior tower into structural masses at different depths.
                    for (int level = 0; level < 3; level++)
                    {
                        float y = (level + 1) * height * .157f;
                        float offset = level == 1 ? -.22f : .19f;
                        Armor(shaft, level == 1 ? relayPaintedSteel : distantInsulation,
                            new Vector3(offset, y, -3.37f), new Vector3(3.61f - level*.39f, .41f, .49f), false);
                        Part(shaft, distantFrame, new Vector3(offset, y - .36f, -3.36f), new Vector3(2.67f, .14f, .41f));
                    }
                    // Low-poly oak crowns on the piers were removed in the visual overhaul: they read as toy
                    // assets against the orbital architecture and broke the near-field identity test.
                }
            }
        }

        void BuildRelayDebris()
        {
            var debris = Group(Root, "Fractured exterior mineral masses");
            var rocks = new []
            {
                (new Vector3(-43, 13, 81), new Vector3(11, 20, 13), Quaternion.Euler(-12, 15, 14)),
                (new Vector3(-56, -9, 70), new Vector3(17, 30, 14), Quaternion.Euler(-25, 28, -9)),
                (new Vector3(35, 11, 121), new Vector3(7, 15, 9), Quaternion.Euler(9, 72, -19)),
                (new Vector3(46, 4, 95), new Vector3(5.34f, 11.5f, 6.6f), Quaternion.Euler(-23, 93, 12))
            };
            for (int i = 0; i < rocks.Length; i++)
            {
                var (position, size, angle) = rocks[i];
                var mount = Group(debris, "Exposed fracture", position);
                mount.localRotation = angle;
                MapProp(i % 2 == 0 ? "splitrock" : "shelves", mount, Vector3.zero, size);
                MapProp("granite", mount, new Vector3(size.x * .68f, size.y * .24f, size.z * .19f), size * .23f);
            }
            foreach (var renderer in debris.GetComponentsInChildren<MeshRenderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Unit outer radius, with real thickness and exposed ends on both sides of the gap.
        internal Transform CeramicArc(Transform parent, Material paint, Vector3 at, Vector3 size,
            int degrees, bool shadows = false)
        {
            if (!ceramicArcs.TryGetValue(degrees, out var mesh))
            {
                const float inner = .79f, upper = -.5f, lower = .5f;
                int count = Mathf.CeilToInt(degrees / 7f);
                var vertices = new List<Vector3>(count * 16 + 8);
                var indices = new List<int>(count * 24 + 12);
                var uv = new List<Vector2>(count * 16 + 8);
                Vector3 Point(int index, float radius, float height)
                {
                    float angle = degrees * (index / (float)count - .5f) * Mathf.Deg2Rad;
                    return new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
                }
                void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
                {
                    int start = vertices.Count;
                    vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                    uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                    indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                    indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
                }
                for (int i = 0; i < count; i++)
                {
                    int j = i + 1;
                    Vector3 a = Point(i, 1, upper), b = Point(j, 1, upper), c = Point(i, 1, lower), d = Point(j, 1, lower);
                    Vector3 e = Point(i, inner, upper), f = Point(j, inner, upper), g = Point(i, inner, lower), h = Point(j, inner, lower);
                    Face(a, b, d, c);
                    Face(f, e, g, h);
                    Face(c, d, h, g);
                    Face(e, f, b, a);
                    if (i == 0) Face(e, a, c, g);
                    if (j == count) Face(b, f, h, d);
                }
                mesh = new Mesh { name = $"Ceramic arc {degrees}°" };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);
                mesh.SetUVs(0, uv);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                ceramicArcs.Add(degrees, mesh);
                meshes.Add(mesh);
            }
            return TriasMarkings.Model(parent, "Separated ceramic arc", mesh, paint, at, size, shadows);
        }
    }
}
