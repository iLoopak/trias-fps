using UnityEngine;

namespace Trias.Native
{
    internal static class PhaseArmory
    {
        internal const string Chamber = "Suspended chamber";

        internal static float Muzzle(string key) => key switch
        {
            "scatter" => .48f, "lance" => .64f, "arc" => .41f, "rift" => .54f, _ => .51f
        };

        internal static void Build(TriasWorld world, Transform root, string key, Material accent)
        {
            if (world.Kit.Armory.Build(world,root,key,accent)) return;
            if (key == "pulse" && world.Spec.id == "relay")
            {
                BuildRelayPulse(world,root,accent);
                return;
            }

            bool scatter = key == "scatter", lance = key == "lance", arc = key == "arc", rift = key == "rift";
            float bore = Muzzle(key), width = scatter || rift ? .26f : arc ? .18f : .205f;
            void Plate(Transform parent, Material material, float x, float y, float z, float w, float h, float l, float pitch = 0)
            {
                world.Armor(parent, material, new Vector3(x,y,z), new Vector3(w,h,l), false)
                    .localRotation = Quaternion.Euler(pitch,0,0);
            }

            Plate(root, world.Graphite, 0, .018f, .045f, width*.86f, .157f, .40f);
            Plate(root, world.Dark, .018f, -.084f, -.075f, .084f, .20f, .122f, -21);
            Plate(root, world.Dark, 0, .009f, -.205f, width*.73f, .129f, .208f);
            Plate(root, world.SafetyAmber, 0, .091f, -.160f, width*.76f, .038f, .188f);
            Plate(root, world.Graphite, 0, .095f, .053f, width*.61f, .043f, .33f);

            var chamber = TriasWorld.Group(root, Chamber, new Vector3(0,.026f,bore-.096f));
            for (int side = -1; side <= 1; side += 2)
            {
                Plate(root, world.Porcelain, side*width*.42f, .052f, .022f, .059f, .14f, arc ? .29f : .35f, -6);
                Plate(root, world.Insulation, side*width*.43f, -.036f, .106f, .048f, .062f, .20f);
                Plate(root, world.SafetyAmber, side*width*.53f, .079f, -.075f, .013f, .044f, .143f, -5);
                Plate(root, accent, side*width*.57f, -.002f, .109f, .012f, .016f, .071f);
            }

            if (scatter)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side*.079f;
                    Plate(chamber, world.Porcelain, x, .01f, -.009f, .133f, .146f, .192f, -4);
                    Plate(chamber, world.Dark, x, .003f, .089f, .096f, .092f, .019f);
                    Plate(chamber, world.SafetyAmber, x, .071f, -.026f, .08f, .025f, .134f);
                }
            }
            else if (lance)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Plate(root, world.Hardware, side*.061f, .035f, .343f, .035f, .041f, .222f);
                    Plate(chamber, world.Porcelain, side*.065f, .012f, .015f, .049f, .073f, .172f, -9);
                }
                Plate(chamber, accent, 0, .011f, .016f, .028f, .027f, .092f);
            }
            else if (rift)
            {
                var cell = TriasWorld.Part(chamber, accent, new Vector3(0,0,-.021f), new Vector3(.105f,.105f,.134f), PrimitiveType.Sphere);
                cell.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int sector = 0; sector < 3; sector++)
                {
                    var shell = TriasWorld.Group(chamber, "Containment petal", new Vector3(0,0,.022f));
                    shell.localRotation = Quaternion.Euler(90,0,sector*120+22);
                    world.CeramicArc(shell, world.Porcelain, Vector3.zero, new Vector3(.138f,.12f,.138f), 87);
                    world.CeramicArc(shell, world.Hardware, Vector3.up*.067f, new Vector3(.126f,.034f,.126f), 74);
                }
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float depth = arc ? side*.027f : 0;
                    Plate(chamber, world.Porcelain, side*width*.33f, .018f, depth, .067f, .134f, arc ? .13f : .192f, -12);
                    Plate(chamber, world.Graphite, side*width*.27f, -.012f, depth+.061f, .044f, .047f, .052f);
                    Plate(chamber, accent, side*width*.43f, .030f, depth-.076f, .016f, .026f, .081f);
                }
            }

            float sight = lance ? .156f : .124f;
            for (int side = -1; side <= 1; side += 2)
                Plate(root, world.Dark, side*.029f, sight, .148f, .024f, .069f, lance ? .111f : .054f, -10);
            Plate(root, world.Graphite, 0, sight+.033f, .148f, .083f, .021f, lance ? .111f : .054f);
            Plate(root, accent, 0, sight-.018f, .149f, .038f, .008f, .036f);
        }

        static void BuildRelayPulse(TriasWorld world, Transform root, Material accent)
        {
            void Case(Transform parent, Material material, Vector3 at, Vector3 size, float pitch = 0, float roll = 0)
            {
                world.Armor(parent,material,at,size,false).localRotation = Quaternion.Euler(pitch,0,roll);
            }

            Case(root,world.Graphite,new Vector3(0,.01f,.009f),new Vector3(.173f,.156f,.40f));
            Case(root,world.Dark,new Vector3(.021f,-.09f,-.073f),new Vector3(.081f,.192f,.116f),-21);
            Case(root,world.Hardware,new Vector3(0,.022f,-.217f),new Vector3(.155f,.104f,.142f));
            Case(root,world.SafetyAmber,new Vector3(0,.083f,-.182f),new Vector3(.117f,.031f,.113f),5);
            Case(root,world.Graphite,new Vector3(0,.106f,.129f),new Vector3(.087f,.024f,.152f));
            for (int side = -1; side <= 1; side += 2)
            {
                Case(root,world.Porcelain,new Vector3(side*.088f,.063f,-.018f),new Vector3(.061f,.122f,.295f),-7,side*13);
                Case(root,world.Hardware,new Vector3(side*.075f,-.009f,.165f),new Vector3(.044f,.058f,.139f),4);
                Case(root,world.Insulation,new Vector3(side*.101f,-.024f,.023f),new Vector3(.030f,.042f,.193f));
                Case(root,accent,new Vector3(side*.115f,.017f,.095f),new Vector3(.013f,.017f,.077f));
                Case(root,world.Dark,new Vector3(side*.029f,.134f,.146f),new Vector3(.024f,.050f,.058f),-11);
            }
            Case(root,world.Hardware,new Vector3(0,.162f,.146f),new Vector3(.082f,.014f,.056f));
            Case(root,accent,new Vector3(0,.126f,.153f),new Vector3(.029f,.008f,.029f));

            var chamber = TriasWorld.Group(root,Chamber,new Vector3(0,.026f,.412f));
            var bore = TriasWorld.Group(chamber,"Pulse bore");
            bore.localRotation = Quaternion.Euler(90,0,0);
            world.CeramicArc(bore,world.Hardware,new Vector3(0,.015f,0),new Vector3(.064f,.170f,.064f),324);
            for (int side = -1; side <= 1; side += 2)
            {
                var guard = TriasWorld.Group(chamber,"Detached stabilizer lobe",new Vector3(0,0,.005f));
                guard.localRotation = bore.localRotation * Quaternion.Euler(0,side*89,0);
                world.CeramicArc(guard,world.Porcelain,Vector3.zero,new Vector3(.099f,.144f,.099f),125);
                world.CeramicArc(guard,world.Graphite,new Vector3(0,-.076f,0),new Vector3(.088f,.022f,.088f),103);
                Case(chamber,accent,new Vector3(side*.059f,.01f,-.081f),new Vector3(.023f,.047f,.013f));
            }
        }
    }
}
