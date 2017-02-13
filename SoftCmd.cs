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
    public class SoftCmd
    {
        private static readonly Regex CmdPattern = new Regex(@"(?<!\^){([^{}:,|^\\ ]+?)(:(?>{(?<n>)|}(?<-n>)|[^{}]+)*(?(n)(?!)))?}");

        private static char[] SpecialChars = { '{', ',', '|' };

        public enum CmdType
        {
            Text = 0, CSharp, Lua, Switch
        }

        public string Content { get; private set; }
        public CmdType Type { get; private set; }
        public DateTime CreationDate { get; private set; }
        public string Creator { get; private set; }

        internal SoftCmd() { }

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

        public SoftCmd(string type, string content, string creator)
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
                if (FuncVars.Has(cmd))
                {
                    string arg = match.Groups[2].Success ? match.Groups[2].Value.Substring(1) : null;
                    if (arg != null) arg = CmdEngine(arg, msg, depth + 1);
                    return FuncVars.Run(cmd, arg == null ? new string[0] : EscSplit(arg, ','), msg);
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