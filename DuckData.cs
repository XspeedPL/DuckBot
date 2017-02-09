using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuckBot
{
    public class DuckData
    {
        internal static Dictionary<ulong, Dictionary<string, Command>> commandDB = new Dictionary<ulong, Dictionary<string, Command>>();
        internal static Dictionary<ulong, bool> showChanges = new Dictionary<ulong, bool>();
        internal static List<ulong> csharpCommandAdders = new List<ulong> { 168285549088604160, 184688687391440904, 137237535700156416, 186295632229695488, 189088171043061760, 197026467446652928 };
        internal static string saveFile = $"/home/{Environment.UserName}/DuckBotSave.json";
        internal static string changelogsFile = $"/home/{Environment.UserName}/DuckBotChangelogs.json";
        internal static Dictionary<ulong, List<string>> messagesToDeliver = new Dictionary<ulong, List<string>>();

    }
}
