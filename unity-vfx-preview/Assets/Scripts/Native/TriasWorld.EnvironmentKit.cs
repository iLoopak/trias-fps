using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        const float ArchitectureLodHeight = .16f;
        BlenderParts environmentArchitecture, environmentNature;

        readonly Dictionary<string, Material> environmentFinishes = new();

        Material EnvironmentFinish(string slot)
        {
            if (environmentFinishes.TryGetValue(slot, out var existing)) return existing;
            Material material;
            switch (slot)
            {
                case "Signal": material = MaterialAt("#5aaba9"); break;
                case "OchreStone": material = MapSurface("#8d704f", "fractured-stone", 0, .13f); break;
                case "LeafShade": material = MaterialAt("#3a523b"); break;
                case "Leaf": material = MaterialAt("#607846"); break;
                case "YoungLeaf": material = MaterialAt("#899552"); break;
                case "Ceramic" when island: material = MapSurface("#bab698", "refractory", .03f, .22f); break;
                case "Ceramic" when Spec.id == "foundry": material = MapSurface("#b69b7d", "refractory", .06f, .23f); break;
                // Meridian: the kit's ceramic stays ceramic machinery; only its secondary inserts are the mined
                // mineral, cut from one solid so the agate reads as material held in the machine.
                case "Secondary" when Spec.id == "basin": material = MineralSolid(); break;
                case "Ceramic" when Spec.id == "basin": material = MapSurface("#9b8576", "refractory", 0, .2f); break;
                case "Secondary" when island: material = MapSurface("#8d9374", "fractured-stone", 0, .19f); break;
                default: return MapFinish(slot);
            }
            environmentFinishes.Add(slot, material);
            return material;
        }

        internal bool ArchitectureMesh(string key, Transform parent, Vector3 center, Vector3 size,
            bool shadows = true, bool lod = false)
        {
            environmentArchitecture ??= new BlenderParts("TRIAS/Environment/Architecture");
            return environmentArchitecture.FitEnvironment(key, parent, center, size, EnvironmentFinish, shadows, lod, ArchitectureLodHeight);
        }

        internal bool NatureMesh(string key, Transform parent, Vector3 center, Vector3 size,
            bool shadows = true, bool lod = true)
        {
            environmentNature ??= new BlenderParts("TRIAS/Environment/Nature");
            return environmentNature.FitEnvironment(key, parent, center, size, EnvironmentFinish, shadows, lod, .105f);
        }

        static int EnvironmentVariant(BoxSpec box)
        {
            int x = Mathf.RoundToInt(box.x * 17);
            int z = Mathf.RoundToInt(box.z * 23);
            return Mathf.Abs((x ^ z) % 12);
        }

        bool DressEnvironmentCover(BoxSpec box, Transform frame, float height)
        {
            // Meridian's central plinth is a solid distribution machine instead of a bare ivory block. An open
            // stabiliser frame was rejected: its gaps showed through a solid collider.
            if (!IsRelayArt && Spec.id == "basin" && box.relay)
                return FitLongFace("MachineDistribution", frame, box, height, lod: true);
            if (box.gantry || box.relay || box.profile == "airlock") return false;
            int variation = EnvironmentVariant(box);
            if (!IsRelayArt)
            {
                string industrial = (Spec.id, box.profile) switch
                {
                    // Sector cover on the central terraces: process equipment instead of bare blocks.
                    ("foundry", null) when box.terrace => variation % 2 == 0 ? "ContainerThermal" : "ContainerPressure",
                    ("basin", null) when box.terrace => variation % 2 == 0 ? "ContainerThermal" : "ContainerPressure",
                    ("foundry", "pylon") => "SupportHeavy",
                    ("foundry", "baffle") => variation % 2 == 0 ? "CoverReinforced" : "CoverEquipment",
                    ("basin", "pylon") => "SupportMechanical",
                    ("basin", "baffle") => variation % 2 == 0 ? "CoverWeathered" : "CoverCeramic",
                    _ => null
                };
                if (industrial == null || !FitLongFace(industrial, frame, box, height, lod: true)) return false;
                if (box.terrace && box.sector >= 0)
                    // Keep the sector colour that marks the terrace's owner on both long faces.
                    for (int sign = -1; sign <= 1; sign += 2)
                        Part(frame, TeamLight[box.sector], new Vector3(0, height + .012f, sign * (box.d * .5f - .06f)),
                            new Vector3(box.w * .7f, .022f, .05f));
                return true;
            }
            string key = box.structure switch
            {
                "pylon" => variation % 2 == 0 ? "SupportPhase" : "SupportTapered",
                "baffle" => (variation % 5) switch
                {
                    0 => "ContainerPressure", 1 => "ContainerThermal", 2 => "CoverLow",
                    3 => "CoverEquipment", _ => "CoverPhase"
                },
                _ when box.profile == "pylon" => "SupportTapered",
                _ when height < 1.3f && box.terrace => "CoverLow",
                _ when height < 1.3f => variation % 2 == 0 ? "ContainerPressure" : "ContainerThermal",
                _ => variation % 2 == 0 ? "CoverFull" : "CoverCeramic"
            };
            return FitLongFace(key, frame, box, height, lod: true);
        }

        bool FitLongFace(string key, Transform frame, BoxSpec box, float height, bool lod = false, bool shadows = true)
        {
            bool transverse = box.d > box.w;
            Vector3 size = transverse ? new Vector3(box.d, height, box.w) : new Vector3(box.w, height, box.d);
            if (!transverse) return ArchitectureMesh(key, frame, Vector3.up * height * .5f, size, shadows, lod);

            environmentArchitecture ??= new BlenderParts("TRIAS/Environment/Architecture");
            if (!environmentArchitecture.Has(key)) return false;
            var axis = Group(frame, "Long face along depth");
            axis.localRotation = Quaternion.Euler(0,90,0);
            return ArchitectureMesh(key, axis, Vector3.up * height * .5f, size, shadows, lod);
        }

        bool DressEnvironmentWall(BoxSpec box, Transform frame, float height)
        {
            if (box.structure == "support" && Spec.id is "foundry" or "basin")
                return FitLongFace(Spec.id == "foundry" ? "SupportHeavy" : "SupportMechanical", frame, box, height, lod: true);

            string[] palette = box.structure switch
            {
                "relay-wall" => new[] { "WallPhase", "WallStandard", "WallRecessed", "WallAngled", "WallCore" },
                "quench-wall" => new[] { "WallReinforced", "WallCore", "WallReinforced" },
                "survey-wall" => new[] { "WallEnd", "WallRecessed", "WallAngled" },
                _ => null
            };
            if (palette == null) return false;
            bool across = box.w > box.d;
            float length = across ? box.w : box.d;
            float thickness = across ? box.d : box.w;
            int variant = EnvironmentVariant(box);
            environmentArchitecture ??= new BlenderParts("TRIAS/Environment/Architecture");
            int bays = Mathf.Max(1, Mathf.RoundToInt(length / 3.2f));
            float width = length / bays;
            for (int i = 0; i < bays; i++)
                if (!environmentArchitecture.Has(palette[(i + variant) % palette.Length])) return false;

            var line = Group(frame, "Modular ceramic wall");
            if (!across) line.localRotation = Quaternion.Euler(0,90,0);
            var keys = new string[bays];
            for (int i = 0; i < bays; i++) keys[i] = palette[(i + variant) % palette.Length];
            if (IsRelayArt && environmentArchitecture.FitEnvironmentRun(keys,line,length,height,thickness,
                false,EnvironmentFinish,meshes,ArchitectureLodHeight)) return true;
            for (int i = 0; i < bays; i++)
            {
                string key = keys[i];
                float x = -length * .5f + width * (i + .5f);
                ArchitectureMesh(key, line, new Vector3(x,height*.5f,0), new Vector3(width,height,thickness), shadows: false, lod: true);
            }
            return true;
        }

        bool DressEnvironmentShaft(BoxSpec box, Transform frame, float height)
        {
            switch (box.structure)
            {
                case "pylon":
                case "baffle":
                    return DressEnvironmentCover(box, frame, height);
                case "stair" when box.z < -8f && Mathf.Abs(box.x) < 14f:
                    return ArchitectureMesh("DeckTread", frame, Vector3.up * height*.5f,
                        new Vector3(box.w,height,box.d), false);
                case "rail":
                    return FitLongFace("RailSwept", frame, box, height, lod: true, shadows: false);
                case "bridge":
                case "arrival":
                case "hub":
                {
                    bool across = box.w > box.d;
                    float length = across ? box.w : box.d;
                    float width = across ? box.d : box.w;
                    string key = box.structure == "bridge" ? "DeckBridge" : "DeckSuspended";
                    environmentArchitecture ??= new BlenderParts("TRIAS/Environment/Architecture");
                    if (!environmentArchitecture.Has(key)) return false;
                    var span = Group(frame, "Ceramic edge and exposed web");
                    if (across) span.localRotation = Quaternion.Euler(0,90,0);
                    int bays = Mathf.Max(1, Mathf.CeilToInt(length / 6f));
                    float pitch = length / bays;
                    var keys = new string[bays];
                    Array.Fill(keys,key);
                    if (environmentArchitecture.FitEnvironmentRun(keys,span,length,height,width,
                        true,EnvironmentFinish,meshes,ArchitectureLodHeight)) return true;
                    for (int i = 0; i < bays; i++)
                    {
                        float z = -length * .5f + (i + .5f) * pitch;
                        ArchitectureMesh(key, span, new Vector3(0,height*.5f,z),
                            new Vector3(width,height,pitch), shadows: false, lod: true);
                    }
                    return true;
                }
                default:
                    return false;
            }
        }

        static void KeepStructuralShadow(Transform solid)
        {
            // One shadow draw for a complete wall or span, independent of its visible material slots.
            solid.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        void DressEnvironmentRelayService()
        {
            if (!IsRelayArt || !Spec.Shaft) return;
            // Dimensions and orientation of the two authored pressure huts in relay-shaft-3.
            // Their broken wall runs already include the real entrances.
            foreach (var roof in Spec.cover)
            {
                if (roof.structure != "roof") continue;
                var hut = Group(Root, "Pressure hut service equipment", new Vector3(roof.x, 0, roof.z));
                hut.localRotation = Quaternion.Euler(0, roof.angle * Mathf.Rad2Deg, 0);
                ArchitectureMesh("DeckFloor", hut, new Vector3(0,.019f,0), new Vector3(2.4f,.028f,2.4f), false);
                // The jambs sit over the two pressure-wall returns, with the cap above head clearance.
                ArchitectureMesh("OpeningPhase", hut, new Vector3(0,1.60f,-3.285f),
                    new Vector3(4.10f,3.20f,.065f), lod: true);
                // The shutoff terminals are mounted on the solid back wall above the floor.
                ArchitectureMesh("MachineStabilizer", hut, new Vector3(0,1.52f,2.69f),
                    new Vector3(1.39f,1.46f,.13f), lod: true);
            }
        }

        bool DressEnvironmentIslandCover(BoxSpec box, Transform parent, float foot, Vector3 previousSize)
        {
            int variant = EnvironmentVariant(box);
            if (box.nature == "tree")
            {
                string species = variant % 3 == 0 ? "TreeSentinel" : variant % 3 == 1 ? "TreeRidge" : "TreeWindward";
                // Keep branch clearance and the visible rootstock close to the legacy trunk footprint.
                return NatureMesh(species, parent, Vector3.up * (foot + previousSize.y * .5f), previousSize);
            }
            string stone = box.strata ? "RockOutcrop" : variant % 3 == 0 ? "RockShelf"
                : variant % 3 == 1 ? "RockSplit" : "RockErased";
            // Avoid making walkable-looking ledges beyond the authored rock blocker.
            Vector3 size = new Vector3(box.w * 1.025f, previousSize.y, box.d * 1.025f);
            return NatureMesh(stone, parent, Vector3.up * (foot + size.y * .5f), size);
        }

        void DressEnvironmentIslandBank(Site crossing)
        {
            // The structure stays low and on the already sloped shoulders outside the bridge and river lane.
            float scale = Spec.IslandScale;
            const float dx = 2.85f;
            float x = crossing.x + (crossing.x > 0 ? dx : -dx);
            float z = crossing.z + 2.7f * scale;
            float y = Height(x,z);
            if (y < .2f || Array.Exists(Spec.cover, box => box.Overlaps(x,z,1.36f))) return;
            var recess = Group(Root, "Eroded water-regulation deflector", new Vector3(x,y-.19f,z));
            recess.localRotation = Quaternion.Euler(0,crossing.x > 0 ? 23 : -27,0);
            NatureMesh("SiteReclaimedDeflector", recess, new Vector3(0,.34f,0),new Vector3(2.1f,.88f,1.23f));
            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * .63f;
                Vector3 edge = recess.TransformPoint(new Vector3(offset,0,.61f));
                NatureMesh(i == 1 ? "PlantSedge" : "PlantFern", Root,
                    new Vector3(edge.x,Height(edge.x,edge.z)+.065f,edge.z),new Vector3(.35f,.17f,.37f),false);
            }
        }
    }
}
