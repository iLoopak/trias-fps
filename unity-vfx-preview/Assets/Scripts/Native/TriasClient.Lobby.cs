using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        enum LobbyPage { Quick, Create, Join, Settings }

        readonly Dictionary<string, Texture2D> arenaCards = new Dictionary<string, Texture2D>();
        readonly List<string> lobbyControls = new List<string>(36);
        LobbyPage lobbyPage, returnToPage;
        TriasWorld hangar;
        Avatar hangarPilot;
        string hangarKit, lobbyFocus, keyboardButton, lobbyHover;
        bool hangarMale, hangarReady, lobbyDragging;
        int previewFaction;
        float hangarStart, pilotSwitchAt, hangarYaw, hoverAt;
        static readonly string[] ModeTitles = { "Týmový boj", "Deathmatch", "Dominance", "Obléhání", "Vlajky", "Horda" };

        void LoadLobby()
        {
            foreach (var map in catalog.maps)
                arenaCards.Add(map.id,Resources.Load<Texture2D>("TRIAS/menu-" + map.id));
            string savedMap = PlayerPrefs.GetString("trias.map",selectedMap);
            if (Array.Exists(catalog.maps,map=>map.id==savedMap)) selectedMap = savedMap;
            selectedMode = SavedChoice("trias.mode",catalog.modes,selectedMode);
            selectedPilot = SavedChoice("trias.pilot",catalog.pilots,selectedPilot);
            fillBots = PlayerPrefs.GetInt("trias.bots",fillBots ? 1 : 0) != 0;
            roomCode = PlayerPrefs.GetString("trias.room","");
            if (!Supports(selectedMap,selectedMode)) selectedMode = "tdm";

            static string SavedChoice(string key,Choice[] data,string fallback)
            {
                string saved = PlayerPrefs.GetString(key,fallback);
                return Catalog.Find(data,saved) != null ? saved : fallback;
            }
        }

        static bool Supports(string arena, string game) =>
            game == "horde" ? arena == "relay" : game != "siege" || arena == "basin";

        void SaveLobby(bool saveRoom)
        {
            PlayerPrefs.SetString("trias.map",selectedMap);
            PlayerPrefs.SetString("trias.mode",selectedMode);
            PlayerPrefs.SetString("trias.pilot",selectedPilot);
            PlayerPrefs.SetInt("trias.pilotMale",selectedMale ? 1 : 0);
            PlayerPrefs.SetInt("trias.bots",fillBots ? 1 : 0);
            PlayerPrefs.SetString("trias.server",serverAddress);
            PlayerPrefs.SetString("trias.nick",nickname);
            if (saveRoom) PlayerPrefs.SetString("trias.room",roomCode);
        }

        static string NewRoomCode() => "TRIAS-" + Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant();
        string QuickRoom() => "QUICK-" + selectedMap.ToUpperInvariant() + "-" + selectedMode.ToUpperInvariant();

        void SubmitLobby()
        {
            if (socket != null) return;
            bool quick = lobbyPage == LobbyPage.Quick;
            bool create = lobbyPage == LobbyPage.Create;
            if (create && string.IsNullOrWhiteSpace(roomCode)) roomCode = NewRoomCode();
            SaveLobby(!quick);
            string code = quick ? QuickRoom() : roomCode;
            Join(serverAddress,code,nickname,selectedMap,selectedMode,selectedPilot,quick || fillBots,
                male: selectedMale, intent: create ? "create" : quick ? null : "join");
            effects.InterfaceTone(socket == null ? "invalid" : "deploy");
        }

        void UpdateLobby(float now)
        {
            if (!hangarReady)
            {
                effects.SetAtmosphere("hangar");
                effects.AmbienceMix(ambienceVolume,false);
                hangarReady = true;
                hangarStart = now;
            }
            if (visualEffects <= .01f)
            {
                if (hangar != null && hangar.Root.gameObject.activeSelf) hangar.Root.gameObject.SetActive(false);
                return;
            }
            if (hangar == null)
                hangar = new TriasWorld(catalog.maps[0],catalog,"tdm",view,true);
            if (!hangar.Root.gameObject.activeSelf) hangar.Root.gameObject.SetActive(true);
            string key = selectedPilot + "-" + previewFaction;
            if (hangarPilot == null || hangarKit != key || hangarMale != selectedMale)
            {
                if (hangarPilot != null)
                {
                    hangarPilot.Root.gameObject.SetActive(false);
                    Destroy(hangarPilot.Root.gameObject);
                }
                hangarKit = key;
                hangarMale = selectedMale;
                pilotSwitchAt = now;
                var pilot = Catalog.Find(catalog.pilots,selectedPilot);
                var row = WireValue.Parse($"[\"hangar\",\"{pilot.id}\",2,{previewFaction},0,\"{pilot.id}\",150,0,0,\"{pilot.weapon}\",[],0,0,0,0,-1,-1,0,-1.15,0.07,0,0,0,0,3.141592,0,0,0,0,0,0,null,\"{(selectedMale ? "male" : "female")}\"]");
                hangarPilot = new Avatar(hangar,"hangar",row,hangar.TeamLight[previewFaction]);
                hangarPilot.Accept(row,now);
            }
            float age = now - hangarStart;
            float motion = cameraMotion;
            float settle = 1 - Mathf.SmoothStep(0,1,(now - pilotSwitchAt)*4);
            hangarPilot.Tick(now);
            hangarPilot.Root.rotation = Quaternion.Euler(0,hangarYaw - 18 + motion*(8*settle + Mathf.Sin(age*.87f)*2.1f),0);
            float ultrawide = Mathf.Clamp(view.aspect / (16f/9),.80f,1.48f);
            float drift = Mathf.Sin(age*.21f)*.075f*motion;
            Vector3 eye = new Vector3(.23f+drift,1.42f,4.14f);
            view.transform.position = eye;
            view.transform.LookAt(new Vector3(eye.x + .58f + (ultrawide-1)*1.85f,.25f,-1));
            view.fieldOfView = 53;
            hangar.AnimateHangar(age,motion * visualEffects);
        }

        void CloseHangar()
        {
            hangar?.Dispose();
            hangar = null;
            hangarPilot = null;
            hangarReady = false;
            lobbyDragging = false;
        }

        void DrawLobby(float w,float h)
        {
            if ((hangar == null || visualEffects <= .01f) && overlay != null)
                GUI.DrawTexture(new Rect(0,0,w,h),overlay,ScaleMode.ScaleAndCrop);
            float x = w*.5f - 591, y = h*.5f - 333;
            Fill(new Rect(0,0,w,h),new Color(.021f,.029f,.050f,.25f));
            DrawInsignia(new Vector2(x+34,y+57),29,Amber);
            GUI.Label(new Rect(x+76,y+10,475,88),"TRIAS",title);
            Fill(new Rect(x,y+94,690,1),Subtle);
            Fill(new Rect(x,y+94,136,2),Amber);

            if (Event.current.type == EventType.Layout)
            {
                lobbyControls.Clear();
                keyboardButton = null;
            }
            else if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                     !string.IsNullOrEmpty(lobbyFocus) && !lobbyFocus.StartsWith("field.",StringComparison.Ordinal))
                keyboardButton = lobbyFocus;

            if (lobbyPage == LobbyPage.Settings)
                DrawLobbySettings(x,y);
            else
            {
                CornerPanel(new Rect(x,y+111,690,528),Amber);
                bool free = socket == null;
                GUI.enabled = free;
                LobbyTab(x+16,y+126,"Rychlá hra",LobbyPage.Quick);
                LobbyTab(x+237,y+126,"Vytvořit zápas",LobbyPage.Create);
                LobbyTab(x+458,y+126,"Připojit se",LobbyPage.Join);
                if (lobbyPage == LobbyPage.Join)
                    DrawJoin(x,y);
                else
                    DrawMatchSetup(x,y);
                DrawPilotDock(x+713,y+111);
                GUI.enabled = true;
                if (free)
                {
                    string label = lobbyPage == LobbyPage.Join ? "Připojit se k místnosti" :
                        lobbyPage == LobbyPage.Create ? "Vytvořit a nasadit se" : "Nasadit se do rychlé hry";
                    if (LobbyButton("deploy",new Rect(x+16,y+579,658,44),label,true)) SubmitLobby();
                }
                else
                {
                    GUI.Label(new Rect(x+17,y+586,444,29),world == null ? "Spojuji se se serverem…" : "Čekám na stav zápasu…",section);
                    if (LobbyButton("cancel",new Rect(x+510,y+579,164,44),"Zrušit",false))
                    {
                        Disconnect(null);
                        effects.InterfaceTone("back");
                    }
                }
                if (!string.IsNullOrEmpty(status) && free)
                    GUI.Label(new Rect(x+17,y+540,657,28),status,section);
            }
            DrawLobbyFooter(x,y+647);
            HandleLobbyKeys();
        }

        void LobbyTab(float x,float y,string label,LobbyPage page)
        {
            if (!LobbyButton("tab."+page,new Rect(x,y,215,42),label,lobbyPage == page)) return;
            lobbyPage = page;
            status = null;
            if (page == LobbyPage.Create) roomCode = NewRoomCode();
            if (page == LobbyPage.Join) roomCode = PlayerPrefs.GetString("trias.room","");
            effects.InterfaceTone("select");
        }

        void DrawMatchSetup(float x,float y)
        {
            for (int i=0; i<catalog.maps.Length; i++)
            {
                var map = catalog.maps[i];
                Rect card = new Rect(x+16+i*165,y+186,158,121);
                bool active = map.id == selectedMap;
                if (LobbyButton("map."+map.id,card,null,active))
                {
                    selectedMap = map.id;
                    if (!Supports(selectedMap,selectedMode)) selectedMode = "tdm";
                    SaveLobby(false);
                    effects.InterfaceTone("select");
                }
                if (arenaCards.TryGetValue(map.id,out var picture) && picture != null)
                    GUI.DrawTexture(new Rect(card.x+2,card.y+3,card.width-4,85),picture,ScaleMode.ScaleAndCrop);
                Fill(new Rect(card.x+2,card.y+82,card.width-4,3),active ? Amber : Subtle);
                GUI.Label(new Rect(card.x+7,card.y+89,143,26),map.label,section);
            }
            var arena = Array.Find(catalog.maps,map=>map.id==selectedMap);
            GUI.Label(new Rect(x+16,y+314,651,25),arena.description,caption);
            GUI.Label(new Rect(x+16,y+339,651,24),selectedMap switch
            {
                "relay" => "ORBITÁLNÍ STANICE   ·   TDM / FFA / DOM / CTF / HORDA",
                "foundry" => "TAVICÍ PROVOZ   ·   TDM / FFA / DOM / CTF",
                "basin" => "TĚŽEBNÍ ÚDOLÍ   ·   TDM / FFA / DOM / CTF / OBLÉHÁNÍ",
                _ => "ZELENÁ MINIPLANETA   ·   TDM / FFA / DOM / CTF"
            },caption);
            for (int i=0;i<catalog.modes.Length;i++)
            {
                var game = catalog.modes[i];
                Rect box = new Rect(x+16+i*110,y+375,104,55);
                bool selected = selectedMode==game.id;
                if (LobbyButton("mode."+game.id,box,null,selected))
                {
                    selectedMode = game.id;
                    if (game.id == "siege") selectedMap = "basin";
                    if (game.id == "horde") selectedMap = "relay";
                    SaveLobby(false);
                    effects.InterfaceTone("select");
                }
                DrawModeMark(new Vector2(box.x+15,box.y+16),i,selected?Amber:Subtle);
                GUI.Label(new Rect(box.x+27,box.y+4,75,24),game.@short,section);
                GUI.Label(new Rect(box.x+7,box.y+29,97,23),ModeTitles[i],caption);
            }
            var modeChoice = Catalog.Find(catalog.modes,selectedMode);
            GUI.Label(new Rect(x+16,y+437,658,37),modeChoice.description,lobbyWrap);
            bool horde = selectedMode == "horde";
            GUI.Label(new Rect(x+16,y+475,318,22),horde ? "1–4 obránci · 8 vln" : selectedMode=="ffa" ? "Až 12 hráčů · každý za sebe" : "Až 12 hráčů · tři týmy po čtyřech",caption);
            if (lobbyPage == LobbyPage.Quick)
                GUI.Label(new Rect(x+16,y+505,649,23),"Místnost " + QuickRoom() + " na nastaveném serveru",caption);
            else
            {
                GUI.Label(new Rect(x+16,y+506,100,23),"Kód místnosti",caption);
                roomCode = LobbyField("field.create",new Rect(x+122,y+498,204,32),roomCode,24);
                if (!horde && LobbyButton("bots",new Rect(x+349,y+498,201,32),fillBots ? "Boti zapnuti" : "Boti vypnuti",fillBots))
                {
                    fillBots = !fillBots;
                    SaveLobby(false);
                }
            }
        }

        void DrawJoin(float x,float y)
        {
            GUI.Label(new Rect(x+17,y+193,650,40),"Připojit se k posádce",heading);
            GUI.Label(new Rect(x+17,y+246,620,50),"Použij kód od hostitele. Arénu, režim a nastavení místnosti načteš ze serveru.",lobbyWrap);
            GUI.Label(new Rect(x+17,y+323,148,30),"Kód místnosti",section);
            roomCode = LobbyField("field.join",new Rect(x+17,y+357,389,45),roomCode,24);
            GUI.Label(new Rect(x+18,y+434,637,39),"Pokročilé připojení a přezdívku najdeš v Nastavení.",caption);
        }

        GUIStyle lobbyWrap;
        void DrawPilotDock(float x,float y)
        {
            var pilot = Catalog.Find(catalog.pilots,selectedPilot);
            var viewBox = new Rect(x,y,458,303);
            Stroke(new Vector2(x,y+26),new Vector2(x,y),2,Amber);
            Stroke(new Vector2(x,y),new Vector2(x+74,y),2,Amber);
            if ((hangar == null || visualEffects <= .01f) && portraits != null)
            {
                int i = Array.FindIndex(catalog.pilots,value=>value.id==selectedPilot);
                var crop = renderedPortraits ? new Rect(i*.25f,selectedMale?0:.5f,.25f,.5f) : new Rect(i*.25f,0,.25f,1);
                GUI.DrawTextureWithTexCoords(new Rect(x+94,y+10,270,270),portraits,crop);
            }
            var evt = Event.current;
            if (GUI.enabled && evt.button==0 && evt.type==EventType.MouseDown && viewBox.Contains(evt.mousePosition))
            {
                lobbyDragging = true;
                evt.Use();
            }
            else if (evt.type==EventType.MouseUp) lobbyDragging = false;
            else if (evt.type==EventType.MouseDrag && lobbyDragging)
            {
                hangarYaw = Mathf.Clamp(hangarYaw - evt.delta.x*.51f,-115,115);
                evt.Use();
            }
            if (visualEffects > .01f) GUI.Label(new Rect(x+11,y+271,260,23),"Tažením otočíš pilota",caption);
            CornerPanel(new Rect(x,y+305,458,223),Amber);
            GUI.Label(new Rect(x+14,y+312,276,39),pilot.label,heading);
            GUI.Label(new Rect(x+15,y+352,428,23),Catalog.Find(catalog.weapons,pilot.weapon)?.label + "  ·  " + pilot.description,caption);
            if (LobbyButton("faction",new Rect(x+272,y+311,173,38),"Znak " + catalog.teams[previewFaction].name,false))
            {
                previewFaction = (previewFaction+1)%catalog.teams.Length;
                effects.InterfaceTone("select");
            }
            DrawFaction(new Vector2(x+430,y+329),7,previewFaction,Catalog.Tint(catalog.teams[previewFaction].color));
            for (int i=0;i<catalog.pilots.Length;i++)
            {
                var candidate = catalog.pilots[i];
                if (!LobbyButton("pilot."+candidate.id,new Rect(x+12+i*110,y+386,105,47),candidate.label,pilot.id==candidate.id)) continue;
                selectedPilot = candidate.id;
                hangarYaw = 0;
                SaveLobby(false);
                effects.InterfaceTone("select");
            }
            if (LobbyButton("frame.f",new Rect(x+12,y+447,138,39),"Ženská verze",!selectedMale))
            {
                selectedMale = false;
                SaveLobby(false);
            }
            if (LobbyButton("frame.m",new Rect(x+158,y+447,138,39),"Mužská verze",selectedMale))
            {
                selectedMale = true;
                SaveLobby(false);
            }
        }

        void DrawLobbyFooter(float x,float y)
        {
            GUI.enabled = socket == null;
            if (LobbyButton("training",new Rect(x,y,230,41),"Trénink offline",false))
            {
                SaveLobby(false);
                SceneManager.LoadScene("Relay Station");
            }
            if (LobbyButton("settings",new Rect(x+240,y,160,41),"Nastavení",lobbyPage == LobbyPage.Settings))
            {
                if (lobbyPage != LobbyPage.Settings) { returnToPage = lobbyPage; lobbyPage = LobbyPage.Settings; }
            }
            if (LobbyButton("exit",new Rect(x+410,y,120,41),"Ukončit",false))
            {
                SaveLobby(false);
                Application.Quit();
            }
            GUI.enabled = true;
        }

        void DrawLobbySettings(float x,float y)
        {
            CornerPanel(new Rect(x,y+111,690,528),Amber);
            GUI.Label(new Rect(x+21,y+123,630,37),"Nastavení",heading);
            DrawFeelOptions(new Rect(x+22,y+177,440,34));
            GUI.Label(new Rect(x+21,y+435,630,34),"Pokročilé připojení",heading);
            GUI.Label(new Rect(x+22,y+470,420,22),"Adresa herního serveru",caption);
            GUI.Label(new Rect(x+460,y+470,190,22),"Přezdívka",caption);
            serverAddress = LobbyField("field.server",new Rect(x+22,y+496,422,34),serverAddress,160);
            nickname = LobbyField("field.player",new Rect(x+460,y+496,207,34),nickname,18);
            if (LobbyButton("back",new Rect(x+22,y+570,206,42),"Hotovo",true)) CloseLobbySettings();
        }

        void CloseLobbySettings()
        {
            lobbyPage = returnToPage;
            SaveLobby(false);
            effects.InterfaceTone("back");
        }

        bool LobbyButton(string id,Rect box,string label,bool selected)
        {
            if (GUI.enabled && Event.current.type == EventType.Layout) lobbyControls.Add(id);
            if (lobbyFocus == id)
            {
                Fill(new Rect(box.x-2,box.y-2,box.width+4,box.height+4),Chalk);
            }
            bool clicked = Preset(box,label,null,selected);
            bool hover = GUI.enabled && box.Contains(Event.current.mousePosition);
            if (hover && lobbyHover != id)
            {
                lobbyHover = id;
                hoverAt = Time.unscaledTime;
            }
            if (hover)
            {
                float amount = cameraMotion > .01f ? Mathf.Clamp01((Time.unscaledTime-hoverAt)*12) : 1;
                Fill(new Rect(box.x+2,box.yMax-2,(box.width-4)*amount,1),selected ? Amber : Chalk);
            }
            if (!GUI.enabled || (!clicked && keyboardButton != id)) return false;
            lobbyFocus = id;
            GUI.FocusControl(null);
            if (Event.current.type == EventType.KeyDown) Event.current.Use();
            keyboardButton = null;
            return true;
        }

        string LobbyField(string id,Rect box,string value,int max)
        {
            if (Event.current.type == EventType.Layout) lobbyControls.Add(id);
            if (Event.current.type == EventType.MouseDown && box.Contains(Event.current.mousePosition)) lobbyFocus = id;
            GUI.SetNextControlName(id);
            if (GUI.GetNameOfFocusedControl()==id || lobbyFocus==id)
                Fill(new Rect(box.x-2,box.y-2,box.width+4,box.height+4),Chalk);
            return GUI.TextField(box,value,max,field);
        }

        void HandleLobbyKeys()
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown) return;
            if (evt.keyCode == KeyCode.Escape && lobbyPage == LobbyPage.Settings)
            {
                CloseLobbySettings();
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.Tab && lobbyControls.Count > 0)
            {
                string active = GUI.GetNameOfFocusedControl();
                int index = lobbyControls.IndexOf(string.IsNullOrEmpty(active) ? lobbyFocus : active);
                index = index < 0 ? (evt.shift ? lobbyControls.Count-1 : 0) :
                    (index + (evt.shift ? -1 : 1) + lobbyControls.Count)%lobbyControls.Count;
                lobbyFocus = lobbyControls[index];
                GUI.FocusControl(lobbyFocus.StartsWith("field.",StringComparison.Ordinal) ? lobbyFocus : null);
                effects.InterfaceTone("navigate");
                evt.Use();
            }
        }

        static void DrawModeMark(Vector2 at,int mode,Color tint)
        {
            void Line(float x,float y,float endX,float endY) =>
                Stroke(at+new Vector2(x,y),at+new Vector2(endX,endY),1.6f,tint);
            switch (mode)
            {
                case 0:
                    Line(-6,4,-3,-5);
                    Line(-1,4,2,-5);
                    Line(4,4,7,-5);
                    break;
                case 1:
                    Line(-7,0,-2,0);
                    Line(2,0,7,0);
                    Line(0,-7,0,-2);
                    Line(0,2,0,7);
                    break;
                case 2:
                case 3:
                case 5:
                    Arc(at,6,0,360,tint,1.8f,mode == 2 ? 3 : mode == 3 ? 4 : 6);
                    if (mode == 5) Fill(new Rect(at.x-1,at.y-1,2,2),tint);
                    break;
                default:
                    Line(-4,6,-4,-6);
                    Line(-4,-6,5,-3);
                    Line(5,-3,-4,1);
                    break;
            }
        }
    }
}
