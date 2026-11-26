using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        bool legacyFirePreview
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return labMode && labBaseline;
#else
                return false;
#endif
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool labJoining, labMode, labBaseline, labDelay;
        bool labEcho = true, labMomentum = true;
        float inputFeedbackMs, labFrameTime;

        public void JoinFeelLab()
        {
            Join("ws://127.0.0.1:3035", "FEEL LAB", "Pilot", "relay", "tdm", "vector", false, false);
            labJoining = true;
        }

        void ResetLab()
        {
            labJoining = labMode = labBaseline = labDelay = false;
            labEcho = labMomentum = true;
            inputFeedbackMs = labFrameTime = 0;
        }

        void UpdateLabControls()
        {
            if (!labMode) return;
            labFrameTime = Mathf.Lerp(labFrameTime, Time.unscaledDeltaTime, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 6));
            if (!captured) return;
            if (Input.GetKeyDown(KeyCode.F5)) labBaseline = !labBaseline;
            if (Input.GetKeyDown(KeyCode.F6))
            {
                labDelay = !labDelay;
                socket.Send(labDelay ? "{\"type\":\"lab-delay\",\"active\":true}" : "{\"type\":\"lab-delay\",\"active\":false}");
            }
            if (Input.GetKeyDown(KeyCode.F7)) socket.Send("{\"type\":\"lab-reset\"}");
            if (state["interactions"]["intercept"].Bool && Input.GetKeyDown(KeyCode.F10))
                socket.Send("{\"type\":\"lab-grenade\"}");
            bool toggleEcho = Input.GetKeyDown(KeyCode.F8), toggleMomentum = Input.GetKeyDown(KeyCode.F9);
            if (toggleEcho) labEcho = !labEcho;
            if (toggleMomentum) labMomentum = !labMomentum;
            if (toggleEcho || toggleMomentum)
                socket.Send($"{{\"type\":\"lab-signature\",\"echo\":{(labEcho ? "true" : "false")},\"momentum\":{(labMomentum ? "true" : "false")}}}");
        }

        void PaintLab(float width)
        {
            bool intercept = state["interactions"]["intercept"].Bool;
            var panel = new Rect((width - 486) * .5f, 90, 486, intercept ? 135 : 113);
            CornerPanel(panel, Shield);
            float milliseconds = labFrameTime * 1000;
            float fps = labFrameTime > 0 ? 1 / labFrameTime : 0;
            hudText.Draw(new Rect(panel.x + 11, 94, 467, 23), "GAME FEEL LAB   ·   " + (labBaseline ? "SERVEROVÁ ODEZVA" : "LOKÁLNÍ PREDIKCE"), section);
            hudText.Draw(new Rect(panel.x + 11, 119, 467, 22), $"{fps:F0} FPS · {milliseconds:F1} ms · RTT {latency} ms · korekce {predictedCorrection * 100:F1} cm", caption);
            hudText.Draw(new Rect(panel.x + 11, 139, 467, 22), $"Vstup → efekt {inputFeedbackMs:F2} ms · potvrzení výstřelu {confirmationMs:F0} ms", caption);
            hudText.Draw(new Rect(panel.x + 11, 159, 467, 21), "F5 srovnání · F6 přidat 160 ms · F7 doplnit terče a munici", caption);
            hudText.Draw(new Rect(panel.x + 11, 180, 467, 21),
                $"F8 Ozvěna {(labEcho ? "zapnuta" : "vypnuta")} · F9 Momentum {(labMomentum ? "zapnuto" : "vypnuto")}", caption);
            if (intercept)
                hudText.Draw(new Rect(panel.x + 11, 202, 467, 21), "F10 tréninkový granát · namiř do volného prostoru", caption);
        }
#endif
    }
}
