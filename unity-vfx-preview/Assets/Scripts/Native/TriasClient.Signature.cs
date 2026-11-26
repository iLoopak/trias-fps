using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        [Header("Signature systems")]
        [SerializeField] KeyCode echoActivation = KeyCode.X;
        [SerializeField, Range(0, 1)] float momentumPresentation = 1;

        sealed class EchoArt
        {
            internal Transform Root;
            internal Material Material;
            internal Vector3 Position;
            internal Color Tint;
        }

        static readonly string[] FlowTiers = { "MOMENTUM", "KINETIC", "SURGE", "OVERDRIVE", "FLOW STATE" };
        static readonly int EchoLife = Shader.PropertyToID("_Life");
        static readonly int EchoIntegrity = Shader.PropertyToID("_Integrity");
        readonly Dictionary<int, EchoArt> echoes = new Dictionary<int, EchoArt>(12);
        readonly List<int> vanishedEchoes = new List<int>(12);
        readonly HashSet<int> visibleEchoes = new HashSet<int>();
        WireValue flow;
        Shader echoShader;
        int activeEcho, echoAction, flowTier;
        float echoExpires, chainWindow, flowStamp, echoRejectUntil, echoAwaitUntil, echoSentAt;
        bool echoHint;
        string echoStatus;

        void ReadEcho(float now)
        {
            if (!Input.GetKeyDown(echoActivation) || !state["signature"]["echo"].Bool || building) return;
            if (now < echoAwaitUntil) return;
            if (activeEcho == 0 || now >= echoExpires || own[15].Int >= 0 || own[16].Int >= 0)
            {
                EchoRejected("OZVĚNA NENÍ DOSTUPNÁ", now);
                return;
            }
            bool alt = own[9].Text == "scatter" && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            SendEcho(alt);
        }

        void SendEcho(bool alt)
        {
            if (!socket.Send(FormattableString.Invariant($"{{\"type\":\"echo-fire\",\"echo\":{activeEcho},\"action\":{++echoAction},\"yaw\":{WebYaw:F5},\"pitch\":{WebPitch:F5},\"alt\":{(alt ? "true" : "false")}}}"))) return;
            echoSentAt = Time.unscaledTime;
            echoAwaitUntil = echoSentAt + Mathf.Clamp(.31f + latency * .0014f, .43f, 1.1f);
        }

        void SyncSignature(WireValue frame, float now)
        {
            visibleEchoes.Clear();
            activeEcho = 0;
            float lifetime = Mathf.Max(.1f, frame["lifetime"].Float);
            if (frame["echoes"].Count > 0 && echoShader == null)
                echoShader = Resources.Load<Shader>("TRIAS/CombatEcho");
            if (echoShader != null && echoShader.isSupported && frame["echoes"].Items != null)
                foreach (var echo in frame["echoes"].Items)
                {
                    int id = echo[1].Int;
                    visibleEchoes.Add(id);
                    if (!echoes.TryGetValue(id, out var art))
                    {
                        art = CreateEcho(echo, echoShader);
                        bool fresh = echo[11].Float > lifetime - .16f;
                        if (fresh) effects.Phase(art.Position, art.Tint, "form", false, visualEffects);
                    }
                    art.Material.SetFloat(EchoLife, Mathf.Clamp01(echo[11].Float / lifetime));
                    art.Material.SetFloat(EchoIntegrity, Mathf.Clamp01(echo[12].Float / Mathf.Max(1, frame["durability"].Float)));
                    if (echo[0].Text == identity)
                    {
                        activeEcho = id;
                        echoExpires = now + echo[11].Float;
                        if (!echoHint && !PlayerPrefs.HasKey("trias.echoIntro"))
                        {
                            echoHint = true;
                            AddNotice(echoActivation + " · vystřelit z ozvěny");
                            PlayerPrefs.SetInt("trias.echoIntro", 1);
                        }
                    }
                }
            vanishedEchoes.Clear();
            foreach (var id in echoes.Keys)
                if (!visibleEchoes.Contains(id)) vanishedEchoes.Add(id);

            flow = null;
            chainWindow = Mathf.Max(.1f, frame["window"].Float);
            flowStamp = now;
            if (frame["chains"].Items != null)
                foreach (var entry in frame["chains"].Items)
                    if (entry[0].Text == identity) { flow = entry; break; }
            int tier = flow == null ? 0 : flow[3].Int;
            if (tier > flowTier && momentumPresentation > .15f)
                effects.Flow(tier, momentumPresentation);
            flowTier = tier;
        }

        EchoArt CreateEcho(WireValue echo, Shader shader)
        {
            int team = Mathf.Clamp(echo[6].Int, 0, catalog.teams.Length - 1);
            Color tint = mode == "ffa" ? Catalog.Tint(catalog.ffaColors[team * 4 + echo[7].Int]) : Catalog.Tint(catalog.teams[team].color);
            // The compact player row lets this pose reuse the exact in-game pilot and weapon silhouette.
            var row = WireValue.Parse(FormattableString.Invariant(
                $"[\"echo\",\"Combat Echo\",{(echo[10].Bool ? 18 : 2)},{team},{echo[7].Int},\"{echo[8].Text}\",150,0,0,\"{echo[9].Text}\",[],0,0,1,0,-1,-1,0,{echo[2].Float},{echo[3].Float},{echo[4].Float},0,0,0,{echo[5].Float},0]"));
            var avatar = new Avatar(world, "echo", row, world.TeamLight[team]);
            avatar.Accept(row, Time.unscaledTime);
            avatar.Tick(Time.unscaledTime);
            foreach (var animation in avatar.Root.GetComponentsInChildren<Animation>())
                animation.enabled = false;
            var material = new Material(shader);
            material.SetColor("_Tint", tint);
            foreach (var renderer in avatar.Root.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                Array.Fill(slots, material);
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            var art = new EchoArt { Root = avatar.Root, Material = material, Position = avatar.Position, Tint = tint };
            echoes.Add(echo[1].Int, art);
            return art;
        }

        void RemoveVanishedEchoes()
        {
            foreach (int id in vanishedEchoes)
            {
                if (!echoes.Remove(id, out var art)) continue;
                Destroy(art.Root.gameObject);
                Destroy(art.Material);
            }
        }

        void ResetSignature()
        {
            foreach (var art in echoes.Values) Destroy(art.Material);
            echoes.Clear();
            flow = null;
            activeEcho = echoAction = flowTier = 0;
            echoRejectUntil = echoAwaitUntil = 0;
        }

        void EchoRejected(string label, float now)
        {
            if (now < echoRejectUntil) return;
            echoStatus = label;
            echoRejectUntil = now + .55f;
            effects.DryFire();
        }

        void OnEchoEnd(WireValue evt, bool local)
        {
            if (!echoes.TryGetValue(evt["echo"].Int, out var art)) return;
            string reason = evt["reason"].Text;
            if (reason == "exit" || reason == "death" || reason == "replace") return;
            effects.Phase(art.Position, art.Tint, reason, local && reason != "fire", visualEffects);
            if (local && reason == "rupture") AddNotice("Ozvěna zničena");
        }

        void ConfirmEchoShot(string weapon, bool alternate, float now)
        {
            echoAwaitUntil = 0;
            if (!fireProfiles.TryGetValue(weapon, out var profile)) return;
            float interval = alternate ? profile.AltInterval : profile.Interval;
            nextLocalShot = Mathf.Max(nextLocalShot, now + Mathf.Max(0, interval - (now - echoSentAt) * .5f));
        }

        void PaintSignature(float w, float h)
        {
            if (!captured || scoreboard || own[6].Int <= 0) return;
            float now = Time.unscaledTime;
            if (activeEcho != 0 && now < echoExpires || now < echoRejectUntil)
            {
                float x = w * .5f - 76, y = h * .5f + 48;
                float lifetime = Mathf.Max(.1f, state["signature"]["lifetime"].Float);
                Color tint = now < echoRejectUntil ? Amber : Shield;
                string label = now < echoRejectUntil ? echoStatus : echoActivation + "   OZVĚNA";
                Fill(new Rect(x, y, 152, 25), Ink);
                GUI.Label(new Rect(x + 5, y, 143, 23), label, center);
                Segments(new Rect(x + 7, y + 22, 138, 2), 12, (echoExpires - now) / lifetime, tint);
            }
            if (flow == null || momentumPresentation <= .01f || (flow[1].Int == 0 && flow[11].Int == 0)) return;
            bool live = flow[1].Int > 0;
            int tier = Mathf.Clamp(flow[3].Int, 0, FlowTiers.Length - 1);
            if (live && tier == 0) return;
            float left = Mathf.Clamp01((flow[4].Float - (now - flowStamp)) / chainWindow);
            float atX = w - 240, atY = h - 167;
            Color edge = new Color(.37f + tier * .10f, .85f, 1f - tier * .12f, momentumPresentation);
            Fill(new Rect(atX, atY, 213, 53), new Color(Ink.r, Ink.g, Ink.b, Ink.a * momentumPresentation));
            Fill(new Rect(atX, atY, 24 + 30 * left, 2), edge);
            string labelFlow = live ? $"{FlowTiers[tier]}  ×{flow[2].Float:0.#}   {flow[1].Int}" : $"ŘETĚZEC  {flow[11].Int}";
            var saved = GUI.color;
            GUI.color = new Color(1, 1, 1, momentumPresentation);
            GUI.Label(new Rect(atX + 8, atY + 4, 201, 22), labelFlow, section);
            GUI.Label(new Rect(atX + 8, atY + 26, 201, 23), FlowAction(flow[10][0].Text), caption);
            GUI.color = saved;
        }

        static string FlowAction(string action) => action switch
        {
            "sprint" => "Sprint", "dodge" => "Úskok", "airDodge" => "Úskok ve vzduchu", "double" => "Dvojskok",
            "launch" => "Odrazová plošina", "land" => "Rychlý dopad", "turn" => "Změna směru",
            "airHit" => "Zásah ze vzduchu", "airKill" => "Vyřazení ze vzduchu", "dodgeKill" => "Vyřazení po úskoku",
            "moveKill" => "Vyřazení za běhu", "echo" => "Výstřel z ozvěny", "echoHit" => "Zásah ozvěnou",
            "echoKill" => "Vyřazení ozvěnou", _ => "",
        };
    }
}
