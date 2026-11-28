using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        internal readonly bool IsRelayArt;
        internal static readonly Vector3 RelayStar = new Vector3(-.76f,.64f,-.39f).normalized;
        float oldShadowDistance, oldReflectionIntensity;
        bool oldHdr;
        Camera relayCamera;
        Light oldSun, relaySun;
        Material relayStructure;
        DefaultReflectionMode oldReflectionMode;

        void ConfigureRelayLight(Light key, Camera view)
        {
            relayCamera = view;
            oldHdr = view.allowHDR;
            view.allowHDR = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
            oldShadowDistance = QualitySettings.shadowDistance;
            QualitySettings.shadowDistance = Mathf.Min(oldShadowDistance, 74f);
            oldSun = RenderSettings.sun;
            relaySun = key;
            RenderSettings.sun = key;
            oldReflectionMode = RenderSettings.defaultReflectionMode;
            oldReflectionIntensity = RenderSettings.reflectionIntensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = .71f;

            key.name = "Relay · orbital key";
            key.transform.rotation = Quaternion.LookRotation(-RelayStar);
            key.color = Catalog.Tint("#ffe0b6");
            key.intensity = 1.42f;
            key.shadowStrength = .93f;
            key.shadowBias = .032f;
            key.shadowNormalBias = .23f;
            key.shadowNearPlane = .25f;
            key.renderMode = LightRenderMode.ForcePixel;

            // Cool but desaturated sky fill; the ground term is warm bounce from the sunlit ivory decks,
            // so gantry undersides read as shaded ceramic instead of saturated navy (linear lighting).
            RenderSettings.ambientSkyColor = Catalog.Tint("#a2adb9");
            RenderSettings.ambientEquatorColor = Catalog.Tint("#646c75");
            RenderSettings.ambientGroundColor = Catalog.Tint("#6f675d");
            RenderSettings.fogColor = Catalog.Tint("#3a485a");
            RenderSettings.fogDensity = .0042f;
        }

        void ConfigureRelaySurfaces()
        {
            var ceramic = Resources.Load<Texture2D>("TRIAS/RelaySurfaces/reactor-ceramic");
            if (ceramic != null) Porcelain.mainTexture = ceramic;
            Porcelain.color = Catalog.Tint("#eee9e0");
            Graphite.color = Catalog.Tint("#34393f");
            Hardware.color = Catalog.Tint("#a3a6a6");
            Hardware.mainTexture = null;
            relayStructure = CeramicFinish("#909698", .37f, .34f);
            RelaySurface(Porcelain, "porcelain", .43f);
            RelaySurface(Graphite, "graphite", .54f);
            RelaySurface(Hardware, "alloy", .29f);
            var ambient = Group(Root, "Relay sky reflection").gameObject.AddComponent<ReflectionProbe>();
            ambient.mode = ReflectionProbeMode.Realtime;
            ambient.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            ambient.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            ambient.resolution = 64;
            ambient.size = new Vector3(Spec.length * 2 + 5, 28, Spec.length * 2 + 5);
            ambient.center = Vector3.up * 10;
            ambient.clearFlags = ReflectionProbeClearFlags.Skybox;
            ambient.cullingMask = 0;
            ambient.intensity = .58f;
            ambient.RenderProbe();
        }

        static void RelaySurface(Material finish, string file, float bump)
        {
            var response = Resources.Load<Texture2D>("TRIAS/RelaySurfaces/" + file + "-surface");
            var normal = Resources.Load<Texture2D>("TRIAS/RelaySurfaces/" + file + "-normal");
            if (response != null)
            {
                finish.SetTexture("_MetallicGlossMap", response);
                finish.SetFloat("_GlossMapScale", 1f);
                finish.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (normal != null)
            {
                finish.SetTexture("_BumpMap", normal);
                finish.SetFloat("_BumpScale", bump);
                finish.EnableKeyword("_NORMALMAP");
            }
        }

        void BuildRelayContacts()
        {
            var shader = Resources.Load<Shader>("TRIAS/RelayContact");
            if (shader == null || !shader.isSupported) return;
            var paint = new Material(shader);
            Paint.Add(paint);
            var points = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var inside = new Color(.051f,.069f,.097f,.33f);
            var outside = new Color(inside.r,inside.g,inside.b,0);
            Physics.SyncTransforms();
            foreach (var box in Spec.cover)
            {
                if (Mathf.Abs(box.bottom) > .02f || (Spec.Shaft && box.structure != "pylon")) continue;
                AddContactSkirt(points,colors,triangles,new Vector3(box.x,.012f,box.z),box.angle,box.w*.5f,box.d*.5f,-.06f,.32f,inside,outside);
            }
            var mesh = new Mesh { name = "Relay grounding" };
            mesh.SetVertices(points);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            var contact = TriasMarkings.Model(Root, "Relay contact shadows", mesh, paint, Vector3.zero, Vector3.one, false);
            contact.GetComponent<Renderer>().receiveShadows = false;
        }

        // Vanta and Meridian: the same soft grounding under floor-standing cover, tinted to each arena's
        // shade, so containers and machines sit on the deck instead of floating on it.
        void BuildGroundContacts()
        {
            if (IsRelayArt || island) return;
            var shader = Resources.Load<Shader>("TRIAS/RelayContact");
            if (shader == null || !shader.isSupported) return;
            var paint = new Material(shader);
            Paint.Add(paint);
            var points = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            Physics.SyncTransforms();
            var inside = Spec.id == "foundry" ? new Color(.11f,.05f,.04f,.30f) : new Color(.10f,.07f,.08f,.27f);
            var outside = new Color(inside.r,inside.g,inside.b,0);
            foreach (var box in Spec.cover)
            {
                if (Mathf.Abs(box.bottom) > .02f || box.structure != null || box.collider) continue;
                AddContactSkirt(points,colors,triangles,new Vector3(box.x,.012f,box.z),box.angle,box.w*.5f,box.d*.5f,-.05f,.34f,inside,outside);
            }
            var mesh = new Mesh { name = "Ground contact and wear" };
            mesh.SetVertices(points);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            var contact = TriasMarkings.Model(Root, "Ground contact and wear", mesh, paint, Vector3.zero, Vector3.one, false);
            contact.GetComponent<Renderer>().receiveShadows = false;
        }

        // A skirt corner that would hang over a drop (the Vanta quench channel, the Relay shaft) folds back
        // onto the cover's footprint, so contact shading never floats in the air above an opening.
        static void AddContactSkirt(List<Vector3> points, List<Color> colors, List<int> triangles, Vector3 pivot, float angle,
            float W, float D, float inner, float outer, Color inside, Color outside)
        {
            var rotation = Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
            int start = points.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                float reach = ring == 0 ? inner : outer;
                float x = Mathf.Max(.02f,W+reach), z = Mathf.Max(.02f,D+reach);
                for (int corner = 0; corner < 4; corner++)
                {
                    float sx = corner == 0 || corner == 3 ? -1 : 1, sz = corner < 2 ? -1 : 1;
                    var at = pivot + rotation*new Vector3(sx*x,0,sz*z);
                    if (ring == 1 && !Physics.Raycast(at + Vector3.up*.4f, Vector3.down, out var floor, .55f, TerrainMask, QueryTriggerInteraction.Ignore))
                        at = points[start + corner];
                    points.Add(at);
                }
                for (int face = 0; face < 4; face++) colors.Add(ring == 0 ? inside : outside);
            }
            for (int edge = 0; edge < 4; edge++)
            {
                int a = start + edge, b = start + (edge+1)%4;
                triangles.Add(a); triangles.Add(a+4); triangles.Add(b+4);
                triangles.Add(a); triangles.Add(b+4); triangles.Add(b);
            }
        }

        // Rejected: paired dust ruts from Meridian's ore stockpiles to the central machine. At a tasteful
        // strength they were invisible on the pale floor; at three times that they read as faint scratches.

        void RestoreRelayLight()
        {
            if (relayCamera != null) relayCamera.allowHDR = oldHdr;
            QualitySettings.shadowDistance = oldShadowDistance;
            RenderSettings.sun = oldSun;
            RenderSettings.defaultReflectionMode = oldReflectionMode;
            RenderSettings.reflectionIntensity = oldReflectionIntensity;
        }
    }
}
