using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        [Header("First-person presentation")]
        [SerializeField, Range(0, 2)] float recoilStrength = 1;
        [SerializeField, Range(.5f, 2)] float recoilRecoveryTime = 1;
        [SerializeField, Range(0, 2)] float weaponInertia = 1;
        [SerializeField, Range(0, 2)] float strideAmplitude = 1;

        float baseFov, cameraMotion, masterVolume, ambienceVolume, musicVolume, visualEffects, radioVolume;
        float gunTravel, gunTravelVelocity, gunLift, gunLiftVelocity, gunTwist, gunTwistVelocity;
        float cameraKick, cameraKickVelocity, landingStep, landingVelocity, airSpeed;
        float previousPitch, previousYaw, swayX, swayY;
        float recoilRecovery = .11f, stridePhase, strideDistance, stanceHeight = 1.58f, stanceVelocity;
        float equipMotion, dryPulse, movementPulse, reticleRange = 50;
        float gunLanding, gunLandingVelocity, equipSettledAt = -1;
        float reloadProgressShown, reloadCueProgress;
        float chaseArm, spectatorYaw, spectatorHeight, nextNearMissAt;
        Avatar followedPilot;
        Vector3 inertia, reticleWorld, viewVelocity;
        ViewComposer.Style viewStyle;
        readonly float[] localMotionAt = new float[5];
        float sceneFade, resultAge;
        int kickShot;
        bool alternateImpulse;
        bool wasGrounded;
        string roundResult;
        GUIStyle optionTrack, optionThumb;

        void LoadFeel()
        {
            baseFov = Mathf.Clamp(PlayerPrefs.GetFloat("trias.fov", 70), 60, 95);
            cameraMotion = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.motion", .82f));
            viewStyle = PlayerPrefs.GetInt("trias.view", 0) == 1 ? ViewComposer.Style.Cinematic : ViewComposer.Style.Competitive;
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.volume", .85f));
            radioVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.radio", .74f));
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.music", .65f));
            effects.MusicMix(musicVolume);
            ambienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.ambience", .72f));
            visualEffects = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.visualEffects", 1));
            momentumPresentation = Mathf.Clamp01(PlayerPrefs.GetFloat("trias.momentum", momentumPresentation));
            visor.Intensity = visualEffects;
            sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("trias.sensitivity", sensitivity), .5f, 5);
            AudioListener.volume = masterVolume;
        }

        void ResetFeel()
        {
            gunTravel = gunTravelVelocity = gunLift = gunLiftVelocity = gunTwist = gunTwistVelocity = 0;
            cameraKick = cameraKickVelocity = landingStep = landingVelocity = airSpeed = 0;
            previousPitch = pitch;
            previousYaw = yaw;
            swayX = swayY = 0;
            equipMotion = dryPulse = movementPulse = stridePhase = strideDistance = stanceVelocity = 0;
            gunLanding = gunLandingVelocity = 0;
            equipSettledAt = -1;
            stanceHeight = 1.58f;
            reloadProgressShown = reloadCueProgress = 0;
            chaseArm = 0;
            followedPilot = null;
            nextNearMissAt = 0;
            inertia = viewVelocity = correction = Vector3.zero;
            System.Array.Fill(localMotionAt, -10f);
            kickShot = 0;
            alternateImpulse = false;
            wasGrounded = false;
        }

        void FollowSpectator(Avatar pilot, float dt)
        {
            float heading = pilot.Root.eulerAngles.y;
            float height = (pilot.Row[2].Int & 16) != 0 ? .84f : 1.26f;
            if (pilot != followedPilot)
            {
                // Switch at the new pilot so the camera never travels across the arena.
                followedPilot = pilot;
                spectatorYaw = heading;
                spectatorHeight = height;
                chaseArm = 1.28f;
            }
            float follow = cameraMotion < .01f ? 1 : 1 - Mathf.Exp(-dt * 13 / cameraMotion);
            spectatorYaw = Mathf.LerpAngle(spectatorYaw, heading, follow);
            spectatorHeight = Mathf.Lerp(spectatorHeight, height, follow);
            Chase(pilot.Position + Vector3.up * spectatorHeight, Quaternion.Euler(0, spectatorYaw, 0) * Vector3.forward,
                3.65f, .99f, clearCorners: true);
        }

        void PlayNearMiss(WireValue shot, Vector3 source, float now)
        {
            var shooter = shot["id"].Text;
            var ends = shot["ends"];
            if (now < nextNearMissAt || own[6].Int <= 0 || own[15].Int >= 0 || own[16].Int >= 0 ||
                ends.Count == 0 || shot["from"].Count != 3 || shooter == null || shooter == identity ||
                !pilots.TryGetValue(shooter, out var attacker)) return;
            bool hostile = mode == "horde" ? attacker.Row[31].Text != null
                : mode == "ffa" || attacker.Row[3].Int != own[3].Int;
            if (!hostile) return;
            if (shot["hits"].Items is { } hits)
                foreach (var hit in hits)
                    if (hit["id"].Text == identity) return;

            const float radius = .85f;
            Vector3 ear = view.transform.position;
            Vector3 closest = default;
            float best = radius * radius;
            for (int i = 0; i < ends.Count; i++)
            {
                Vector3 segment = At(ends[i]) - source;
                float length = segment.magnitude;
                if (length <= .6f) continue;
                Vector3 direction = segment / length;
                float along = Vector3.Dot(ear - source, direction);
                if (along <= .5f || along >= length - .1f) continue;
                Vector3 point = source + direction * along;
                float gap = (point - ear).sqrMagnitude;
                if (gap >= best || Physics.Linecast(ear, point, TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore)) continue;
                closest = point;
                best = gap;
            }
            if (best >= radius * radius) return;
            effects.NearMiss(closest, 1 - Mathf.Sqrt(best) / radius);
            nextNearMissAt = now + .26f;
        }

        void KickWeapon(string key, bool alt)
        {
            alternateImpulse = alt;
            var profile = key switch
            {
                "pulse" when alt => (.092f, 9.5f, 1.65f, .135f),
                "lance" when alt => (.165f, 11.1f, 1.55f, .185f),
                "arc" when alt => (.087f, 10.1f, 3.5f, .145f),
                "rift" when alt => (.181f, 15.1f, 4.7f, .212f),
                "scatter" when alt => (.128f, 13f, 2.5f, .145f),
                "scatter" => (.113f, 16f, 3.2f, .132f),
                "lance" => (.144f, 9.3f, 1.35f, .158f),
                "rift" => (.154f, 13.1f, 4.2f, .186f),
                "arc" => (.027f, 3.85f, 1.25f, .071f),
                _ => (.055f, 6.6f, 1.13f, .098f)
            };
            int sign = (kickShot++ & 1) == 0 ? -1 : 1;
            gunTravel = Mathf.Min(.21f, gunTravel + profile.Item1 * recoilStrength);
            gunLift = Mathf.Min(19, gunLift + profile.Item2 * recoilStrength);
            gunTwist = Mathf.Clamp(gunTwist + profile.Item3 * sign * recoilStrength, -5.5f, 5.5f);
            recoilRecovery = profile.Item4 * recoilRecoveryTime;
            cameraKick = Mathf.Min(1.7f, cameraKick + profile.Item2 * .064f * recoilStrength);
        }

        int FootSurface(Vector3 at) => world.Spec.id != "island" ? 0 : Motor.Shallow(at, world.Spec) ? 2 : 1;

        void PlayLocalMotion(MotionCue cue, Vector3 at)
        {
            localMotionAt[(int)cue] = Time.unscaledTime;
            movementPulse = cue == MotionCue.Dodge ? 1 : cue == MotionCue.Launch ? .85f : cue == MotionCue.Double ? .60f : .24f;
            if (cue == MotionCue.Jump) landingStep = .027f;
            effects.Motion(at, teamTint, cue, true, visualEffects, mode == "ffa" ? -1 : own[3].Int);
        }

        float ReloadProgress(float now)
        {
            if ((own[2].Int & 64) != 0)
                return Mathf.Clamp01(1 - Mathf.Max(0, own[30].Float - (now - lastPacket)) / Mathf.Max(.1f, reloadPeriod));
            return now < reloadPreviewUntil ? Mathf.Clamp01((now - reloadPreviewStarted) / Mathf.Max(.1f, reloadPeriod)) : 0;
        }

        void UpdateFeel(float now, float dt, bool driving)
        {
            dt = Mathf.Min(.05f, dt);
            // Presentation follows the motion the view shows this frame (between this step and the next),
            // so lean, bob and FOV change every frame instead of in 30 Hz stairs.
            Vector3 shownVelocity = viewVelocity = ShownVelocity(now);
            float speed = new Vector2(shownVelocity.x, shownVelocity.z).magnitude;
            var tuning = ViewComposer.For(viewStyle);
            if (!predicted.Grounded) airSpeed = Mathf.Min(airSpeed, predicted.Velocity.y);
            else if (!wasGrounded)
            {
                float carried = new Vector2(predicted.Velocity.x, predicted.Velocity.z).magnitude - 10.1f;
                if (airSpeed < -4.3f && !driving)
                {
                    landingStep = Mathf.Min(.125f, -airSpeed * .010f);
                    effects.Land(display, Mathf.Clamp01(-airSpeed / 14), FootSurface(display));
                }
                else if (carried > 1.5f && !driving)
                {
                    // A dodge that touches down still fast (about 17 m/s) brakes to a run in four steps: the
                    // boots bite and the body settles, though the fall itself was too slow to count as a landing.
                    landingStep = Mathf.Max(landingStep, Mathf.Min(.045f, carried * .006f));
                    effects.Land(display, Mathf.Clamp01(carried / 12) * .55f, FootSurface(display));
                }
                airSpeed = 0;
            }
            wasGrounded = predicted.Grounded;
            gunTravel = Mathf.SmoothDamp(gunTravel, 0, ref gunTravelVelocity, recoilRecovery, Mathf.Infinity, dt);
            gunLift = Mathf.SmoothDamp(gunLift, 0, ref gunLiftVelocity, recoilRecovery * 1.17f, Mathf.Infinity, dt);
            gunTwist = Mathf.SmoothDamp(gunTwist, 0, ref gunTwistVelocity, recoilRecovery * 1.28f, Mathf.Infinity, dt);
            cameraKick = Mathf.SmoothDamp(cameraKick, 0, ref cameraKickVelocity, .112f, Mathf.Infinity, dt);
            landingStep = Mathf.SmoothDamp(landingStep, 0, ref landingVelocity, .15f, Mathf.Infinity, dt);
            var temper = WeaponHandling.Temperament(weaponName);
            swayX = KineticView.Sway(swayX, Mathf.DeltaAngle(previousYaw, yaw), dt, -.48f * temper.SwayScale, 5.5f, temper.SwayRate);
            swayY = KineticView.Sway(swayY, Mathf.DeltaAngle(previousPitch, pitch), dt, -.38f * temper.SwayScale, 4.5f, temper.SwayRate);
            gunLanding = Mathf.SmoothDamp(gunLanding, landingStep * temper.LandingWeight, ref gunLandingVelocity, .055f, Mathf.Infinity, dt);
            previousYaw = yaw; previousPitch = pitch;
            float progress = ReloadProgress(now);
            bool reloading = (own[2].Int & 64) != 0 || now < reloadPreviewUntil;
            reloadProgressShown = reloading ? Mathf.Max(reloadProgressShown, progress) : 0;
            var reload = WeaponHandling.At(weaponName, reloadProgressShown);
            if (weaponName != null && reloading && (own[2].Int & 64) != 0)
            {
                float insertAt = WeaponHandling.ServiceBeat(weaponName);
                if (reloadCueProgress < insertAt && reloadProgressShown >= insertAt)
                    effects.ReloadSeat(weaponName);
                reloadCueProgress = reloadProgressShown;
            }
            else if (!reloading) reloadCueProgress = 0;
            float motion = cleanView ? 0 : cameraMotion;
            if (predicted.Grounded && !driving && speed > .35f)
            {
                strideDistance += dt * speed;
                stridePhase += dt * speed * 3.20f;
                if (strideDistance >= (predicted.Crouch ? 1.62f : 2.25f))
                {
                    strideDistance = 0;
                    effects.Footstep(display, true, FootSurface(display), predicted.Crouch);
                }
            }
            else strideDistance = 0;
            float strideReach = predicted.Grounded && !cleanView ? Mathf.Min(speed * .0025f, .022f) * strideAmplitude : 0;
            float stride = Mathf.Sin(stridePhase) * strideReach;
            Quaternion aim = Quaternion.Euler(pitch, yaw, 0);
            // The acceleration the current prediction step applies, held for the whole step.
            Vector3 acceleration = driving ? Vector3.zero : (peek.Velocity - predicted.Velocity) / Motor.Tick;
            inertia = KineticView.Inertia(inertia, Quaternion.Inverse(aim) * acceleration, dt, 17);
            float lean = driving ? 0 : KineticView.Lean(Vector3.Dot(shownVelocity, aim * Vector3.right)) * tuning.Roll;
            Quaternion angle = aim * Quaternion.Euler(0, 0, lean * motion);
            float eye = predicted.Crouch ? .92f : 1.58f;
            stanceHeight = Mathf.SmoothDamp(stanceHeight, eye, ref stanceVelocity, predicted.Crouch ? .046f : .083f, Mathf.Infinity, dt);
            float shownHeight = Mathf.Lerp(eye, stanceHeight, motion);
            if (driving) Chase(display + Vector3.up * 1.45f, angle * Vector3.forward, 5.5f, 2.1f, false);
            else
            {
                chaseArm = 0;
                // Every presentation ray starts at the displayed eye, the camera's own base, so the camera,
                // crosshair and pilot identification agree on every frame.
                Vector3 eyeOrigin = display + Vector3.up * eye;
                Vector3 camera = display + Vector3.up * (shownHeight + (stride * tuning.HeadBob - landingStep) * motion);
                if (Physics.Linecast(eyeOrigin, camera, out var ceiling, TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
                    camera = ceiling.point + ceiling.normal * .048f;
                view.transform.SetPositionAndRotation(camera, angle);
                Vector3 ray = aim * Vector3.forward;
                reticleWorld = eyeOrigin + ray * reticleRange;
                if (Physics.Raycast(eyeOrigin, ray, out var surface, reticleRange, TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
                    reticleWorld = surface.point;
                IdentifyPilot(eyeOrigin, ray);
            }
            float wasEquipping = equipMotion;
            equipMotion = now < switchPreviewUntil
                ? Mathf.MoveTowards(equipMotion, 1, dt * 15f)
                : Mathf.MoveTowards(equipMotion, 0, dt / temper.RaiseTime);
            if (wasEquipping > 0 && equipMotion <= 0) equipSettledAt = now;
            dryPulse = Mathf.MoveTowards(dryPulse, 0, dt * 5);
            movementPulse = Mathf.MoveTowards(movementPulse, 0, dt * 3.1f);
            float retract = 0;
            if (!driving && Physics.Raycast(view.transform.position, angle * Vector3.forward, out var wall, .94f, TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore))
                retract = Mathf.Lerp(.39f, 0, Mathf.Clamp01(wall.distance / .94f));
            float idle = Mathf.Sin(now * 1.75f) * .006f * motion;
            var presentation = WeaponHandling.Presentation(weaponName,now,equipMotion*motion,speed,
                movementPulse,landingStep,motion);
            // The grip traces a figure of eight: sideways at half the step rate, vertically at the step rate.
            var held = new WeaponHandling.View
            {
                Reload = reload, Presentation = presentation, Motion = motion, Inertia = weaponInertia,
                Settle = equipSettledAt >= 0 && equipMotion <= 0 ? WeaponHandling.SettleRotation(weaponName, now - equipSettledAt) : Vector3.zero,
                SwayX = swayX, SwayY = swayY, InertiaX = inertia.x,
                Stride = stride * temper.Bob, StrideSide = Mathf.Sin(stridePhase * .5f) * strideReach * .55f * temper.Bob,
                Landing = gunLanding, Travel = gunTravel, Lift = gunLift, Twist = gunTwist, Retract = retract, Idle = idle,
            };
            WeaponHandling.Grip(held, out var gripAt, out var gripTurn);
            grip.SetLocalPositionAndRotation(gripAt, gripTurn);
            if (weaponName != null && weaponActions.TryGetValue(weaponName, out var weaponAction))
                weaponAction.Update(reload, gunTravel, flowTier * .25f * momentumPresentation * visualEffects,
                    motion, alternateImpulse, kickShot);
            float fov = ViewComposer.Fov(tuning, baseFov, speed, movementPulse, cameraKick, motion, driving);
            view.fieldOfView = ViewComposer.FollowFov(view.fieldOfView, fov, dt);
        }

        void UpdateResult(float dt)
        {
            string current = state["winner"]["label"].Text;
            if (current != roundResult)
            {
                if (current != null)
                {
                    resumeCapture = captured;
                    Capture(false);
                    effects.InterfaceTone("result");
                    world.SurgeLandmark();
                }
                else if (resumeCapture && Application.isFocused)
                    Capture(true);
                resultContinued = false;
                roundResult = current;
                resultAge = 0;
            }
            resultAge += dt;
            sceneFade = Mathf.MoveTowards(sceneFade, 0, dt * 3.4f);
        }

        void PaintPause(float w, float h)
        {
            float x = w / 2 - 242, y = h / 2 - 238;
            Fill(new Rect(0,0,w,h),new Color(.012f,.024f,.041f,.80f));
            CornerPanel(new Rect(x, y, 484, 507), Amber);
            DrawInsignia(new Vector2(x + 37, y + 35), 17, Amber);
            hudText.Draw(new Rect(x + 62, y + 16, 388, 38), "Nastavení", heading);
            if (!practiceRoom && Preset(new Rect(x + 278, y + 20, 184, 32),
                Time.unscaledTime < inviteCopiedUntil ? "Odkaz zkopírován" : "Kopírovat odkaz", null, false))
                CopyInvite(Event.current.shift);
            hudText.Draw(new Rect(x + 24, y + 64, 436, 27), state["directorBots"].Bool
                ? "Online zápas běží dál  ·  Dynamické hlídky aktivní" : "Online zápas běží dál", caption);
            DrawFeelOptions(new Rect(x + 22,y + 94,440,34));
            bool waitingInvite = pendingInviteServer != null;
            if (Preset(new Rect(x + 22, y + 397, waitingInvite ? 207 : 440, 39), "Zpět do zápasu", null, true)) Capture(true);
            if (waitingInvite && Preset(new Rect(x + 239, y + 397, 223, 39), "Přijmout pozvánku", null, false)) EnterInvite();
            if (Preset(new Rect(x + 22, y + 442, 440, 35), "Opustit místnost", null, false)) Disconnect(null);
            hudText.Draw(new Rect(x + 24, y + 480, 430, 22), "Q úhyb  ·  " + echoActivation + " ozvěna  ·  R přebít  ·  E použít", caption);
        }

        void DrawFeelOptions(Rect box)
        {
            Option(box, "Zorné pole", "trias.fov", ref baseFov, 60, 95, false);
            box.y += 30; Option(box, "Citlivost myši", "trias.sensitivity", ref sensitivity, .5f, 5, false, 1);
            box.y += 30; ViewStyleOption(box);
            box.y += 30; Option(box, "Pohyb kamery", "trias.motion", ref cameraMotion, 0, 1, true);
            box.y += 30; Option(box, "Hlasitost", "trias.volume", ref masterVolume, 0, 1, true);
            box.y += 30; Option(box, "Atmosféra", "trias.ambience", ref ambienceVolume, 0, 1, true);
            box.y += 30; Option(box, "Hudba v menu", "trias.music", ref musicVolume, 0, 1, true);
            box.y += 30; Option(box, "Světelné efekty", "trias.visualEffects", ref visualEffects, 0, 1, true);
            box.y += 30; Option(box, "Momentum HUD", "trias.momentum", ref momentumPresentation, 0, 1, true);
            box.y += 30; Option(box, "Týmové rádio", "trias.radio", ref radioVolume, 0, 1, true);
            visor.Intensity = visualEffects;
            effects.MusicMix(musicVolume);
            AudioListener.volume = masterVolume;
            effects.AmbienceMix(ambienceVolume, false);
        }

        // Soutěžní: stable aiming camera (no strafe roll, no recoil FOV pumping, small sprint FOV, half head bob).
        // Filmový: the full first-person motion. Both keep the static crosshair.
        void ViewStyleOption(Rect row)
        {
            const string key = "trias.view";
            hudText.Draw(new Rect(row.x, row.y + 1, 145, 28), "Kamera", section);
            bool cinematic = viewStyle == ViewComposer.Style.Cinematic, next = cinematic;
            if (Preset(new Rect(row.x + 156, row.y + 3, 107, 26), "Soutěžní", null, !cinematic)) next = false;
            if (Preset(new Rect(row.x + 268, row.y + 3, 107, 26), "Filmový", null, cinematic)) next = true;
            if (world == null)
            {
                if (Event.current.type == EventType.Layout) lobbyControls.Add(key);
                var evt = Event.current;
                if (lobbyFocus == key)
                {
                    Fill(new Rect(row.x + 152, row.y + 31, 227, 1), Chalk);
                    if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow))
                    {
                        next = evt.keyCode == KeyCode.RightArrow;
                        evt.Use();
                    }
                }
            }
            if (next == cinematic) return;
            viewStyle = next ? ViewComposer.Style.Cinematic : ViewComposer.Style.Competitive;
            PlayerPrefs.SetInt(key, next ? 1 : 0);
        }

        void Option(Rect row, string name, string key, ref float value, float min, float max, bool percent, int decimals = 0)
        {
            hudText.Draw(new Rect(row.x, row.y + 1, 145, 28), name, section);
            optionTrack ??= new GUIStyle { fixedHeight = 19 };
            optionThumb ??= new GUIStyle { fixedWidth = 12, fixedHeight = 19 };
            var control = new Rect(row.x + 156, row.y + 7, 219, 19);
            float next = GUI.HorizontalSlider(control,value,min,max,optionTrack,optionThumb);
            if (world == null)
            {
                if (Event.current.type == EventType.Layout) lobbyControls.Add(key);
                if (lobbyFocus == key)
                {
                    Fill(new Rect(control.x-4,control.center.y-7,control.width+8,1),Chalk);
                    var evt = Event.current;
                    if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow))
                    {
                        next = Mathf.Clamp(value + (max-min)*.05f*(evt.keyCode == KeyCode.RightArrow ? 1 : -1),min,max);
                        evt.Use();
                    }
                }
            }
            if (Mathf.Abs(next - value) > .0001f)
            {
                value = next;
                PlayerPrefs.SetFloat(key, value);
            }
            float rail = Mathf.Lerp(control.x + 6, control.xMax - 6, Mathf.InverseLerp(min, max, value));
            Fill(new Rect(control.x, control.center.y - 1, control.width, 2), Subtle);
            Fill(new Rect(control.x, control.center.y - 1, rail - control.x, 2), Amber);
            Fill(new Rect(rail - 3, control.center.y - 5, 6, 10), control.Contains(Event.current.mousePosition) ? Chalk : Amber);
            string readout = percent ? Mathf.RoundToInt(value * 100) + "%" : value.ToString(decimals == 0 ? "F0" : "F1");
            if (key == "trias.fov") readout += "°";
            hudText.Draw(new Rect(row.x + 385, row.y + 2, 54, 26), readout, caption);
        }
    }
}
