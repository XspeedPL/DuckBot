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

        public static string ScriptEngine(string content, CmdContext context) => Unescape(new ScriptEvaluator(context).Evaluate(content));

        public string Run(CmdContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            string content;
            lock (this) content = Content;
            try
            {
                if (Type == CmdType.Switch) return new SwitchEvaluator(context).Evaluate(content) ?? "Switch " + context.GetString("ret_empty");
                else if (Type == CmdType.Lua) return Sandbox.Lua.Execute(content, context);
                else if (Type == CmdType.CSharp) return Sandbox.CS.Execute(content, context);
                else return ScriptEngine(content, context);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.InnerException is Antlr4.Runtime.RecognitionException inner)
                {
                    return context.GetString("err_syntax", inner.OffendingToken.Text, inner.OffendingToken.Column, inner.OffendingToken.Line);
                }
                // TODO: Wrap and throw Lua and C# compilation errors and catch them here
                else return context.GetString("err_syntax", null, null, null);
            }
        }
    }
}