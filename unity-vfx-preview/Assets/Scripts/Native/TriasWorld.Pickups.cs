using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Transform[] pickupBeacons;
        readonly Dictionary<string,Material> pickupTints = new();

        void BuildPickup(Site item, Catalog catalog, int index)
        {
            bool weapon=Catalog.Find(catalog.weapons,item.type)!=null;
            var choice=Catalog.Find(weapon ? catalog.weapons : catalog.powerups,item.type);
            if (!pickupTints.TryGetValue(item.type,out var tint))
            {
                tint=MaterialAt(choice?.color ?? "#f6faab",true);
                pickupTints.Add(item.type,tint);
            }
            var station=Group(Root,choice?.label ?? item.type,item.Position);
            var pad=new PlantBatch();
            for(int i=0;i<6;i++)
            {
                pad.Sector(Graphite,.42f,.31f,.036f,.07f,i*60,60);
                pad.Sector(island ? MapFinish("DarkStone") : Hardware,.58f,.113f,.058f,.07f,i*60+5,50);
            }
            pad.Mount(this,station);

            var ready=Group(station,"Available equipment");
            Pickups[index]=ready;
            var cradle=new PlantBatch();
            for(int side=0;side<4;side++)
                cradle.Sector(tint,.555f,.037f,.105f,.036f,side*90+11,65);
            cradle.Mount(this,ready);

            var mark=Group(ready,"Suspended equipment",Vector3.up*.92f);
            pickupMotion[index]=mark;
            if (weapon)
            {
                var art=Group(mark,"Weapon display",(Vector3.back*.32f));
                art.localScale=Vector3.one*(item.type=="rift" ? 1.28f : item.type=="lance" ? 1.36f : 1.75f);
                art.localRotation=Quaternion.Euler(0,0,-13);
                TriasModels.Gun(this,art,item.type,tint);
                foreach(var renderer in art.GetComponentsInChildren<MeshRenderer>())
                    renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            else
                BuildPowerCell(mark,item.type,tint);

            var beacon=Group(ready,"Equipment locator",Vector3.up*1.57f);
            var marker=new PlantBatch();
            var point=Vector3.up*.085f;
            marker.Strut(tint,new Vector3(-.12f,.205f,0),point,.022f,.028f);
            marker.Strut(tint,point,new Vector3(.12f,.205f,0),.022f,.028f);
            // Give the locator a second cross-section so it reads even edge-on.
            marker.Strut(tint,new Vector3(0,.205f,-.12f),point,.022f,.028f);
            marker.Strut(tint,point,new Vector3(0,.205f,.12f),.022f,.028f);
            marker.Mount(this,beacon);
            pickupBeacons[index]=beacon;
        }

        BlenderParts powerCells;

        void BuildPowerCell(Transform mark,string type,Material accent)
        {
            // Blender cells (tools/blender/build-powerups.py); the block assembly below is the fallback.
            powerCells ??= new BlenderParts("TRIAS/PowerCells/PowerCells");
            string key = type == "med" ? "Med" : type == "shield" ? "Shield" : "Surge";
            if (powerCells.Has(key))
            {
                powerCells.Bind(key, Group(mark, key + " cell"), slot => slot switch
                {
                    "Glow" => accent, "Ceramic" => WeaponCeramic, "Graphite" => WeaponGraphite, "Alloy" => WeaponAlloy, _ => Dark
                }, false);
                return;
            }
            var art=new PlantBatch();
            if(type=="med")
            {
                art.Block(Porcelain,Vector3.zero,new Vector3(.65f,.45f,.40f));
                art.Block(Graphite,new Vector3(0,0,-.224f),new Vector3(.55f,.39f,.052f));
                art.Block(Graphite,new Vector3(0,0,.224f),new Vector3(.55f,.39f,.052f));
                for(int face=-1;face<=1;face+=2)
                {
                    float z=face*.262f;
                    art.Block(accent,new Vector3(0,0,z),new Vector3(.29f,.078f,.027f));
                    art.Block(accent,new Vector3(0,0,z+.001f*face),new Vector3(.078f,.29f,.027f));
                }
                art.Block(Hardware,new Vector3(0,.258f,0),new Vector3(.26f,.079f,.24f));
            }
            else if(type=="shield")
            {
                // Three armored plates surround a differently oriented, unmistakable blue shield core.
                for(int i=0;i<3;i++)
                {
                    var q=Quaternion.Euler(0,i*120,0);
                    art.Block(Graphite,q*new Vector3(0,-.016f,.243f),new Vector3(.36f,.61f,.10f),q);
                    art.Block(Porcelain,q*new Vector3(0,.06f,.306f),new Vector3(.32f,.36f,.075f),q);
                    art.Block(accent,q*new Vector3(0,.104f,.352f),new Vector3(.036f,.22f,.025f),q);
                }
                art.Block(accent,Vector3.up*.042f,new Vector3(.27f,.43f,.27f),Quaternion.Euler(27,45,24));
            }
            else
            {
                art.Block(accent,Vector3.zero,new Vector3(.34f,.34f,.34f),Quaternion.Euler(35,52,40));
                for (int i=0;i<3;i++)
                {
                    var q=Quaternion.Euler(0,i*120,0);
                    art.Block(Graphite,q*new Vector3(0,-.14f,.22f),new Vector3(.24f,.28f,.21f),q*Quaternion.Euler(23,0,0));
                    art.Block(Hardware,q*new Vector3(0,.28f,.28f),new Vector3(.19f,.15f,.14f),q*Quaternion.Euler(39,0,0));
                    art.Block(accent,q*new Vector3(0,-.13f,.365f),new Vector3(.045f,.075f,.024f),q);
                }
            }
            art.Mount(this,mark);
        }

        void AnimatePickups(float time,Vector3 eye,float motion,float effects)
        {
            for(int i=0;i<pickupMotion.Length;i++)
            {
                if(!Pickups[i].gameObject.activeSelf) continue;
                var display=pickupMotion[i];
                float age=time-plantStarted;
                if((display.position-eye).sqrMagnitude<72*72)
                {
                    display.localPosition=Vector3.up*(.91f+Mathf.Sin(age*1.85f+i)*.10f*motion);
                    display.localRotation=Quaternion.Euler(0,33+i*51+age*18*motion,0);
                }
                pickupBeacons[i].localPosition=Vector3.up*(1.51f+Mathf.Sin(age*1.85f+i)*.06f*motion);
                pickupBeacons[i].localScale=Vector3.one*(.92f+Mathf.Sin(age*2.35f+i)*.13f*effects*motion);
            }
        }
    }
}
