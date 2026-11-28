using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient : MonoBehaviour
    {
        public Camera view;
        public Preview.RelayEffects effects;

        static readonly string[] Arsenal = { "pulse", "scatter", "lance", "arc", "rift" };
        readonly Dictionary<string, Avatar> pilots = new Dictionary<string, Avatar>();
        sealed class ProjectileArt
        {
            internal Transform Mesh;
            internal TrailRenderer Trail;
            internal Vector3 Goal;
            internal bool Grenade;
            internal Renderer Surface;
        }

        readonly Dictionary<int, ProjectileArt> projectiles = new Dictionary<int, ProjectileArt>();
        readonly Stack<ProjectileArt> riftReserve = new Stack<ProjectileArt>(8);
        readonly Stack<ProjectileArt> grenadeReserve = new Stack<ProjectileArt>(8);
        readonly Dictionary<int, (Transform body, Transform gun)> vehicles = new Dictionary<int, (Transform, Transform)>();
        readonly HashSet<string> present = new HashSet<string>();
        readonly HashSet<int> visibleOrbs = new HashSet<int>();
        readonly List<string> removed = new List<string>();
        readonly List<int> goneOrbs = new List<int>();
        readonly List<Command> commands = new List<Command>(30);
        readonly List<FallenPilot> fallen = new List<FallenPilot>();
        readonly Dictionary<string, Transform> weapons = new Dictionary<string, Transform>();
        readonly Dictionary<string, WeaponHandling> weaponActions = new Dictionary<string, WeaponHandling>();
        Catalog catalog;
        TriasWorld world;
        TriasSocket socket;
        WireValue state, own;
        Motor predicted;
        Vector3 display;
        Transform grip;
        string identity, mode, room, weaponName, requestedWeapon;
        float sendAt, pingAt, pingSent, joinedAt, lastPacket, yaw, pitch, tension, hurt, hit, toastUntil;
        int sequence, queuedButtons, lastTick, latency, pingId;
        bool captured, scoreboard;
        string status, toast, spectator, killer;
        float replayUntil;
        Vector3 replayOrigin, replayPoint;
        Color teamTint;
        Material orb, grenade, spores, rubble;

        public string serverAddress = "ws://127.0.0.1:3000/play";
        public string roomCode = "";
        public string nickname = "Pilot Unity";
        public bool fillBots = true;
        public float sensitivity = 2.1f;
        string selectedMap = "relay", selectedMode = "tdm", selectedPilot = "vector";
        bool selectedMale;

        void Awake()
        {
            catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TRIAS/World").text);
            Array.Resize(ref catalog.modes, catalog.modes.Length + 1);
            catalog.modes[catalog.modes.Length - 1] = new Choice
            {
                id = "horde", label = "Obrana jádra", @short = "HORDA",
                description = "Až čtyři piloti proti osmi vlnám na Relay Station.",
            };
            serverAddress = PlayerPrefs.GetString("trias.server", serverAddress);
            nickname = PlayerPrefs.GetString("trias.nick", nickname);
            selectedMale = PlayerPrefs.GetInt("trias.pilotMale", 0) != 0;
            view.farClipPlane = 235;
            visor = view.gameObject.AddComponent<VisorComposite>();
            LoadFeel();
            view.backgroundColor = Catalog.Tint("#10182a");
            overlay = Resources.Load<Texture2D>("TRIAS/relay-eclipse") ?? Resources.Load<Texture2D>("TRIAS/orbit");
            portraits = Resources.Load<Texture2D>("TRIAS/exosuit-lineup");
            renderedPortraits = portraits != null;
            if (!renderedPortraits) portraits = Resources.Load<Texture2D>("TRIAS/visor-pilots") ?? Resources.Load<Texture2D>("TRIAS/pilot-portraits");
            displayFont = Resources.Load<Font>("TRIAS/Fonts/ChakraPetch-Bold");
            textFont = Resources.Load<Font>("TRIAS/Fonts/BarlowSemiCondensed-Medium");
            LoadLobby();
            InitDiagnostics();
            InitInvites();
        }

        void OnDestroy()
        {
            Application.deepLinkActivated -= ReceiveInvite;
            socket?.Leave();
            CloseHangar();
            world?.Dispose();
            if (radarPlate != null) Destroy(radarPlate);
            Capture(false);
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) Capture(false);
        }

        void LateUpdate()
        {
            if (world == null && hangarPilot != null && hangarPilot.Root.gameObject.activeInHierarchy)
                hangarPilot.PlaceHands();
            foreach (var actor in pilots.Values)
                if (actor.Root.gameObject.activeSelf) actor.PlaceHands();
        }

        public void Join(string address, string code, string player, string map, string game, string kit, bool bots, bool remember = true, bool male = false, string intent = null, bool hordeBots = false)
        {
            Disconnect(null);
            if (address.IndexOf("://", StringComparison.Ordinal) < 0) address = "ws://" + address;
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "ws" && uri.Scheme != "wss" && uri.Scheme != "http" && uri.Scheme != "https"))
            {
                status = "Zadej adresu herního serveru.";
                return;
            }
            var endpoint = new UriBuilder(uri)
            {
                Scheme = uri.Scheme == "wss" || uri.Scheme == "https" ? "wss" : "ws",
                Path = "/play", Query = "", Fragment = "",
            }.Uri;
            if (code.Trim().Length < 3)
            {
                status = "Kód místnosti má nejméně tři znaky.";
                return;
            }
            if (remember)
            {
                PlayerPrefs.SetString("trias.server", address);
                PlayerPrefs.SetString("trias.nick", player);
                PlayerPrefs.SetInt("trias.pilotMale", male ? 1 : 0);
            }
            sessionEndpoint = endpoint;
            practiceRequested = intent == "practice";
            socket = new TriasSocket();
            var request = new JoinCommand { type = "join", v = 2, room = code.Trim(), name = player.Trim(), mode = game, arena = map, layoutSet = catalog.layoutSet, geometry = Array.Find(catalog.maps, arena => arena.id == "relay")?.revision, character = kit, frame = male ? "male" : "female", intent = intent, bots = bots, predictFire = true, signature = true, botRadio = true, hordeBots = hordeBots, resume = true };
            socket.Start(endpoint, JsonUtility.ToJson(request));
            joinedAt = Time.unscaledTime;
            status = "Připojuji se k serveru…";
        }

        [Serializable] sealed class JoinCommand
        {
            public string type, room, name, mode, arena, character, frame, intent, ticket, geometry, layoutSet;
            public int v;
            public bool bots, predictFire, signature, botRadio, hordeBots, resume;
        }

        internal void Disconnect(string reason)
        {
            bool refused = reason != null && socket != null && own == null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResetLab();
#endif
            socket?.Leave(); socket = null;
            ResetConnection();
            world?.Dispose(); world = null;
            ResetConstruction();
            ResetPerks();
            ResetSignature();
            arenaSecond = -1;
            arenaRhythm = default;
            ResetFeel();
            ResetDeployment();
            hangarReady = false;
            sceneFade = 0;
            visor.InMatch = false;
            Array.Clear(incomingHits, 0, incomingHits.Length);
            killFeedCount = 0;
            waveFlash = coreFlash = hit = hurt = 0;
            toast = null; toastUntil = 0;
            identified = null;
            nextSurfacePulse = nextBracedNotice = 0;
            sawInterceptHint = false;
            radioCaption = null; radioUntil = 0;
            if (grip != null) Destroy(grip.gameObject);
            grip = null;
            effects.ClearTransient();
            weapons.Clear(); pilots.Clear(); vehicles.Clear(); projectiles.Clear(); commands.Clear();
            riftReserve.Clear(); grenadeReserve.Clear(); weaponActions.Clear();
            fireProfiles.Clear(); pendingShots.Clear();
            fireId = localFeedbackFrame = 0;
            confirmationMs = predictedCorrection = nextLocalShot = reloadPreviewUntil = lastDryClick = switchPreviewUntil = 0;
            fallen.Clear();
            own = state = null; identity = weaponName = null;
            sequence = lastTick = queuedButtons = latency = 0;
            requestedWeapon = spectator = killer = null;
            replayUntil = 0;
            status = reason;
            Capture(false);
            if (refused) effects.InterfaceTone("invalid");
        }

        void Capture(bool enabled)
        {
            captured = enabled;
            scoreboard = false;
            if (!enabled) { queuedButtons = 0; requestedWeapon = null; building = false; }
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
            if (effects != null) effects.AmbienceMix(ambienceVolume, enabled);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            UpdateDiagnostics(now);
            if (socket != null)
            {
                DrainMatchPackets(now);
                if (socket != null && socket.Error != null) ConnectionInterrupted(socket.Error, now);
                if (socket != null && ((own == null && now - joinedAt > 12) || (own != null && now - lastPacket > 6)))
                    ConnectionInterrupted("Server neodpověděl včas.", now);
            }
            if (TickReconnect(now)) return;
            if (world == null)
            {
                UpdateLobby(now);
                return;
            }
            if (own == null) return;
            ReadCouncil();
            if (practiceRoom && captured && state["practice"][0].Int == 3 && Input.GetKeyDown(KeyCode.Return))
            {
                Disconnect(null);
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpdateLabControls();
#endif
            while (pendingShots.Count > 0 && now - pendingShots[0].Sent > 1.5f) pendingShots.RemoveAt(0);
            if (!CanBuild) building = false;
            if (HandlePresentationKeys(now)) return;
            if (Input.GetKeyDown(KeyCode.Escape) && roundResult == null) Capture(!captured);
            if (captured && Cursor.lockState != CursorLockMode.Locked && Application.isFocused) Capture(false);

            if (captured)
            {
                scoreboard = Input.GetKey(KeyCode.Tab);
                if (!scoreboard && own[6].Int > 0 && state["winner"]["label"].Text == null)
                    ReadKeys(now);
                else if (!scoreboard && own[6].Int <= 0)
                {
                    if (Input.GetKeyDown(KeyCode.Space)) replayUntil = 0;
                    if (Input.GetKeyDown(KeyCode.E)) spectator = NextAlly(spectator);
                }
            }
            if (now >= sendAt)
            {
                SendInput();
                sendAt = Mathf.Max(sendAt + Motor.Tick, now - Motor.Tick * .6f);
            }
            if (now >= pingAt && socket != null)
            {
                pingAt = now + 2.5f; pingSent = now;
                socket.Send("{\"type\":\"ping\",\"id\":" + (++pingId) + "}");
            }
            RenderWorld(now, Time.unscaledDeltaTime);
            RenderConstruction();
        }

        void ReadKeys(float now)
        {
            yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * sensitivity, 360);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, -84.8f, 84.8f);
            if (Input.GetKeyDown(KeyCode.Space)) queuedButtons |= 1 << 5;
            if (Input.GetKeyDown(KeyCode.Q)) queuedButtons |= 1 << 6;
            if (ReadConstruction()) return;
            ReadPerks();
            ReadEcho(now);
            if (Input.GetKeyDown(KeyCode.R)) queuedButtons |= 1 << 4;
            if (Input.GetKeyDown(KeyCode.G)) queuedButtons |= 1 << 8;
            if (Input.GetKeyDown(KeyCode.E)) queuedButtons |= 1 << 9;
            if (Input.GetKeyDown(KeyCode.Alpha1)) requestedWeapon = "pulse";
            if (Input.GetKeyDown(KeyCode.Alpha2)) requestedWeapon = "scatter";
            if (Input.GetKeyDown(KeyCode.Alpha3)) requestedWeapon = "lance";
            if (Input.GetKeyDown(KeyCode.Alpha4)) requestedWeapon = "arc";
            if (Input.GetKeyDown(KeyCode.Alpha5)) requestedWeapon = "rift";
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .1f)
            {
                int current = Array.IndexOf(Arsenal, own[9].Text);
                for (int i = 1; i <= Arsenal.Length; i++)
                {
                    int slot = (current + i * (wheel > 0 ? 1 : -1) + 2 * Arsenal.Length) % Arsenal.Length;
                    if (own[10][slot].Count == 0) continue;
                    requestedWeapon = Arsenal[slot];
                    break;
                }
            }
            if (requestedWeapon != null) PreviewSwitch(now);
            if ((queuedButtons & (1 << 4)) != 0) PreviewReload(now);
            ReadFire(now);
        }

        float WebYaw => (yaw - 180) * Mathf.Deg2Rad;
        float WebPitch => -pitch * Mathf.Deg2Rad;
        bool Playing => captured && !scoreboard && own[6].Int > 0 && state["winner"]["label"].Text == null;

        void SendInput()
        {
            if (socket == null) return;
            int bits = Playing ? queuedButtons : 0;
            if (Playing)
            {
                if (Input.GetKey(KeyCode.C)) bits |= 1;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) bits |= 1 << 1;
                if (Input.GetKey(KeyCode.Space)) bits |= 1 << 2;
                if (!predictedFire && !building && Input.GetMouseButton(0)) bits |= 1 << 3;
            }
            var command = new Command {
                Seq = sequence + 1, Bits = bits, Horizontal = Playing ? -Input.GetAxisRaw("Horizontal") : 0,
                Vertical = Playing ? Input.GetAxisRaw("Vertical") : 0,
                Yaw = Mathf.Round(WebYaw * 1000) * .001f, Pitch = Mathf.Round(WebPitch * 1000) * .001f,
                Weapon = Playing ? requestedWeapon : null,
            };
            string weapon = command.Weapon == null ? "null" : "\"" + command.Weapon + "\"";
            string wire = FormattableString.Invariant($"{{\"type\":\"input\",\"data\":[{command.Seq},{command.Horizontal},{command.Vertical},{command.Yaw:F3},{command.Pitch:F3},{bits},{weapon}]}}");
            if (!socket.Send(wire)) return;
            sequence = command.Seq;
            queuedButtons = 0; requestedWeapon = null;
            if (own[6].Int <= 0 || own[15].Int >= 0 || own[16].Int >= 0 || state["winner"]["label"].Text != null) return;
            commands.Add(command);
            if (commands.Count > 24) commands.RemoveAt(0);
            var cue = predicted.Step(command, movement ?? world.Spec);
            if (cue != MotionCue.None) PlayLocalMotion(cue, predicted.Position);
        }

        void Accept(WireValue message, float now)
        {
            switch (message["type"].Text)
            {
                case "bot-radio":
                    ReadBotRadio(message,now);
                    return;
                case "perk-error":
                case "build-error":
                    AddNotice(message["message"].Text);
                    return;
                case "error":
                    Disconnect(message["message"].Text);
                    return;
                case "pong":
                    if (message["id"].Int == pingId) latency = Mathf.RoundToInt((now - pingSent) * 1000);
                    return;
                case "joined":
                    var arena = Array.Find(catalog.maps, a => a.id == message["arena"].Text);
                    string geometry = message["geometry"].Text;
                    if (arena?.id == "relay")
                    {
                        if (string.IsNullOrEmpty(geometry)) arena = catalog.relayService;
                        else if (geometry == catalog.relayHorde?.revision) arena = catalog.relayHorde;
                    }
                    if (message["v"].Int != 2 || arena == null || (geometry ?? "") != (arena.revision ?? ""))
                    {
                        Disconnect("Server nemá kompatibilní verzi TRIAS.");
                        return;
                    }
                    float worldExtent = arena.edgeRadius > 0 ? arena.edgeRadius : arena.length;
                    if (Mathf.Abs(message["worldExtent"].Float - worldExtent) > .01f)
                    {
                        Disconnect("Server používá jinou geometrii map. Aktualizuj server TRIAS.");
                        return;
                    }
                    reconnectTicket = message["resume"].Text;
                    reconnectGrace = message["resumeSeconds"].Float;
                    practiceRoom = message["training"].Bool;
                    if (practiceRequested && !practiceRoom)
                    {
                        Disconnect("Výcvik pilota vyžaduje server TRIAS W8.");
                        return;
                    }
                    if (message["resumed"].Bool && world != null && reconnectUntil > 0)
                    {
                        ReadyForResume(message, now);
                        return;
                    }
                    if (reconnectUntil > 0)
                    {
                        Disconnect("Server už nemůže obnovit původní zápas.");
                        return;
                    }
                    identity = message["id"].Text;
                    LoadFireProfiles(message["fireProfiles"]);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    labMode = labJoining && message["lab"].Bool;
#endif
                    room = message["room"].Text;
                    mode = message["mode"].Text;
                    var pace = message["pace"];
                    pilotMaxHealth = pace["pilotHp"].Int > 0 ? pace["pilotHp"].Int : 150;
                    respawnDelay = pace["respawn"].Float > 0 ? pace["respawn"].Float : mode == "horde" ? 5.5f : 2.8f;
                    CloseHangar();
                    world = new TriasWorld(arena, catalog, mode, view);
                    visor.InMatch = true;
                    visor.Grade(arena.id);
                    effects.listener = view.transform;
                    effects.SetAtmosphere(arena.id);
                    effects.AmbienceMix(ambienceVolume, true);
                    sceneFade = 0;
                    orb = world.MaterialAt("#ff62c5", true);
                    grenade = world.MaterialAt("#ffd28e");
                    if (mode == "horde")
                    {
                        spores = world.MaterialAt("#a8cb6a", true);
                        rubble = world.MaterialAt("#80664c", false, "SurfaceDetail/fractured-stone");
                    }
                    grip = TriasWorld.Group(view.transform, "First person arms");
                    status = null;
                    sendAt = pingAt = lastPacket = now;
                    return;
                case "state":
                    if (world != null && message["tick"].Int > lastTick)
                    {
                        Apply(message, now);
                        if (awaitingResumeFrame && own != null) CompleteResume();
                    }
                    return;
            }
        }

        void Apply(WireValue snapshot, float now)
        {
            lastPacket = now;
            lastTick = snapshot["tick"].Int;
            var priorState = state;
            state = snapshot;
            SyncDirector(snapshot["arenaDirector"], now);
            if (mode == "horde" && priorState != null)
            {
                var oldHorde = priorState["horde"];
                var current = state["horde"];
                bool newWave = current["wave"].Int > oldHorde["wave"].Int;
                bool cleared = oldHorde["wave"].Int > 0 && current["countdown"].Int > 0 && oldHorde["countdown"].Int == 0;
                if (newWave || cleared)
                {
                    waveStarts = newWave;
                    waveFlash = 1;
                    effects.Wave(newWave);
                    if (newWave) world.SurgeLandmark();
                }
                if (current["core"]["hp"].Int < oldHorde["core"]["hp"].Int) coreFlash = 1;
            }
            WireValue previous = own;
            present.Clear();
            foreach (var row in snapshot["players"].Items)
            {
                string key = row[0].Text;
                if (key == identity) { own = row; continue; }
                present.Add(key);
                if (!pilots.TryGetValue(key, out var actor))
                {
                    Material color = row[31].Text != null ? world.Hostile
                        : mode == "ffa" ? world.MaterialAt(catalog.ffaColors[row[3].Int * 4 + row[4].Int], true)
                        : world.TeamLight[row[3].Int];
                    actor = new Avatar(world, key, row, color);
                    pilots.Add(key, actor);
                }
                int before = actor.Spawn;
                actor.Accept(row, now);
                if (before >= 0 && actor.Spawn != before && (own == null || own[6].Int > 0 || SpectatorAlly(actor)))
                    effects.Reentry(actor.Desired, PlayerTint(row), false, mode == "ffa" || row[31].Text != null ? -1 : row[3].Int, visualEffects);
            }

            removed.Clear();
            foreach (var pair in pilots) if (!present.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var key in removed)
            {
                Destroy(pilots[key].Root.gameObject);
                pilots.Remove(key);
            }

            if (own == null) return;
            bool spawn = previous == null || own[13].Int != previous[13].Int;
            bool mountChange = previous != null && (own[16].Int != previous[16].Int || own[15].Int != previous[15].Int);
            Vector3 beforeCorrection = predicted.Position;
            predicted.Read(own);
            if (spawn || mountChange)
            {
                commands.Clear();
                pendingShots.Clear();
                reloadPreviewUntil = 0;
                switchPreviewUntil = 0;
                nextLocalShot = now + (spawn ? .20f : .16f);
                display = predicted.Position;
                yaw = (own[24].Float + Mathf.PI) * Mathf.Rad2Deg;
                pitch = -own[25].Float * Mathf.Rad2Deg;
                if (spawn)
                {
                    healthTrail = HealthFraction;
                    armorTrail = Mathf.Clamp01(own[7].Float / 50);
                    shieldAlertUntil = shieldReturnUntil = vitalsPulse = 0;
                    Array.Clear(incomingHits, 0, incomingHits.Length);
                    teamTint = PlayerTint(own);
                    if (own[6].Int > 0 && state["winner"]["label"].Text == null) BeginDeployment(now, previous == null);
                    spectator = killer = null; replayUntil = 0;
                }
            }
            SyncConstruction(snapshot["horde"]["build"]);
            SyncPerks(snapshot["streaks"]);
            int applied = own[14].Int;
            int count = 0;
            while (count < commands.Count && commands[count].Seq <= applied) count++;
            if (count > 0) commands.RemoveRange(0, count);
            if (own[6].Int > 0 && own[15].Int < 0 && own[16].Int < 0 && snapshot["winner"]["label"].Text == null)
                foreach (var input in commands) predicted.Step(input, movement ?? world.Spec);
            if (!spawn && !mountChange) predictedCorrection = Vector3.Distance(beforeCorrection, predicted.Position);
            if (previous != null)
            {
                bool healthLost = own[6].Int < previous[6].Int;
                bool armorLost = own[7].Int < previous[7].Int;
                if (!spawn && own[6].Int > 0) UpdateShieldFeedback(previous[7].Int, own[7].Int, now);
                if (!spawn && (healthLost || armorLost))
                {
                    effects.Hurt(armorLost && !healthLost, armorLost && own[7].Int == 0);
                    hurt = 1;
                    vitalsPulse = 1;
                    damageTint = armorLost && !healthLost ? Shield : Fault;
                }
                if ((own[2].Int & 64) != (previous[2].Int & 64) && !spawn && own[6].Int > 0)
                {
                    bool completed = (own[2].Int & 64) == 0;
                    string key = own[9].Text;
                    int slot = Array.IndexOf(Arsenal, key);
                    bool loaded = completed && key == previous[9].Text && own[10][slot][0].Int > previous[10][slot][0].Int;
                    if (loaded || (!completed && now >= reloadPreviewUntil)) effects.Reload(display, completed, key);
                    reloadPreviewUntil = 0;
                    if ((own[2].Int & 64) != 0) reloadPeriod = Mathf.Max(.10f, own[30].Float);
                }
            }
            SwitchWeapon(own[9].Text ?? "pulse");
            if (spawn && own[6].Int > 0) equipMotion = 1;
            SyncWorld(snapshot);
            SyncSignature(snapshot["signature"], now);
            foreach (var evt in deferredEvents) GameEvent(evt, now);
            foreach (var evt in snapshot["events"].Items) GameEvent(evt, now);
            RemoveVanishedEchoes(now);
        }

        void SwitchWeapon(string key)
        {
            if (key == weaponName) return;
            if (weaponName != null)
            {
                weapons[weaponName].gameObject.SetActive(false);
                nextLocalShot = Mathf.Max(nextLocalShot, Time.unscaledTime + .18f);
                reloadPreviewUntil = 0;
                effects.Equip(key);
            }
            equipMotion = 1;
            switchPreviewUntil = 0;
            reloadProgressShown = reloadCueProgress = 0;
            if (!weapons.TryGetValue(key, out var art))
            {
                art = TriasWorld.Group(grip, key);
                var accent = world.MaterialAt(Catalog.Find(catalog.weapons, key)?.color ?? "#b9fff1", true);
                TriasModels.Gun(world, art, key, accent);
                weaponActions[key] = new WeaponHandling(world, art, key, accent, true, own[5].Text, own[32].Text == "male");
                weapons[key] = art;
            }
            art.gameObject.SetActive(true);
            weaponName = key;
        }

        Color PlayerTint(WireValue row) => Catalog.Tint(mode == "ffa" ? catalog.ffaColors[row[3].Int * 4 + row[4].Int]
            : row[31].Text != null ? "#ff5555" : catalog.teams[row[3].Int].color);
        static Vector3 At(WireValue row) => new Vector3(row[0].Float, row[1].Float, row[2].Float);
        static Vector3 AtObject(WireValue data) => new Vector3(data["x"].Float, data["y"].Float, data["z"].Float);

        void SyncWorld(WireValue frame)
        {
            for (int i = 0; i < world.Pickups.Length; i++) world.Pickups[i].gameObject.SetActive(frame["pickups"][i].Int == 0);
            var nodes = mode == "siege" ? frame["siege"]["nodes"] : frame["points"];
            for (int i = 0; i < world.Zones.Length; i++)
            {
                var point = nodes[i];
                int owner = point["capture"].Int >= 0 ? point["capture"].Int : point["owner"].Int;
                world.SetControl(i,owner,point["progress"].Float,point["contested"].Bool,point["linked"].Bool);
            }
            const float siegeCoreCapacity = 720f;
            for (int i = 0; i < world.CoreShield.Length; i++)
            {
                var info = frame["siege"]["cores"][i];
                world.SetCore(i,info["hp"].Float/siegeCoreCapacity,info["opened"].Int==0);
            }
            if (world.DefendedCore != null)
            {
                var core = frame["horde"]["core"];
                world.SetDefendedCore(core["hp"].Float/Mathf.Max(1f,core["maxHp"].Float));
            }
            for (int i = 0; i < world.Links.Length; i++)
            {
                var link = world.Spec.siege.links[i];
                var first = nodes[link.a]; var second = nodes[link.b];
                int owner = first["owner"].Int;
                world.Links[i].sharedMaterial = owner >= 0 && owner < 3 && owner == second["owner"].Int
                    && first["linked"].Bool && second["linked"].Bool ? world.TeamLight[owner] : world.Dark;
            }
            for (int i = 0; i < world.Flags.Length; i++)
            {
                var flag = frame["flags"][i];
                string status = flag["status"].Text;
                var at = AtObject(flag);
                float heading = 0;
                if (status == "carried")
                {
                    string carrier = flag["carrier"].Text;
                    var row = carrier==identity ? own : carrier!=null && pilots.TryGetValue(carrier,out var model) ? model.Row : null;
                    heading = row==null ? 0 : (row[24].Float+Mathf.PI)*Mathf.Rad2Deg;
                    at += Quaternion.Euler(0,heading,0)*new Vector3(.14f,.95f,-.35f);
                }
                world.SetFlag(i,at,status,heading);
            }
            foreach (var vehicle in frame["vehicles"].Items)
            {
                int index = vehicle[1].Int;
                if (!vehicles.TryGetValue(index, out var mesh))
                {
                    var body = TriasModels.Vehicle(world, vehicle[2].Text == "bastion", vehicle[3].Int, out var gun);
                    mesh = (body, gun); vehicles[index] = mesh;
                }
                mesh.body.gameObject.SetActive(vehicle[5].Int > 0);
                mesh.body.SetPositionAndRotation(new Vector3(vehicle[7].Float, .05f, vehicle[8].Float),
                    Quaternion.Euler(0, (vehicle[9].Float + Mathf.PI) * Mathf.Rad2Deg, 0));
                mesh.gun.rotation = Quaternion.Euler(-vehicle[11].Float * Mathf.Rad2Deg, (vehicle[10].Float + Mathf.PI) * Mathf.Rad2Deg, 0);
            }
            foreach (var pilot in pilots.Values)
            {
                int id = pilot.Row[15].Int;
                if (id >= 0 && id < world.Turrets.Length)
                    world.Turrets[id].rotation = Quaternion.Euler(-pilot.Row[25].Float * Mathf.Rad2Deg, (pilot.Row[24].Float + Mathf.PI) * Mathf.Rad2Deg, 0);
            }
            int ownTurret = own[15].Int;
            if (ownTurret >= 0 && ownTurret < world.Turrets.Length)
                world.Turrets[ownTurret].rotation = Quaternion.Euler(-own[25].Float * Mathf.Rad2Deg, (own[24].Float + Mathf.PI) * Mathf.Rad2Deg, 0);
            visibleOrbs.Clear();
            foreach (var item in frame["projectiles"].Items)
            {
                int id = item[0].Int;
                string kind = item[1].Text;
                Vector3 target = new Vector3(item[2].Float, item[3].Float, item[4].Float);
                if (kind == "grenade") HintAtGrenade(target);
                if (!projectiles.TryGetValue(id, out var art))
                {
                    art = AcquireProjectile(kind, target, item[6].Bool);
                    projectiles.Add(id, art);
                }
                art.Goal = target;
                visibleOrbs.Add(id);
            }
            goneOrbs.Clear();
            foreach (var pair in projectiles) if (!visibleOrbs.Contains(pair.Key)) goneOrbs.Add(pair.Key);
            foreach (int id in goneOrbs)
            {
                var art = projectiles[id];
                projectiles.Remove(id);
                art.Mesh.gameObject.SetActive(false);
                art.Trail.Clear();
                var pool = art.Grenade ? grenadeReserve : riftReserve;
                if (pool.Count < 8) pool.Push(art);
                else Destroy(art.Mesh.gameObject);
            }
        }

        ProjectileArt AcquireProjectile(string kind, Vector3 at, bool alternate)
        {
            bool isGrenade = kind == "grenade";
            var pool = isGrenade ? grenadeReserve : riftReserve;
            if (pool.TryPop(out var cached))
            {
                cached.Mesh.position = at;
                cached.Goal = at;
                cached.Mesh.gameObject.SetActive(true);
                cached.Trail.Clear();
                ShapeProjectile(cached, kind, alternate);
                return cached;
            }
            var mesh = TriasWorld.Part(world.Actors, isGrenade ? grenade : orb, at, Vector3.one, PrimitiveType.Sphere);
            var trail = mesh.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = effects.tracerMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.minVertexDistance = .18f;
            trail.endWidth = 0;
            var art = new ProjectileArt { Mesh = mesh, Trail = trail, Goal = at, Grenade = isGrenade, Surface = mesh.GetComponent<Renderer>() };
            ShapeProjectile(art, kind, alternate);
            return art;
        }

        void ShapeProjectile(ProjectileArt art, string kind, bool alternate)
        {
            var (paint, hex, size, width, trailTime) = kind switch
            {
                "grenade" => (grenade, "#ffb863", .24f, .06f, .095f),
                "spore" => (spores ?? orb, "#bad96e", .30f, .14f, .29f),
                "rubble" => (rubble ?? grenade, "#c59b72", .43f, .08f, .17f),
                _ => (orb, alternate ? "#ffad89" : "#fe85d0", alternate ? .64f : .43f, alternate ? .42f : .27f, .19f)
            };
            art.Surface.sharedMaterial = paint;
            Color tint = Catalog.Tint(hex);
            art.Trail.startColor = tint;
            tint.a = 0;
            art.Trail.endColor = tint;
            art.Trail.startWidth = width;
            art.Trail.time = trailTime;
            art.Mesh.localScale = Vector3.one * size;
        }

        void GameEvent(WireValue evt, float now)
        {
            string type = evt["type"].Text;
            string key = evt["weapon"].Text ?? "pulse";
            string actorId = evt["id"].Text;
            bool local = actorId == identity;
            if (own[6].Int <= 0 && !local && (type is "shot" or "tower-shot" or "burst" or "movement" or "pickup" or "echo-recall" or "intercept" or "horde-windup" or "horde-release" or "horde-impact") &&
                (actorId == null || !pilots.TryGetValue(actorId,out var sourcePilot) || !SpectatorAlly(sourcePilot))) return;
            switch (type)
            {
                case "echo-recall":
                    PresentRecall(evt, actorId, local, now);
                    break;
                case "intercept":
                    if (local) AddNotice("Granát sestřelen");
                    break;
                case "echo-end":
                    OnEchoEnd(evt, local);
                    break;
                case "echo-reject":
                    if (local)
                    {
                        echoAwaitUntil = 0;
                        EchoRejected(evt["reason"].Text switch
                        {
                            "blocked" => "TRASA JE BLOKOVANÁ",
                            "ground" => "NÁVRAT JEN NA ZEMI",
                            "objective" => "NÁVRAT MIMO CÍL",
                            "range" => "MIMO DOSAH NÁVRATU",
                            "occupied" => "MÍSTO JE OBSAZENÉ",
                            _ => "OZVĚNA NENÍ PŘIPRAVENÁ",
                        }, now);
                    }
                    break;
                case "trigger-rejected":
                    if (local) ResolveFire(evt["fireId"].Int, now, false);
                    break;
                case "perk-ready":
                    if (local)
                    {
                        effects.Wave(false);
                        int slot = Array.IndexOf(Perks, evt["perk"].Text);
                        if (slot >= 0) AddNotice((slot == 1 ? ModuleName() : PerkNames[slot]) + " připraveno  ·  " + (6 + slot));
                    }
                    break;
                case "perk-used":
                    effects.Pickup(At(evt["position"]), Amber, local);
                    break;
                case "orbital-impact":
                    effects.OrbitalImpact(At(evt["from"]));
                    break;
                case "built":
                    effects.Pickup(At(evt["position"]), Catalog.Tint("#c3fa83"), false);
                    break;
                case "horde-windup":
                    if (actorId != null && pilots.TryGetValue(actorId, out var infected))
                        infected.Windup(now, evt["duration"].Float);
                    effects.InfectedAttack(At(evt["from"]) + Vector3.up, evt["attack"].Text, "windup", visualEffects);
                    break;
                case "horde-release":
                    effects.InfectedAttack(At(evt["from"]), evt["attack"].Text, "release", visualEffects);
                    break;
                case "horde-impact":
                    effects.InfectedAttack(At(evt["position"]), evt["attack"].Text, "impact", visualEffects);
                    if (evt["objective"].Bool) coreFlash = 1;
                    break;
                case "tower-shot":
                    effects.Shot(At(evt["from"]), At(evt["position"]), true, false, Catalog.Tint("#99f8ff"), "arc");
                    break;
                case "shot":
                    Vector3 source = At(evt["from"]);
                    bool echoShot = evt["echo"].Int > 0;
                    if (local && echoShot) ConfirmEchoShot(key, evt["alt"].Bool, now);
                    bool ownGun = local && !echoShot && !evt["mounted"].Bool && own[15].Int < 0 && own[16].Int < 0 && own[6].Int > 0;
                    bool tagged = local && evt["fireId"].Int > 0;
                    if (tagged) ResolveFire(evt["fireId"].Int, now, true);
                    if (ownGun)
                    {
                        source = MuzzlePoint();
                        if (!tagged) PlayLocalShot(key, evt["alt"].Bool);
                    }
                    else if (!tagged) effects.FireWorld(source, key, evt["alt"].Bool, local && !echoShot);
                    bool alternate = evt["alt"].Bool;
                    Color tint = ShotTint(key, alternate);
                    Vector3 discharge = evt["ends"].Count > 0 ? (At(evt["ends"][0])-source).normalized : Vector3.zero;
                    if (!local && !echoShot && pilots.TryGetValue(evt["id"].Text, out var firingPilot))
                    {
                        firingPilot.Fire(key,alternate);
                        effects.DistantMuzzle(source + firingPilot.Root.forward * .53f, tint, visualEffects,
                            key,alternate,discharge.sqrMagnitude>.5f?discharge:firingPilot.Root.forward);
                    }
                    if (echoShot)
                    {
                        AnimateEchoShot(evt["echo"].Int, key, alternate, now);
                        effects.DistantMuzzle(source,tint,visualEffects,key,alternate,discharge);
                    }
                    if (!local) PlayNearMiss(evt, source, now);
                    for (int i = 0; i < evt["ends"].Count; i++)
                    {
                        var end = evt["ends"][i];
                        var at = At(end);
                        string kind = end[3].Text;
                        if (kind == "wall" && world.Spec.id == "island" && at.y <= world.Height(at.x, at.z) + .11f)
                            kind = Motor.Shallow(at, world.Spec) ? "water" : "soil";
                        effects.Shot(source, at, kind != null, local, kind == "grenade" ? Amber : tint, key, false, kind, visualEffects, i == 0, alternate);
                        if (kind is "water" or "wall") ReactToSurface(at, now, false);
                        if (alternate && key == "lance") source = at;
                    }
                    break;
                case "burst":
                    Vector3 blastAt = At(evt["from"]);
                    effects.Blast(blastAt, key == "grenade", visualEffects, evt["alt"].Bool);
                    ReactToSurface(blastAt, now, true);
                    break;
                case "core-hit":
                    effects.Shot(At(evt["from"]), At(evt["position"]), false, false, Catalog.Tint("#ff7a5e"), key);
                    break;
                case "core-wreck":
                case "wreck":
                    effects.Detonation(At(evt["position"]));
                    world.SurgeLandmark();
                    if (type == "core-wreck") AddNotice("Jádro " + catalog.teams[evt["team"].Int].name + " zničeno.");
                    break;
                case "pickup":
                    string item = evt["item"].Text;
                    var collectedWeapon = Catalog.Find(catalog.weapons, item);
                    var collected = collectedWeapon ?? Catalog.Find(catalog.powerups, item);
                    effects.Pickup(At(evt["position"]),Catalog.Tint(collected?.color),local,collectedWeapon!=null,visualEffects);
                    if (local) AddNotice("Sebráno · " + collected?.label);
                    break;
                case "movement":
                    string action = evt["action"].Text;
                    Vector3 motionAt = At(evt["position"]);
                    if (action == "step" && !local)
                        effects.Footstep(motionAt + Vector3.up * .08f, false, FootSurface(motionAt));
                    else if (action != "step" && action != "land")
                    {
                        var cue = action == "launch" ? MotionCue.Launch : action == "double" ? MotionCue.Double
                            : action == "dodge" ? MotionCue.Dodge : MotionCue.Jump;
                        if (!local || now - localMotionAt[(int)cue] > Mathf.Max(.24f, latency * .0013f))
                            effects.Motion(motionAt, local ? teamTint : Catalog.Tint(catalog.teams[evt["team"].Int].color), cue, local, visualEffects, mode == "ffa" ? -1 : evt["team"].Int);
                        if (!local && pilots.TryGetValue(evt["id"].Text, out var movingPilot))
                            movingPilot.BoostPose(cue);
                    }
                    break;
                case "notice":
                    string notice = evt["text"].Text;
                    if (mode == "horde" && waveFlash > .9f && notice?.StartsWith("Vlna ", StringComparison.Ordinal) == true)
                    {
                        int reward = notice.IndexOf(" · ", StringComparison.Ordinal);
                        if (waveStarts && reward >= 0) AddNotice(notice.Substring(reward + 3));
                    }
                    else AddNotice(notice);
                    break;
                case "hazard":
                    if (local) damageTint = Amber;
                    break;
                case "kill":
                    var body = evt["body"];
                    Vector3 spot = AtObject(body);
                    Color casualtyTint = body["hordeType"].Text == null
                        ? Catalog.Tint(catalog.teams[body["team"].Int].color) : Catalog.Tint("#ff5555");
                    Material casualtyPaint = body["hordeType"].Text == null ? world.TeamLight[body["team"].Int] : world.Hostile;
                    bool mine = evt["victimId"].Text == identity;
                    bool environment = key == "fall" || key == "plasma";
                    if (key != "fall" && (own[6].Int > 0 || mine || SpectatorTeam(body["team"].Int) && body["hordeType"].Text == null))
                    {
                        var corpse = new FallenPilot(world,body,casualtyPaint,casualtyTint,mine);
                        if (corpse.Visible) fallen.Add(corpse);
                        else effects.Elimination(spot,casualtyTint,visualEffects);
                    }
                    AddKill(evt["attacker"].Text, evt["victim"].Text, CauseLabel(key) ?? key, evt["team"].Int, now);
                    if (mine && environment) { killer = evt["attacker"].Text; replayUntil = 0; }
                    if (mine && own[6].Int <= 0 && !environment)
                    {
                        killer = evt["attacker"].Text;
                        killerWeapon = key switch
                        {
                            "claw" or "spore" or "rubble" => CauseLabel(key),
                            _ => Catalog.Find(catalog.weapons,key)?.label
                        };
                        deathReplayLength = Mathf.Min(1.85f, own[29].Float);
                        replayUntil = now + deathReplayLength;
                        deploymentUntil = 0;
                        replayOrigin = At(body["from"]);
                        replayPoint = evt["impact"].Count == 3 ? At(evt["impact"]) : spot + Vector3.up;
                        if (key is not ("claw" or "spore" or "rubble"))
                            effects.Shot(replayOrigin, replayPoint, true, false, ShotTint(key), key, false);
                    }
                    break;
            }
            int feedback = -1;
            if (evt["hits"].Items != null) foreach (var strike in evt["hits"].Items)
            {
                if (local)
                    feedback = Mathf.Max(feedback, strike["eliminated"].Bool ? 3 : strike["shieldBreak"].Bool ? 2 : strike["absorbed"].Int > 0 ? 1 : 0);
                if (strike["id"].Text == identity && evt["from"].Count == 3)
                {
                    bool explosive = type == "burst" || type == "orbital-impact";
                    bool broken = strike["shieldBreak"].Bool;
                    bool braced = strike["braced"].Bool;
                    if (braced && now >= nextBracedNotice)
                    {
                        nextBracedNotice = now + 2;
                        AddNotice("Štít ztlumil odhoz");
                    }
                    damageTint = braced ? Shield : explosive ? Amber : broken || strike["health"].Int == 0 ? Shield : Fault;
                    RecordIncoming(At(evt["from"]), damageTint, explosive || broken, now);
                }
                if (strike["id"].Text != null && pilots.TryGetValue(strike["id"].Text, out var struck))
                    struck.React(strike["shieldBreak"].Bool);
            }
            if (feedback >= 0)
            {
                if (hit < .62f || feedback > confirmStyle || feedback >= 2) effects.Confirm(feedback == 1, feedback == 3, feedback == 2);
                confirmStyle = Mathf.Max(hit > .65f ? confirmStyle : 0, feedback);
                hit = 1;
            }
        }

        void AddNotice(string message) { toast = message; toastUntil = Time.unscaledTime + 3.2f; }

        bool SpectatorTeam(int team) => mode != "ffa" && team == own[3].Int;
        bool SpectatorAlly(Avatar actor) => SpectatorTeam(actor.Row[3].Int) && actor.Row[31].Text == null;

        string NextAlly(string previous)
        {
            if (mode == "ffa") return null;
            var options = new List<Avatar>();
            foreach (var actor in pilots.Values)
                if (actor.Row[6].Int > 0 && SpectatorAlly(actor)) options.Add(actor);
            options.Sort((a, b) => a.Row[4].Int.CompareTo(b.Row[4].Int));
            return options.Count == 0 ? null : options[(options.FindIndex(a => a.Id == previous) + 1) % options.Count].Id;
        }

        void RenderWorld(float now, float dt)
        {
            UpdateResult(dt);
            UpdateArenaRhythm(now);
            float arenaClock = arenaSecond < 0 ? -1 : arenaSecond + Mathf.Clamp01(now - arenaSampleAt);
            world.Animate(now, visualEffects, arenaRhythm.Pulse * visualEffects, view.transform.position,
                cameraMotion, ambienceVolume * (captured ? 1f : .45f), arenaClock, arenaSeed);
            bool alive = own[6].Int > 0, driving = own[16].Int >= 0, mounted = own[15].Int >= 0;
            float travel = 1 - Mathf.Exp(-dt * 54);
            foreach (var art in projectiles.Values)
            {
                art.Mesh.position = Vector3.Lerp(art.Mesh.position, art.Goal, travel);
                bool visible = art.Mesh.gameObject.activeSelf;
                if (visible && !alive) art.Trail.Clear();
                if (visible != alive) art.Mesh.gameObject.SetActive(alive);
                art.Trail.emitting = alive && visualEffects > .08f;
            }
            for (int index = fallen.Count - 1; index >= 0; index--)
            {
                var body = fallen[index];
                bool visible = alive || body.Local || !body.HordeEnemy && SpectatorTeam(body.Team);
                if (body.Root.gameObject.activeSelf != visible) body.Root.gameObject.SetActive(visible);
                if (!body.Step(world,dt)) continue;
                if (visible) effects.Elimination(body.Landing,body.Tint,visualEffects);
                Destroy(body.Root.gameObject);
                fallen.RemoveAt(index);
            }
            bool replay = !alive && now < replayUntil;
            ShowEchoes(alive, now);
            UpdateVitals(now, dt);
            foreach (var actor in pilots.Values)
            {
                actor.Tick(now);
                bool show = actor.Row[6].Int > 0 && actor.Row[16].Int < 0;
                if (!alive) show &= SpectatorAlly(actor);
                actor.Root.gameObject.SetActive(show);
            }
            tension = Mathf.MoveTowards(tension, 0, dt * 6.7f);
            hurt = Mathf.MoveTowards(hurt, 0, dt * 3.2f);
            hit = Mathf.MoveTowards(hit, 0, dt * 5.7f);
            visor.DamagePulse = hurt;
            visor.DamageColor = damageTint;
            coreFlash = Mathf.MoveTowards(coreFlash, 0, dt * 2.85f);
            waveFlash = Mathf.MoveTowards(waveFlash, 0, dt * .53f);
            grip.gameObject.SetActive(alive && !driving && !mounted);
            if (alive)
            {
                followedPilot = null;
                display = Vector3.Lerp(display, predicted.Position, 1 - Mathf.Exp(-dt * 39));
                UpdateFeel(now, dt, driving);
            }
            else if (replay)
            {
                followedPilot = null;
                Vector3 direction = Vector3.ProjectOnPlane(replayOrigin - replayPoint, Vector3.up).normalized;
                if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
                var angle = Quaternion.Euler(0, 29 + ReplayRemaining(now) * 24, 0);
                Chase(replayPoint, angle * direction, 3.50f, 1.20f);
                view.transform.LookAt(Vector3.Lerp(replayPoint, replayOrigin, .14f));
            }
            else
            {
                if (spectator == null || !pilots.TryGetValue(spectator, out var watched) || watched.Row[6].Int <= 0) spectator = NextAlly(null);
                if (spectator != null && pilots.TryGetValue(spectator, out watched))
                    FollowSpectator(watched, dt);
                else
                {
                    if (followedPilot != null) chaseArm = 0;
                    followedPilot = null;
                    Chase(display + Vector3.up * 1.11f, Quaternion.Euler(0, yaw, 0) * Vector3.forward, 2.1f, .87f);
                }
            }
        }

        void Chase(Vector3 focus, Vector3 direction, float depth, float rise, bool smooth = true, bool clearCorners = false)
        {
            Vector3 arm = -direction * depth + Vector3.up * rise;
            float distance = arm.magnitude;
            if (Physics.Raycast(focus, arm.normalized, out var hit, arm.magnitude, TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(clearCorners ? 0 : .16f, hit.distance - .17f);
            if (clearCorners && Physics.SphereCast(focus, .26f, arm.normalized, out var edge, distance,
                    TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0, edge.distance - .04f);
            if (!smooth || (!clearCorners && chaseArm <= 0) || distance < chaseArm || cameraMotion < .01f) chaseArm = distance;
            else chaseArm = Mathf.Lerp(chaseArm, distance, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 14 / cameraMotion));
            view.transform.position = focus + arm.normalized * chaseArm;
            view.transform.LookAt(focus + direction * 2.4f);
        }
    }
}
