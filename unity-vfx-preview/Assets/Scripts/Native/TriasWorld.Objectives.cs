using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        sealed class ControlHardware
        {
            internal LineRenderer Boundary, Progress;
            internal MeshRenderer Signal;
            internal Transform Lens;
            internal float LensHeight = .28f;
            internal Material Tint;
            internal int Step = -1;
            internal bool Contested, Linked;
        }

        sealed class CoreHardware
        {
            internal Transform Root, Halo, Spindle;
            internal readonly Transform[] Shutters = new Transform[3];
            internal Material Charge;
            internal Color Team;
            internal float Opening, Life = 1;
            internal bool Open;
        }

        sealed class FlagHardware
        {
            internal Transform Marker, Standard;
            internal Vector3 Target;
            internal float Heading;
            internal string Status;
        }

        ControlHardware[] controlHardware;
        static BlenderParts objectiveParts;
        CoreHardware[] coreHardware;
        CoreHardware defenseHardware;
        FlagHardware[] flagHardware;
        Material disputeSignal, dormantSignal;

        Transform BuildControl(Site site, int index)
        {
            var art=controlHardware[index]=new ControlHardware();
            art.Boundary=Ring(Root,site.Position+Vector3.up*.067f,site.radius>0 ? site.radius : 3.05f,TeamNeutral,.068f,48);
            art.Boundary.name="Contested zone footprint " + site.label;
            var panel=Group(art.Boundary.transform,"Phase relay terminal");
            var terminal=new PlantBatch();
            for(int i=0;i<3;i++)
            {
                float angle=i*120;
                terminal.Sector(Graphite,.46f,.30f,.045f,.10f,angle+4,111);
                terminal.Sector(Porcelain,.48f,.19f,.126f,.11f,angle+11,97);
                terminal.Sector(Hardware,.48f,.12f,.189f,.036f,angle+30,48);
            }
            terminal.Block(Graphite,Vector3.up*.073f,new Vector3(.30f,.14f,.30f),Quaternion.Euler(0,45,0));
            terminal.Mount(this,panel);
            // A slender relay mast lifts the phase lens to 2.35 m so the objective reads across the arena;
            // it is far narrower than a pilot, so it never reads as cover. The low terminal is the fallback.
            objectiveParts ??= new BlenderParts("TRIAS/Objectives/Objectives");
            if (objectiveParts.Has("ControlRelay"))
            {
                objectiveParts.Bind("ControlRelay",Group(panel,"Relay mast"),slot => slot switch
                {
                    "Ceramic" => Porcelain, "Alloy" => Hardware, _ => Graphite,
                },true);
                art.LensHeight=2.35f;
            }
            art.Lens=Group(panel,"Phase lens",Vector3.up*art.LensHeight);
            var lens=Part(art.Lens,TeamNeutral,Vector3.zero,art.LensHeight>1 ? new Vector3(.30f,.24f,.30f) : new Vector3(.25f,.19f,.25f));
            lens.localRotation=Quaternion.Euler(25,45,28);
            art.Signal=lens.GetComponent<MeshRenderer>();
            art.Signal.shadowCastingMode=ShadowCastingMode.Off;
            art.Progress=Line(panel,Vector3.zero,Vector3.zero,TeamNeutral,.098f);
            art.Progress.name="Authoritative capture progress";
            art.Progress.enabled=false;
            return art.Boundary.transform;
        }

        internal void SetControl(int index,int owner,float progress,bool contested,bool linked)
        {
            var art=controlHardware[index];
            Material faction=owner>=0&&owner<TeamLight.Length ? TeamLight[owner] : TeamNeutral;
            art.Tint=contested ? disputeSignal : faction;
            art.Contested=contested;
            art.Linked=linked || Mode=="dom";
            art.Boundary.sharedMaterial=art.Tint;
            art.Progress.sharedMaterial=art.Tint;
            art.Signal.sharedMaterial=art.Linked || progress>0 || contested ? art.Tint : dormantSignal;
            int samples=Mathf.Clamp(Mathf.RoundToInt(progress*48),0,48);
            if(samples==art.Step) return;
            art.Step=samples;
            art.Progress.enabled=samples>0;
            if(samples==0) return;
            art.Progress.positionCount=samples+1;
            // The inner arc gives an exact progress readout independently from team color.
            for(int i=0;i<=samples;i++)
            {
                float angle=i*Mathf.PI/24f;
                art.Progress.SetPosition(i,new Vector3(Mathf.Sin(angle)*.80f,.04f,Mathf.Cos(angle)*.80f));
            }
        }

        CoreHardware BuildCore(Vector3 at,int team,float radius,float height,bool defense)
        {
            var core=new CoreHardware { Root=Group(Root,defense ? "Horde containment core" : "Siege phase capacitor",at),
                Team=TeamLight[team].color };
            var dock=Group(Root,"Anchored containment cradle",at-Vector3.up*(defense ? .78f : height+.23f));
            var seat=new PlantBatch();
            for(int port=0;port<3;port++)
            {
                float angle=port*120f+8;
                seat.Sector(Graphite,radius*.92f,radius*.30f,.069f,.12f,angle,106);
                seat.Sector(Hardware,radius*.86f,radius*.17f,.17f,.071f,angle+7,85);
                seat.Sector(TeamLight[team],radius*.90f,.027f,.206f,.019f,angle+31,31);
            }
            seat.Mount(this,dock);
            core.Charge=MaterialAt("#ffffff",true);
            core.Charge.color=core.Team*.90f;
            core.Spindle=Group(core.Root,"Suspended tri-phase spindle");
            var engine=new PlantBatch();
            engine.Block(Graphite,Vector3.zero,new Vector3(radius*.69f,height*1.52f,radius*.69f),Quaternion.Euler(0,45,0));
            engine.Block(core.Charge,Vector3.zero,new Vector3(radius*.38f,height*1.66f,radius*.38f),Quaternion.Euler(0,25,29));
            for(int i=-1;i<=1;i+=2)
            {
                engine.Sector(Hardware,radius*.38f,radius*.20f,i*height*.61f,height*.17f,0,360);
                engine.Sector(core.Charge,radius*.38f,.024f,i*height*.52f,.055f,4,331);
            }
            engine.Mount(this,core.Spindle);
            for(int i=0;i<core.Shutters.Length;i++)
            {
                var joint=core.Shutters[i]=Group(core.Root,"Ceramic containment shutter");
                joint.localRotation=Quaternion.Euler(0,i*120,0);
                var shutter=new PlantBatch();
                shutter.Sector(Graphite,radius*.66f,radius*.25f,0,height*1.35f,-37,74);
                shutter.Sector(Porcelain,radius*.75f,radius*.19f,height*.23f,height*.86f,-33,66);
                shutter.Sector(Hardware,radius*.76f,radius*.18f,-height*.42f,height*.26f,-30,60);
                shutter.Sector(core.Charge,radius*.862f,.029f,-height*.14f,.045f,-29,58);
                shutter.Mount(this,joint);
            }
            core.Halo=Group(core.Root,"Shield field emitters");
            for(int i=0;i<3;i++)
            {
                var field=Group(core.Halo,"Shield field layer",Vector3.up*((i-1)*height*.52f));
                field.localRotation=Quaternion.Euler(i==1?13:-13,i*31,i==1?8:-8);
                var bands=new PlantBatch();
                for(int j=0;j<3;j++)
                    bands.Sector(TeamLight[team],radius*(i==1?1.32f:1.16f),.017f,0,.033f,j*120+15,77);
                bands.Mount(this,field);
            }
            if(defense) core.Open=true;
            return core;
        }

        internal void SetCore(int index,float life,bool shielded)
            => UpdateCore(coreHardware[index],life,!shielded);

        internal void SetDefendedCore(float life) => UpdateCore(defenseHardware,life,true);

        static void UpdateCore(CoreHardware core,float life,bool open)
        {
            core.Root.gameObject.SetActive(life>0);
            core.Life=Mathf.Clamp01(life);
            core.Open=open;
            core.Halo.gameObject.SetActive(!open);
        }

        Transform BuildFlag(Vector3 at,int team)
        {
            var flag=flagHardware[team]=new FlagHardware { Target=at, Status="home" };
            var dock=Group(Root,"Flag receiving dock",at);
            var baseBatch=new PlantBatch();
            for(int i=0;i<3;i++)
            {
                baseBatch.Sector(Graphite,.56f,.29f,.07f,.14f,i*120+9,103);
                baseBatch.Sector(Porcelain,.59f,.14f,.151f,.077f,i*120+18,88);
                baseBatch.Sector(TeamLight[team],.651f,.030f,.19f,.027f,i*120+28,59);
            }
            baseBatch.Mount(this,dock);
            flag.Marker=Group(Root,"Portable faction standard",at);
            flag.Standard=Group(flag.Marker,"Ceramic pennant");
            var standard=new PlantBatch();
            standard.Block(Graphite,Vector3.up*.83f,new Vector3(.074f,1.66f,.075f));
            standard.Block(Hardware,Vector3.up*.45f,new Vector3(.14f,.22f,.14f));
            standard.Block(Graphite,new Vector3(.32f,1.56f,.0f),new Vector3(.72f,.09f,.086f));
            standard.Block(Porcelain,new Vector3(.32f,1.42f,0),new Vector3(.64f,.20f,.100f));
            standard.Block(Team[team],new Vector3(.35f,1.16f,0),new Vector3(.60f,.38f,.084f));
            standard.Block(Team[team],new Vector3(.45f,.94f,0),new Vector3(.43f,.16f,.080f),Quaternion.Euler(0,0,-13));
            standard.Block(Graphite,new Vector3(.34f,1.02f,.001f),new Vector3(.53f,.065f,.093f));
            standard.Block(TeamLight[team],new Vector3(.37f,1.50f,0),new Vector3(.48f,.033f,.105f));
            standard.Mount(this,flag.Standard);
            var stamp=TriasMarkings.Emblem(team);
            meshes.Add(stamp);
            for(int side=-1;side<=1;side+=2)
            {
                var badge=TriasMarkings.Model(flag.Standard,"Faction crest",stamp,TeamLight[team],
                    new Vector3(.35f,1.196f,side*.052f),Vector3.one*.186f,false);
                badge.localRotation=Quaternion.Euler(0,side==-1?180:0,0);
            }
            return flag.Marker;
        }

        internal void SetFlag(int index,Vector3 at,string status,float bearing)
        {
            var flag=flagHardware[index];
            bool changed=flag.Status!=status;
            flag.Status=status;
            flag.Target=at;
            flag.Heading=bearing;
            if(changed || (flag.Marker.position-at).sqrMagnitude>16) flag.Marker.position=at;
        }

        void AnimateObjectives(float time,float motion)
        {
            float seconds=time-plantStarted;
            if(controlHardware!=null)
                foreach(var art in controlHardware)
                {
                    float lift=(art.Contested?.039f:.02f)*Mathf.Sin(seconds*(art.Contested?4f:2f))*motion;
                    art.Lens.localPosition=Vector3.up*(art.LensHeight+lift);
                    art.Lens.localRotation=Quaternion.Euler(0,seconds*(art.Linked?22f:9f)*motion,0);
                }
            if(coreHardware!=null)
                foreach(var core in coreHardware) AnimateCore(core,seconds,motion);
            if(defenseHardware!=null) AnimateCore(defenseHardware,seconds,motion);
            if(flagHardware==null) return;
            float smoothing=1-Mathf.Exp(-Time.unscaledDeltaTime*24);
            for(int i=0;i<flagHardware.Length;i++)
            {
                var flag=flagHardware[i];
                bool carried=flag.Status=="carried";
                flag.Marker.position=Vector3.Lerp(flag.Marker.position,flag.Target,smoothing);
                flag.Standard.localScale=Vector3.one*(carried?.71f:1f);
                flag.Standard.localRotation=Quaternion.Euler(carried?-14:0,carried?flag.Heading:i*60+32,
                    Mathf.Sin(seconds*1.2f+i)*(carried?3.5f:1.8f)*motion);
            }
        }

        static void AnimateCore(CoreHardware core,float seconds,float motion)
        {
            if(!core.Root.gameObject.activeSelf) return;
            float target=core.Open?.19f:0;
            core.Opening=motion>.01f ? Mathf.MoveTowards(core.Opening,target,Time.unscaledDeltaTime*.62f) : target;
            for(int i=0;i<core.Shutters.Length;i++)
                core.Shutters[i].localPosition=core.Shutters[i].localRotation*Vector3.forward*core.Opening;
            core.Spindle.localRotation=Quaternion.Euler(0,seconds*(core.Open?18f:9f)*motion,0);
            core.Halo.localRotation=Quaternion.Euler(0,seconds*12f*motion,0);
            core.Charge.color=Color.Lerp(core.Team,new Color(1f,.42f,.22f),Mathf.Clamp01((.44f-core.Life)*1.6f))*.90f;
        }
    }
}
