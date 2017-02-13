using System;

namespace DuckBot
{
    public struct HardCmd
    {
        public delegate void CmdAct(string[] args, CmdParams msg, Session s);

        public readonly CmdAct func;
        public readonly byte argsMin, argsMax;
        public readonly bool admin;
        public readonly string help;

        public HardCmd(byte minArgs, byte maxArgs, CmdAct action, string helpText, bool reqAdmin = false)
        {
            argsMin = minArgs; argsMax = maxArgs; func = action; help = helpText; admin = reqAdmin;
        }
    }
}
