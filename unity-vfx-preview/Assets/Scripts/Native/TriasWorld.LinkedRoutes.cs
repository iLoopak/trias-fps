using UnityEngine;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Material routeTread, routeFrame, routeTrim, routeLamp;
        BlenderParts linkedParts;

        void ConfigureLinkedRoutes()
        {
            bool foundry = Spec.id == "foundry";
            routeTread = island ? MapFinish("Wood") :
                MapSurface(foundry ? "#6b5c56" : "#8d8273", "brushed-deck", .43f, .24f);
            routeTread.mainTextureScale = island ? new Vector2(2, 5) : new Vector2(4, 6);
            routeFrame = island ? MapFinish("Bark") :
                MapSurface(foundry ? "#474043" : "#594647", "bulkhead", .46f, .28f);
            routeTrim = IsRelayArt ? Porcelain : island ? MapFinish("DarkStone") : foundry ? MapFinish("Refractory") : MapFinish("Alloy");
            routeLamp = MaterialAt(foundry ? "#ef8849" : "#d2b284", true);
        }

        void DressLinkedRoute(BoxSpec box, Transform frame, float height)
        {
            frame.name = box.structure switch
            {
                "bank" => "Casting deck", "channel" => "Quench channel floor", "timber" => "Ravine span",
                "catwalk" => "Ore conveyor gallery", "bridge" => "Casting crane bridge",
                "stair" => "Access stair tread", "support" => "Load-bearing pier",
                "rail" => "Guardrail", "roof" => "Utility building roof",
                "quench-wall" => "Heat exchanger hall wall", "survey-wall" => "Survey shed wall",
                "refuge-wall" => "Rangers' refuge wall", _ => "Service baffle",
            };
            Material finish = box.structure is "rail" or "support" or "baffle" ? routeFrame : routeTread;
            if (box.structure.EndsWith("-wall")) finish = routeTrim;
            var solid = Part(frame, finish, Vector3.up * (height * .5f),
                new Vector3(box.w,height,box.d), PrimitiveType.Cube,true);
            if (DressEnvironmentWall(box, frame, height))
            {
                if (box.structure.EndsWith("-wall", System.StringComparison.Ordinal)) KeepStructuralShadow(solid);
                else solid.GetComponent<Renderer>().enabled = false;
                return;
            }
            switch (box.structure)
            {
                case "roof":
                    DressUtilityRoof(box,frame,height);
                    break;
                case "relay-wall":
                case "quench-wall":
                case "survey-wall":
                case "refuge-wall":
                    DressUtilityWall(box,frame,height);
                    break;
                case "bridge":
                case "catwalk":
                    DressIndustrialSpan(box,frame,height);
                    break;
                case "timber":
                    DressTimberSpan(box,frame,height);
                    break;
                case "stair":
                    Part(frame, island ? routeFrame : routeTrim,
                        new Vector3(0,height+.004f,box.d*.5f-.058f),new Vector3(box.w-.09f,.012f,.09f));
                    break;
                case "support":
                    if (island)
                        solid.GetComponent<Renderer>().enabled = !MapProp("granite",frame,Vector3.up*height*.5f,
                            new Vector3(box.w*1.11f,height+.32f,box.d*1.23f));
                    else
                    {
                        Armor(frame,routeTrim,new Vector3(0,height*.09f,0),new Vector3(box.w*1.18f,height*.18f,box.d*1.16f),false);
                        Armor(frame,routeTrim,new Vector3(0,height*.91f,0),new Vector3(box.w*1.22f,height*.18f,box.d*1.15f),false);
                    }
                    break;
                case "rail":
                    solid.GetComponent<Renderer>().enabled = false;
                    DressSpanRail(box,frame,height);
                    break;
            }
        }

        void DressIndustrialSpan(BoxSpec box, Transform frame, float height)
        {
            bool across = box.w > box.d;
            float width = across ? box.d : box.w, length = across ? box.w : box.d;
            var span = Group(frame,"Box truss");
            if (across) span.localRotation = Quaternion.Euler(0,90,0);
            linkedParts ??= new BlenderParts("TRIAS/RelayStructure/RelayStructure");
            int spans = Mathf.CeilToInt(length / 8f);
            for (int i = 0; i < spans; i++)
            {
                float segment = length/spans;
                float z = -length*.5f+(i+.5f)*segment;
                var girder = Group(span,"Structural steel bay",new Vector3(0,0,z));
                girder.localRotation = Quaternion.Euler(0,90,0);
                linkedParts.Fit("ContinuousGantry",girder,new Vector3(0,height*.42f,0),
                    new Vector3(segment*.98f,height*.82f,width*.98f),
                    slot => slot == "Amber" ? routeTrim : routeFrame);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side*(width*.5f-.02f);
                    Part(span, routeLamp,new Vector3(x,height*.72f,z),new Vector3(.045f,.085f,.74f));
                    Part(span, routeTrim,new Vector3(x,height*.22f,z),new Vector3(.083f,.16f,segment*.98f));
                }
            }
        }

        void DressTimberSpan(BoxSpec box, Transform frame, float height)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side*(box.w*.5f-.26f);
                Armor(frame,routeFrame,new Vector3(x,height*.3f,0),new Vector3(.3f,.32f,box.d+.24f),false);
                Beam(frame,routeFrame,new Vector3(x,-.58f,-box.d*.39f),new Vector3(x,-1.35f,0),.18f);
                Beam(frame,routeFrame,new Vector3(x,-1.35f,0),new Vector3(x,-.58f,box.d*.39f),.18f);
            }
            // The rock shoulders rise into the soffit on both sides of the ravine.
            for (int end = -1; end <= 1; end += 2)
            for (int side = -1; side <= 1; side += 2)
            {
                float offset = end*(box.d*.5f-1.6f);
                float rise = end > 0 ? 1.65f : 2.45f;
                MapProp("splitrock",frame,new Vector3(side*2.15f,-rise*.63f,offset),
                    new Vector3(1.40f,rise,2.1f));
            }
            int planks = Mathf.CeilToInt(box.d/.39f);
            for (int i = 0; i < planks; i++)
            {
                float pitch = box.d/planks;
                Part(frame,i%8 == 1 ? routeFrame : routeTread,new Vector3(0,height+.007f,-box.d*.5f+(i+.5f)*pitch),
                    new Vector3(box.w-.05f,.026f,pitch-.016f));
            }
        }

        void DressSpanRail(BoxSpec box, Transform frame, float height)
        {
            bool across = box.w > box.d;
            float length = across ? box.w : box.d;
            var rail = Group(frame,"Guardrail frame");
            if (across) rail.localRotation = Quaternion.Euler(0,90,0);
            Part(rail,routeTrim,new Vector3(0,height-.08f,0),new Vector3(.16f,.13f,length));
            Part(rail,routeFrame,new Vector3(0,height*.41f,0),new Vector3(.099f,.095f,length));
            int posts = Mathf.CeilToInt(length/2.1f);
            for (int i = 0; i <= posts; i++)
                Part(rail,routeFrame,new Vector3(0,height*.45f,Mathf.Lerp(-length*.5f+.055f,length*.5f-.055f,(float)i/posts)),
                    new Vector3(.12f,height*.91f,.11f));
        }

        void DressUtilityRoof(BoxSpec box, Transform frame, float height)
        {
            string label = IsRelayArt ? "SERVICE / LOCK" : island ? "HORNÍ ÚTULNA" : Spec.id == "foundry" ? "QUENCH / WORKS" : "ORE / SURVEY";
            for (int side = -1; side <= 1; side += 2)
            {
                Armor(frame,routeFrame,new Vector3(0,height*.72f,side*box.d*.5f),
                    new Vector3(box.w,.16f,.19f),false);
                Part(frame,island ? routeTrim : routeLamp,new Vector3(0,-.024f,side*1.65f),
                    new Vector3(2.6f,.054f,.12f));
            }
            if (island)
            {
                for (int i = -2; i <= 2; i++)
                    Beam(frame,routeFrame,new Vector3(i*1.78f,-.21f,-box.d*.5f+.13f),
                        new Vector3(i*1.78f,-.21f,box.d*.5f-.13f),.17f);
                // A turf roof ties the refuge to the meadow; a thin moss skin, not extra cover.
                Part(frame,MapFinish("Moss"),new Vector3(0,height+.035f,0),new Vector3(box.w-.34f,.07f,box.d-.34f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(frame,routeFrame,new Vector3(side*3.48f,height+.045f,0),new Vector3(.11f,.07f,box.d-.28f));
                    // Rafter tails under the eaves at a 1.1 m pitch.
                    for (int i = 0; i < 8; i++)
                        Part(frame,routeFrame,new Vector3(-3.85f+i*1.1f,height*.3f,side*(box.d*.5f+.12f)),
                            new Vector3(.13f,.16f,.3f));
                }
            }
            else
            {
                for (int i = -2; i <= 2; i++)
                    Part(frame,routeFrame,new Vector3(i*1.56f,height+.008f,.12f),new Vector3(.075f,.024f,box.d-.38f));
                var equipment = Group(frame,"Service plenum",new Vector3(2.55f,-.34f,.78f));
                MapProp(Spec.id == "foundry" ? "foundry-bay" : IsRelayArt ? "relay-bay" : "basin-bay",
                    equipment,Vector3.zero,new Vector3(1.86f,.52f,1.35f));
            }
            var fascia = Group(frame,label,new Vector3(0,height*.47f,-box.d*.5f-.112f));
            var font = Resources.Load<Font>("TRIAS/Fonts/ChakraPetch-Bold");
            if (font == null) return;
            if (island)
            {
                // A hand-cut board hangs from the fascia instead of free-floating letters.
                Part(frame,routeFrame,new Vector3(0,height*.47f-.02f,-box.d*.5f-.07f),new Vector3(3.1f,.5f,.07f));
                fascia.localPosition += new Vector3(0,-.02f,.0f);
            }
            var sign = fascia.gameObject.AddComponent<TextMesh>();
            sign.font = font;
            sign.text = label;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.alignment = TextAlignment.Center;
            sign.fontSize = 64;
            sign.characterSize = island ? .062f : .114f;
            sign.color = Catalog.Tint(island ? "#eee0bf" : "#f2cbac");
            fascia.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        void DressUtilityWall(BoxSpec box, Transform frame, float height)
        {
            bool across = box.w > box.d;
            float length = across ? box.w : box.d, thickness = across ? box.d : box.w;
            var panel = Group(frame,"Wall structure");
            if (!across) panel.localRotation = Quaternion.Euler(0,90,0);
            if (island)
            {
                DressRefugeWall(panel,length,thickness,height);
                return;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float face = side*(thickness*.5f+.027f);
                Part(panel,routeFrame,new Vector3(0,height*.12f,face),new Vector3(length,.16f,.072f));
                Part(panel,routeTread,new Vector3(0,height*.87f,face),new Vector3(length,.29f,.062f));
                int bays = Mathf.Max(1,Mathf.FloorToInt(length/1.26f));
                for (int i = 0; i < bays; i++)
                {
                    float x = Mathf.Lerp(-length*.5f,length*.5f,(i+.5f)/bays);
                    Part(panel,routeFrame,new Vector3(x,height*.5f,face),new Vector3(.10f,height*.73f,.10f));
                }
            }
        }

        // Kordilera's rangers' refuge: the collider stays the stone wall. Above a dry-stone plinth with a
        // proud capping course the faces carry horizontal log courses of uneven depth, notched corner
        // posts and one weathered replacement log, so the hut reads as built and maintained by hand.
        void DressRefugeWall(Transform panel, float length, float thickness, float height)
        {
            float plinth = height - 1.92f;
            var cap = MapFinish("DryStone");
            Part(panel,cap,new Vector3(0,plinth,0),new Vector3(length+.1f,.13f,thickness+.14f));
            float top = height + .02f;
            int courses = Mathf.Max(1,Mathf.RoundToInt((top - plinth - .07f) / .31f));
            float course = (top - plinth - .07f) / courses;
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < courses; i++)
            {
                float y = plinth + .07f + (i + .5f) * course;
                // Logs alternate their protrusion and every fifth one is a darker, older member.
                float proud = .055f + (i % 2) * .022f;
                bool old = (i + (side > 0 ? 2 : 0)) % 5 == 3;
                Part(panel,old ? routeFrame : routeTread,new Vector3((i % 2) * .05f * side,y,side*(thickness*.5f+proud*.5f)),
                    new Vector3(length-.06f,course-.035f,proud));
            }
            for (int end = -1; end <= 1; end += 2)
                Part(panel,routeFrame,new Vector3(end*(length*.5f-.09f),(plinth+top)*.5f,0),
                    new Vector3(.2f,top-plinth,thickness+.2f));
        }

        void BuildMoltenSheet(HazardSpec hazard)
        {
            var shader=Resources.Load<Shader>("TRIAS/MoltenFlow");
            if (shader==null || !shader.isSupported)
            {
                Part(Root,MapSurface("#bc5e36","refractory",.11f,.32f),hazard.Position+Vector3.up*.026f,
                    new Vector3(hazard.w,.035f,hazard.d));
                return;
            }
            moltenSurface=new Material(shader);
            Paint.Add(moltenSurface);
            const int columns=6, rows=42;
            var vertices=new System.Collections.Generic.List<Vector3>((columns+1)*(rows+1));
            var uv=new System.Collections.Generic.List<Vector2>(vertices.Capacity);
            var indices=new System.Collections.Generic.List<int>(columns*rows*6);
            for (int z=0;z<=rows;z++)
            for (int x=0;x<=columns;x++)
            {
                var point=new Vector2(x/(float)columns,z/(float)rows);
                uv.Add(point);
                vertices.Add(hazard.Position+new Vector3((point.x-.5f)*hazard.w,.049f,(point.y-.5f)*hazard.d));
                if (x==columns || z==rows) continue;
                int a=z*(columns+1)+x, next=a+columns+1;
                indices.Add(a); indices.Add(next); indices.Add(a+1);
                indices.Add(a+1); indices.Add(next); indices.Add(next+1);
            }
            MeshAt("Directional molten spillway",vertices,indices,uv,moltenSurface);
        }

        void BuildFoundryChannel()
        {
            var channel = Group(Root,"Crucible cooling and drainage");
            var dark = MapFinish("Refractory");
            if (Spec.hazard != null)
            {
                var hazard = Spec.hazard;
                var plasma = Group(channel,"Plasma spillway",hazard.Position);
                BuildMoltenSheet(hazard);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(plasma,routeTrim,new Vector3(side*(hazard.w*.5f+.09f),.018f,0),new Vector3(.17f,.035f,hazard.d));
                    for (int i=-5;i<=5;i++)
                        Part(plasma,routeLamp,new Vector3(side*(hazard.w*.5f+.20f),.024f,i*2.85f),
                            new Vector3(.20f,.026f,.16f)).localRotation=Quaternion.Euler(0,side*35,0);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Beam(channel,routeFrame,new Vector3(5.76f*side,6.39f,0),new Vector3(3.8f*side,8.15f,0),.95f);
                Beam(channel,routeFrame,new Vector3(5.76f*side,6.39f,0),new Vector3(5.76f*side,3.9f,4.4f),.52f);
                Armor(channel,dark,new Vector3(side*5.36f,-.18f,0),new Vector3(.12f,.37f,59.5f),false);
                Part(channel,routeFrame,new Vector3(side*5.32f,-2.18f,0),new Vector3(.14f,.28f,57.8f));
                for (int i = -5; i <= 5; i++)
                {
                    float z = i*4.9f;
                    Part(channel,routeLamp,new Vector3(side*5.23f,-2.14f,z),new Vector3(.045f,.095f,.62f));
                    Armor(channel,routeTrim,new Vector3(side*5.34f,-1.37f,z),new Vector3(.12f,2.25f,.46f),false);
                }
            }
        }
    }
}
