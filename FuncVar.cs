using System;
using System.Collections.Generic;

namespace DuckBot
{
    public delegate string CmdHandler(string[] args, CmdContext context);

    public static class FuncVar
    {
        private static Dictionary<string, CmdHandler> vars = CreateDefault();

        private static Dictionary<string, CmdHandler> CreateDefault()
        {
            Dictionary<string, CmdHandler> dict = new Dictionary<string, CmdHandler>();

            dict.Add("user", (args, msg) => { return msg.Sender.Username; });

            dict.Add("nickOrUser", (args, msg) =>
            {
                return string.IsNullOrWhiteSpace(msg.Sender.Nickname) ? msg.Sender.Username : msg.Sender.Nickname;
            });

            dict.Add("input", (args, msg) =>
            {
                if (args.Length >= 1)
                    try
                    {
                        int ix = int.Parse(args[0]);
                        return SoftCmd.Escape(msg.Args.Split(' ')[ix]);
                    }
                    catch (FormatException) { return "ERROR"; }
                return SoftCmd.Escape(msg.Args);
            });

            dict.Add("mention", (args, msg) =>
            {
                if (args.Length >= 1)
                {
                    Discord.IUser u = Program.FindUser(msg.Server, args[0]);
                    return u == null ? "ERROR" : u.Mention;
                }
                else return msg.Sender.Mention;
            });

            dict.Add("rand", (args, msg) =>
            {
                try
                {
                    int i1 = int.Parse(args[0]);
                    if (args.Length >= 2)
                        return (Program.Rand.Next(int.Parse(args[1]) - i1) + i1).ToString();
                    else return Program.Rand.Next(i1).ToString();
                }
                catch (FormatException) { return "ERROR"; }
            });

            dict.Add("command", (args, msg) =>
            {
                if (args.Length >= 1)
                {
                    Session s = msg.Session;
                    SoftCmd c;
                    lock (s) c = s.Cmds.ContainsKey(args[0]) ? s.Cmds[args[0]] : null;
                    if (c != null) return c.Run(new CmdContext(msg, args.Length >= 2 ? args[1] : ""));
                }
                return "ERROR";
            });

            dict.Add("if", (args, msg) =>
            {
                if (args.Length >= 3)
                    return args[0].Length == args[1].Length ? args[2] : args[3];
                else return "ERROR";
            });

            dict.Add("length", (args, msg) =>
            {
                return args.Length >= 1 ? args[0].Length.ToString() : "ERROR";
            });

            dict.Add("substr", (args, msg) =>
            {
                try
                {
                    string s = args[0];
                    int i1 = int.Parse(args[1]);
                    if (args.Length >= 3)
                    {
                        int i2 = int.Parse(args[2]);
                        return s.Substring(i1 >= 0 ? i1 : s.Length + i1, i2);
                    }
                    else return s.Substring(i1 >= 0 ? i1 : s.Length + i1);
                }
                catch (FormatException) { return "ERROR"; }
            });

            dict.Add("date", (args, msg) =>
            {
                if (args.Length >= 1) return DateTime.UtcNow.ToString(args[0]);
                else return DateTime.UtcNow.ToShortDateString();
            });

            dict.Add("time", (args, msg) =>
            {
                if (args.Length >= 1 && args[0] == "long") return DateTime.UtcNow.ToLongTimeString();
                else return DateTime.UtcNow.ToShortTimeString();
            });

            dict.Add("get", (args, msg) =>
            {
                Session s = msg.Session;
                lock (s)
                    return args.Length >= 1 && s.Vars.ContainsKey(args[0]) ? s.Vars[args[0]] : "ERROR";
            });

            dict.Add("set", (args, msg) =>
            {
                if (args.Length >= 2)
                {
                    Session s = msg.Session;
                    lock (s)
                        if (s.Vars.ContainsKey(args[0])) s.Vars[args[0]] = args[1];
                        else s.Vars.Add(args[0], args[1]);
                    s.SetPending();
                    return "";
                }
                else return "ERROR";
            });

            dict.Add("eval", (args, msg) =>
            {
                string func = SoftCmd.Unescape(string.Join(",", args));
                return SoftCmd.CmdEngine(func, msg);
            });

            dict.Add("find", (args, msg) =>
            {
                return args.Length >= 2 ? args[0].IndexOf(args[1]).ToString() : "ERROR";
            });

            return dict;
        }

        public static bool Exists(string name)
        {
            return vars.ContainsKey(name);
        }

        public static string Run(string name, string[] args, CmdContext ctx)
        {
            return vars[name](args, ctx);
        }

        public static bool Register(string name, CmdHandler action)
        {
            if (Exists(name)) return false;
            else { vars.Add(name, action); return true; }
        }
    }
}
