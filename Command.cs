using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Discord;
using NLua;

namespace DuckBot
{
    public class Command : IBinary
    {
        private static readonly Regex CmdPattern = new Regex(@"{([^{}:, ]+?)(:(?>{(?<n>)|}(?<-n>)|[^{}]+)*(?(n)(?!)))?}");

        public delegate string CmdAct(string[] args, CmdParams msg);
        private static readonly Dictionary<string, CmdAct> FuncVars = new Dictionary<string, CmdAct>();

        static Command()
        {
            FuncVars.Add("user", (args, msg) => { return msg.sender.Name; });
            FuncVars.Add("nickOrUser", (args, msg) =>
            {
                return string.IsNullOrWhiteSpace(msg.sender.Nickname) ? msg.sender.Name : msg.sender.Nickname;
            });
            FuncVars.Add("input", (args, msg) =>
            {
                if (args.Length > 0)
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
                if (args.Length > 0)
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
                    if (args.Length > 1)
                    {
                        int min = int.Parse(args[0]);
                        return (Program.Rand.Next(int.Parse(args[1]) - min) - min).ToString();
                    }
                    else if (args.Length == 1) return Program.Rand.Next(int.Parse(args[0])).ToString();
                }
                catch { }
                return "ERROR";
            });
            FuncVars.Add("command", (args, msg) =>
            {
                Command c = Program.Inst.GetCommand(msg.server, args[0]);
                return c == null ? "ERROR" : c.Run(new CmdParams(msg, args[1]));
            });
            FuncVars.Add("if", (args, msg) =>
            {
                if (args.Length != 4) return "ERROR";
                return args[0].Length == args[1].Length ? args[2] : args[3];
            });
            FuncVars.Add("length", (args, msg) =>
            {
                return args.Length > 0 ? args[0].Length.ToString() : "ERROR";
            });
            FuncVars.Add("substr", (args, msg) =>
            {
                string s = args[0];
                int i1 = int.Parse(args[1]);
                if (args.Length > 2)
                {
                    int i2 = int.Parse(args[2]);
                    return s.Substring(i1 >= 0 ? i1 : s.Length + i1, i2);
                }
                else return s.Substring(i1 >= 0 ? i1 : s.Length + i1);
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

        public string CmdEngine(string content, CmdParams msg, int depth = 0)
        {
            return depth > 10 || !content.Contains("{") ? content : CmdPattern.Replace(content, (match) =>
            {
                string cmd = match.Groups[1].Value;
                if (FuncVars.ContainsKey(cmd))
                {
                    string arg = match.Groups[2].Success ? match.Groups[2].Value.Substring(1) : null;
                    if (arg != null) arg = CmdEngine(arg, msg, depth + 1);
                    return FuncVars[cmd](arg == null ? new string[0] : arg.Split(','), msg);
                }
                else return match.Value;
            });
        }

        public string Run(CmdParams msg)
        {
            if (Type == CmdType.Text) return CmdEngine(Content, msg);
            else if (Type == CmdType.Switch)
            {
                string[] cases = Content.Split('|');
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
                return "Switch didn't return anything";
            }
            else if (Type == CmdType.Lua)
            {
                using (Lua lua = new Lua())
                {
                    LuaPrintProxy proxy = new LuaPrintProxy();
                    lua.RegisterFunction("print", proxy, proxy.GetType().GetMethod("Print", new Type[] { typeof(object[]) }));
                    string code;
                    using (StreamReader sr = new StreamReader(GetType().Assembly.GetManifestResourceStream("DuckBot.Resources.Sandbox.lua")))
                        code = sr.ReadToEnd();
                    try
                    {
                        const string template = "args = {...};rawText,sender,server,channel=args[1],args[2],args[3],args[4]\n";
                        string source = template + Content;
                        using (LuaFunction func = (LuaFunction)lua.DoString(code, "sandbox")[0])
                        {
                            object[] res = func.Call(source, msg.args, msg.sender, msg.server, msg.channel);
                            return proxy.Length == 0 ? "Script didn't return anything" : proxy.Contents;
                        }
                    }
                    catch (NLua.Exceptions.LuaScriptException ex) { return "An error has occured: " + ex.Message + "\n``` " + ex.Source + " ```"; }
                }
            }
            else if (Type == CmdType.CSharp)
            {
                const string template = "using System;using System.Collections.Generic;using Discord.Net;using Discord;using Discord.Commands;namespace DuckCommand {public class Command {public static string Main(string rawText,User sender,Server server,Channel channel){\n";
                string source = template + Content + "}}}";
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
                        StringBuilder errors = new StringBuilder("Compilation error: ");
                        errors.AppendFormat("{0},{1}: ``` {2} ```", results.Errors[0].Line - 1, results.Errors[0].Column, results.Errors[0].ErrorText);
                        return errors.ToString();
                    }
                }
            }
            else throw new ArgumentOutOfRangeException("Type");
        }

        private class LuaPrintProxy
        {
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