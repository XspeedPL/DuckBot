using System;
using System.Collections.Generic;
using DuckBot.Resources;
using Discord;

namespace DuckBot
{
    public class HardCmd
    {
        public delegate string HardCmdHandler(string[] args, CmdContext context);

        public HardCmdHandler Func { get; private set; }
        public byte ArgsMin { get; private set; }
        public byte ArgsMax { get; private set; }
        public bool AdminOnly { get; private set; }
        public string HelpText { get; private set; }

        public HardCmd(byte minArgs, byte maxArgs, HardCmdHandler action, string helpText, bool requireAdmin = false)
        {
            ArgsMin = minArgs; ArgsMax = maxArgs; Func = action; HelpText = helpText; AdminOnly = requireAdmin;
        }

        internal static Dictionary<string, HardCmd> CreateDefault() => new Dictionary<string, HardCmd>
        {
            { "add", new HardCmd(3, 3, (args, msg) =>
                {
                    if (args[0].ToLower() == "csharp")
                    {
                        if (Program.Inst.IsAdvancedUser(msg.Sender.Id))
                        {
                            if (msg.Sender.Id != DuckData.SuperUserId && (args[2].Contains("Assembly") || args[2].Contains("System.IO") || args[2].Contains("Environment")))
                                return Strings.err_badcode;
                        }
                        else return Strings.err_permissions;
                    }
                    else if (args[0].ToLower() == "whitelist")
                    {
                        if (msg.Sender.Id == DuckData.SuperUserId)
                        {
                            Program.Inst.AddAdvancedUser(ulong.Parse(args[1]));
                            return Strings.ret_success;
                        }
                        else return Strings.err_permissions;
                    }
                    string cmd = args[1].ToLowerInvariant(), oldContent = " ";
                    SoftCmd nc = new SoftCmd(args[0], args[2], msg.Sender.Username);
                    Session s = msg.Session;
                    lock (s)
                        if (s.Cmds.ContainsKey(cmd))
                        {
                            oldContent = s.Cmds[cmd].AsCodeBlock();
                            s.Cmds[cmd] = nc;
                        }
                        else s.Cmds.Add(cmd, nc);
                    s.SetPending();
                    string toSend;
                    if (s.ShowChanges) lock (s) toSend = string.Format(Strings.ret_changes, oldContent, s.Cmds[cmd].AsCodeBlock());
                    else toSend = Strings.ret_success;
                    return toSend;
                }, string.Format("<{0}> <{1}> <{2}>", Strings.lab_type, Strings.lab_name, Strings.lab_content) + "`\n" + Strings.lab_type.StartCase() + ": `csharp, lua, switch, text", true)
            },
            { "remove", new HardCmd(1, 2, (args, msg) =>
                {
                    string cmd = args[0].ToLowerInvariant();
                    bool check;
                    Session s = msg.Session;
                    lock (s) check = s.Cmds.ContainsKey(cmd);
                    if (check)
                    {
                        string log;
                        lock (s)
                        {
                            log = s.ShowChanges ? "\n" + Strings.lab_content.StartCase() + ": " + s.Cmds[cmd].AsCodeBlock() : "";
                            s.Cmds.Remove(cmd);
                        }
                        s.SetPending();
                        return string.Format(Strings.ret_removed, Strings.lab_cmd, cmd) + log;
                    }
                    else return string.Format(Strings.err_nogeneric, Strings.lab_usercmd);
                }, string.Format("<{0}>", Strings.lab_cmd), true)
            },
            { "options", new HardCmd(1, 2, (args, msg) =>
                {
                    string arg = args[0].ToLowerInvariant();
                    if (arg == "showchanges")
                    {
                        if (args.Length == 1)
                            return FormatHelp("options showchanges", string.Format("<{0}>", Strings.lab_action) + "`\n" + Strings.lab_action.StartCase() + ": `enable, disable");
                        else if (args[1] == "enable") msg.Session.ShowChanges = true;
                        else if (args[1] == "disable") msg.Session.ShowChanges = false;
                        else return Strings.err_nosubcmd;
                    }
                    else if (arg == "commandprefix")
                    {
                        if (args.Length == 1 || string.IsNullOrWhiteSpace(args[1]))
                            return FormatHelp("options commandprefix", string.Format("<{0}>", Strings.lab_prefix));
                        else msg.Session.CommandPrefix = args[1];
                    }
                    else if (arg == "musicchannel")
                    {
                        if (args.Length == 1 || string.IsNullOrWhiteSpace(args[1]))
                            return FormatHelp("options musicchannel", string.Format("<{0}>", Strings.lab_channel));
                        else if (args[1].Equals("-"))
                        {
                            msg.Session.LeaveAudioAsync().GetAwaiter().GetResult();
                            msg.Session.MusicChannel = "";
                        }
                        else
                        {
                            bool found = false;
                            foreach (IVoiceChannel c in msg.Server.GetVoiceChannelsAsync().GetAwaiter().GetResult())
                                if (c.Name.Equals(args[1], StringComparison.OrdinalIgnoreCase))
                                {
                                    msg.Session.JoinAudioAsync(c).GetAwaiter().GetResult();
                                    msg.Session.MusicChannel = c.Name;
                                    found = true;
                                    break;
                                }
                            if (!found) return string.Format(Strings.err_nogeneric, Strings.lab_channel);
                        }
                    }
                    else if (arg == "language")
                    {
                        if (args.Length == 1 || string.IsNullOrWhiteSpace(args[1]))
                            return FormatHelp("options language", string.Format("<{0}>", Strings.lab_language));
                        else if (!msg.Session.SetLanguage(args[1])) return Strings.err_nolanguage;
                    }
                    else return Strings.err_nosubcmd;
                    msg.Session.SetPending();
                    return Strings.ret_success;
                }, string.Format("<{0}>", Strings.lab_action) + "`\n" + Strings.lab_action.StartCase() + ": `language, musicchannel, showchanges", true)
            },
            { "inform", new HardCmd(2, 2, (args, msg) =>
                {
                    IUser u = msg.Server.FindUser(args[0]);
                    if (u != null)
                    {
                        string message = args[1];
                        if (!message.StartsWith("`")) message = "`" + message + "`";
                        message = "`[" + DateTime.UtcNow.ToShortDateString() + "]` " + msg.Channel.Mention + ": " + message;
                        string removed = msg.Session.AddMessage(msg.Sender.Id, u.Id, message);
                        string ret = string.Format(Strings.ret_delivery, u.Username);
                        if (!string.IsNullOrWhiteSpace(removed))
                            ret += "\n" + Strings.title_fullinbox + "\n" + removed;
                        msg.Session.SetPending();
                        return ret;
                    }
                    else return string.Format(Strings.err_nogeneric, Strings.lab_user);
                }, string.Format("<{0}> <{1}>", Strings.lab_user, Strings.lab_message))
            },
            { "help", new HardCmd(0, 2, (args, msg) =>
                {
                    Session s = msg.Session;
                    Dictionary<string, HardCmd> dict = Program.Inst.HardCmds;
                    if (args.Length > 0)
                    {
                        string cmd = args[0].ToLowerInvariant();
                        if (dict.ContainsKey(cmd)) return FormatHelp(cmd, dict[cmd].HelpText);
                        else
                        {
                            bool check;
                            lock (s) check = s.Cmds.ContainsKey(cmd);
                            if (check)
                            {
                                string ret;
                                lock (s)
                                {
                                    SoftCmd c = s.Cmds[cmd];
                                    ret = string.Format(Strings.ret_cmdinfo, cmd, c.CreationDate.ToShortDateString(), c.Creator);
                                    ret += "\n" + Strings.lab_type.StartCase() + ": `" + c.Type + "`\n" + Strings.lab_content.StartCase() + ": " + c.AsCodeBlock();
                                }
                                return ret;
                            }
                            else return string.Format(Strings.err_nogeneric, Strings.lab_cmd);
                        }
                    }
                    else
                    {
                        string ret = Strings.title_hardcmdlist + "\n``` ";
                        foreach (string cmd in new SortedSet<string>(dict.Keys))
                            ret += cmd + ", ";
                        ret = ret.Remove(ret.Length - 2) + " ```\n";
                        lock (s)
                            if (s.Cmds.Count > 0)
                            {
                                ret += Strings.title_usercmdlist + "\n``` ";
                                foreach (string cmd in new SortedSet<string>(s.Cmds.Keys))
                                    ret += cmd + ", ";
                                ret = ret.Remove(ret.Length - 2) + " ```\n";
                            }
                        return ret;
                    }
                }, string.Format("[{0}]", Strings.lab_cmd))
            },
            { "var", new HardCmd(1, 2, (args, msg) =>
                {
                    Session s = msg.Session;
                    if (args[0] == "list")
                    {
                        string ret = Strings.title_varlist + "\n``` ";
                        lock (s)
                            if (s.Vars.Count > 0)
                            {
                                foreach (KeyValuePair<string, string> kvp in new SortedDictionary<string, string>(s.Vars))
                                    ret += kvp.Key + " -> '" + kvp.Value + "', ";
                                ret = ret.Remove(ret.Length - 2);
                            }
                        return ret + " ```\n";
                    }
                    else if (args[0] == "remove")
                    {
                        if (args.Length >= 2)
                        {
                            bool res;
                            lock (s) res = s.Vars.Remove(args[1]);
                            return res ? string.Format(Strings.ret_removed, Strings.lab_var, args[1]) : string.Format(Strings.err_nogeneric, Strings.lab_var);
                        }
                        else return Strings.err_params;
                    }
                    else return Strings.err_nosubcmd;
                }, string.Format("<{0}> [{1}]", Strings.lab_action, Strings.lab_var) + "`\n" + Strings.lab_action.StartCase() + ": `list, remove", true)
            },
            { "playsong", new HardCmd(1, 1, (args, msg) =>
                {
                    if (msg.Session.AudioPlayer == null) return Strings.err_generic;
                    (string result, string song, string url) = Audio.SoundCloudAPI.Search(args[0]).GetAwaiter().GetResult();
                    if (song != null)
                    {
                        msg.Channel.SendMessageAsync(result);
                        msg.Session.PlayAudioAsync(url).ContinueWith((t) =>
                        {
                            if (!Program.Inst.End)
                            {
                                string send;
                                if (t.IsFaulted) send = Strings.err_generic + ' ' + t.Exception.Message;
                                else send = string.Format(Strings.ret_songend, song);
                                msg.Channel.SendMessageAsync(send);
                            }
                        });
                        return null;
                    }
                    else return result;
                }, "<song-name>")
            },
            { "stopsong", new HardCmd(0, 1, (args, msg) =>
                {
                    if (msg.Session.AudioPlayer == null) return Strings.err_generic;
                    else msg.Session.AudioPlayer.StopAsync().GetAwaiter().GetResult();
                    return Strings.ret_success;
                }, "")
            },
            { "credits", new HardCmd(0, 1, (args, msg) => Strings.info_credits, "") }
        };

        public static string FormatHelp(string cmdName, string cmdHelp)
        {
            if (cmdHelp == null) throw new ArgumentNullException("cmdHelp");
            string ret = "";
            if (cmdHelp.Contains("<")) ret += "< > - " + Strings.lab_required + ", ";
            if (cmdHelp.Contains("[")) ret += "[ ] - " + Strings.lab_optional + ", ";
            if (cmdHelp.Contains("'")) ret += "' ' - " + Strings.lab_literal + ", ";
            if (ret.Length > 2) ret = ret.Remove(ret.Length - 2);
            return string.Format(Strings.ret_cmdhelp, cmdName, cmdHelp, ret);
        }
    }
}
