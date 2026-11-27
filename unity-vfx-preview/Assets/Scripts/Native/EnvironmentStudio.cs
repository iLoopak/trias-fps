#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Trias.Native
{
    // Editor review scene. It uses the same catalog, material resolution and directional light as the arena.
    public sealed class EnvironmentStudio : MonoBehaviour
    {
        [SerializeField] string arena = "relay";
        TriasWorld lighting;

        void Start()
        {
            var camera = GetComponent<Camera>();
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TRIAS/World").text);
            var spec = Array.Find(catalog.maps, map => map.id == arena) ?? catalog.maps[0];
            lighting = new TriasWorld(spec, catalog, "tdm", camera);
            foreach (Transform part in lighting.Root)
                if (part.GetComponent<Light>() == null && part.GetComponent<ReflectionProbe>() == null)
                    part.gameObject.SetActive(false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Catalog.Tint("#232e39");
            var stage = TriasWorld.Group(transform.parent, "Environment library");
            TriasWorld.Part(stage, lighting.MaterialAt("#50575e"),new Vector3(0,-.15f,1),new Vector3(19,.30f,13));

            Add("WallStandard", -5.5f, 5.3f, 3.2f,3,.62f);
            Add("WallRecessed", -2.3f, 5.3f, 3.2f,3,.62f);
            Add("WallPhase", .9f, 5.3f, 3.2f,3,.62f);
            Add("OpeningPhase", 4.0f, 5.3f, 3.0f,3,.63f);
            Add("SupportPhase", 6.6f, 5.3f, 1.6f,3.2f,1.38f);
            Add("DeckBridge", -4.5f, 1.0f, 5.6f,.72f,3.50f);
            Add("RailSwept", -4.7f, 2.55f, 3.2f,.87f,.2f,.72f);
            Add("DeckFloor", .3f, .6f, 3.2f,.045f,2.8f);
            Add("ContainerPressure", .25f, -.2f, 1.85f,1.08f,1.26f,.045f);
            Add("CoverPhase", -4.6f, -2.45f, 2.45f,1.36f,1.13f);
            Add("MachineStabilizer", 3.2f, .25f, 2.27f,2.1f,1.37f);
            Add("RockOutcrop", 6.0f, -1.95f, 3.2f,1.38f,1.87f, natural:true);
            Add("TreeWindward", 6.8f, 2.7f, 3.1f,4.4f,3.1f, natural:true);
            Add("SiteReclaimedDeflector", 2.1f, -2.45f, 2.4f,1.05f,1.2f, natural:true);
            Add("PlantBramble", 3.8f,-2.0f,.75f,.43f,.7f, natural:true);
            var reference = lighting.MaterialAt("#a4afba");
            // Physical eye-height reference beside the open portal.
            TriasWorld.Part(stage, reference, new Vector3(4, .79f, 4.65f),
                new Vector3(.27f, .79f, .27f), PrimitiveType.Capsule);

            void Add(string key, float x, float z, float width, float height, float depth,
                float baseY = 0, bool natural = false)
            {
                var center = new Vector3(x, baseY+height*.5f, z);
                var size = new Vector3(width,height,depth);
                if (natural) lighting.NatureMesh(key,stage,center,size,lod:false);
                else lighting.ArchitectureMesh(key,stage,center,size);
            }
        }

        void OnDestroy() => lighting?.Dispose();
    }
}
#endif
