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
        readonly Dictionary<int, Transform> projectiles = new Dictionary<int, Transform>();
        readonly Dictionary<int, (Transform body, Transform gun)> vehicles = new Dictionary<int, (Transform, Transform)>();
        readonly HashSet<string> present = new HashSet<string>();
        readonly HashSet<int> visibleOrbs = new HashSet<int>();
        readonly List<string> removed = new List<string>();
        readonly List<int> goneOrbs = new List<int>();
        readonly List<Command> commands = new List<Command>(30);
        readonly List<FallenPilot> fallen = new List<FallenPilot>();
        readonly Dictionary<string, Transform> weapons = new Dictionary<string, Transform>();
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
        Material orb, grenade;

        public string serverAddress = "ws://127.0.0.1:3000/play";
        public string roomCode = "";
        public string nickname = "Pilot Unity";
        public bool fillBots = true;
        public float sensitivity = 2.1f;
        string selectedMap = "relay", selectedMode = "tdm", selectedPilot = "vector";

        void Awake()
        {
            catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TRIAS/World").text);
            Array.Resize(ref catalog.modes, catalog.modes.Length + 1);
            catalog.modes[catalog.modes.Length - 1] = new Choice
            {
                id = "horde", label = "Obrana jádra", @short = "HORDA",
                description = "Až čtyři piloti proti osmi vlnám na Relay Station.",
            };
            roomCode = "NATIVE-" + Guid.NewGuid().ToString("N").Substring(0, 5).ToUpperInvariant();
            serverAddress = PlayerPrefs.GetString("trias.server", serverAddress);
            nickname = PlayerPrefs.GetString("trias.nick", nickname);
            view.farClipPlane = 235;
            view.backgroundColor = Catalog.Tint("#10182a");
            overlay = Resources.Load<Texture2D>("TRIAS/orbit");
            portraits = Resources.Load<Texture2D>("TRIAS/pilot-portraits");
        }

        void OnDestroy()
        {
            socket?.Dispose();
            world?.Dispose();
            Capture(false);
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) Capture(false);
        }

        public void Join(string address, string code, string player, string map, string game, string kit, bool bots)
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
            PlayerPrefs.SetString("trias.server", address);
            PlayerPrefs.SetString("trias.nick", player);
            socket = new TriasSocket();
            var request = new JoinCommand { type = "join", v = 2, room = code.Trim(), name = player.Trim(), mode = game, arena = map, character = kit, bots = bots };
            socket.Start(endpoint, JsonUtility.ToJson(request));
            joinedAt = Time.unscaledTime;
            status = "Připojuji se k serveru…";
        }

        [Serializable] sealed class JoinCommand
        {
            public string type, room, name, mode, arena, character;
            public int v;
            public bool bots;
        }

        internal void Disconnect(string reason)
        {
            socket?.Dispose(); socket = null;
            world?.Dispose(); world = null;
            ResetConstruction();
            if (grip != null) Destroy(grip.gameObject);
            grip = null;
            effects.ClearTransient();
            weapons.Clear(); pilots.Clear(); vehicles.Clear(); projectiles.Clear(); commands.Clear();
            fallen.Clear();
            own = state = null; identity = weaponName = null;
            sequence = lastTick = queuedButtons = latency = 0;
            requestedWeapon = spectator = killer = null;
            replayUntil = 0;
            status = reason;
            Capture(false);
        }

        void Capture(bool enabled)
        {
            captured = enabled;
            scoreboard = false;
            if (!enabled) { queuedButtons = 0; requestedWeapon = null; building = false; }
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (socket != null)
            {
                for (int i = 0; i < 18 && socket != null && socket.Received.TryDequeue(out var raw); i++)
                {
                    try { Accept(WireValue.Parse(raw), now); }
                    catch (FormatException) { Disconnect("Server poslal neplatný stav zápasu."); }
                }
                if (socket != null && socket.Error != null) Disconnect(socket.Error);
                if (socket != null && ((own == null && now - joinedAt > 12) || (own != null && now - lastPacket > 6)))
                    Disconnect("Server neodpověděl včas.");
            }
            if (world == null || own == null) return;
            if (!CanBuild) building = false;
            if (Input.GetKeyDown(KeyCode.Escape)) Capture(!captured);
            if (captured && Cursor.lockState != CursorLockMode.Locked && Application.isFocused) Capture(false);

            if (captured)
            {
                scoreboard = Input.GetKey(KeyCode.Tab);
                if (!scoreboard && own[6].Int > 0 && state["winner"]["label"].Text == null)
                    ReadKeys();
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

        void ReadKeys()
        {
            yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * sensitivity, 360);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, -84.8f, 84.8f);
            if (Input.GetKeyDown(KeyCode.Space)) queuedButtons |= 1 << 5;
            if (Input.GetKeyDown(KeyCode.Q)) queuedButtons |= 1 << 6;
            if (ReadConstruction()) return;
            if (Input.GetKeyDown(KeyCode.R)) queuedButtons |= 1 << 4;
            if (Input.GetKeyDown(KeyCode.G)) queuedButtons |= 1 << 8;
            if (Input.GetKeyDown(KeyCode.E)) queuedButtons |= 1 << 9;
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F) && own[9].Text == "scatter")
            {
                bool alt = Input.GetKeyDown(KeyCode.F);
                string trigger = FormattableString.Invariant($"{{\"type\":\"trigger\",\"yaw\":{WebYaw:F5},\"pitch\":{WebPitch:F5},\"alt\":{(alt ? "true" : "false")}}}");
                if (!socket.Send(trigger)) queuedButtons |= 1 << (alt ? 7 : 3);
            }
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
                if (!building && Input.GetMouseButton(0)) bits |= 1 << 3;
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
            predicted.Step(command, movement ?? world.Spec);
        }

        void Accept(WireValue message, float now)
        {
            switch (message["type"].Text)
            {
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
                    if (message["v"].Int != 2 || arena == null)
                    {
                        Disconnect("Server nemá kompatibilní verzi TRIAS.");
                        return;
                    }
                    identity = message["id"].Text;
                    room = message["room"].Text;
                    mode = message["mode"].Text;
                    world = new TriasWorld(arena, catalog, mode, view);
                    effects.listener = view.transform;
                    orb = world.MaterialAt("#ff62c5", true);
                    grenade = world.MaterialAt("#ffd28e");
                    grip = TriasWorld.Group(view.transform, "First person arms");
                    status = null;
                    sendAt = pingAt = lastPacket = now;
                    return;
                case "state":
                    if (world != null && message["tick"].Int > lastTick) Apply(message, now);
                    return;
            }
        }

        void Apply(WireValue snapshot, float now)
        {
            lastPacket = now;
            lastTick = snapshot["tick"].Int;
            state = snapshot;
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
                if (before >= 0 && actor.Spawn != before)
                    effects.Reentry(actor.Desired, PlayerTint(row), false);
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
            predicted.Read(own);
            if (spawn || mountChange)
            {
                commands.Clear();
                display = predicted.Position;
                yaw = (own[24].Float + Mathf.PI) * Mathf.Rad2Deg;
                pitch = -own[25].Float * Mathf.Rad2Deg;
                if (spawn)
                {
                    teamTint = PlayerTint(own);
                    if (previous != null) effects.Reentry(display, teamTint, true);
                    spectator = killer = null; replayUntil = 0;
                }
            }
            SyncConstruction(snapshot["horde"]["build"]);
            int applied = own[14].Int;
            int count = 0;
            while (count < commands.Count && commands[count].Seq <= applied) count++;
            if (count > 0) commands.RemoveRange(0, count);
            if (own[6].Int > 0 && own[15].Int < 0 && own[16].Int < 0 && snapshot["winner"]["label"].Text == null)
                foreach (var input in commands) predicted.Step(input, movement ?? world.Spec);
            if (previous != null)
            {
                if (own[6].Int < previous[6].Int || own[7].Int < previous[7].Int) hurt = 1;
                if ((own[2].Int & 64) != (previous[2].Int & 64) && !spawn && own[6].Int > 0)
                    effects.Reload(display, (own[2].Int & 64) == 0);
            }
            SwitchWeapon(own[9].Text ?? "pulse");
            SyncWorld(snapshot);
            foreach (var evt in snapshot["events"].Items) GameEvent(evt, now);
        }

        void SwitchWeapon(string key)
        {
            if (key == weaponName) return;
            if (weaponName != null) weapons[weaponName].gameObject.SetActive(false);
            if (!weapons.TryGetValue(key, out var art))
            {
                art = TriasWorld.Group(grip, key);
                TriasModels.Gun(world, art, key, world.MaterialAt(Catalog.Find(catalog.weapons, key)?.color ?? "#b9fff1", true));
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
                world.Zones[i].GetComponent<LineRenderer>().sharedMaterial = owner >= 0 && owner < 3 ? world.TeamLight[owner] : world.TeamNeutral;
            }
            for (int i = 0; i < world.CoreShield.Length; i++)
            {
                var info = frame["siege"]["cores"][i];
                world.Cores[i].gameObject.SetActive(info["hp"].Int > 0);
                world.CoreShield[i].gameObject.SetActive(info["hp"].Int > 0 && info["opened"].Int == 0);
            }
            if (world.DefendedCore != null)
            {
                int health = frame["horde"]["core"]["hp"].Int;
                world.DefendedCore.gameObject.SetActive(health > 0);
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
                world.Flags[i].position = AtObject(frame["flags"][i]) + Vector3.up * (frame["flags"][i]["status"].Text == "carried" ? 1 : 0);
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
                bool pulse = item[1].Text == "rift";
                if (!projectiles.TryGetValue(id, out var mesh))
                {
                    mesh = TriasWorld.Part(world.Actors, pulse ? orb : grenade, Vector3.zero, Vector3.one * (pulse ? .43f : .24f), PrimitiveType.Sphere);
                    projectiles.Add(id, mesh);
                }
                mesh.position = new Vector3(item[2].Float, item[3].Float, item[4].Float);
                visibleOrbs.Add(id);
            }
            goneOrbs.Clear();
            foreach (var pair in projectiles) if (!visibleOrbs.Contains(pair.Key)) goneOrbs.Add(pair.Key);
            foreach (int id in goneOrbs) { Destroy(projectiles[id].gameObject); projectiles.Remove(id); }
        }

        void GameEvent(WireValue evt, float now)
        {
            string type = evt["type"].Text;
            string key = evt["weapon"].Text ?? "pulse";
            bool local = evt["id"].Text == identity;
            switch (type)
            {
                case "built":
                    effects.Pickup(At(evt["position"]), Catalog.Tint("#c3fa83"), false);
                    break;
                case "tower-shot":
                    effects.Shot(At(evt["from"]), At(evt["position"]), true, false, Catalog.Tint("#99f8ff"), "arc");
                    break;
                case "shot":
                    Vector3 source = At(evt["from"]);
                    if (local && own[15].Int < 0 && own[16].Int < 0) source = grip.TransformPoint(new Vector3(0, .04f, .32f));
                    Color tint = Catalog.Tint(Catalog.Find(catalog.weapons, key)?.color ?? "#ffe49e");
                    if (evt["ends"].Count == 0)
                        effects.Shot(source, source + (local ? view.transform.forward : Vector3.up) * .22f, false, local, tint, key);
                    for (int i = 0; i < evt["ends"].Count; i++)
                    {
                        var end = evt["ends"][i];
                        effects.Shot(source, At(end), end[3].Text != null, local, tint, key, i == 0);
                    }
                    if (local && own[6].Int > 0) tension = key == "scatter" || key == "rift" ? 1.5f : key == "lance" ? 1.2f : .82f;
                    break;
                case "burst":
                    effects.Detonation(At(evt["from"]));
                    break;
                case "core-hit":
                    effects.Shot(At(evt["from"]), At(evt["position"]), false, false, Catalog.Tint("#ff7a5e"), key);
                    break;
                case "core-wreck":
                case "wreck":
                    effects.Detonation(At(evt["position"]));
                    if (type == "core-wreck") AddNotice("Jádro " + catalog.teams[evt["team"].Int].name + " zničeno.");
                    break;
                case "pickup":
                    effects.Pickup(At(evt["position"]), Catalog.Tint((Catalog.Find(catalog.powerups, evt["item"].Text) ?? Catalog.Find(catalog.weapons, evt["item"].Text))?.color), local);
                    if (local) AddNotice("Sebráno · " + (Catalog.Find(catalog.powerups, evt["item"].Text) ?? Catalog.Find(catalog.weapons, evt["item"].Text))?.label);
                    break;
                case "movement":
                    if (evt["action"].Text == "launch" || evt["action"].Text == "double")
                        effects.Launch(At(evt["position"]), Catalog.Tint(catalog.teams[evt["team"].Int].color), local);
                    break;
                case "notice":
                    AddNotice(evt["text"].Text);
                    break;
                case "kill":
                    var body = evt["body"];
                    Vector3 spot = AtObject(body);
                    Color casualtyTint = body["hordeType"].Text == null
                        ? Catalog.Tint(catalog.teams[body["team"].Int].color) : Catalog.Tint("#ff5555");
                    Material casualtyPaint = body["hordeType"].Text == null ? world.TeamLight[body["team"].Int] : world.Hostile;
                    var corpse = new FallenPilot(world, body, casualtyPaint, casualtyTint);
                    if (corpse.Visible) fallen.Add(corpse);
                    else effects.Elimination(spot, casualtyTint);
                    AddNotice(evt["attacker"].Text + "  ›  " + evt["victim"].Text);
                    if (evt["victimId"].Text == identity && own[6].Int <= 0)
                    {
                        killer = evt["attacker"].Text;
                        replayUntil = now + 1.02f;
                        replayOrigin = At(body["from"]);
                        replayPoint = evt["impact"].Count == 3 ? At(evt["impact"]) : spot + Vector3.up;
                        effects.Shot(replayOrigin, replayPoint, true, false, Catalog.Tint(Catalog.Find(catalog.weapons, key)?.color), key, false);
                    }
                    break;
            }
            if (local && evt["hits"].Count > 0) hit = 1;
        }

        void AddNotice(string message) { toast = message; toastUntil = Time.unscaledTime + 3.2f; }

        string NextAlly(string previous)
        {
            if (mode == "ffa") return null;
            var options = new List<Avatar>();
            foreach (var actor in pilots.Values)
                if (actor.Row[3].Int == own[3].Int && actor.Row[6].Int > 0) options.Add(actor);
            options.Sort((a, b) => a.Row[4].Int.CompareTo(b.Row[4].Int));
            return options.Count == 0 ? null : options[(options.FindIndex(a => a.Id == previous) + 1) % options.Count].Id;
        }

        void RenderWorld(float now, float dt)
        {
            world.Animate(now);
            for (int index = fallen.Count - 1; index >= 0; index--)
            {
                var body = fallen[index];
                if (!body.Step(world, dt)) continue;
                effects.Elimination(body.Landing, body.Tint);
                Destroy(body.Root.gameObject);
                fallen.RemoveAt(index);
            }
            bool alive = own[6].Int > 0, driving = own[16].Int >= 0, mounted = own[15].Int >= 0;
            bool replay = !alive && now < replayUntil;
            foreach (var actor in pilots.Values)
            {
                actor.Tick(now);
                bool show = actor.Row[6].Int > 0 && actor.Row[16].Int < 0;
                if (replay || !alive && mode == "ffa") show &= mode != "ffa" && actor.Row[3].Int == own[3].Int;
                actor.Root.gameObject.SetActive(show);
            }
            tension = Mathf.MoveTowards(tension, 0, dt * 6.7f);
            hurt = Mathf.MoveTowards(hurt, 0, dt * 3.2f);
            hit = Mathf.MoveTowards(hit, 0, dt * 5.7f);
            grip.gameObject.SetActive(alive && !driving && !mounted);
            if (alive)
            {
                display = Vector3.Lerp(display, predicted.Position, 1 - Mathf.Exp(-dt * 39));
                Quaternion angle = Quaternion.Euler(pitch - tension * 1.5f, yaw, 0);
                if (driving) Chase(display + Vector3.up * 1.45f, angle * Vector3.forward, 5.5f, 2.1f);
                else view.transform.SetPositionAndRotation(display + Vector3.up * (predicted.Crouch ? .92f : 1.58f), angle);
                float speed = predicted.Velocity.magnitude;
                float reload = (own[2].Int & 64) == 0 ? 0 : Mathf.Sin(Mathf.Clamp01(own[30].Float / 1.55f) * Mathf.PI);
                grip.localPosition = new Vector3(.37f + Mathf.Sin(now * 10) * Mathf.Min(speed * .002f, .016f),
                    -.30f - reload * .12f, .60f - tension * .076f);
                grip.localRotation = Quaternion.Euler(-tension * 8 + reload * 38, -4, reload * 19);
                view.fieldOfView = Mathf.Lerp(view.fieldOfView, driving ? 68 : speed > 8 ? 72 : 67, 1 - Mathf.Exp(-dt * 6));
            }
            else if (replay)
            {
                Vector3 direction = Vector3.ProjectOnPlane(replayOrigin - replayPoint, Vector3.up).normalized;
                if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
                var angle = Quaternion.Euler(0, 29 + (replayUntil - now) * 20, 0);
                Chase(replayPoint, angle * direction, 3.50f, 1.20f);
                view.transform.LookAt(Vector3.Lerp(replayPoint, replayOrigin, .14f));
            }
            else
            {
                if (spectator == null || !pilots.TryGetValue(spectator, out var watched) || watched.Row[6].Int <= 0) spectator = NextAlly(null);
                if (spectator != null && pilots.TryGetValue(spectator, out watched))
                    Chase(watched.Position + Vector3.up * 1.26f, watched.Root.forward, 3.65f, .99f);
                else Chase(display + Vector3.up * 1.11f, Quaternion.Euler(0, yaw, 0) * Vector3.forward, 2.1f, .87f);
            }
        }

        void Chase(Vector3 focus, Vector3 direction, float depth, float rise)
        {
            Vector3 arm = -direction * depth + Vector3.up * rise;
            if (Physics.Raycast(focus, arm.normalized, out var hit, arm.magnitude, TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
                arm = arm.normalized * Mathf.Max(.16f, hit.distance - .17f);
            view.transform.position = focus + arm;
            view.transform.LookAt(focus + direction * 2.4f);
        }
    }
}
