using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Trias.Native
{
    public sealed partial class TriasClient
    {
        static readonly Regex InviteRoom = new Regex(@"\A[A-Za-z0-9-]{3,24}\z", RegexOptions.CultureInvariant);
        Uri pendingInviteServer;
        string pendingInviteRoom;
        float inviteCopiedUntil;

        void InitInvites()
        {
            Application.deepLinkActivated += ReceiveInvite;
            if (!string.IsNullOrWhiteSpace(Application.absoluteURL)) ReceiveInvite(Application.absoluteURL);
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-trias-invite") ReceiveInvite(args[i + 1]);
        }

        internal static bool TryInvite(string text, out Uri origin, out string code)
        {
            origin = null;
            code = null;
            if (text == null || text.Length > 512 || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) ||
                uri.UserInfo.Length != 0 || uri.Fragment.Length != 0) return false;
            string room;
            Uri server;
            if (uri.Scheme == "trias" && uri.Host == "join" && uri.Query.StartsWith("?server=", StringComparison.Ordinal))
            {
                room = uri.AbsolutePath.Trim('/');
                if (!Uri.TryCreate(Uri.UnescapeDataString(uri.Query.Substring(8)), UriKind.Absolute, out server) ||
                    server.PathAndQuery != "/" || server.Fragment.Length != 0) return false;
            }
            else if (uri.AbsolutePath.StartsWith("/join/", StringComparison.Ordinal) && uri.Query.Length == 0)
            {
                room = uri.AbsolutePath.Substring(6);
                server = uri;
            }
            else return false;
            if (!InviteRoom.IsMatch(room) || server.UserInfo.Length != 0 || string.IsNullOrEmpty(server.Host) ||
                server.Scheme is not ("http" or "https" or "ws" or "wss")) return false;
            origin = new Uri(server.GetLeftPart(UriPartial.Authority));
            code = room.ToUpperInvariant();
            return true;
        }

        void ReceiveInvite(string link)
        {
            if (!TryInvite(link, out var origin, out var code))
            {
                if (world == null) status = "Tahle pozvánka nemá platný odkaz na místnost.";
                return;
            }
            pendingInviteServer = origin;
            pendingInviteRoom = code;
            if (world != null || reconnectUntil > 0) AddNotice("Pozvánka čeká v nabídce Esc");
            else EnterInvite();
        }

        void EnterInvite()
        {
            if (pendingInviteServer == null) return;
            string address = pendingInviteServer.GetLeftPart(UriPartial.Authority);
            string code = pendingInviteRoom;
            pendingInviteServer = null;
            pendingInviteRoom = null;
            serverAddress = address;
            roomCode = code;
            lobbyPage = LobbyPage.Join;
            Join(address, code, nickname, selectedMap, selectedMode, selectedPilot, false, male: selectedMale, intent: "join");
        }

        void CopyInvite(bool direct)
        {
            if (sessionEndpoint == null || !InviteRoom.IsMatch(room ?? "")) return;
            var http = new UriBuilder(sessionEndpoint)
            {
                Scheme = sessionEndpoint.Scheme == "wss" ? "https" : "http",
                Path = "/join/" + room, Query = "", Fragment = ""
            };
            GUIUtility.systemCopyBuffer = direct
                ? "trias://join/" + room + "?server=" + Uri.EscapeDataString(http.Uri.GetLeftPart(UriPartial.Authority))
                : http.Uri.AbsoluteUri;
            inviteCopiedUntil = Time.unscaledTime + 2.8f;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true)]
        static extern int RegSetKeyValueW(IntPtr root, string subKey, string name, int type, string data, int bytes);

        void RegisterWindowsInvites()
        {
            try
            {
                string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                const string protocolKey = @"Software\Classes\trias";
                var currentUser = new IntPtr(unchecked((int)0x80000001));
                bool Put(string key, string name, string text)
                    => RegSetKeyValueW(currentUser, key, name, 1, text, (text.Length + 1) * 2) == 0;
                bool installed = Put(protocolKey + @"\shell\open\command", null, "\"" + exe + "\" -trias-invite \"%1\"")
                    && Put(protocolKey, null, "URL:TRIAS") && Put(protocolKey, "URL Protocol", "");
                status = installed ? "Klikací pozvánky jsou povolené."
                    : "Přímé otevření se nepodařilo nastavit. Použij Z pozvánky.";
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is System.IO.IOException ||
                e is System.Security.SecurityException || e is InvalidOperationException)
            {
                status = "Přímé otevření se nepodařilo nastavit. Použij Z pozvánky.";
            }
        }
#endif
    }
}
