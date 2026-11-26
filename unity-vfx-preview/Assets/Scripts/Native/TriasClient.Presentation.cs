using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        float deploymentUntil, deploymentLength, deathReplayLength;
        float pilotMaxHealth = 150, respawnDelay;
        bool firstDeployment, resumeCapture, resultContinued;
        string killerWeapon;

        float HealthFraction => Mathf.Clamp01(own[6].Float / pilotMaxHealth);
        float ReplayRemaining(float now) => Mathf.Clamp01((replayUntil - now) / Mathf.Max(.01f, deathReplayLength));

        void ResetDeployment()
        {
            deploymentUntil = deathReplayLength = 0;
            firstDeployment = resumeCapture = resultContinued = false;
            killerWeapon = roundResult = null;
        }

        void BeginDeployment(float now,bool initial)
        {
            firstDeployment = initial;
            deploymentLength = initial ? 1.75f : 1.08f;
            deploymentUntil = now + deploymentLength;
            view.fieldOfView = baseFov;
            sceneFade = (initial ? .36f : .18f) * visualEffects * cameraMotion;
            effects.Reentry(display,teamTint,true,mode == "ffa" ? -1 : own[3].Int,visualEffects);
            if (initial && Application.isFocused && state["winner"]["label"].Text == null)
                Capture(true);
        }

        bool HandlePresentationKeys(float now)
        {
            if (roundResult != null)
            {
                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    Disconnect(null);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) resultContinued = true;
            }
            else if (now < deploymentUntil && Input.GetKeyDown(KeyCode.Space))
                deploymentUntil = 0;
            return false;
        }

        void PaintDeployment(float w,float h)
        {
            float left = deploymentUntil - Time.unscaledTime;
            if (left <= 0 || !captured || scoreboard) return;
            float age = 1 - Mathf.Clamp01(left / deploymentLength);
            float fade = Mathf.Clamp01(left*6);
            float x = w*.5f - 196, y = 93 + (1 - Mathf.Clamp01(age*6))*12*cameraMotion;
            var edge = teamTint;
            edge.a = fade;
            Fill(new Rect(x,y,392,firstDeployment ? 79 : 54),new Color(Ink.r,Ink.g,Ink.b,Ink.a*fade));
            Fill(new Rect(x,y,48,2),edge);
            if (mode == "ffa") DrawInsignia(new Vector2(x+29,y+27),12,edge);
            else DrawFaction(new Vector2(x+29,y+27),13,own[3].Int,edge);
            var prior = GUI.color;
            GUI.color = new Color(1,1,1,fade);
            hudText.Draw(new Rect(x+53,y+6,333,27),firstDeployment ? world.Spec.label : "Průzor aktivní",section);
            string operation = Catalog.Find(catalog.modes,mode)?.label ?? mode;
            string faction = mode == "ffa" ? "SOLO" : catalog.teams[own[3].Int].name;
            hudText.Draw(new Rect(x+53,y+32,330,21),firstDeployment ? faction + "  /  " + operation : "Výstroj připravena",caption);
            if (firstDeployment)
                hudText.Draw(new Rect(x+53,y+53,332,22),mode switch
                {
                    "tdm" => "Bod za vyřazení soupeře", "dom" => "Obsazujte a držte stanice",
                    "ctf" => "Přineste vlajku na vlastní základnu", "siege" => "Propojte relé a odkryjte jádro",
                    "horde" => "Chraňte jádro proti osmi vlnám", _ => "Rozhodují vyřazení"
                },caption);
            GUI.color = prior;
        }

        void PaintDeath(float cx,float h)
        {
            float now = Time.unscaledTime;
            bool replay = now < replayUntil;
            bool observing = !replay && spectator != null && pilots.TryGetValue(spectator,out var pilot) && pilot.Row[6].Int > 0;
            string label = replay ? "ZÁZNAM ZÁSAHU" : observing ? "SLEDUJEŠ SPOLUHRÁČE" : "VYŘAZENÍ";
            string who = replay ? killer : observing ? pilots[spectator].Row[1].Text : "Čekáš na nasazení";
            string tool = replay && killerWeapon != null ? killerWeapon + "  ·  " : "";
            string controls = replay ? tool + "Mezerník přeskočí záznam" : observing ? "E přepne sledovaného pilota" : "";
            float y = h-159;
            var box = new Rect(cx-204,y,408,112);
            CornerPanel(box,replay ? Fault : teamTint);
            if (!replay && mode != "ffa") DrawFaction(new Vector2(box.x+22,y+21),7,own[3].Int,teamTint);
            hudText.Draw(new Rect(box.x+41,y+8,236,21),label,section);
            hudText.Draw(new Rect(box.x+306,y+7,83,23),own[29].Float > 0 ? Mathf.CeilToInt(own[29].Float) + " s" : "ČEKÁM",caption);
            hudText.Draw(new Rect(box.x+16,y+34,372,35),who,heading);
            hudText.Draw(new Rect(box.x+17,y+75,365,23),controls,caption);
            float phase = replay ? ReplayRemaining(now) : Mathf.Clamp01(own[29].Float / respawnDelay);
            Segments(new Rect(box.x+17,y+100,371,3),18,phase,replay ? Fault : teamTint);
        }

        void PaintResult(float cx,float h)
        {
            var winner = state["winner"];
            int winningTeam = mode == "ffa" || mode == "horde" ? -1 :
                System.Array.FindIndex(catalog.teams,value=>value.name==winner["label"].Text);
            var tint = winningTeam >= 0 ? Catalog.Tint(catalog.teams[winningTeam].color) :
                mode == "horde" && state["horde"]["core"]["hp"].Int == 0 ? Fault : Amber;
            string countdown = "Další kolo za " + state["restart"].Int + " s";
            if (resultContinued)
            {
                CornerPanel(new Rect(cx-142,h-115,284,54),tint);
                hudText.Draw(new Rect(cx-117,h-108,246,27),countdown,center);
                hudText.Draw(new Rect(cx-115,h-84,230,21),"Backspace · do lobby",caption);
                return;
            }
            float age = Mathf.Clamp01(resultAge/.32f);
            float y = h*.5f - 169 + (1-age)*(1-age)*22*cameraMotion;
            var plate = new Rect(cx-304,y,608,338);
            CornerPanel(plate,tint);
            if (winningTeam >= 0 && mode != "ffa")
                DrawFaction(new Vector2(plate.x+44,y+51),19,winningTeam,tint);
            else DrawInsignia(new Vector2(plate.x+44,y+51),20,tint);
            hudText.Draw(new Rect(plate.x+79,y+12,488,23),"VÝSLEDEK  /  " + world.Spec.label,section);
            string outcome = mode == "horde" ? winner["label"].Text : winner["label"].Text == "Remíza" ? "Remíza" : "Vítězí " + winner["label"].Text;
            hudText.Draw(new Rect(plate.x+76,y+39,495,40),outcome,heading);
            if (mode == "horde" || mode == "ffa")
            {
                string score = mode == "ffa" ? "Nejvyšší skóre  " + LeadingKills() :
                    "Vlna  " + state["horde"]["wave"].Int + " / 8   ·   Jádro  " + state["horde"]["core"]["hp"].Int + " HP";
                hudText.Draw(new Rect(plate.x+20,y+94,566,30),score,section);
            }
            else
                for (int i=0;i<catalog.teams.Length;i++)
                {
                    float at = plate.x+21+i*193;
                    int score = mode == "siege" ? state["siege"]["cores"][i]["hp"].Int : state["scores"][i].Int;
                    var teamColor = Catalog.Tint(catalog.teams[i].color);
                    CornerPanel(new Rect(at,y+92,180,51),teamColor);
                    DrawFaction(new Vector2(at+20,y+117),9,i,teamColor);
                    hudText.Draw(new Rect(at+39,y+101,132,27),catalog.teams[i].name + "  " + score + (mode=="siege" ? " HP" : ""),section);
                }
            hudText.Draw(new Rect(plate.x+21,y+157,563,27),"VYŘAZENÍ  " + own[11].Int + "     ·     PÁDY  " + own[12].Int,section);
            if (flow != null && state["signature"]["momentum"].Bool)
                hudText.Draw(new Rect(plate.x+21,y+192,569,24),
                    $"STYL   ·   Nejlepší řetězec {flow[5].Int}   ·   Násobič ×{flow[7].Float:0.#}   ·   Vyřazení ozvěnou {flow[8].Int}",caption);
            hudText.Draw(new Rect(plate.x+21,y+230,565,24),countdown,caption);
            Segments(new Rect(plate.x+21,y+254,567,3),19,state["restart"].Float/9,tint);
            if (Preset(new Rect(plate.x+21,y+270,272,42),"Zůstat na další kolo",null,true)) resultContinued = true;
            if (Preset(new Rect(plate.x+309,y+270,279,42),"Do lobby",null,false)) Disconnect(null);
        }

        int LeadingKills()
        {
            int best = 0;
            foreach (var pilot in state["players"].Items) best = Mathf.Max(best,pilot[11].Int);
            return best;
        }
    }
}
