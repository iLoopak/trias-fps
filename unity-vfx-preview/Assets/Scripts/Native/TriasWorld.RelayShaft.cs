using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Native
{
    internal sealed partial class TriasWorld
    {
        Material shaftFloor, shaftBridge, stairTread, shaftSteel, shaftSignal, shaftRailing;
        Material shaftDeckInk;

        void BuildRelayShaft()
        {
            shaftFloor = MapSurface("#777f82", "brushed-deck", .25f, .19f);
            shaftFloor.mainTextureScale = new Vector2(5, 7);
            shaftBridge = MapSurface("#ac9b82", "brushed-deck", .33f, .24f);
            shaftBridge.mainTextureScale = new Vector2(2, 5);
            shaftSteel = MapSurface("#4a505a", "brushed-deck", .42f, .26f);
            stairTread = MapSurface("#797d7c", "brushed-deck", .32f, .23f);
            shaftRailing = CeramicFinish("#5e666c", .40f, .26f);
            shaftSignal = new Material(Shader.Find("Unlit/Color")) { color = new Color(1f,.58f,.27f) };
            Paint.Add(shaftSignal);
            var stencil = Resources.Load<Shader>("TRIAS/DeckStencil");
            if (relayStencilFont != null && stencil != null && stencil.isSupported)
            {
                shaftDeckInk = new Material(stencil);
                Paint.Add(shaftDeckInk);
                Font.textureRebuilt += UpdateDeckFontAtlas;
                UpdateDeckFontAtlas(relayStencilFont);
            }

            var machinery = Group(Root, "Reactor shaft · foundation and load paths");
            var dark = MaterialAt("#252b31");
            for (int i = 0; i < 4; i++)
            {
                var quadrant = Group(machinery, "Shaft frame");
                quadrant.localRotation = Quaternion.Euler(0, i * 90, 0);
                Armor(quadrant, shaftSteel, new Vector3(0,-11.7f,13.45f), new Vector3(25.8f, 22f, .95f), false);
                for (int support = -1; support <= 1; support++)
                {
                    float x = support * 8.45f;
                    var brace = Armor(quadrant, Graphite, new Vector3(x,-7.5f,12.70f),
                        new Vector3(1.26f,15.1f,1.9f), false);
                    brace.localRotation = Quaternion.Euler(7,0,-support * 8);
                    Armor(quadrant, relayStructure, new Vector3(x + .7f,-2.65f,12.62f),
                        new Vector3(4.18f,.50f,.82f), false);
                    Part(quadrant, shaftSignal, new Vector3(x+.7f,-2.61f,12.16f),
                        new Vector3(2.4f,.085f,.079f));
                }
                // The truss runs from the high bridge to the spine. It sits below the walking surface.
                Beam(quadrant, shaftSteel, new Vector3(0,-1.8f,2.03f), new Vector3(0,5.6f,11.55f), .81f);
                Beam(quadrant, shaftSteel, new Vector3(0,5.60f,11.55f), new Vector3(0,1.1f,13.38f), .78f);
                Armor(quadrant, dark, new Vector3(0,-16.95f,10.4f), new Vector3(23.2f,.5f,8.3f), false);
                BuildDeckSign(quadrant, "01   SERVICE", new Vector3(-4.45f, .012f, 27.8f));
                BuildDeckSign(quadrant, "02   TRANSFER", new Vector3(-6.1f, 3.312f, 16.85f));
                BuildDeckSign(quadrant, "03   CORE", new Vector3(0, 6.612f, 13.5f));
            }
        }

        void BuildDeckSign(Transform root, string title, Vector3 position)
        {
            if (shaftDeckInk == null) return;
            var plaque = Group(root, title, position);
            plaque.localRotation = Quaternion.Euler(90,0,0);
            var text = plaque.gameObject.AddComponent<TextMesh>();
            plaque.gameObject.layer = Decor;
            text.font = relayStencilFont;
            text.text = title;
            text.fontSize = 72;
            // Worn paint scale, not a billboard: the deck marking must not out-shout pilots and cover.
            text.characterSize = .068f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            var paint = Catalog.Tint("#d9ccb3");
            paint.a = .42f;
            text.color = paint;
            var renderer = text.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = shaftDeckInk;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void UpdateDeckFontAtlas(Font font)
        {
            if (font == relayStencilFont) shaftDeckInk.mainTexture = font.material.mainTexture;
        }

        void DressShaftBox(BoxSpec box, Transform frame, float h)
        {
            Material shell = box.structure switch
            {
                "stair" => stairTread, "service" or "gallery" => shaftFloor,
                "bridge" or "hub" or "arrival" => shaftBridge, "rail" => shaftRailing,
                "baffle" => shaftSteel, _ => Graphite
            };
            var slab = Part(frame, shell, Vector3.up * (h * .5f), new Vector3(box.w,h,box.d), PrimitiveType.Cube,true);
            if (DressEnvironmentShaft(box, frame, h))
            {
                if (box.structure is "bridge" or "arrival" or "hub") KeepStructuralShadow(slab);
                else slab.GetComponent<Renderer>().enabled = false;
                return;
            }
            if (box.structure is "service" or "gallery") DressRelaySoffit(box, frame);
            switch (box.structure)
            {
                case "service":
                    // The narrow exposed edge deliberately marks the drop without acting as a barrier.
                    Part(frame, relayPaintedSteel, new Vector3(0,h-.054f,-box.d*.5f-.012f),
                        new Vector3(box.w-.05f,.087f,.04f));
                    break;
                case "gallery":
                    Part(frame, Porcelain, new Vector3(0,h*.53f,-box.d*.5f-.035f),
                        new Vector3(box.w-.11f,h*.64f,.10f));
                    Part(frame, relayPaintedSteel, new Vector3(0,h+.004f,-box.d*.5f+.15f),
                        new Vector3(box.w-.18f,.009f,.12f));
                    break;
                case "bridge":
                    slab.GetComponent<Renderer>().enabled = !BuildRelayShaftGirder(box,frame,h);
                    for (int edge=-1; edge<=1; edge+=2)
                        Part(frame, shaftSignal, new Vector3(edge*(box.w*.38f),h+.011f,0),
                            new Vector3(.044f,.014f,box.d-.16f));
                    break;
                case "stair":
                    Part(frame, relayPaintedSteel, new Vector3(0,h+.003f,-box.d*.5f+.038f),
                        new Vector3(box.w-.13f,.01f,.062f));
                    break;
                case "rail":
                    Part(frame, Porcelain, new Vector3(0,h-.058f,0), new Vector3(box.w+.048f,.102f,box.d));
                    break;
                case "pylon":
                    if (BuildRelayStructuralCover(box,frame,h)) slab.GetComponent<Renderer>().enabled = false;
                    break;
                case "spine":
                    for (int i=0;i<4;i++)
                    {
                        var face = Group(frame, "Reactor compression rib");
                        face.localRotation = Quaternion.Euler(0,i*90,0);
                        Armor(face, Porcelain, new Vector3(0,h*.51f,-box.d*.5f-.047f),
                            new Vector3(box.w*.89f,h*.93f,.18f), false);
                        Part(face, shaftSignal, new Vector3(0,h*.53f,-box.d*.5f-.147f),
                            new Vector3(.094f,h*.61f,.028f));
                    }
                    break;
            }
        }

        bool BuildRelayShaftGirder(BoxSpec box, Transform frame, float h)
        {
            var deck = Group(frame, "Radial bridge girder");
            deck.localRotation = Quaternion.Euler(0,90,0);
            var deckBox = new BoxSpec { gantry = true, w = box.d, d = box.w };
            return BuildRelayStructuralCover(deckBox, deck, h);
        }
    }
}
