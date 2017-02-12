using System;
using System.IO;
using System.Collections.Generic;

namespace DuckBot
{
    internal class DuckData
    {
        internal static readonly ulong SuperUserId = 168285549088604160uL;

        internal static readonly DirectoryInfo SessionsDir = new DirectoryInfo("sessions");
        internal static readonly FileInfo LogFile = new FileInfo("Activity.log");
        internal static readonly FileInfo WhitelistFile = new FileInfo("Whitelist.cfg");
        internal static readonly FileInfo TokenFile = new FileInfo("Token.cfg");

        internal readonly Dictionary<ulong, Session> ServerSessions = new Dictionary<ulong, Session>();
        internal readonly List<ulong> AdvancedUsers = new List<ulong> { SuperUserId, 184688687391440904uL, 137237535700156416uL, 186295632229695488uL, 189088171043061760uL, 197026467446652928uL, 100615281310695424uL };
    }
}
