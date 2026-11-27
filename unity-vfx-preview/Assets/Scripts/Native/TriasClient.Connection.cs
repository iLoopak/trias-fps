using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        Uri sessionEndpoint;
        string reconnectTicket;
        float reconnectUntil, reconnectAt, reconnectGrace;
        int reconnectAttempt;
        bool awaitingResumeFrame, recaptureAfterReconnect;
        readonly List<WireValue> deferredEvents = new List<WireValue>(32);

        void DrainMatchPackets(float now)
        {
            WireValue newest = null;
            deferredEvents.Clear();
            try
            {
                for (int i = 0; i < 18 && socket != null && socket.Received.TryDequeue(out var raw); i++)
                {
                    var packet = WireValue.Parse(raw);
                    if (packet["type"].Text == "state")
                    {
                        if (packet["tick"].Int <= (newest?["tick"].Int ?? lastTick)) continue;
                        if (newest != null && newest["events"].Items != null) deferredEvents.AddRange(newest["events"].Items);
                        newest = packet;
                        continue;
                    }
                    if (newest != null)
                    {
                        Accept(newest, now);
                        newest = null;
                        deferredEvents.Clear();
                        if (socket == null) return;
                    }
                    Accept(packet, now);
                }
                if (newest != null) Accept(newest, now);
            }
            catch (FormatException) { Disconnect("Server poslal neplatný stav zápasu."); }
            finally { deferredEvents.Clear(); }
        }

        void ResetConnection()
        {
            sessionEndpoint = null;
            reconnectTicket = null;
            reconnectUntil = reconnectAt = reconnectGrace = 0;
            reconnectAttempt = 0;
            awaitingResumeFrame = practiceRoom = practiceRequested = recaptureAfterReconnect = false;
        }

        void ConnectionInterrupted(string reason, float now)
        {
            if (sessionEndpoint == null || world == null || string.IsNullOrEmpty(reconnectTicket))
            {
                Disconnect(reason);
                return;
            }
            socket?.Dispose();
            socket = null;
            if (reconnectUntil <= 0)
            {
                recaptureAfterReconnect = captured;
                reconnectUntil = now + Mathf.Clamp(reconnectGrace - 6, 8, 49);
                reconnectAt = now + .25f;
                reconnectAttempt = 0;
                Capture(false);
                effects.ClearTransient();
                return;
            }
            awaitingResumeFrame = false;
            reconnectAt = now + Mathf.Min(4, .65f * reconnectAttempt);
        }

        bool TickReconnect(float now)
        {
            if (reconnectUntil <= 0) return false;
            if (now >= reconnectUntil)
            {
                Disconnect("Spojení s touto místností se nepodařilo obnovit.");
                return true;
            }
            if (socket == null && now >= reconnectAt)
            {
                reconnectAttempt++;
                var request = new JoinCommand { type = "join", v = 2, room = room, intent = "resume", ticket = reconnectTicket,
                    signature = true, predictFire = true, botRadio = true,
                    geometry = Array.Find(catalog.maps, arena => arena.id == "relay")?.revision };
                socket = new TriasSocket();
                joinedAt = lastPacket = now;
                socket.Start(sessionEndpoint, JsonUtility.ToJson(request));
            }
            return true;
        }

        void ReadyForResume(WireValue message, float now)
        {
            awaitingResumeFrame = true;
            own = null;
            commands.Clear();
            pendingShots.Clear();
            sequence = lastTick = fireId = echoAction = queuedButtons = 0;
            echoAwaitUntil = echoRejectUntil = replayUntil = 0;
            ResetFeel();
            sendAt = pingAt = lastPacket = now;
            LoadFireProfiles(message["fireProfiles"]);
        }

        void CompleteResume()
        {
            awaitingResumeFrame = false;
            reconnectUntil = 0;
            deploymentUntil = 0;
            effects.SetAtmosphere(world.Spec.id);
            Capture(recaptureAfterReconnect && Application.isFocused);
            AddNotice("Spojení obnoveno · stejný zápas");
        }

        void DrawReconnect(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(.018f,.035f,.055f,.82f));
            float x = w * .5f - 215, y = h * .5f - 87;
            CornerPanel(new Rect(x, y, 430, 174), Amber);
            hudText.Draw(new Rect(x + 18, y + 15, 394, 39), "Obnovuji spojení", heading);
            hudText.Draw(new Rect(x + 18, y + 60, 394, 28), room + "  ·  " +
                Mathf.CeilToInt(reconnectUntil - Time.unscaledTime) + " s", caption);
            if (Preset(new Rect(x + 18, y + 109, 210, 40), "Zpět do lobby", null, false)) Disconnect(null);
        }
    }
}
