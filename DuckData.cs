using System;
using System.IO;
using System.Collections.Generic;

namespace DuckBot
{
    public class DuckData
    {
        internal static readonly Dictionary<ulong, Session> ServerSessions = new Dictionary<ulong, Session>();
        internal static readonly DirectoryInfo SessionsDir = new DirectoryInfo("sessions"); //Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        internal static readonly FileInfo LogFile = new FileInfo("BotErrors.log");
        internal static readonly List<ulong> CSharpCommandAdders = new List<ulong> { 168285549088604160uL, 184688687391440904uL, 137237535700156416uL, 186295632229695488uL, 189088171043061760uL, 197026467446652928uL };

        internal static readonly ulong SuperUser = 168285549088604160uL;
    }
}
