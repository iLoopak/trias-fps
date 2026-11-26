using System;
using UnityEngine;

namespace Trias.Preview
{
    public sealed partial class RelayMatch
    {
        enum Pacing { BuildUp, Engagement, Peak, Recovery }
        static readonly float[] DesiredRanges = { 7, 10, 14, 16 };
        const float EngagementThreshold = .29f, PeakThreshold = .67f, CoolThreshold = .27f;
        bool directBots, livingArena;
        string seedEntry;
        int seed, round;
        Pacing phase;
        float intensity, phaseAt, nextSample, lastHit, impacts;
        int shots, eliminations, clustered;
        ArenaRhythm rhythm;
        Light reactorLamp;
        float reactorBaseline, offlineVisualEffects;
        float nextRadio, offlineCaptionUntil;
        string offlineCaption;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool directorDebug;
#endif

        internal bool Directed => directBots;
        internal bool Regrouping => directBots && phase == Pacing.Recovery;
        internal float PreferredRange(RelayPilot pilot)
            => DesiredRanges[(pilot.Team * 4 + pilot.Slot + seed % 13) % DesiredRanges.Length] +
                (phase == Pacing.Engagement ? -1.35f : Regrouping ? 2.1f : 0);

        void LoadDirector()
        {
            directBots = PlayerPrefs.GetInt("trias.offlineDirector", 1) != 0;
            livingArena = PlayerPrefs.GetInt("trias.offlineArena", 1) != 0;
            seedEntry = PlayerPrefs.GetString("trias.offlineSeed", "");
            reactorLamp = GameObject.Find("Reactor spill")?.GetComponent<Light>();
            reactorBaseline = reactorLamp != null ? reactorLamp.intensity : 0;
            offlineVisualEffects = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.visualEffects", 1));
            ResetDirector();
        }

        void ResetDirector()
        {
            int stable = int.TryParse(seedEntry, out var supplied) && supplied >= 0 ? supplied : UnityEngine.Random.Range(0, 1_000_000_000);
            seed = (int)(((long)stable + 104729L * round) % 1_000_000_000L);
            phase = Pacing.BuildUp;
            phaseAt = intensity = nextSample = impacts = 0;
            lastHit = -30;
            shots = eliminations = clustered = 0;
            rhythm = default;
            nextRadio = 4;
            offlineCaptionUntil = 0;
        }

        void DirectorShot() { if (Directed) shots++; }

        internal void DirectorHit(int amount, bool eliminated)
        {
            if (!Directed) return;
            impacts += amount;
            lastHit = 360 - remaining;
            if (eliminated) eliminations++;
        }

        void UpdateDirector()
        {
            float clock = 360 - remaining;
            if (clock >= nextRadio && Player.Health > 0)
            {
                nextRadio = clock + 3;
                foreach (var ally in Pilots)
                {
                    if (ally.Human || ally.Team != Player.Team || ally.Health <= 0 ||
                        (ally.transform.position - Player.transform.position).sqrMagnitude > 16 * 16) continue;
                    nextRadio = clock + 17;
                    string line = ally.Health < RelayPilot.MaxHealth * .35f ? "retreat" : ally.DirectorContact ? "contact" : "cover";
                    string speech = line == "retreat" ? "Stahuju se do krytu." : line == "contact" ? "Kontakt vpředu." : "Kryju náš sektor.";
                    offlineCaption = ally.Callsign + "  ·  " + speech;
                    offlineCaptionUntil = clock + 2.5f;
                    Effects.BotRadio(line, ally.Slot % 2 == 0, PlayerPrefs.GetFloat("trias.radio", .74f));
                    break;
                }
            }
            if (Directed && clock >= nextSample)
            {
                nextSample = clock + .5f;
                int alive = 0, moving = 0, looking = 0;
                float pressure = 0;
                clustered = 0;
                foreach (var pilot in Pilots)
                {
                    if (pilot.Health <= 0) continue;
                    alive++;
                    pressure += 1 - pilot.Health / (float)RelayPilot.MaxHealth;
                    if (pilot.Velocity.sqrMagnitude > 25) moving++;
                    if (pilot.DirectorContact) looking++;
                    if (pilot.Human) continue;
                    int close = 0;
                    foreach (var ally in Pilots)
                        if (ally.Team == pilot.Team && !ally.Human && ally.Health > 0 &&
                            (ally.transform.position - pilot.transform.position).sqrMagnitude < 25) close++;
                    clustered = Mathf.Max(clustered, close);
                }
                float demand = Mathf.Clamp01(Mathf.Min(.19f, shots * .024f) + Mathf.Min(.30f, impacts / 390) +
                    Mathf.Min(.17f, eliminations * .085f) + .21f * Mathf.Clamp01(1 - (clock - lastHit) / 5) +
                    (.11f * pressure + .08f * moving + .12f * looking) / Mathf.Max(1, alive) + (clustered >= 4 ? .05f : 0));
                intensity = Mathf.Lerp(intensity, demand, demand > intensity ? .43f : .25f);
                shots = eliminations = 0;
                impacts = 0;
                float age = clock - phaseAt;
                var next = phase switch
                {
                    Pacing.BuildUp when age >= 3 && intensity >= EngagementThreshold => Pacing.Engagement,
                    Pacing.Engagement when age >= 2 && intensity >= PeakThreshold => Pacing.Peak,
                    Pacing.Engagement when age >= 5 && intensity <= CoolThreshold => Pacing.Recovery,
                    Pacing.Peak when age >= 3.5f && (age >= 8 || intensity < CoolThreshold) => Pacing.Recovery,
                    Pacing.Recovery when age >= 6.5f => intensity >= EngagementThreshold ? Pacing.Engagement : Pacing.BuildUp,
                    _ => phase
                };
                if (next != phase) { phase = next; phaseAt = clock; }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F4)) directorDebug = !directorDebug;
#endif
            if (!livingArena) return;
            rhythm = ArenaRhythm.At(seed, clock);
            float localCombat = Mathf.Clamp01(Mathf.Max(Player.HitFlash, Player.ConfirmFlash));
            Effects.MixArenaRhythm(rhythm.Level + .12f * localCombat);
            if (reactorLamp != null)
                reactorLamp.intensity = reactorBaseline * (1 + .21f * rhythm.Pulse * offlineVisualEffects);
        }

        internal Vector3 PatrolFor(RelayPilot pilot, Vector3 previous)
        {
            var pads = Layout.pads;
            if (pads.Length == 0) return pilot.transform.position;
            int preferred = (pilot.Team + pilot.Slot + seed % 131 + Mathf.FloorToInt((360 - remaining) / 13)) % pads.Length;
            float best = float.NegativeInfinity;
            Vector3 goal = pilot.transform.position;
            for (int i = 0; i < pads.Length; i++)
            {
                var pad = pads[i];
                var position = new Vector3(pad.x, 0, pad.z);
                Vector3 radial = position.normalized;
                Vector3 tangent = Vector3.Cross(Vector3.up, radial);
                Vector3 candidate = position * (Regrouping ? .99f : phase == Pacing.Engagement ? .61f : .83f)
                    + tangent * (pilot.Slot % 2 == 0 ? -2.3f : 2.3f);
                if (Physics.CheckSphere(candidate + Vector3.up, .6f, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                int reserved = 0;
                foreach (var ally in Pilots)
                    if (ally != pilot && ally.Health > 0 && ally.Team == pilot.Team && !ally.Human &&
                        (ally.DirectorGoal - candidate).sqrMagnitude < 16) reserved++;
                float score = (i == preferred ? 8 : 0) - 9 * reserved - Vector3.Distance(pilot.transform.position, candidate) * .28f
                    - ((candidate - previous).sqrMagnitude < 9 ? 13 : 0);
                if (score > best) { best = score; goal = candidate; }
            }
            return goal;
        }

        void DirectorSettings(float x, float y)
        {
            bool bots = GUI.Toggle(new Rect(x, y, 130, 24), directBots, "Dynamické hlídky");
            bool arena = GUI.Toggle(new Rect(x + 141, y, 123, 24), livingArena, "Živá aréna");
            hudText.Draw(new Rect(x + 271, y, 86, 24), "Seed (volit.)", tiny);
            string entered = GUI.TextField(new Rect(x + 361, y + 1, 72, 22), seedEntry, 9);
            if (bots != directBots || arena != livingArena || entered != seedEntry)
            {
                directBots = bots;
                livingArena = arena;
                seedEntry = entered;
                PlayerPrefs.SetInt("trias.offlineDirector", bots ? 1 : 0);
                PlayerPrefs.SetInt("trias.offlineArena", arena ? 1 : 0);
                PlayerPrefs.SetString("trias.offlineSeed", seedEntry);
                if (remaining >= 359.9f) ResetDirector();
                if (!livingArena)
                {
                    rhythm = default;
                    Effects.MixArenaRhythm(0);
                    Effects.StopAtmosphere();
                    if (reactorLamp != null) reactorLamp.intensity = reactorBaseline;
                }
            }
        }

        void PaintDirector(float cx)
        {
            if (livingArena && rhythm.Warning > 0)
                hudText.Draw(new Rect(cx - 150, 84, 300, 26), $"Cyklus reaktoru za {rhythm.Warning} s", center);
            if (360 - remaining < offlineCaptionUntil && Player.Health > 0)
            {
                Panel(new Rect(18, Screen.height - 123, 279, 31));
                hudText.Draw(new Rect(25, Screen.height - 121, 264, 26), offlineCaption, tiny);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!directorDebug) return;
            Panel(new Rect(18, 92, 370, 77));
            hudText.Draw(new Rect(26, 95, 348, 23), $"W7 {phase} · {intensity:F2} · seed {seed}", text);
            hudText.Draw(new Rect(26, 118, 348, 22), $"Hlídky {Directed} · shluk {clustered} · aréna {rhythm.Level:F2}/{rhythm.Pulse:F2}", tiny);
            hudText.Draw(new Rect(26, 139, 348, 22), $"Kontakt {EngagementThreshold:F2} · vrchol {PeakThreshold:F2} · útlum {CoolThreshold:F2}", tiny);
#endif
        }
    }
}
