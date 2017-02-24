using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DuckBot.Resources;

namespace DuckBot
{
    public class SoftCmd
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

        public static string Escape(string s)
        {
            foreach (char c in SpecialChars) s = s.Replace(c.ToString(), "^" + c);
            return s;
        }

        public static string Unescape(string s)
        {
            foreach (char c in SpecialChars) s = s.Replace("^" + c, c.ToString());
            return s;
        }

        public static string CmdEngine(string content, CmdParams msg)
        {
            string ret = CmdEngine(content, msg, 0);
            return Unescape(ret);
        }

        private static string CmdEngine(string content, CmdParams msg, int depth)
        {
            return depth > 10 || !content.Contains("{") ? content : CmdPattern.Replace(content, (match) =>
            {
                string cmd = match.Groups[1].Value;
                if (FuncVars.Exists(cmd))
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
            else if (Type == CmdType.Lua) return Sandbox.Lua.Execute(content, msg);
            else if (Type == CmdType.CSharp) return Sandbox.CS.Execute(content, msg);
            else throw new InvalidOperationException("Type");
        }
    }
}