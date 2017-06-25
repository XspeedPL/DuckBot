using System;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;

namespace DuckBot
{
    public static class FuncVar
    {
        public delegate string FuncVarHandler(string[] args, CmdContext context);

        private static Dictionary<string, FuncVarHandler> vars = new Dictionary<string, FuncVarHandler>
        {
            { "user", (args, msg) => msg.Sender.Username },
            { "nickOrUser", (args, msg) => string.IsNullOrWhiteSpace(msg.Sender.Nickname) ? msg.Sender.Username : msg.Sender.Nickname },
            { "input", (args, msg) =>
                {
                    if (args.Length >= 1)
                    {
                        int ix = int.Parse(args[0]);
                        return SoftCmd.Escape(msg.Args.Split(' ')[ix]);
                    }
                    else return SoftCmd.Escape(msg.Args);
                }
            },
            { "mention", (args, msg) =>
                {
                    if (args.Length >= 1)
                    {
                        Discord.IUser u = msg.Server.FindUser(args[0]);
                        return u == null ? Utils.ErrorCode("UnknownUser") : u.Mention;
                    }
                    else return msg.Sender.Mention;
                }
            },
            { "rand", (args, msg) =>
                {
                    int i1 = int.Parse(args[0]);
                    if (args.Length >= 2)
                        return (Program.Rand.Next(int.Parse(args[1]) - i1) + i1).ToString();
                    else return Program.Rand.Next(i1).ToString();
                }
            },
            { "command", (args, msg) =>
                {
                    return msg.Session.Cmds[args[0]].Run(new CmdContext(msg, args.Length >= 2 ? args[1] : ""));
                }
            },
            { "if", (args, msg) => args[0].Length == args[1].Length ? args[2] : args[3] },
            { "length", (args, msg) => args[0].Length.ToString() },
            { "substr", (args, msg) =>
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
            },
            { "date", (args, msg) => args.Length >= 1 ? DateTime.UtcNow.ToString(args[0]) : DateTime.UtcNow.ToShortDateString() },
            { "time", (args, msg) => args.Length >= 1 && args[0] == "long" ? DateTime.UtcNow.ToLongTimeString() : DateTime.UtcNow.ToShortTimeString() },
            { "get", (args, msg) =>
                {
                    Session s = msg.Session;
                    lock (s) return args.Length >= 1 && s.Vars.ContainsKey(args[0]) ? s.Vars[args[0]] : Utils.ErrorCode("UnknownVar");
                }
            },
            { "set", (args, msg) =>
                {
                    Session s = msg.Session;
                    lock (s)
                        if (s.Vars.ContainsKey(args[0])) s.Vars[args[0]] = args[1];
                        else s.Vars.Add(args[0], args[1]);
                    s.SetPending();
                    msg.Processed = true;
                    return "";
                }
            },
            { "eval", (args, msg) => SoftCmd.ScriptEngine(SoftCmd.Unescape(string.Join(",", args)), msg) },
            { "find", (args, msg) => args[0].IndexOf(args[1]).ToString() },
            { "replace", (args, msg) => args[0].Replace(args[1], args.Length >= 3 ? args[2] : "") },
            { "calc", (args, msg) =>
                {
                    using (System.Data.DataTable dt = new System.Data.DataTable())
                        return dt.Compute(args[0], "").ToString();
                }
            },
            { "download", (args, msg) =>
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.AcceptEncoding.Add(System.Net.Http.Headers.StringWithQualityHeaderValue.Parse("utf-8"));
                        client.Timeout = TimeSpan.FromSeconds(5);
                        using (Stream s = client.GetStreamAsync(args[0]).GetAwaiter().GetResult())
                        {
                            byte[] buf = new byte[4096];
                            int read = s.Read(buf, 0, buf.Length);
                            return System.Text.Encoding.UTF8.GetString(buf, 0, read);
                        }
                    }
                }
            },
            { "img", (args, msg) =>
                {
                    string url = args[0];
                    int ix = url.LastIndexOf('.');
                    string ext = ix != -1 && url.Length - ix <= 5 ? url.Substring(ix + 1) : "png";
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("image/*"));
                        using (Stream s = client.GetStreamAsync(url).GetAwaiter().GetResult())
                            msg.Channel.SendFileAsync(s, "image." + ext).GetAwaiter().GetResult();
                        msg.Processed = true;
                        return "";
                    }
                }
            }
        };

        public static bool Exists(string name) => vars.ContainsKey(name);

        public static string Run(string name, string[] args, CmdContext context)
        {
            if (!Exists(name)) return Utils.ErrorCode("UnknownFunc");
            try { return vars[name](args, context); }
            catch (IndexOutOfRangeException) { return Utils.ErrorCode("ArgCount"); }
            catch (Exception ex) { return ex.ErrorCode(); }
        }

        public static bool Register(string name, FuncVarHandler action)
        {
            if (Exists(name)) return false;
            else { vars.Add(name, action); return true; }
        }
    }
}
