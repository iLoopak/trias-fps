using System;
using System.Collections.Generic;
using Trias.Native;
using UnityEngine;

namespace Trias.Preview
{
    public sealed partial class RelayMatch : MonoBehaviour
    {
        public TextAsset layoutJson;
        public Camera viewCamera;
        public RelayEffects effects;
        public Material[] shell, stripe, glow;
        public Material suit;
        [Min(0.1f)] public float lookSensitivity = 2.1f;

        internal const int PilotLayer = 8;
        internal const int WorldMask = ~(1 << PilotLayer);
        static readonly string[] Callsigns = { "Pilot", "Nova", "Runa", "Apex", "Helix", "Jiskra", "Kobalt", "Volt", "Polaris", "Signal", "Flux", "Azimut" };
        internal readonly List<RelayPilot> Pilots = new List<RelayPilot>(12);
        internal readonly int[] Scores = new int[3];
        readonly RaycastHit[] contacts = new RaycastHit[32];
        readonly List<Flight> flights = new List<Flight>(12);
        internal RelayLayout Layout { get; private set; }
        internal RelayPilot Player { get; private set; }
        internal bool Running { get; private set; }
        internal Camera View => viewCamera;
        internal RelayEffects Effects => effects;
        internal int LastWinner { get; private set; } = -2;
        internal int TotalEliminations { get; private set; }
        float remaining = 360;
        float nextRound;
        RelayRoutes routes;
        Transform held;
        Transform[] weaponModels;
        internal Choice[] Arsenal { get; private set; }
        internal int StartingWeapon { get; private set; }
        readonly HudText hudText = new HudText();
        GUIStyle text, heading, tiny, center;
        string recentKill;
        float killAt;
        Transform spectator;
        const int Goal = 35;

        struct Flight
        {
            public RelayPilot Owner;
            public Vector3 Direction;
            public Transform Orb;
            public float Life;
            public ShotSpec Spec;
            public bool Alternate;
        }

        void Awake()
        {
            Layout = JsonUtility.FromJson<RelayLayout>(layoutJson.text);
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TRIAS/World").text);
            Arsenal = catalog.weapons;
            string selected = Catalog.Find(catalog.pilots, PlayerPrefs.GetString("trias.pilot", "vector"))?.weapon;
            StartingWeapon = Mathf.Max(0, Array.FindIndex(Arsenal, item => item.id == selected));
            routes = new RelayRoutes(Layout);
            LoadDirector();
            BuildViewModel();
            for (int team = 0; team < Layout.teams.Length; team++)
            for (int slot = 0; slot < Layout.teams[team].spawns.Length; slot++)
            {
                var actor = new GameObject(Layout.teams[team].name + " " + Callsigns[team * 4 + slot]);
                actor.layer = PilotLayer;
                actor.transform.SetParent(transform, false);
                var pilot = actor.AddComponent<RelayPilot>();
                pilot.Setup(this, team, slot, team == 0 && slot == 0);
                pilot.Callsign = Callsigns[team * 4 + slot];
                Pilots.Add(pilot);
                if (pilot.Human) Player = pilot;
                else BuildPilot(pilot);
                Spawn(pilot, false);
            }
        }

        void OnDisable()
        {
            if (!Running) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Begin()
        {
            if (livingArena)
            {
                Effects.SetAtmosphere("relay");
                Effects.AmbienceMix(PlayerPrefs.GetFloat("trias.ambience", .72f), true);
            }
            Running = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Player.NextShot = Time.time + 0.25f;
        }

        void Update()
        {
            if (!Running)
            {
                if (Input.GetKeyDown(KeyCode.Return)) Begin();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (livingArena) Effects.AmbienceMix(PlayerPrefs.GetFloat("trias.ambience", .72f), false);
                Running = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (LastWinner != -2)
            {
                if (Time.time >= nextRound) ResetRound();
                return;
            }
            remaining = Mathf.Max(0, remaining - Time.deltaTime);
            UpdateDirector();
            foreach (var pilot in Pilots)
            {
                if (pilot.Health == 0 && Time.time >= pilot.ReturnAt) Spawn(pilot, true);
                pilot.Tick(Mathf.Min(0.05f, Time.deltaTime));
                if (LastWinner != -2) break;
            }
            if (LastWinner == -2) UpdateFlights(Mathf.Min(0.05f, Time.deltaTime));
            UpdateView();
            if (LastWinner == -2 && remaining <= 0)
            {
                int highest = Mathf.Max(Scores);
                int lead = System.Array.IndexOf(Scores, highest);
                int tied = System.Array.LastIndexOf(Scores, highest);
                EndRound(lead == tied ? lead : -1);
            }
        }

        void Spawn(RelayPilot pilot, bool flourish)
        {
            var spawns = Layout.teams[pilot.Team].spawns;
            int first = (pilot.Slot + pilot.Deaths) % spawns.Length;
            Vector3 point = spawns[first].Position;
            for (int offset = 0; offset < spawns.Length; offset++)
            {
                Vector3 candidate = spawns[(first + offset) % spawns.Length].Position;
                bool occupied = false;
                foreach (var other in Pilots)
                {
                    if (other == pilot || other.Health <= 0 || (other.transform.position - candidate).sqrMagnitude > 1.0f) continue;
                    occupied = true;
                    break;
                }
                if (occupied) continue;
                point = candidate;
                break;
            }
            pilot.Spawn(point);
            if (flourish) Effects.Reentry(point, Layout.teams[pilot.Team].Tint, pilot.Human);
        }

        internal bool LaunchAt(Vector3 position, out Vector3 velocity)
        {
            foreach (var pad in Layout.pads)
            {
                float x = position.x - pad.x, z = position.z - pad.z;
                if (position.y > 0.12f || x * x + z * z >= pad.radius * pad.radius) continue;
                velocity = new Vector3(pad.vx, 0, pad.vz);
                return true;
            }
            velocity = Vector3.zero;
            return false;
        }

        internal Vector3 Route(Vector3 from, Vector3 target) => routes.Next(from, target);

        internal RelayPilot TargetFor(RelayPilot pilot, out bool visible)
        {
            RelayPilot closest = null, seen = null;
            float near = float.PositiveInfinity, seenNear = 29f * 29f;
            foreach (var candidate in Pilots)
            {
                if (candidate.Team == pilot.Team || candidate.Health <= 0) continue;
                float square = (candidate.Eye - pilot.Eye).sqrMagnitude;
                if (square < near)
                {
                    near = square;
                    closest = candidate;
                }
                if (square >= seenNear || Physics.Linecast(pilot.Eye, candidate.Eye - Vector3.up * 0.25f, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                seenNear = square;
                seen = candidate;
            }
            visible = seen != null;
            return seen ?? closest;
        }

        bool Trace(RelayPilot owner, Vector3 from, Vector3 direction, float range, out RaycastHit closest, out RelayPilot target, RelayPilot pierced = null)
        {
            int count = Physics.RaycastNonAlloc(from, direction, contacts, range, ~0, QueryTriggerInteraction.Ignore);
            float best = range + 1;
            closest = default;
            target = null;
            for (int i = 0; i < count; i++)
            {
                var hit = contacts[i];
                if (hit.distance >= best || hit.collider == owner.Body || hit.collider == pierced?.Body) continue;
                best = hit.distance;
                closest = hit;
                target = hit.collider.GetComponent<RelayPilot>();
            }
            return closest.collider != null;
        }

        internal void FireWeapon(RelayPilot source, Vector3 origin, Vector3 direction, ShotSpec spec, bool alternate)
        {
            DirectorShot();
            string key = source.Weapon.id;
            Color tint = Catalog.Tint(source.Weapon.color);
            if (alternate) tint = Color.Lerp(tint, Color.white, .4f);
            Vector3 muzzle = source.Human ? held.TransformPoint(new Vector3(0, .04f, PhaseArmory.Muzzle(key)))
                : origin - Vector3.up * .12f + Vector3.Cross(Vector3.up, direction).normalized * .22f;
            if (source.Human) Effects.FireMuzzle(muzzle, tint, key, alternate, 1);
            else Effects.FireWorld(muzzle, key, alternate, false);
            if (spec.projectile != null && spec.projectile.speed > 0)
            {
                LaunchRift(source, origin, direction, spec, alternate);
                return;
            }
            for (int i = 0; i < spec.pattern.Length; i++)
            {
                float offset = spec.pattern[i] * Mathf.Rad2Deg;
                float elevation = spec.horizontal ? 0 : (i % 2 == 0 ? offset : -offset) * .7f;
                Vector3 ray = Quaternion.Euler(elevation, offset, 0) * direction;
                bool contact = Trace(source, origin, ray, spec.range, out var hit, out var victim);
                Vector3 end = contact ? hit.point : origin + ray * spec.range;
                Effects.Shot(muzzle, end, contact, source.Human, tint, key, false);
                bool vulnerable = victim != null && victim.Damage(source, spec.damage);
                if (spec.penetration <= 0 || !vulnerable) continue;
                bool next = Trace(source, origin, ray, spec.range, out var through, out var second, victim);
                if (next && through.distance <= hit.distance) continue;
                Effects.Shot(end, next ? through.point : origin + ray * spec.range, next, source.Human, tint, key, false);
                second?.Damage(source, Mathf.RoundToInt(spec.damage * spec.penetration));
            }
        }

        void LaunchRift(RelayPilot source, Vector3 origin, Vector3 direction, ShotSpec spec, bool alternate)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = alternate ? "Rift heavy bolt" : "Rift bolt";
            var collision = orb.GetComponent<Collider>();
            collision.enabled = false;
            Destroy(collision);
            orb.transform.position = origin + direction * .20f;
            orb.transform.localScale = Vector3.one * (alternate ? .51f : .32f);
            orb.GetComponent<Renderer>().sharedMaterial = Effects.orbMaterial;
            flights.Add(new Flight { Owner = source, Direction = direction, Orb = orb.transform,
                Life = spec.range / spec.projectile.speed, Spec = spec, Alternate = alternate });
        }

        void UpdateFlights(float dt)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight flight = flights[i];
                float step = flight.Spec.projectile.speed * Mathf.Min(dt, flight.Life);
                Vector3 origin = flight.Orb.position;
                bool hit = Trace(flight.Owner, origin, flight.Direction, step, out var impact, out var direct);
                flight.Life -= dt;
                if (!hit && flight.Life > 0)
                {
                    flight.Orb.position = origin + flight.Direction * step;
                    flights[i] = flight;
                    continue;
                }
                Vector3 center = hit ? impact.point - flight.Direction * .045f : origin + flight.Direction * step;
                Effects.Blast(center, false, 1, flight.Alternate);
                float radius = flight.Spec.projectile.radius;
                foreach (var pilot in Pilots)
                {
                    if (pilot == direct)
                    {
                        pilot.Damage(flight.Owner, flight.Spec.damage);
                        continue;
                    }
                    Vector3 target = pilot.Eye - Vector3.up * 0.65f;
                    float distance = Vector3.Distance(center, target);
                    if (distance < radius && !Physics.Linecast(center, target, WorldMask, QueryTriggerInteraction.Ignore))
                        pilot.Damage(flight.Owner, Mathf.Max(8, Mathf.RoundToInt(flight.Spec.projectile.splash * (1 - distance / radius))));
                }
                Destroy(flight.Orb.gameObject);
                flights.RemoveAt(i);
            }
        }

        internal void Eliminated(RelayPilot killer, RelayPilot victim)
        {
            TotalEliminations++;
            Effects.Elimination(victim.transform.position, Layout.teams[victim.Team].Tint);
            recentKill = killer.Callsign + "  ›  " + victim.Callsign;
            killAt = Time.time;
            if (victim.Human) spectator = null;
            Scores[killer.Team]++;
            if (Scores[killer.Team] >= Goal && LastWinner == -2) EndRound(killer.Team);
        }

        void EndRound(int winner)
        {
            LastWinner = winner;
            nextRound = Time.time + 4.5f;
        }

        void ResetRound()
        {
            LastWinner = -2;
            remaining = 360;
            round++;
            ResetDirector();
            recentKill = null;
            System.Array.Clear(Scores, 0, Scores.Length);
            foreach (var flight in flights) Destroy(flight.Orb.gameObject);
            flights.Clear();
            foreach (var pilot in Pilots) Spawn(pilot, true);
        }

        void UpdateView()
        {
            held.gameObject.SetActive(Player.Health > 0);
            if (Player.Health <= 0)
            {
                RelayPilot teammate = null;
                foreach (var pilot in Pilots)
                {
                    if (pilot.Team != Player.Team || pilot.Health <= 0) continue;
                    if (teammate == null || pilot.transform == spectator) teammate = pilot;
                }
                if (teammate == null) return;
                spectator = teammate.transform;
                Vector3 focus = teammate.Eye;
                Vector3 back = (-spectator.forward * 3.4f + spectator.right * 0.55f + Vector3.up * 0.9f);
                if (Physics.Raycast(focus, back.normalized, out var hit, back.magnitude, WorldMask, QueryTriggerInteraction.Ignore))
                    back = back.normalized * Mathf.Max(0.18f, hit.distance - 0.23f);
                View.transform.position = focus + back;
                View.transform.LookAt(focus + spectator.forward * 2.7f);
                return;
            }
            for (int i = 0; i < weaponModels.Length; i++)
                if (weaponModels[i].gameObject.activeSelf != (Player.WeaponIndex == i))
                    weaponModels[i].gameObject.SetActive(Player.WeaponIndex == i);
            float recoil = Player.Kick * (Player.Weapon.id == "rift" ? .028f : .022f);
            float speed = Player.Velocity.magnitude;
            float sway = Mathf.Sin(Time.time * (speed > 7.6f ? 12f : 9.0f)) * Mathf.Min(speed, 8f) * 0.0012f;
            float reloading = Player.ReloadAt > 0 ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((Player.ReloadAt - Time.time) / Player.Weapon.reload)) : 0f;
            held.localPosition = new Vector3(0.38f + sway, -0.32f - reloading * 0.15f, 0.63f - recoil);
            held.localRotation = Quaternion.Euler(-Player.Kick * 2.0f + reloading * 27, -3, reloading * 13);
        }

        void BuildViewModel()
        {
            held = Mount(viewCamera.transform, "Weapon mount", new Vector3(0.38f, -0.32f, 0.63f));
            weaponModels = new Transform[Arsenal.Length];
            var art = new BlenderArmory();
            for (int i = 0; i < Arsenal.Length; i++)
            {
                var weapon = Arsenal[i];
                var mount = weaponModels[i] = Mount(held, weapon.label, Vector3.zero);
                int paint = weapon.id == "rift" ? 2 : 0;
                Material Finish(string slot) => slot switch
                {
                    "Ceramic" => shell[paint], "Secondary" => stripe[paint], "Alloy" => shell[paint],
                    "Amber" => glow[1], _ => slot.EndsWith("Glow", StringComparison.Ordinal) ? glow[paint] : suit
                };
                if (!art.Build(mount, weapon.id, Finish)) Carbine(mount, weapon.id == "rift", paint);
                mount.gameObject.SetActive(i == StartingWeapon);
            }
        }

        void BuildPilot(RelayPilot pilot)
        {
            pilot.Art = Mount(pilot.transform, "Pilot armor", Vector3.zero);
            int team = pilot.Team;
            float width = pilot.Slot % 3 == 1 ? 0.61f : 0.49f;
            Piece(pilot.Art, stripe[team], new Vector3(0, 1.08f, 0), new Vector3(width, 0.44f, 0.27f));
            Piece(pilot.Art, stripe[team], new Vector3(0, 1.17f, 0.117f), new Vector3(width * 0.84f, 0.24f, 0.12f));
            Piece(pilot.Art, shell[team], new Vector3(0, 1.095f, -0.185f), new Vector3(width * 0.71f, 0.30f, 0.13f));
            Piece(pilot.Art, glow[team], new Vector3(0, 1.10f, -0.258f), new Vector3(0.047f, 0.22f, 0.015f));
            pilot.Visor = Mount(pilot.Art, "Helmet joint", new Vector3(0, 1.46f, 0));
            Piece(pilot.Visor, stripe[team], new Vector3(0, 0.11f, 0), new Vector3(0.32f, 0.30f, 0.33f),
                team == 1 ? PrimitiveType.Cube : PrimitiveType.Sphere);
            Piece(pilot.Visor, glow[team], new Vector3(0, 0.12f, 0.156f), new Vector3(0.25f, 0.033f, 0.025f));
            for (int side = -1; side <= 1; side += 2)
            {
                var leg = Mount(pilot.Art, "Hip", new Vector3(side * 0.145f, 0.86f, 0));
                if (side < 0) pilot.LeftLeg = leg;
                else pilot.RightLeg = leg;
                Piece(leg, suit, new Vector3(0, -0.29f, 0), new Vector3(0.17f, 0.61f, 0.16f));
                Piece(leg, shell[team], new Vector3(0, -0.55f, 0.037f), new Vector3(0.18f, 0.37f, 0.20f));
                Piece(leg, suit, new Vector3(0, -0.77f, 0.041f), new Vector3(0.20f, 0.16f, 0.29f));
                Piece(pilot.Art, stripe[team], new Vector3(side * width * 0.57f, 1.29f, 0), new Vector3(team == 1 ? .26f : .19f, 0.17f, 0.29f));
                if (team == 0) Piece(pilot.Art, stripe[team], new Vector3(side*.19f,1.29f,-.18f),new Vector3(.069f,.24f,.13f));
                Piece(pilot.Art, suit, new Vector3(side * width * 0.54f, 1.13f, 0.17f), new Vector3(0.13f, 0.14f, 0.40f));
            }
            if (team == 2) Piece(pilot.Art, stripe[team], new Vector3(0,1.34f,-.24f),new Vector3(.11f,.38f,.12f));
            Transform gun = Mount(pilot.Visor, "Weapon", new Vector3(0.21f, -0.34f, 0.33f));
            gun.localScale = Vector3.one * 0.72f;
            Carbine(gun, false, team);
        }

        void Carbine(Transform root, bool heavy, int team)
        {
            Piece(root, suit, Vector3.zero, new Vector3(heavy ? 0.27f : 0.20f, 0.18f, 0.58f));
            Piece(root, shell[team], new Vector3(0, 0.10f, 0.04f), new Vector3(0.21f, 0.07f, 0.40f));
            Piece(root, suit, new Vector3(0, -0.13f, 0.06f), new Vector3(0.10f, 0.22f, 0.14f));
            Piece(root, glow[team], new Vector3(0, 0.016f, 0.32f), new Vector3(0.15f, 0.077f, 0.18f));
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(root, stripe[team], new Vector3(side * (heavy ? 0.145f : 0.105f), 0.025f, 0.115f), new Vector3(0.023f, 0.07f, 0.37f));
                Piece(root, glow[team], new Vector3(side * (heavy ? 0.16f : 0.12f), 0.032f, 0.16f), new Vector3(0.011f, 0.02f, 0.17f));
            }
        }

        static Transform Mount(Transform parent, string label, Vector3 position)
        {
            var joint = new GameObject(label).transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            return joint;
        }

        static void Piece(Transform root, Material paint, Vector3 position, Vector3 scale, PrimitiveType shape = PrimitiveType.Cube)
        {
            var item = GameObject.CreatePrimitive(shape);
            var collider = item.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            item.layer = PilotLayer;
            item.transform.SetParent(root, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = paint;
        }

        void OnGUI()
        {
            if (Player == null) return;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) {
                    fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Overflow, wordWrap = false, normal = { textColor = Color.white }
                };
                heading = new GUIStyle(text) { fontSize = 32 };
                tiny = new GUIStyle(text) { fontSize = 13, normal = { textColor = new Color(0.72f, 0.82f, 0.91f) } };
                center = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            }
            float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
            Panel(new Rect(15, 16, 242, 60));
            hudText.Draw(new Rect(27, 20, 215, 29), "RELAY STATION", text);
            hudText.Draw(new Rect(28, 48, 205, 21), "TRÉNINK OFFLINE", tiny);
            Panel(new Rect(x - 181, 16, 362, 60));
            for (int i = 0; i < Scores.Length; i++)
            {
                GUI.contentColor = Layout.teams[i].Tint;
                hudText.Draw(new Rect(x - 180 + i * 100, 28, 100, 36), Layout.teams[i].name + " " + Scores[i], center);
            }
            GUI.contentColor = Color.white;
            int seconds = Mathf.CeilToInt(remaining);
            hudText.Draw(new Rect(x + 121, 28, 53, 36), (seconds / 60) + ":" + (seconds % 60).ToString("00"), center);
            if (!string.IsNullOrEmpty(recentKill) && Time.time - killAt < 3.4f)
                hudText.Draw(new Rect(Screen.width - 244, 96, 224, 26), recentKill, tiny);

            if (!Running)
            {
                Panel(new Rect(x - 240, y - 109, 480, 223));
                hudText.Draw(new Rect(x - 217, y - 93, 440, 48), "Relay Station", heading);
                hudText.Draw(new Rect(x - 215, y - 45, 438, 32), "WASD   /   myš   /   Shift   /   dvojskok mezerníkem", tiny);
                hudText.Draw(new Rect(x - 215, y - 20, 438, 32), "1–5 zbraně   /   F sekundární   /   R přebít   /   Esc pauza", tiny);
                DirectorSettings(x - 215, y + 21);
                if (GUI.Button(new Rect(x - 216, y + 61, 307, 34), "Převzít ovládání")) Begin();
                if (UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/TRIAS.unity") >= 0
                    && GUI.Button(new Rect(x + 96, y + 61, 120, 34), "Do lobby"))
                    UnityEngine.SceneManagement.SceneManager.LoadScene("TRIAS");
                return;
            }
            PaintDirector(x);
            if (LastWinner != -2 || Player.Health == 0)
            {
                Panel(new Rect(x - 150, Screen.height - 146, 300, 70));
                string title = LastWinner == -1 ? "Remíza" : LastWinner >= 0 ? "Vítězí " + Layout.teams[LastWinner].name
                    : "Respawn za " + Mathf.Max(0, Player.ReturnAt - Time.time).ToString("0.0") + " s";
                hudText.Draw(new Rect(x - 148, Screen.height - 142, 296, 58), title, center);
                return;
            }
            if (Player.HitFlash > 0)
            {
                var hurt = new Color(1, 0.22f, 0.16f, Player.HitFlash * 0.63f);
                Draw(new Rect(0, 0, Screen.width, 4), hurt);
                Draw(new Rect(0, Screen.height - 4, Screen.width, 4), hurt);
                Draw(new Rect(0, 0, 4, Screen.height), hurt);
                Draw(new Rect(Screen.width - 4, 0, 4, Screen.height), hurt);
            }
            Color reticle = Player.ConfirmFlash > 1 ? new Color(1, 0.62f, 0.34f) :
                Player.ConfirmFlash > 0 ? new Color(0.62f, 1, 0.88f) : Color.white;
            Draw(new Rect(x - 8, y - 1, 5, 2), reticle);
            Draw(new Rect(x + 3, y - 1, 5, 2), reticle);
            Draw(new Rect(x - 1, y - 8, 2, 5), reticle);
            Draw(new Rect(x - 1, y + 3, 2, 5), reticle);
            Panel(new Rect(15, Screen.height - 83, 126, 64));
            hudText.Draw(new Rect(27, Screen.height - 78, 83, 21), "ION", tiny);
            hudText.Draw(new Rect(27, Screen.height - 59, 95, 36), Player.Health.ToString(), heading);
            Panel(new Rect(Screen.width - 182, Screen.height - 83, 165, 64));
            hudText.Draw(new Rect(Screen.width - 169, Screen.height - 79, 142, 22), $"{Player.WeaponIndex + 1}   {Player.Weapon.label.ToUpperInvariant()}", tiny);
            hudText.Draw(new Rect(Screen.width - 169, Screen.height - 60, 145, 38),
                Player.Ammo + " / " + (Player.WeaponIndex == 0 ? "∞" : Player.Reserve.ToString()), heading);
            Panel(new Rect(Screen.width - 182, Screen.height - 107, 165, 21));
            hudText.Draw(new Rect(Screen.width - 169, Screen.height - 107, 149, 21),
                $"F   {Player.Weapon.alt.label} · {Player.Weapon.alt.Cost}", tiny);
            if (Player.ReloadAt > 0)
                hudText.Draw(new Rect(x - 86, Screen.height - 83, 172, 37), "Přebíjíš…", center);
        }

        static void Panel(Rect rect) => Draw(rect, new Color(0.025f, 0.065f, 0.106f, 0.92f));

        static void Draw(Rect rect, Color color)
        {
            var saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = saved;
        }
    }
}
