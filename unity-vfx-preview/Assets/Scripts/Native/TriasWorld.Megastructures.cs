using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Transform industry;
        readonly List<PlantMotion> plantMotion = new();
        Material plantSignal, plantHeat, moltenSurface;
        float plantStarted, plantTick, impactUntil;
        Vector3 moltenImpact;
        ParticleSystem furnaceSparks, pressureRelease;
        AudioSource plantAudio;
        Light forgeBounce;
        int lastPlantBurst = -1;
        static BlenderParts megaParts;

        // Distant Blender silhouettes (decor only, no collider). Authored at typical size in metres with the
        // arena-facing side on local -z; returns false when the asset is unavailable so callers keep a fallback.
        bool Megastructure(string key, Transform parent, Vector3 position, float yaw, float scale,
            System.Func<string, Material> finish)
        {
            megaParts ??= new BlenderParts("TRIAS/Megastructures/Megastructures");
            if (!megaParts.Has(key)) return false;
            var mount = Group(parent, key, position);
            mount.localRotation = Quaternion.Euler(0, yaw, 0);
            mount.localScale = Vector3.one * scale;
            megaParts.Bind(key, mount, finish, false);
            return true;
        }

        // A whole rigid assembly renders as one mesh per material, including its joints and rails.
        sealed class PlantBatch
        {
            readonly Dictionary<Material, (List<Vector3> Vertices, List<int> Triangles)> shapes = new();

            public void Block(Material paint, Vector3 center, Vector3 size, Quaternion rotation = default)
            {
                if (rotation == default) rotation = Quaternion.identity;
                Vector3 At(int x,int y,int z) => center + rotation * Vector3.Scale(size,new Vector3(x,y,z)*.5f);
                var a = At(-1,-1,-1); var b = At(1,-1,-1); var c = At(1,-1,1); var d = At(-1,-1,1);
                var e = At(-1,1,-1); var f = At(1,1,-1); var g = At(1,1,1); var h = At(-1,1,1);
                Face(paint,a,b,c,d); Face(paint,e,h,g,f); Face(paint,a,e,f,b);
                Face(paint,d,c,g,h); Face(paint,a,d,h,e); Face(paint,b,f,g,c);
            }

            public void Strut(Material paint, Vector3 a, Vector3 b, float width, float depth)
            {
                var axis = b-a;
                Block(paint,(a+b)*.5f,new Vector3(width,depth,axis.magnitude),Quaternion.LookRotation(axis));
            }

            static readonly Vector2[] BedOutline =
            {
                new(-.50f,-.21f), new(-.30f,-.48f), new(.16f,-.44f), new(.48f,-.32f),
                new(.50f,.14f), new(.26f,.49f), new(-.28f,.42f), new(-.46f,.23f),
            };

            public void Bed(Material paint, Vector3 center, Vector3 size, float yaw)
            {
                Quaternion rotation=Quaternion.Euler(0,yaw,0);
                Vector3 At(int index,float y)
                {
                    Vector2 corner=BedOutline[index];
                    return center+rotation*new Vector3(corner.x*size.x,(y+corner.x*.31f)*size.y,corner.y*size.z);
                }
                for(int i=0;i<BedOutline.Length;i++)
                {
                    int next=(i+1)%BedOutline.Length;
                    var a=At(i,-.5f); var b=At(next,-.5f); var c=At(next,.5f); var d=At(i,.5f);
                    Face(paint,a,d,c,b);
                    Face(paint,a,b,center-Vector3.up*size.y*.5f,center-Vector3.up*size.y*.5f);
                    Face(paint,c,d,center+Vector3.up*size.y*.5f,center+Vector3.up*size.y*.5f);
                }
            }

            public void Sector(Material paint, float radius, float width, float y, float height, float from, float sweep)
            {
                int count = Mathf.CeilToInt(sweep/8f);
                Vector3 At(float degrees, float r, float vertical)
                {
                    float angle = degrees*Mathf.Deg2Rad;
                    return new Vector3(Mathf.Sin(angle)*r,y+vertical,Mathf.Cos(angle)*r);
                }
                float half = height*.5f, inner = radius-width*.5f, outer = radius+width*.5f;
                for (int i=0;i<count;i++)
                {
                    float a=from+sweep*i/count, b=from+sweep*(i+1)/count;
                    var ia=At(a,inner,-half); var ib=At(b,inner,-half); var oa=At(a,outer,-half); var ob=At(b,outer,-half);
                    var Ita=At(a,inner,half); var Itb=At(b,inner,half); var Ota=At(a,outer,half); var Otb=At(b,outer,half);
                    Face(paint,Ota,Otb,Itb,Ita);
                    Face(paint,oa,ia,ib,ob);
                    Face(paint,oa,ob,Otb,Ota);
                    Face(paint,ib,ia,Ita,Itb);
                    if (i==0) Face(paint,ia,oa,Ota,Ita);
                    if (i==count-1) Face(paint,ob,ib,Itb,Otb);
                }
            }

            void Face(Material paint, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                if (!shapes.TryGetValue(paint,out var data))
                {
                    data=(new List<Vector3>(),new List<int>());
                    shapes.Add(paint,data);
                }
                int n=data.Vertices.Count;
                data.Vertices.Add(a); data.Vertices.Add(b); data.Vertices.Add(c); data.Vertices.Add(d);
                data.Triangles.Add(n); data.Triangles.Add(n+1); data.Triangles.Add(n+2);
                if (c != d)
                {
                    data.Triangles.Add(n); data.Triangles.Add(n+2); data.Triangles.Add(n+3);
                }
            }

            public void Mount(TriasWorld world, Transform group)
            {
                foreach (var pair in shapes)
                {
                    var mesh = new Mesh { name=group.name };
                    mesh.SetVertices(pair.Value.Vertices);
                    mesh.SetTriangles(pair.Value.Triangles,0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    world.meshes.Add(mesh);
                    var skin = Group(group,pair.Key.name);
                    skin.gameObject.layer=Decor;
                    skin.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=skin.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=pair.Key;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;
                    renderer.receiveShadows=false;
                }
            }
        }

        readonly struct PlantMotion
        {
            internal readonly Transform Root;
            internal readonly Vector3 Rest, Travel, Axis;
            internal readonly Quaternion RestRotation;
            internal readonly float Rate, Alignment;

            internal PlantMotion(Transform root,Vector3 travel,Vector3 axis,float rate,float alignment)
            {
                Root=root; Rest=root.localPosition; RestRotation=root.localRotation;
                Travel=travel; Axis=axis; Rate=rate; Alignment=alignment;
            }

            internal void Place(float seconds,float cycle,float eventStrength,float motion)
            {
                // Outbound after the heat dwell, inbound early in the next cycle.
                float shuttle = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.36f,cycle))
                    - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.91f,cycle));
                Root.localPosition=Rest+Travel*(shuttle*motion);
                Root.localRotation=RestRotation*Quaternion.AngleAxis((seconds*Rate+Alignment*eventStrength)*motion,Axis);
            }
        }

        void BuildMegastructure()
        {
            plantStarted=Time.unscaledTime;
            industry=Group(Root,"Living megastructure");
            industry.gameObject.layer=Decor;
            plantSignal=MaterialAt(Spec.id switch
            {
                "relay" => "#85b8c2", "basin" => "#bca47a", "island" => "#94a88b", _ => "#a25530",
            },true);
            switch (Spec.id)
            {
                case "foundry": BuildPhaseForge(); break;
                case "relay": BuildOrbitalArray(); break;
                case "basin": BuildExtractionShelf(); break;
                case "island": BuildWeatherRuins(); break;
            }
        }

        void AddRotor(Transform parent, string name, Vector3 position, Vector3 tilt, float radius, float width, float height,
            float rate, Material ceramic, Material frame, bool splitInTwo = false)
        {
            var ring=Group(parent,name,position);
            ring.localRotation=Quaternion.Euler(tilt);
            var batch=new PlantBatch();
            int divisions=splitInTwo?2:3;
            float quarter=360f/divisions;
            for (int i=0;i<divisions;i++)
            {
                float start=i*quarter+11, sweep=quarter-(i==divisions-1?35:26);
                batch.Sector(frame,radius,width,height*-.16f,height*.73f,start,sweep);
                batch.Sector(ceramic,radius+width*.09f,width*.81f,height*.32f,height*.47f,start+2,sweep-5);
                batch.Sector(frame,radius+width*.18f,width*.067f,height*.55f,height*.06f,start+10,sweep-20);
                batch.Sector(plantSignal,radius-width*.537f,width*.042f,-height*.14f,height*.064f,start+8,sweep-23);
                for (int seam=0;seam<3;seam++)
                {
                    float angle=(start+9+seam*(sweep-18)/2)*Mathf.Deg2Rad;
                    batch.Block(frame,new Vector3(Mathf.Sin(angle)*radius,height*.53f,Mathf.Cos(angle)*radius),
                        new Vector3(width*.15f,height*.13f,width*.86f),Quaternion.Euler(0,angle*Mathf.Rad2Deg+90,0));
                }
            }
            batch.Mount(this,ring);
            plantMotion.Add(new PlantMotion(ring,Vector3.zero,Vector3.up,rate,Mathf.Sign(rate)*18));
        }

        void BuildPhaseForge()
        {
            var refractory=MaterialAt("#a78e77");
            var ivory=MaterialAt("#d3b798");
            var iron=MaterialAt("#352b31");
            var warmMetal=MaterialAt("#774e40");
            plantHeat=MaterialAt("#df671d",true);

            // The cradle rises from the two existing heat-exchanger supports.
            var throat=Group(industry,"Phase forge and furnace throat",new Vector3(0,10.8f,0));
            var frame=new PlantBatch();
            for(int side=-1;side<=1;side+=2)
            {
                frame.Strut(iron,new Vector3(side*5.76f,-4.4f,0),new Vector3(side*4.25f,1.2f,0),2.6f,2.1f);
                frame.Strut(warmMetal,new Vector3(side*6.04f,-4.45f,.68f),new Vector3(side*4.5f,1.16f,.68f),.61f,.52f);
                frame.Block(refractory,new Vector3(side*3.65f,.8f,0),new Vector3(2.6f,2.9f,3.8f),Quaternion.Euler(0,0,side*-17));
            }
            frame.Sector(iron,3.8f,2.6f,-.89f,1.72f,0,360);
            frame.Sector(refractory,3.88f,1.74f,.52f,1.12f,12,336);
            frame.Sector(plantHeat,2.59f,.24f,-.06f,.19f,0,360);
            frame.Mount(this,throat);
            var heat=Part(throat,plantHeat,new Vector3(0,1.05f,0),new Vector3(2.1f,2.8f,2.1f),PrimitiveType.Sphere);
            heat.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            forgeBounce=Group(throat,"Reflected furnace light",new Vector3(0,-.8f,0)).gameObject.AddComponent<Light>();
            forgeBounce.type=LightType.Point;
            forgeBounce.renderMode=LightRenderMode.ForceVertex;
            forgeBounce.range=13;
            forgeBounce.color=Catalog.Tint("#dc8455");
            forgeBounce.intensity=.43f;

            AddRotor(industry,"Primary ceramic pressure mold",new Vector3(0,14.4f,0),new Vector3(18,19,-15),7.3f,3.4f,1.8f,.59f,ivory,iron);
            AddRotor(industry,"Suspended shear ring",new Vector3(0,21.1f,1.6f),new Vector3(-19,-18,11),12.8f,3.5f,2.45f,-.30f,refractory,iron);
            AddRotor(industry,"Outer thermal containment crown",new Vector3(0,28.6f,2.6f),new Vector3(19,-12,7),16.7f,3.3f,2.18f,.18f,ivory,iron,true);

            // The extrapolated axis of the quench line ends in a receiver behind the arena.
            var works=Group(industry,"Receiving works and transfer viaduct",new Vector3(0,0,64));
            Material Works(string slot) => slot switch
            {
                "Shell" => refractory, "Trim" => warmMetal, "Signal" => plantSignal, "Heat" => plantHeat, _ => iron,
            };
            if (Megastructure("KilnHall",works,new Vector3(0,0,12),0,1,Works))
            {
                // Five refractory flues of three heights stand behind the hall at the old stack positions.
                for(int i=0;i<5;i++)
                    Megastructure("KilnStack",works,new Vector3((i-2)*14.4f,0,25+(i%2)*3),i*37,(34+(i%3)*7)/40f,Works);
                var bridges=new PlantBatch();
                bridges.Strut(iron,new Vector3(-34,29,25),new Vector3(-61,24,53),4.6f,4.2f);
                bridges.Strut(iron,new Vector3(34,28,25),new Vector3(63,21,33),5.2f,4.6f);
                bridges.Mount(this,works);
            }
            else
            {
                var campus=new PlantBatch();
                campus.Block(iron,new Vector3(0,14,12),new Vector3(67,16,19));
                campus.Block(refractory,new Vector3(0,21,17),new Vector3(57,9,16));
                campus.Block(warmMetal,new Vector3(2,8,2),new Vector3(52,11,8));
                for(int i=0;i<5;i++)
                {
                    float x=(i-2)*14.4f;
                    float high=34+(i%3)*7;
                    campus.Block(iron,new Vector3(x,high*.5f,21),new Vector3(7,high,7));
                    campus.Block(refractory,new Vector3(x,high-1,21),new Vector3(9,5.4f,9));
                    campus.Block(plantSignal,new Vector3(x,22,10),new Vector3(.9f,3.2f,.21f));
                }
                campus.Strut(iron,new Vector3(-34,29,25),new Vector3(-61,24,53),4.6f,4.2f);
                campus.Strut(iron,new Vector3(34,28,25),new Vector3(63,21,33),5.2f,4.6f);
                campus.Mount(this,works);
            }

            var carrier=Group(industry,"Ceramic billet on overhead transfer",new Vector3(0,17.4f,4));
            var slab=new PlantBatch();
            slab.Block(iron,new Vector3(0,2.9f,0),new Vector3(4.5f,1.1f,4.1f));
            slab.Block(ivory,Vector3.zero,new Vector3(3.9f,3.9f,3.65f),Quaternion.Euler(16,45,0));
            slab.Block(warmMetal,new Vector3(0,-1.66f,0),new Vector3(3.5f,.69f,3.5f),Quaternion.Euler(0,45,0));
            slab.Mount(this,carrier);
            plantMotion.Add(new PlantMotion(carrier,new Vector3(0,9.3f,51),Vector3.up,.48f,11));

            var feed=Group(industry,"Gravity-inverted material transfer",new Vector3(0,13,0));
            var drops=new PlantBatch();
            for(int i=0;i<8;i++)
            {
                float angle=i*2.39f, r=.45f+(i%3)*.17f;
                drops.Block(plantHeat,new Vector3(Mathf.Sin(angle)*r,1.15f+i*.53f,Mathf.Cos(angle)*r),
                    new Vector3(.11f,.22f+(i%3)*.065f,.12f),Quaternion.Euler(i*18,angle*Mathf.Rad2Deg,i*-13));
            }
            drops.Mount(this,feed);
            plantMotion.Add(new PlantMotion(feed,Vector3.up*2.5f,Vector3.up,13f,24));
            BuildPlantSparks(feed);
            BuildPlantAudio(throat);
        }

        void BuildOrbitalArray()
        {
            var graphite=MaterialAt("#2d3845");
            var alloy=MaterialAt("#6e7c89");
            var porcelain=MaterialAt("#c2b9a2");
            // These are two different baselines, with a large shared aperture towards the planet.
            var platform = Group(industry,"Outer interferometer anchorage",new Vector3(35,0,66));
            var frame = new PlantBatch();
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*23.5f;
                frame.Strut(graphite,new Vector3(x,-18,-3),new Vector3(x+4,33,1),7,8);
                frame.Strut(alloy,new Vector3(x+3.9f,31,2),new Vector3(x-12,38,12),2.6f,4.4f);
                frame.Block(porcelain,new Vector3(x+2,24,-3),new Vector3(4.5f,20.5f,3.8f),Quaternion.Euler(0,0,-5));
            }
            frame.Block(graphite,new Vector3(4,8,-2),new Vector3(68,6.6f,10));
            frame.Block(alloy,new Vector3(4,11,-7),new Vector3(68,2.1f,1.8f));
            frame.Mount(this,platform);
            AddRotor(industry,"Near interferometer baseline",new Vector3(35,44,68),new Vector3(19,-28,-18),
                32,5.2f,4.2f,.25f,porcelain,graphite);
            AddRotor(industry,"Distant orbital alignment aperture",new Vector3(53,50,99),new Vector3(47,-6,-19),
                44,4.8f,3.7f,-.16f,alloy,graphite,true);
        }

        void BuildExtractionShelf()
        {
            var stone=MaterialAt("#625352");
            var strata=MaterialAt("#8d745e");
            var ore=MaterialAt("#9b7b62");
            var dark=MaterialAt("#35313a");
            var warmSteel=MaterialAt("#ae9880");
            BuildMeridianSection();
            var shelf=Group(industry,"Extraction cut and suspended seam",new Vector3(-43,7,88));
            shelf.localRotation=Quaternion.Euler(0,-16,0);
            var ridge=new PlantBatch();
            ridge.Bed(stone,new Vector3(-39,8,7),new Vector3(78,26,49),2);
            ridge.Bed(strata,new Vector3(-38,17,7),new Vector3(73,5.1f,47),2);
            ridge.Bed(stone,new Vector3(44,6,17),new Vector3(65,24,61),-8);
            ridge.Bed(strata,new Vector3(43,15,16),new Vector3(60,4.7f,57),-8);
            ridge.Strut(dark,new Vector3(-25,7,-4),new Vector3(-22,32,-4),4.3f,4.8f);
            ridge.Strut(dark,new Vector3(30,7,-4),new Vector3(21,32,-4),4.3f,4.8f);
            ridge.Strut(dark,new Vector3(-22,32,-4),new Vector3(21,32,-4),3.6f,3.35f);
            ridge.Strut(warmSteel,new Vector3(-22,33.6f,-6),new Vector3(21,33.6f,-6),1.2f,1.05f);
            ridge.Mount(this,shelf);
            // The mineral beds are offset along a single fault plane, with matching banding on each slice.
            for(int i=0;i<4;i++)
            {
                var chunk=Group(shelf,"Suspended mineral bed",new Vector3(-12+i*7.7f,11+i*4.15f,3+i*1.17f));
                chunk.localRotation=Quaternion.Euler(3,-11,-13);
                var rocks=new PlantBatch();
                rocks.Bed(stone,new Vector3(0,-.8f,0),new Vector3(27-i*2.9f,3.1f,14-i*1.8f),14+i*4);
                rocks.Bed(strata,new Vector3(.35f,.86f,.3f),new Vector3(26-i*2.9f,1.35f,13-i*1.8f),14+i*4);
                rocks.Bed(ore,new Vector3(-1.1f,1.64f,.4f),new Vector3(20-i*1.5f,.77f,10-i*1.5f),14+i*4);
                rocks.Mount(this,chunk);
                plantMotion.Add(new PlantMotion(chunk,new Vector3(i*1.05f,i*.72f,-i*.7f),Vector3.forward,0,4+i*2.5f));
            }
            var gantry=Group(shelf,"Mobile extraction coil",new Vector3(-12,23,-4));
            var coil=new PlantBatch();
            coil.Block(warmSteel,new Vector3(0,8.3f,0),new Vector3(7,2.25f,7));
            coil.Strut(dark,new Vector3(0,8.25f,0),new Vector3(0,.3f,0),1.65f,1.75f);
            for (int i=0;i<3;i++)
                coil.Sector(dark,4.25f,1.45f,0,1.4f,i*120+15,83);
            coil.Sector(plantSignal,3.75f,.15f,-.75f,.08f,12,322);
            coil.Mount(this,gantry);
            plantMotion.Add(new PlantMotion(gantry,new Vector3(22,-2.4f,0),Vector3.up,0,7));
        }

        void BuildWeatherRuins()
        {
            var mineral=MaterialAt("#73806c");
            var aged=MaterialAt("#a3aa83");
            var basalt=MaterialAt("#3a564f");
            var temple=Group(industry,"Weathering array beyond the river rim",new Vector3(-4,-6,57));
            temple.localRotation=Quaternion.Euler(0,-12,0);
            var ruins=new PlantBatch();
            for(int i=0;i<3;i++)
            {
                float x= (i-1)*12;
                float high= 23+4*i;
                ruins.Strut(basalt,new Vector3(x,-7,4),new Vector3(x+4,high,7),5.2f,4.4f);
                ruins.Strut(aged,new Vector3(x+.7f,2,2),new Vector3(x+4.4f,high-3,4),2.2f,1.42f);
                ruins.Block(mineral,new Vector3(x+3,high-2,7),new Vector3(7.7f,4,5.4f),Quaternion.Euler(-14,i*8,0));
            }
            ruins.Strut(mineral,new Vector3(-12,22,6),new Vector3(11,28,8),2.85f,4.1f);
            ruins.Mount(this,temple);
            AddRotor(temple,"Ancient weathering ring",new Vector3(8,30,7),new Vector3(59,16,-24),
                14.8f,3.05f,1.7f,.13f,aged,basalt,true);
            var lift=Group(temple,"Suspended water lenses",new Vector3(-2,14,1));
            var lenses=new PlantBatch();
            var damp=MaterialAt("#59958c");
            for(int i=0;i<9;i++)
            {
                float phase=i*.88f;
                lenses.Block(damp,new Vector3(Mathf.Sin(phase)*2.1f,i*1.12f,Mathf.Cos(phase)*1.8f),
                    new Vector3(.42f+i*.054f,.93f+i*.06f,.48f),Quaternion.Euler(i*12,32+i*12,-21));
            }
            lenses.Mount(this,lift);
            plantMotion.Add(new PlantMotion(lift,Vector3.up*2.4f,Vector3.up,3.1f,13));
        }

        void BuildPlantSparks(Transform parent)
        {
            var source=Group(parent,"Localized phase-forge sparks",new Vector3(0,1.8f,0));
            source.gameObject.layer=Decor;
            furnaceSparks=source.gameObject.AddComponent<ParticleSystem>();
            furnaceSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=furnaceSparks.main;
            main.playOnAwake=false;
            main.loop=false;
            main.duration=.95f;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.25f,.75f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.85f,2.65f);
            main.startSize=new ParticleSystem.MinMaxCurve(.032f,.082f);
            main.startColor=new Color(.94f,.52f,.25f,.9f);
            main.maxParticles=24;
            var emission=furnaceSparks.emission;
            emission.rateOverTime=0;
            emission.SetBursts(new [] { new ParticleSystem.Burst(.04f,(short)16) });
            var shape=furnaceSparks.shape;
            shape.shapeType=ParticleSystemShapeType.Cone;
            shape.angle=37;
            shape.radius=.62f;
            source.localRotation=Quaternion.Euler(-87,0,0);
            var renderer=furnaceSparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=plantHeat;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;

            var shader=Resources.Load<Shader>("TRIAS/Vapor");
            if (shader==null || !shader.isSupported) return;
            var material=new Material(shader);
            Paint.Add(material);
            var outlet=Group(industry,"Furnace pressure release",new Vector3(6.6f,12.4f,1.6f));
            outlet.gameObject.layer=Decor;
            pressureRelease=outlet.gameObject.AddComponent<ParticleSystem>();
            pressureRelease.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var vapor=pressureRelease.main;
            vapor.playOnAwake=false;
            vapor.loop=false;
            vapor.duration=1.2f;
            vapor.startLifetime=new ParticleSystem.MinMaxCurve(1.8f,2.4f);
            vapor.startSpeed=new ParticleSystem.MinMaxCurve(.65f,1.25f);
            vapor.startSize=new ParticleSystem.MinMaxCurve(.75f,1.6f);
            vapor.startColor=new Color(.70f,.57f,.49f,.12f);
            vapor.maxParticles=12;
            var outletShape=pressureRelease.shape;
            outletShape.shapeType=ParticleSystemShapeType.Cone;
            outletShape.angle=18;
            outletShape.radius=.42f;
            outlet.localRotation=Quaternion.Euler(-62,0,0);
            var release=pressureRelease.emission;
            release.rateOverTime=0;
            release.SetBursts(new[] { new ParticleSystem.Burst(.08f,(short)10) });
            var vaporRenderer=pressureRelease.GetComponent<ParticleSystemRenderer>();
            vaporRenderer.sharedMaterial=material;
            vaporRenderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        void BuildPlantAudio(Transform source)
        {
            plantAudio=Group(source,"Forge transmission and pressure release").gameObject.AddComponent<AudioSource>();
            plantAudio.playOnAwake=false;
            plantAudio.loop=true;
            plantAudio.spatialBlend=.86f;
            plantAudio.minDistance=6;
            plantAudio.maxDistance=32;
            plantAudio.rolloffMode=AudioRolloffMode.Linear;
            plantAudio.dopplerLevel=0;
            plantAudio.priority=180;
            plantAudio.volume=0;
            const int rate=16000, seconds=8;
            var samples=new float[rate*seconds];
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                float motor=Mathf.Sin(t*2*Mathf.PI*47)*.34f+Mathf.Sin(t*2*Mathf.PI*73)*.22f;
                float pressure=.5f+.5f*Mathf.Sin(t*Mathf.PI*.25f-.48f);
                float vibration=Mathf.Sin(t*2*Mathf.PI*113)*Mathf.Sin(t*2*Mathf.PI*1.625f);
                float air=Mathf.Sin(t*2*Mathf.PI*389)*Mathf.Sin(t*2*Mathf.PI*541);
                samples[i]=(motor+vibration*.15f+air*pressure*pressure*.09f)*(.23f+.29f*pressure);
            }
            var clip=AudioClip.Create("TRIAS original phase forge",samples.Length,1,rate,false);
            clip.SetData(samples,0);
            plantAudio.clip=clip;
            plantAudio.Play();
        }

        void AnimateMegastructure(float time,float clock,int seed,Vector3 eye,float motion,float effects,float volume)
        {
            float distance=(eye-industry.position).sqrMagnitude;
            float seconds=Mathf.Max(0,clock>=0 ? clock : time-plantStarted);
            float period=Spec.id switch { "foundry" => 78, "relay" => 107, "basin" => 94, _ => 121 };
            float offset=seed==0 ? 0 : (uint)seed%13;
            float cycle=Mathf.Repeat(seconds+offset,period)/period;
            float alignment=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.51f,cycle))
                - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.63f,.73f,cycle));
            float glow=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.54f,.56f,cycle))
                - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.60f,.66f,cycle));

            if (moltenSurface!=null)
            {
                moltenSurface.SetFloat("_FlowClock",seconds * motion);
                moltenSurface.SetFloat("_Heat",glow*effects);
                moltenSurface.SetFloat("_Detail",effects);
                float hit=Mathf.Clamp01((impactUntil-time)*3f)*effects;
                moltenSurface.SetVector("_Impact",new Vector4(moltenImpact.x,moltenImpact.z,hit,0));
            }

            if (time<plantTick) return;
            plantTick=time+(distance<=110*110 ? .036f : .115f);
            foreach (var assembly in plantMotion)
                if ((assembly.Root.position-eye).sqrMagnitude<225*225)
                    assembly.Place(seconds,cycle,alignment,motion);
            if (plantHeat!=null)
                plantHeat.color=Color.Lerp(Catalog.Tint("#dc6224"),Catalog.Tint("#f4a953"),glow*effects*.55f);
            if (forgeBounce!=null) forgeBounce.intensity=.43f+glow*effects*.23f;
            int eventNumber=Mathf.FloorToInt((seconds+offset)/period);
            if (eventNumber<lastPlantBurst) lastPlantBurst=-1;
            if (furnaceSparks!=null && lastPlantBurst!=eventNumber && glow>.82f && effects>.55f
                && distance<60*60 && QualitySettings.GetQualityLevel()>1)
            {
                lastPlantBurst=eventNumber;
                furnaceSparks.Play();
                if (pressureRelease != null) pressureRelease.Play();
            }
            if (plantAudio!=null)
                plantAudio.volume=Mathf.Clamp01(volume)*(.023f+glow*.022f);
        }

        internal void ReactMegastructure(Vector3 at,float now)
        {
            var channel=Spec.hazard;
            if (channel==null || now<impactUntil-.25f || Mathf.Abs(at.y-channel.y)>.38f
                || Mathf.Abs(at.x-channel.x)>channel.w*.55f || Mathf.Abs(at.z-channel.z)>channel.d*.5f) return;
            moltenImpact=at;
            impactUntil=now+.34f;
        }
    }
}
