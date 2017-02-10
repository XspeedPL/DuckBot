using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Discord;

namespace DuckBot
{
    public class Command : IBinary
    {
        private static readonly Regex cmdMatch = new Regex("%(.+?)(:.+?)*%");

        internal delegate string CmdAct(string[] args, CmdParams msg);
        internal static readonly Dictionary<string, CmdAct> cmdProc = new Dictionary<string, CmdAct>();

        static Command()
        {
            cmdProc.Add("user", (args, msg) => { return msg.sender.Name; });
            cmdProc.Add("nickOrUser", (args, msg) =>
            {
                return string.IsNullOrWhiteSpace(msg.sender.Nickname) ? msg.sender.Name : msg.sender.Nickname;
            });
            cmdProc.Add("input", (args, msg) => { return msg.args; });
            cmdProc.Add("inputOrUser", (args, msg) =>
            {
                return string.IsNullOrWhiteSpace(msg.args) ? msg.sender.Name : msg.args;
            });
            cmdProc.Add("mention", (args, msg) =>
            {
                if (args.Length > 0)
                {
                    User u = Program.FindUser(msg.server, args[0]);
                    return u == null ? "ERROR" : u.Mention;
                }
                else return msg.sender.Mention;
            });
            cmdProc.Add("rand", (args, msg) =>
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
            cmdProc.Add("command", (args, msg) =>
            {
                Command c = Program.Inst.GetCommand(msg.server, args[0]);
                return c == null ? "ERROR" : c.Run(new CmdParams(msg, args[1]));
            });
            cmdProc.Add("giphy", (args, msg) =>
            {
                using (System.Net.WebClient wc = new System.Net.WebClient())
                {
                    string data = wc.DownloadString("http://api.giphy.com/v1/gifs/random?api_key=dc6zaTOxFJmzC&tag=" + msg.args.Replace(' ', '+'));
                    int ix = data.IndexOf("\"image_url\":\"");
                    data = data.Substring(ix + 13);
                    ix = data.IndexOf("\",");
                    data = data.Remove(ix);
                    return data.Replace("\\/", "/");
                }
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
        
        public string CmdEngine(string content, CmdParams msg)
        {
            return cmdMatch.Replace(content, (match) =>
            {
                string cmd = match.Groups[1].Value;
                if (cmdProc.ContainsKey(cmd))
                {
                    string[] args = match.Groups[2].Success ? match.Groups[2].Value.Substring(1).Split(',') : new string[0];
                    return cmdProc[cmd](args, msg);
                }
                else return match.Value;
            });
        }

        public string Run(CmdParams msg)
        {
            try
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
                            if (cases[0] == match.Remove(ix))
                                return CmdEngine(data, msg);
                        }
                    }
                    return "Switch didn't return anything";
                }
                else if (Type == CmdType.Lua)
                {
                    /*
                    Lua lua = new Lua();
                    string toDo = Content;
                    toDo = toDo.Replace("%user%", user);
                    toDo = toDo.Replace("%userAsMention%", userAsMention);
                    toDo = toDo.Replace("%input%", input);
                    try
                    {
                        string toRun = $"local user = \"{user}\"{Environment.NewLine}local userAsMention = \"{userAsMention}\"{Environment.NewLine}local input = \"{input}\" {Environment.NewLine} {toDo}";
                        object[] ran = lua.DoString(toRun);
                        string toReturn = ran[0].ToString();
                        /*for (int i = 0; i < ran.Length; i++)
                        {
                            toReturn += $"{ran[i]} \n";
                        }

                        return toReturn;
                    }
                    catch (Exception e)
                    {
                        return e.ToString();
                    }
                    */
                    return "Lua is NYI, soz";
                }
                else if (Type == CmdType.CSharp)
                {
                    const string template = "using System;using System.Collections.Generic;using Discord.Net;using Discord;using Discord.Commands;namespace DuckCommand {public class Command {public static string Main(string rawText,User sender,Server server,Channel channel){";
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
                            errors.AppendFormat("{0},{1}: {2}", results.Errors[0].Line - 1, results.Errors[0].Column, results.Errors[0].ErrorText);
                            return errors.ToString();
                        }
                    }
                }
                else throw new ArgumentOutOfRangeException("Type");
            }
            catch (Exception ex)
            {
                Program.Log(ex.ToString(), true);
                return "Welp, an exception occured: " + ex.Message + ". Ping EchoDuck to see it if you need to.";
            }
        }
    }
}