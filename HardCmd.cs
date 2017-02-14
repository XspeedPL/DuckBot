﻿using System;
using System.Collections.Generic;
using DuckBot.Resources;
using Discord;

namespace DuckBot
{
    public struct HardCmd
    {
        public delegate void CmdAct(string[] args, CmdParams msg, Session s);

        public readonly CmdAct func;
        public readonly byte argsMin, argsMax;
        public readonly bool admin;
        public readonly string help;

        public HardCmd(byte minArgs, byte maxArgs, CmdAct action, string helpText, bool reqAdmin = false)
        {
            argsMin = minArgs; argsMax = maxArgs; func = action; help = helpText; admin = reqAdmin;
        }

        internal static Dictionary<string, HardCmd> CreateDefault()
        {
            Dictionary<string, HardCmd> dict = new Dictionary<string, HardCmd>();

            dict.Add("add", new HardCmd(3, 3, async (args, msg, s) =>
            {
                if (args[0].ToLower() == "csharp")
                {
                    if (Program.Inst.IsAdvancedUser(msg.sender.Id))
                    {
                        if (msg.sender.Id != DuckData.SuperUserId && (args[2].Contains("Assembly") || args[2].Contains("System.IO") || args[2].Contains("Environment")))
                        {
                            await msg.channel.SendMessage(Strings.err_badcode);
                            return;
                        }
                    }
                    else { await msg.channel.SendMessage(Strings.err_permissions); return; }
                }
                else if (args[0].ToLower() == "whitelist")
                {
                    if (msg.sender.Id == DuckData.SuperUserId)
                    {
                        Program.Inst.AddAdvancedUser(ulong.Parse(args[1]));
                        await msg.channel.SendMessage(Strings.ret_success);
                    }
                    else await msg.channel.SendMessage(Strings.err_permissions);
                    return;
                }
                string cmd = args[1].ToLowerInvariant(), oldContent = " ";
                SoftCmd nc = new SoftCmd(args[0], args[2], msg.sender.Name);
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
                await msg.channel.SendMessage(toSend);
            }, string.Format("<{0}> <{1}> <{2}>", Strings.lab_type, Strings.lab_name, Strings.lab_content) + "`\n" + Strings.lab_type.StartCase() + ": `csharp, lua, switch, text", true));

            dict.Add("remove", new HardCmd(1, 2, async (args, msg, s) =>
            {
                string cmd = args[0].ToLowerInvariant();
                bool check;
                lock (s) check = s.Cmds.ContainsKey(cmd);
                if (check)
                {
                    string log;
                    lock (s)
                    {
                        log = s.ShowChanges ? "\n" + Strings.lab_content.StartCase() + ": " + s.Cmds[cmd].AsCodeBlock() : "";
                        s.Cmds.Remove(cmd);
                    }
                    await msg.channel.SendMessage(string.Format(Strings.ret_removed, Strings.lab_cmd, cmd) + log);
                    s.SetPending();
                }
                else await msg.channel.SendMessage(string.Format(Strings.err_nogeneric, Strings.lab_usercmd));
            }, string.Format("<{0}>", Strings.lab_cmd), true));

            dict.Add("showchanges", new HardCmd(1, 1, async (args, msg, s) =>
            {
                string arg = args[0].ToLowerInvariant();
                if (arg == "enable")
                    lock (s) s.ShowChanges = true;
                else if (arg == "disable")
                    lock (s) s.ShowChanges = false;
                else
                {
                    await msg.channel.SendMessage(Strings.err_nosubcmd);
                    return;
                }
                s.SetPending();
                await msg.channel.SendMessage(Strings.ret_success);
            }, string.Format("<{0}>", Strings.lab_action) + "`\n" + Strings.lab_action.StartCase() + ": `enable, disable", true));

            dict.Add("inform", new HardCmd(2, 2, async (args, msg, s) =>
            {
                User u = Program.FindUser(msg.server, args[0]);
                if (u != null)
                {
                    string message = args[1];
                    if (!message.StartsWith("`")) message = "`" + message + "`";
                    message = "`[" + DateTime.UtcNow.ToShortDateString() + "]` " + msg.channel.Mention + ": " + message;
                    string removed = s.AddMessage(msg.sender.Id, u.Id, message);
                    await msg.channel.SendMessage(string.Format(Strings.ret_delivery, u.Name));
                    if (!string.IsNullOrWhiteSpace(removed))
                        await msg.channel.SendMessage(Strings.title_fullinbox + "\n" + removed);
                    s.SetPending();
                }
                else await msg.channel.SendMessage(string.Format(Strings.err_nogeneric, Strings.lab_user));
            }, string.Format("<{0}> <{1}>", Strings.lab_user, Strings.lab_message)));

            dict.Add("help", new HardCmd(0, 2, async (args, msg, s) =>
            {
                if (args.Length > 0)
                {
                    string cmd = args[0].ToLowerInvariant();
                    if (dict.ContainsKey(cmd))
                    {
                        HardCmd hcmd = dict[cmd];
                        string ret = "";
                        if (hcmd.help.Contains("<")) ret += "< > - " + Strings.lab_required + ", ";
                        if (hcmd.help.Contains("[")) ret += "[ ] - " + Strings.lab_optional + ", ";
                        if (hcmd.help.Contains("'")) ret += "' ' - " + Strings.lab_literal + ", ";
                        ret = ret.Remove(ret.Length - 2);
                        await msg.channel.SendMessage(string.Format(Strings.ret_cmdhelp, cmd, hcmd.help, ret));
                    }
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
                            await msg.channel.SendMessage(ret);
                        }
                        else await msg.channel.SendMessage(string.Format(Strings.err_nogeneric, Strings.lab_cmd));
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
                    await msg.channel.SendMessage(ret);
                }
            }, string.Format("[{0}]", Strings.lab_cmd)));

            dict.Add("variable", new HardCmd(1, 2, async (args, msg, s) =>
            {
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
                    await msg.channel.SendMessage(ret + " ```\n");
                }
                else if (args[0] == "remove")
                {
                    if (args.Length >= 2)
                    {
                        bool res;
                        lock (s) res = s.Vars.Remove(args[1]);
                        await msg.channel.SendMessage(res ? string.Format(Strings.ret_removed, Strings.lab_var, args[1]) : string.Format(Strings.err_nogeneric, Strings.lab_var));
                    }
                    else await msg.channel.SendMessage(Strings.err_params);
                }
                else await msg.channel.SendMessage(Strings.err_nosubcmd);
            }, string.Format("<{0}> [{1}]", Strings.lab_action, Strings.lab_var) + "`\n" + Strings.lab_action.StartCase() + ": `list, remove", true));

            return dict;
        }
    }
}