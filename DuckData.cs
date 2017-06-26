using System;
using System.IO;
using System.Collections.Generic;

namespace DuckBot
{
    internal class DuckData
    {
        internal const ulong SuperUserId = 100615281310695424uL; // Xspeed

        internal static readonly DirectoryInfo SessionsDir = new DirectoryInfo("sessions");
        internal static readonly FileInfo StateLogFile = new FileInfo("State.log");
        internal static readonly FileInfo WhitelistFile = new FileInfo("Whitelist.cfg");
        internal static readonly FileInfo TokenFile = new FileInfo("Token.cfg");

        internal readonly Dictionary<ulong, Session> ServerSessions = new Dictionary<ulong, Session>();
        internal readonly List<ulong> AdvancedUsers = new List<ulong> { SuperUserId, 168285549088604160uL }; // Xspeed, EchoNex
    }
}
