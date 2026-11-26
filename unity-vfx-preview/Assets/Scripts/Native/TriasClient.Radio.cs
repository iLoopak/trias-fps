using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        float radioUntil;
        string radioCaption;

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
            var panel = new Rect(26,h-147,258,37);
            CornerPanel(panel,teamTint);
            GUI.Label(new Rect(panel.x+9,panel.y+6,panel.width-16,25),radioCaption,caption);
        }
    }
}
