using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        float radioUntil;
        string radioCaption;
        bool previewMale;

        void PreviewBotRadio()
        {
            if (effects.BotRadio("cover", previewMale, radioVolume, true)) previewMale = !previewMale;
            else effects.InterfaceTone("invalid");
        }

        void ReadBotRadio(WireValue message,float now)
        {
            if (own == null || own[6].Int <= 0 || !captured || now < radioUntil || mode == "ffa") return;
            string line = message["line"].Text;
            string captionText = line switch
            {
                "contact" => "Kontakt vpředu.",
                "cover" => "Kryju náš sektor.",
                "flank" => "Obcházím je z boku.",
                "retreat" => "Stahuju se do krytu.",
                "flag" => "Mám vlajku. Vracím se.",
                "core" => "Držím obranu u jádra.",
                _ => null
            };
            if (captionText == null) return;
            radioCaption = message["name"].Text + "  ·  " + captionText;
            radioUntil = now + 2.45f;
            effects.BotRadio(line,message["frame"].Text == "male",radioVolume);
        }

        void PaintBotRadio(float h)
        {
            if (string.IsNullOrEmpty(radioCaption) || Time.unscaledTime >= radioUntil) return;
            var panel = new Rect(22,h-122,294,34);
            HudStrip(panel,teamTint);
            hudText.Draw(new Rect(panel.x+9,panel.y+2,panel.width-18,30),radioCaption,lobbyWrap);
        }
    }
}
