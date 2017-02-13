using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DuckBot.Resources;
using Discord;
using NLua;

namespace DuckBot
{
    public class Command : IBinary
    {
        private static readonly Regex CmdPattern = new Regex(@"(?<!\^){([^{}:,|^\\ ]+?)(:(?>{(?<n>)|}(?<-n>)|[^{}]+)*(?(n)(?!)))?}");

        public delegate string CmdAct(string[] args, CmdParams msg);
        private static readonly Dictionary<string, CmdAct> FuncVars = new Dictionary<string, CmdAct>();

        private static char[] SpecialChars = { '{', ',', '|' };

        static Command()
        {
            FuncVars.Add("user", (args, msg) => { return msg.sender.Name; });
            FuncVars.Add("nickOrUser", (args, msg) =>
            {
                return string.IsNullOrWhiteSpace(msg.sender.Nickname) ? msg.sender.Name : msg.sender.Nickname;
            });
            FuncVars.Add("input", (args, msg) =>
            {
                if (args.Length >= 1)
                    try
                    {
                        int ix = int.Parse(args[0]);
                        return msg.args.Split(' ')[ix];
                    }
                    catch { return "ERROR"; }
                return msg.args;
            });
            FuncVars.Add("inputOrUser", (args, msg) =>
            {
                return string.IsNullOrWhiteSpace(msg.args) ? msg.sender.Name : msg.args;
            });
            FuncVars.Add("mention", (args, msg) =>
            {
                if (args.Length >= 1)
                {
                    User u = Program.FindUser(msg.server, args[0]);
                    return u == null ? "ERROR" : u.Mention;
                }
                else return msg.sender.Mention;
            });
            FuncVars.Add("rand", (args, msg) =>
            {
                try
                {
                    int i1 = int.Parse(args[0]);
                    if (args.Length >= 2)
                        return (Program.Rand.Next(int.Parse(args[1]) - i1) + i1).ToString();
                    else return Program.Rand.Next(i1).ToString();
                }
                catch { return "ERROR"; }
            });
            FuncVars.Add("command", (args, msg) =>
            {
                if (args.Length >= 1)
                {
                    Session s = Program.Inst.CreateSession(msg.server);
                    Command c;
                    lock (s) c = s.Cmds.ContainsKey(args[0]) ? s.Cmds[args[0]] : null;
                    if (c != null) return c.Run(new CmdParams(msg, args.Length >= 2 ? args[1] : ""));
                }
                return "ERROR";
            });
            FuncVars.Add("if", (args, msg) =>
            {
                if (args.Length >= 3)
                    return args[0].Length == args[1].Length ? args[2] : args[3];
                else return "ERROR";
            });
            FuncVars.Add("length", (args, msg) =>
            {
                return args.Length >= 1 ? args[0].Length.ToString() : "ERROR";
            });
            FuncVars.Add("substr", (args, msg) =>
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
                catch { return "ERROR"; }
            });
            FuncVars.Add("date", (args, msg) =>
            {
                if (args.Length >= 1) return DateTime.UtcNow.ToString(args[0]);
                else return DateTime.UtcNow.ToShortDateString();
            });
            FuncVars.Add("time", (args, msg) =>
            {
                if (args.Length >= 1 && args[0] == "long") return DateTime.UtcNow.ToLongTimeString();
                else return DateTime.UtcNow.ToShortTimeString();
            });
            FuncVars.Add("get", (args, msg) =>
            {
                Session s = Program.Inst.CreateSession(msg.server);
                lock (s)
                    return args.Length >= 1 && s.Vars.ContainsKey(args[0]) ? s.Vars[args[0]] : "ERROR";
            });
            FuncVars.Add("set", (args, msg) =>
            {
                if (args.Length >= 2)
                {
                    Session s = Program.Inst.CreateSession(msg.server);
                    lock (s)
                        if (s.Vars.ContainsKey(args[0])) s.Vars[args[0]] = args[1];
                        else s.Vars.Add(args[0], args[1]);
                    s.SetPending();
                    return "";
                }
                else return "ERROR";
            });
            FuncVars.Add("eval", (args, msg) =>
            {
                string arg = string.Join(",", args);
                return CmdEngine(arg.Replace("^{", "{"), msg);
            });
            FuncVars.Add("find", (args, msg) =>
            {
                return args.Length >= 2 ? args[0].IndexOf(args[1]).ToString() : "ERROR";
            });
        }

        public enum CmdType
        {
            Text = 0, CSharp, Lua, Switch
        }

        public string Content { get; private set; }
        public CmdType Type { get; private set; }
        public DateTime CreationDate { get; private set; }
        public string Creator { get; private set; }

        internal Command() { }

        public string AsCodeBlock()
        {
            string pre;
            if (Type == CmdType.CSharp) pre = "cs";
            else if (Type == CmdType.Lua) pre = "lua";
            else pre = "";
            return "```" + pre + "\n" + Content + "\n```";
        }

        public void Load(BinaryReader br)
        {
            lock (this)
            {
                Creator = br.ReadString();
                CreationDate = DateTime.FromBinary(br.ReadInt64());
                Type = (CmdType)br.ReadByte();
                Content = br.ReadString();
            }
        }

        public void Save(BinaryWriter bw)
        {
            lock (this)
            {
                bw.Write(Creator);
                bw.Write(CreationDate.Ticks);
                bw.Write((byte)Type);
                bw.Write(Content);
            }
        }

        public Command(string type, string content, string creator)
        {
            Creator = creator;
            CreationDate = DateTime.UtcNow;
            CmdType ctype;
            if (!Enum.TryParse(type, true, out ctype)) ctype = CmdType.Text;
            Type = ctype;
            Content = content;
        }

        private static string[] EscSplit(string args, char delim)
        {
            List<string> ret = new List<string>();
            int old, prev, ix;
            if (args[0] == delim)
            {
                ret.Add("");
                old = 1;
            }
            else old = 0;
            prev = old;
            while ((ix = args.IndexOf(delim, prev)) != -1)
            {
                if (args[ix - 1] != '^')
                {
                    ret.Add(args.Substring(old, ix - old));
                    old = ix + 1;
                }
                prev = ix + 1;
            }
            ret.Add(args.Substring(old));
            return ret.ToArray();
        }

        public static string CmdEngine(string content, CmdParams msg)
        {
            string ret = CmdEngine(content, msg, 0);
            foreach (char c in SpecialChars) ret = ret.Replace("^" + c, c.ToString());
            return ret;
        }

        private static string CmdEngine(string content, CmdParams msg, int depth)
        {
            return depth > 10 || !content.Contains("{") ? content : CmdPattern.Replace(content, (match) =>
            {
                string cmd = match.Groups[1].Value;
                if (FuncVars.ContainsKey(cmd))
                {
                    string arg = match.Groups[2].Success ? match.Groups[2].Value.Substring(1) : null;
                    if (arg != null) arg = CmdEngine(arg, msg, depth + 1);
                    return FuncVars[cmd](arg == null ? new string[0] : EscSplit(arg, ','), msg);
                }
                else return match.Value;
            });
        }

        public string Run(CmdParams msg)
        {
            string content;
            lock (this) content = Content;
            if (Type == CmdType.Text) return CmdEngine(content, msg);
            else if (Type == CmdType.Switch)
            {
                string[] cases = EscSplit(content, '|');
                cases[0] = CmdEngine(cases[0].Trim(), msg);
                for (int i = 1; i < cases.Length; ++i)
                {
                    cases[i] = cases[i].Trim();
                    if (cases[i].StartsWith("default"))
                        return CmdEngine(cases[i].Substring(cases[i].IndexOf(' ') + 1), msg);
                    else if (cases[i].StartsWith("case"))
                    {
                        string match = cases[i].Substring(cases[i].IndexOf('"') + 1);
                        int ix = match.IndexOf('"');
                        string data = match.Substring(ix + 1).Trim();
                        if (cases[0] == CmdEngine(match.Remove(ix), msg))
                            return CmdEngine(data, msg);
                    }
                }
                return "Switch " + Strings.ret_empty;
            }
            else if (Type == CmdType.Lua)
            {
                using (Lua lua = new Lua())
                {
                    PrintProxy proxy = new PrintProxy();
                    lua.RegisterFunction("print", proxy, proxy.GetType().GetMethod("Print", new Type[] { typeof(object[]) }));
                    string code;
                    using (StreamReader sr = new StreamReader(GetType().Assembly.GetManifestResourceStream("DuckBot.Resources.Sandbox.lua")))
                        code = sr.ReadToEnd();
                    try
                    {
                        const string template = "args = {...};rawText,sender,server,channel=args[1],args[2],args[3],args[4]\n";
                        string source = template + content;
                        using (LuaFunction func = (LuaFunction)lua.DoString(code, "sandbox")[0])
                        {
                            object[] res = func.Call(source, msg.args, msg.sender, msg.server, msg.channel);
                            return proxy.Length == 0 ? Strings.ret_empty_script : proxy.Contents;
                        }
                    }
                    catch (NLua.Exceptions.LuaScriptException ex) { return Strings.err_generic + ": " + ex.Message + "\n``` " + ex.Source + " ```"; }
                }
            }
            else if (Type == CmdType.CSharp)
            {
                const string template = "using System;using System.Collections.Generic;using Discord.Net;using Discord;using Discord.Commands;namespace DuckCommand {public class Command {public static string Main(string rawText,User sender,Server server,Channel channel){\n";
                string source = template + content + "}}}";
                using (CodeDomProvider compiler = CodeDomProvider.CreateProvider("CSharp"))
                {
                    CompilerParameters pars = new CompilerParameters();
                    pars.ReferencedAssemblies.Add("System.dll");
                    pars.ReferencedAssemblies.Add("Discord.Net.dll");
                    pars.ReferencedAssemblies.Add("Discord.Net.Commands.dll");
                    pars.GenerateExecutable = false;
                    pars.GenerateInMemory = true;
                    CompilerResults results = compiler.CompileAssemblyFromSource(pars, source);
                    if (!results.Errors.HasErrors)
                    {
                        MethodInfo method = results.CompiledAssembly.GetType("DuckCommand.Command").GetMethod("Main");
                        return method.Invoke(null, new object[] { msg.args, msg.sender, msg.server, msg.channel }).ToString();
                    }
                    else
                    {
                        StringBuilder errors = new StringBuilder(Strings.err_compile + ": ");
                        errors.AppendFormat("{0},{1}: ``` {2} ```", results.Errors[0].Line - 1, results.Errors[0].Column, results.Errors[0].ErrorText);
                        return errors.ToString();
                    }
                }
            }
            else throw new ArgumentOutOfRangeException("Type");
        }

        private class PrintProxy : TextWriter
        {
            public override Encoding Encoding { get { return Encoding.UTF8; } }

            private StringBuilder buffer = new StringBuilder();

            public void Print(params object[] args)
            {
                if (args.Length > 0)
                {
                    buffer.Append(args[0].ToString());
                    for (int i = 1; i < args.Length; ++i) buffer.Append("    " + args[i].ToString());
                }
                buffer.AppendLine();
            }

            public int Length { get { return buffer.Length; } }

            public string Contents { get { return buffer.ToString().TrimEnd('\n', '\r'); } }
        }
    }
}