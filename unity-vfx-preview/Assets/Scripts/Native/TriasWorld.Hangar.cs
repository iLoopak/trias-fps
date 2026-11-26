using UnityEngine;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Transform hangarCourier, hangarBeacon;
        Material hangarLamp;

        void BuildHangar()
        {
            if (orbitalSky != null)
            {
                orbitalSky.SetFloat("_SkyScene", 2);
                orbitalSky.SetVector("_Planet", new Vector4(-.44f, .34f, -1, .40f));
                orbitalSky.SetColor("_Body", Catalog.Tint("#62799b"));
                orbitalSky.SetColor("_LightColor", Catalog.Tint("#b4c7df"));
            }
            RenderSettings.ambientSkyColor = Catalog.Tint("#93aabd");
            RenderSettings.ambientEquatorColor = Catalog.Tint("#657582");
            var graphite = MaterialAt("#293a49", false, "bulkhead");
            var deck = MaterialAt("#657786", false, "deck");
            deck.mainTextureScale = new Vector2(6,4);
            var brass = MaterialAt("#a67e57", false, "armor");
            var amber = MaterialAt("#e8a065", true);
            hangarLamp = MaterialAt("#a9dde0", true);
            Armor(Root, deck, new Vector3(0,-.17f,-.2f), new Vector3(11,.34f,8));
            var rails = Group(Root,"Panoramic observation window",new Vector3(0,0,-3.5f));
            for (int side = -1; side <= 1; side += 2)
            {
                var rib = Armor(rails,graphite,new Vector3(side*3.1f,2.1f,0),new Vector3(.43f,4.4f,.55f));
                rib.localRotation = Quaternion.Euler(0,0,side*13);
                Armor(rails,brass,new Vector3(side*3.15f,.12f,.09f),new Vector3(3.2f,.11f,.17f));
            }
            Armor(rails,graphite,new Vector3(0,3.8f,0),new Vector3(8.7f,.46f,.64f));
            Armor(rails,graphite,new Vector3(0,.35f,0),new Vector3(8,.46f,.42f));
            for (int i = -4; i <= 4; i++)
                Armor(rails,i%2 == 0 ? amber : hangarLamp,new Vector3(i*.73f,.57f,.23f),new Vector3(.37f,.043f,.04f),false);
            var rigLight = Group(Root,"Pilot bounce",new Vector3(-1.3f,2.4f,1.4f)).gameObject.AddComponent<Light>();
            rigLight.type = LightType.Point;
            rigLight.color = Catalog.Tint("#edc097");
            rigLight.intensity = 2.4f;
            rigLight.range = 4;
            rigLight.shadows = LightShadows.None;
            var stand = Group(Root,"Deployment stand",new Vector3(-1.15f,.02f,0));
            Part(stand,graphite,Vector3.zero,new Vector3(1.38f,.04f,1.38f),PrimitiveType.Cylinder);
            Ring(stand,new Vector3(0,.052f,0),.63f,amber,.026f,36);
            hangarBeacon = Armor(stand,hangarLamp,new Vector3(.56f,.063f,.52f),new Vector3(.16f,.031f,.078f),false);
            var gantry = Group(Root,"Cargo gantry",new Vector3(-18,1,-39));
            gantry.localRotation = Quaternion.Euler(0,-25,-14);
            Kit.SpaceFixture(gantry,"monorail_trackStraight",new Vector3(37,2.2f,4.7f),new Vector3(0,1.7f,0),90,
                (graphite,Dark,Glass,brass));
            Armor(gantry,amber,new Vector3(0,3.9f,-1),new Vector3(28,.18f,.18f),false);
            hangarCourier = Group(Root,"Distant transfer shuttle",new Vector3(8,9,-44));
            Kit.Spacecraft(hangarCourier,"craft_cargoB",13,Steel,graphite,Glass,brass);
        }

        internal void AnimateHangar(float age, float motion)
        {
            if (hangarCourier != null)
            {
                hangarCourier.localPosition = new Vector3(8 + Mathf.Sin(age*.23f)*17*motion,9 + Mathf.Sin(age*.39f)*1.2f*motion,-44);
                hangarCourier.localRotation = Quaternion.Euler(4,77 + Mathf.Sin(age*.24f)*12*motion,-9);
            }
            if (hangarBeacon != null)
                hangarLamp.color = Color.Lerp(Catalog.Tint("#a9dde0"),Catalog.Tint("#628a94"),
                    (.5f + .5f*Mathf.Sin(age*2.7f))*motion*.58f);
        }
    }
}
