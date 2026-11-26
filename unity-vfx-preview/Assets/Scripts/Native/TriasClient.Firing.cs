using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        struct FireProfile
        {
            internal float Interval, Reload, AltInterval;
            internal int Magazine, AltCost;
        }

        struct PendingShot
        {
            internal int Id, Cost;
            internal string Weapon;
            internal float Sent;
        }

        readonly Dictionary<string, FireProfile> fireProfiles = new Dictionary<string, FireProfile>(5);
        readonly List<PendingShot> pendingShots = new List<PendingShot>(12);
        int fireId, localFeedbackFrame;
        float nextLocalShot, lastDryClick, reloadPreviewUntil, reloadPreviewStarted, switchPreviewUntil;
        float confirmationMs, predictedCorrection;
        bool predictedFire => fireProfiles.Count == Arsenal.Length && !legacyFirePreview && own[15].Int < 0 && own[16].Int < 0;

        void LoadFireProfiles(WireValue message)
        {
            fireProfiles.Clear();
            if (message.Items == null) return;
            foreach (var profile in message.Items)
            {
                string key = profile[0].Text;
                if (Array.IndexOf(Arsenal, key) < 0 || profile[1].Float <= 0 || profile[2].Int <= 0) continue;
                fireProfiles[key] = new FireProfile
                {
                    Interval = profile[1].Float, Magazine = profile[2].Int, Reload = profile[3].Float,
                    AltInterval = profile[4].Float, AltCost = profile[5].Int,
                };
            }
        }

        int VisibleAmmo(string key)
        {
            int ammo = own[10][Array.IndexOf(Arsenal, key)][0].Int;
            foreach (var shot in pendingShots)
                if (shot.Weapon == key) ammo -= shot.Cost;
            return Mathf.Max(0, ammo);
        }

        void ResolveFire(int id, float now, bool confirmed)
        {
            for (int i = 0; i < pendingShots.Count; i++)
            {
                var shot = pendingShots[i];
                if (shot.Id != id) continue;
                if (confirmed) confirmationMs = (now - shot.Sent) * 1000;
                pendingShots.RemoveAt(i);
                return;
            }
        }

        void ReadFire(float now)
        {
            string key = own[9].Text ?? "pulse";
            bool click = Input.GetMouseButtonDown(0);
            bool alt = key == "scatter" && Input.GetKeyDown(KeyCode.F);
            if (!predictedFire)
            {
                if (click || alt)
                {
                    string legacy = FormattableString.Invariant($"{{\"type\":\"trigger\",\"yaw\":{WebYaw:F5},\"pitch\":{WebPitch:F5},\"alt\":{(alt ? "true" : "false")}}}");
                    if (!socket.Send(legacy)) queuedButtons |= 1 << (alt ? 7 : 3);
                }
                return;
            }

            bool automatic = key == "pulse" || key == "arc";
            if (!click && !alt && !(automatic && Input.GetMouseButton(0))) return;
            if (requestedWeapon != null || (queuedButtons & (1 << 9 | 1 << 4)) != 0 ||
                (own[2].Int & 64) != 0 || now < reloadPreviewUntil || now < nextLocalShot || now < echoAwaitUntil) return;
            var profile = fireProfiles[key];
            int cost = alt ? profile.AltCost : 1;
            if (cost <= 0 || VisibleAmmo(key) < cost)
            {
                if ((click || alt) && now >= lastDryClick)
                {
                    lastDryClick = now + .22f;
                    effects.DryFire();
                    dryPulse = .42f;
                }
                return;
            }
            PredictFire(key, profile, alt, cost, now);
        }

        bool PredictFire(string key, FireProfile profile, bool alt, int cost, float now)
        {
            if (now < nextLocalShot || pendingShots.Count >= 12) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            double inputStamp = Time.realtimeSinceStartupAsDouble;
#endif
            int id = ++fireId;
            string trigger = FormattableString.Invariant($"{{\"type\":\"trigger\",\"fireId\":{id},\"weapon\":\"{key}\",\"spawn\":{own[13].Int},\"yaw\":{WebYaw:F5},\"pitch\":{WebPitch:F5},\"alt\":{(alt ? "true" : "false")}}}");
            if (!socket.Send(trigger)) return false;
            pendingShots.Add(new PendingShot { Id = id, Cost = cost, Sent = now, Weapon = key });
            float interval = alt ? profile.AltInterval : profile.Interval;
            nextLocalShot = now + Mathf.Ceil(interval / Motor.Tick) * Motor.Tick;
            PlayLocalShot(key, alt);
            localFeedbackFrame = Time.frameCount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            inputFeedbackMs = (float)((Time.realtimeSinceStartupAsDouble - inputStamp) * 1000);
#endif
            return true;
        }

        void PlayLocalShot(string key, bool alt)
        {
            tension = key == "scatter" || key == "rift" ? 1.45f : key == "lance" ? 1.16f : .77f;
            KickWeapon(key, alt);
            effects.FireMuzzle(MuzzlePoint(), ShotTint(key), key, alt, visualEffects);
        }

        Color ShotTint(string key) => Catalog.Tint(Catalog.Find(catalog.weapons, key)?.color ?? "#ffe49e");

        Vector3 MuzzlePoint()
        {
            Vector3 muzzle = grip.TransformPoint(new Vector3(0, .04f, .32f));
            if (Physics.Linecast(view.transform.position, muzzle, out var block, TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
                muzzle = block.point + block.normal * .045f;
            return muzzle;
        }

        void PreviewReload(float now)
        {
            string key = own[9].Text ?? "pulse";
            if (!predictedFire || !fireProfiles.TryGetValue(key, out var profile) || (own[2].Int & 64) != 0 ||
                now < reloadPreviewUntil || VisibleAmmo(key) >= profile.Magazine ||
                own[10][Array.IndexOf(Arsenal, key)][1].Int == 0 || requestedWeapon != null) return;
            reloadPeriod = profile.Reload;
            reloadPreviewStarted = now;
            reloadPreviewUntil = now + Mathf.Clamp(.20f + latency * .0015f, .32f, .90f);
            effects.Reload(display, false, key);
        }

        void PreviewSwitch(float now)
        {
            int slot = Array.IndexOf(Arsenal, requestedWeapon);
            if (slot < 0 || requestedWeapon == weaponName || own[10][slot].Count == 0) return;
            switchPreviewUntil = now + Mathf.Clamp(.23f + latency * .0014f, .32f, .9f);
            reloadPreviewUntil = 0;
        }
    }
}
