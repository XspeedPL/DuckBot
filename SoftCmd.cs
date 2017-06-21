using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DuckBot.DuckScript;

namespace DuckBot
{
    public class SoftCmd : ICmd
    {
        private static readonly Regex CmdPattern = new Regex(@"(?<!\^){([^{}:,|^\\ ]+?)(:(?>{(?<n>)|}(?<-n>)|[^{}]+)*(?(n)(?!)))?}");

        private static readonly char[] SpecialChars = { '{', ',', '|' };

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

        public void Load(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            lock (this)
            {
                Creator = reader.ReadString();
                CreationDate = DateTime.FromBinary(reader.ReadInt64());
                Type = (CmdType)reader.ReadByte();
                Content = reader.ReadString();
            }
        }

        public void Save(BinaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            lock (this)
            {
                writer.Write(Creator);
                writer.Write(CreationDate.Ticks);
                writer.Write((byte)Type);
                writer.Write(Content);
            }
        }

        public SoftCmd(string type, string content, string creator)
        {
            Creator = creator;
            CreationDate = DateTime.UtcNow;
            if (!Enum.TryParse(type, true, out CmdType ctype)) ctype = CmdType.Text;
            Type = ctype;
            Content = content;
        }

        private static string[] EscSplit(string args, char delimiter)
        {
            if (string.IsNullOrEmpty(args)) return new string[0];
            List<string> ret = new List<string>();
            int old, prev, ix;
            if (args[0] == delimiter)
            {
                ret.Add("");
                old = 1;
            }
            else old = 0;
            prev = old;
            while ((ix = args.IndexOf(delimiter, prev)) != -1)
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

        public static string Escape(string data)
        {
            foreach (char c in SpecialChars) data = data.Replace(c.ToString(), "^" + c);
            return data;
        }

        public static string Unescape(string data)
        {
            foreach (char c in SpecialChars) data = data.Replace("^" + c, c.ToString());
            return data;
        }

        public static string CmdEngine(string content, CmdContext context) => Unescape(CmdEngine(content, context, 0));

        private static string CmdEngine(string content, CmdContext context, int depth)
        {
            return depth > 10 || !content.Contains("{") ? content : CmdPattern.Replace(content, (match) =>
            {
                string cmd = match.Groups[1].Value;
                if (FuncVar.Exists(cmd))
                {
                    string arg = match.Groups[2].Success ? match.Groups[2].Value.Substring(1) : null;
                    if (arg != null) arg = CmdEngine(arg, context, depth + 1);
                    return FuncVar.Run(cmd, arg == null ? new string[0] : EscSplit(arg, ','), context);
                }
                else return match.Value;
            });
        }

        public string Run(CmdContext context)
        {
            string content;
            lock (this) content = Content;
            if (Type == CmdType.Text)
            {
                return CmdEngine(content, context);
                //return new ScriptEvaluator(context).Evaluate(content);
            }
            else if (Type == CmdType.Switch)
            {
                string[] cases = EscSplit(content, '|');
                cases[0] = CmdEngine(cases[0].Trim(), context);
                for (int i = 1; i < cases.Length; ++i)
                {
                    cases[i] = cases[i].Trim();
                    if (cases[i].StartsWith("default"))
                        return CmdEngine(cases[i].Substring(cases[i].IndexOf(' ') + 1), context);
                    else if (cases[i].StartsWith("case"))
                    {
                        string match = cases[i].Substring(cases[i].IndexOf('"') + 1);
                        int ix = match.IndexOf('"');
                        string data = match.Substring(ix + 1).Trim();
                        if (cases[0] == CmdEngine(match.Remove(ix), context))
                            return CmdEngine(data, context);
                    }
                }
                return "Switch " + context.GetString("ret_empty");
            }
            else if (Type == CmdType.Lua) return Sandbox.Lua.Execute(content, context);
            else if (Type == CmdType.CSharp) return Sandbox.CS.Execute(content, context);
            else throw new ArgumentOutOfRangeException(nameof(Type));
        }
    }
}