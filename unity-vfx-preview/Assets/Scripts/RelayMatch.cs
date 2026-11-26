using System.Collections.Generic;
using UnityEngine;

namespace Trias.Preview
{
    public sealed class RelayMatch : MonoBehaviour
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
        Transform held, pulseModel, riftModel;
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
        }

        void Awake()
        {
            Layout = JsonUtility.FromJson<RelayLayout>(layoutJson.text);
            routes = new RelayRoutes(Layout);
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

        bool Trace(RelayPilot owner, Vector3 from, Vector3 direction, float range, out RaycastHit closest, out RelayPilot target)
        {
            int count = Physics.RaycastNonAlloc(from, direction, contacts, range, ~0, QueryTriggerInteraction.Ignore);
            float best = range + 1;
            closest = default;
            target = null;
            for (int i = 0; i < count; i++)
            {
                var hit = contacts[i];
                if (hit.distance >= best || hit.collider == owner.Body) continue;
                best = hit.distance;
                closest = hit;
                target = hit.collider.GetComponent<RelayPilot>();
            }
            return closest.collider != null;
        }

        internal void ShootPulse(RelayPilot source, Vector3 origin, Vector3 direction)
        {
            bool contact = Trace(source, origin, direction, 60, out var hit, out var victim);
            Vector3 end = contact ? hit.point : origin + direction * 60;
            Color paint = victim != null ? Layout.teams[victim.Team].Tint : new Color(0.80f, 0.89f, 0.98f);
            Vector3 tracer = origin - Vector3.up * 0.12f + Vector3.Cross(Vector3.up, direction).normalized * 0.22f;
            if (source.Human) tracer = held.TransformPoint(new Vector3(0, 0.032f, 0.35f));
            Effects.Shot(tracer, end, contact, source.Human, paint);
            if (victim != null) victim.Damage(source, 23);
        }

        internal void ShootRift(RelayPilot source, Vector3 origin, Vector3 direction)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Rift bolt";
            var collision = orb.GetComponent<Collider>();
            collision.enabled = false;
            Destroy(collision);
            orb.transform.position = origin + direction * 0.20f;
            orb.transform.localScale = Vector3.one * 0.32f;
            orb.GetComponent<Renderer>().sharedMaterial = Effects.orbMaterial;
            flights.Add(new Flight { Owner = source, Direction = direction, Orb = orb.transform, Life = 55f / 22f });
        }

        void UpdateFlights(float dt)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight flight = flights[i];
                float step = 22 * dt;
                Vector3 origin = flight.Orb.position;
                bool hit = Trace(flight.Owner, origin, flight.Direction, step, out var impact, out var direct);
                flight.Life -= dt;
                if (!hit && flight.Life > 0)
                {
                    flight.Orb.position = origin + flight.Direction * step;
                    flights[i] = flight;
                    continue;
                }
                Vector3 center = hit ? impact.point - flight.Direction * 0.045f : origin;
                Effects.Detonation(center);
                foreach (var pilot in Pilots)
                {
                    if (pilot == direct)
                    {
                        pilot.Damage(flight.Owner, 72);
                        continue;
                    }
                    Vector3 target = pilot.Eye - Vector3.up * 0.65f;
                    float distance = Vector3.Distance(center, target);
                    if (distance < 3.2f && !Physics.Linecast(center, target, WorldMask, QueryTriggerInteraction.Ignore))
                        pilot.Damage(flight.Owner, Mathf.Max(8, Mathf.RoundToInt(54 * (1 - distance / 3.2f))));
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
            pulseModel.gameObject.SetActive(!Player.Rift);
            riftModel.gameObject.SetActive(Player.Rift);
            float recoil = Player.Kick * (Player.Rift ? 0.028f : 0.022f);
            float speed = Player.Velocity.magnitude;
            float sway = Mathf.Sin(Time.time * (speed > 7.6f ? 12f : 9.0f)) * Mathf.Min(speed, 8f) * 0.0012f;
            float reloading = Player.ReloadAt > 0 ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((Player.ReloadAt - Time.time) / 1.3f)) : 0f;
            held.localPosition = new Vector3(0.38f + sway, -0.32f - reloading * 0.15f, 0.63f - recoil);
            held.localRotation = Quaternion.Euler(-Player.Kick * 2.0f + reloading * 27, -3, reloading * 13);
        }

        void BuildViewModel()
        {
            held = Mount(viewCamera.transform, "Weapon mount", new Vector3(0.38f, -0.32f, 0.63f));
            pulseModel = Mount(held, "Pulse", Vector3.zero);
            riftModel = Mount(held, "Rift", Vector3.zero);
            Carbine(pulseModel, false, 0);
            Carbine(riftModel, true, 2);
            riftModel.gameObject.SetActive(false);
        }

        void BuildPilot(RelayPilot pilot)
        {
            pilot.Art = Mount(pilot.transform, "Pilot armor", Vector3.zero);
            int team = pilot.Team;
            float width = pilot.Slot % 3 == 1 ? 0.61f : 0.49f;
            Piece(pilot.Art, suit, new Vector3(0, 1.08f, 0), new Vector3(width, 0.44f, 0.27f));
            Piece(pilot.Art, stripe[team], new Vector3(0, 1.17f, 0.117f), new Vector3(width * 0.84f, 0.24f, 0.12f));
            Piece(pilot.Art, shell[team], new Vector3(0, 1.095f, -0.185f), new Vector3(width * 0.71f, 0.30f, 0.13f));
            Piece(pilot.Art, glow[team], new Vector3(0, 1.10f, -0.258f), new Vector3(0.047f, 0.22f, 0.015f));
            pilot.Visor = Mount(pilot.Art, "Helmet joint", new Vector3(0, 1.46f, 0));
            Piece(pilot.Visor, shell[team], new Vector3(0, 0.11f, 0), new Vector3(0.32f, 0.30f, 0.33f),
                pilot.Slot % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
            Piece(pilot.Visor, glow[team], new Vector3(0, 0.12f, 0.156f), new Vector3(0.25f, 0.033f, 0.025f));
            for (int side = -1; side <= 1; side += 2)
            {
                var leg = Mount(pilot.Art, "Hip", new Vector3(side * 0.145f, 0.86f, 0));
                if (side < 0) pilot.LeftLeg = leg;
                else pilot.RightLeg = leg;
                Piece(leg, suit, new Vector3(0, -0.29f, 0), new Vector3(0.17f, 0.61f, 0.16f));
                Piece(leg, shell[team], new Vector3(0, -0.55f, 0.037f), new Vector3(0.18f, 0.37f, 0.20f));
                Piece(leg, suit, new Vector3(0, -0.77f, 0.041f), new Vector3(0.20f, 0.16f, 0.29f));
                Piece(pilot.Art, stripe[team], new Vector3(side * width * 0.57f, 1.29f, 0), new Vector3(0.19f, 0.17f, 0.29f));
                Piece(pilot.Art, suit, new Vector3(side * width * 0.54f, 1.13f, 0.17f), new Vector3(0.13f, 0.14f, 0.40f));
            }
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
                text = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                heading = new GUIStyle(text) { fontSize = 32 };
                tiny = new GUIStyle(text) { fontSize = 13, normal = { textColor = new Color(0.72f, 0.82f, 0.91f) } };
                center = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            }
            float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
            Panel(new Rect(15, 16, 242, 60));
            GUI.Label(new Rect(27, 20, 215, 29), "TRIAS   /   RELAY STATION", text);
            GUI.Label(new Rect(28, 48, 205, 21), "UNITY   ·   TEAM DEATHMATCH", tiny);
            Panel(new Rect(x - 181, 16, 362, 60));
            for (int i = 0; i < Scores.Length; i++)
            {
                GUI.contentColor = Layout.teams[i].Tint;
                GUI.Label(new Rect(x - 180 + i * 100, 28, 100, 36), Layout.teams[i].name + " " + Scores[i], center);
            }
            GUI.contentColor = Color.white;
            int seconds = Mathf.CeilToInt(remaining);
            GUI.Label(new Rect(x + 121, 28, 53, 36), (seconds / 60) + ":" + (seconds % 60).ToString("00"), center);
            if (!string.IsNullOrEmpty(recentKill) && Time.time - killAt < 3.4f)
                GUI.Label(new Rect(Screen.width - 244, 96, 224, 26), recentKill, tiny);

            if (!Running)
            {
                Panel(new Rect(x - 240, y - 89, 480, 178));
                GUI.Label(new Rect(x - 217, y - 75, 440, 48), "Relay Station", heading);
                GUI.Label(new Rect(x - 215, y - 22, 438, 32), "WASD   /   myš   /   Shift   /   dvojskok mezerníkem", tiny);
                GUI.Label(new Rect(x - 215, y + 3, 438, 32), "1 Pulse   /   2 Rift   /   R přebít   /   Esc pauza", tiny);
                if (GUI.Button(new Rect(x - 216, y + 42, 307, 32), "Převzít ovládání")) Begin();
                if (UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/TRIAS.unity") >= 0
                    && GUI.Button(new Rect(x + 96, y + 42, 120, 32), "Do lobby"))
                    UnityEngine.SceneManagement.SceneManager.LoadScene("TRIAS");
                return;
            }
            if (LastWinner != -2 || Player.Health == 0)
            {
                Panel(new Rect(x - 150, Screen.height - 146, 300, 70));
                string title = LastWinner == -1 ? "Remíza" : LastWinner >= 0 ? "Vítězí " + Layout.teams[LastWinner].name
                    : "Respawn za " + Mathf.Max(0, Player.ReturnAt - Time.time).ToString("0.0") + " s";
                GUI.Label(new Rect(x - 148, Screen.height - 142, 296, 58), title, center);
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
            GUI.Label(new Rect(27, Screen.height - 78, 83, 21), "ION", tiny);
            GUI.Label(new Rect(27, Screen.height - 59, 95, 36), Player.Health.ToString(), heading);
            Panel(new Rect(Screen.width - 182, Screen.height - 83, 165, 64));
            GUI.Label(new Rect(Screen.width - 169, Screen.height - 79, 142, 22), Player.Rift ? "2   RIFT" : "1   PULSE", tiny);
            GUI.Label(new Rect(Screen.width - 169, Screen.height - 60, 145, 38),
                Player.Rift ? Player.RiftAmmo.ToString() : Player.Ammo + " / ∞", heading);
            if (Player.ReloadAt > 0)
                GUI.Label(new Rect(x - 86, Screen.height - 83, 172, 37), "Přebíjíš…", center);
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
