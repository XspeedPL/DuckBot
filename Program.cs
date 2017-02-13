using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using DuckBot.Resources;
using Discord;

namespace DuckBot
{
    internal sealed class Program : IDisposable
    {
        public static readonly Random Rand = new Random();
        internal static Program Inst = null;

        private readonly Dictionary<string, HardCmd> hardCmds;
        private readonly DiscordClient client;
        private readonly DuckData data;
        private readonly string token, prefix;
        private readonly Task bgSaver;
        private readonly CancellationTokenSource bgCancel;

        static void Main(string[] args)
        {
            Console.Title = "DuckBot";
            if (!DuckData.LogFile.Exists) File.WriteAllText(DuckData.LogFile.FullName, "");
            if (!DuckData.TokenFile.Exists) Log(LogSeverity.Error, Strings.start_err_notoken);
            else
            {
                string token = File.ReadAllText(DuckData.TokenFile.FullName, System.Text.Encoding.UTF8);
                int ix = token.IndexOf(' ');
                if (ix < 1) Log(LogSeverity.Error, Strings.start_err_badtoken);
                else using (Inst = new Program(token.Substring(ix + 1), token.Remove(ix)))
                {
                    Inst.LoadData();
                    Inst.Start();
                    while (Console.ReadKey(true).Key != ConsoleKey.Q)
                        Thread.Sleep(100);
                }
            }
        }

        private Program(string userToken, string cmdPrefix)
        {
            client = new DiscordClient(x =>
            {
                x.AppName = Console.Title;
                x.LogLevel = LogSeverity.Info;
                x.LogHandler = Log;
            });
            token = userToken;
            prefix = cmdPrefix;
            data = new DuckData();
            hardCmds = new Dictionary<string, HardCmd>();
            CreateCommands();
            bgCancel = new CancellationTokenSource();
            bgSaver = Task.Run(AsyncSaver);
        }

        public async Task AsyncSaver()
        {
            while (!bgCancel.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(120000, bgCancel.Token);
                    lock (data.ServerSessions)
                        foreach (Session s in data.ServerSessions.Values)
                            if (s.PendingSave) s.SaveAsync();
                }
                catch { }
            }
            lock (data.ServerSessions)
                foreach (Session s in data.ServerSessions.Values)
                    if (s.PendingSave) s.Save();
        }

        public void Dispose()
        {
            Log(LogSeverity.Info, Strings.exit_start);
            client.Disconnect().Wait();
            client.Dispose();
            bgCancel.Cancel();
            bgSaver.Wait();
            bgSaver.Dispose();
            bgCancel.Dispose();
            Log(LogSeverity.Info, Strings.exit_end);
        }

        private void SaveWhitelist()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(new FileStream(DuckData.WhitelistFile.FullName, FileMode.Create, FileAccess.Write)))
                    lock (data.AdvancedUsers)
                        foreach (ulong u in data.AdvancedUsers)
                            sw.WriteLine(u.ToString());
            }
            catch (Exception ex) { Log(ex); }
        }

        private void LoadData()
        {
            if (!DuckData.SessionsDir.Exists) DuckData.SessionsDir.Create();
            if (DuckData.WhitelistFile.Exists)
                try
                {
                    foreach (string line in File.ReadLines(DuckData.WhitelistFile.FullName))
                    {
                        ulong id = ulong.Parse(line);
                        if (!data.AdvancedUsers.Contains(id))
                            data.AdvancedUsers.Add(ulong.Parse(line));
                    }
                }
                catch (Exception ex) { Log(new Exception(string.Format(Strings.start_err_fileload, DuckData.WhitelistFile.Name), ex)); }
            foreach (FileInfo fi in DuckData.SessionsDir.EnumerateFiles("session_*.dat"))
                try
                {
                    ulong id = ulong.Parse(fi.Name.Remove(fi.Name.Length - 4).Substring(8));
                    Session s = new Session(id);
                    using (BinaryReader br = new BinaryReader(fi.OpenRead()))
                        s.Load(br);
                    data.ServerSessions.Add(id, s);
                }
                catch (Exception ex) { Log(new Exception(string.Format(Strings.start_err_fileload, fi.Name), ex)); }
        }

        private void Start()
        {
            Log(LogSeverity.Info, string.Format(Strings.start_info, client.Config.AppName));
            client.MessageReceived += MessageRecieved;
            client.UserUpdated += UserUpdated;
            client.Connect(token, TokenType.Bot);
        }

        internal Session CreateSession(Server srv)
        {
            lock (data.ServerSessions)
                if (!data.ServerSessions.ContainsKey(srv.Id))
                {
                    Session s = new Session(srv.Id);
                    data.ServerSessions.Add(srv.Id, s);
                    return s;
                }
                else return data.ServerSessions[srv.Id];
        }

        private void CreateCommands()
        {
            hardCmds.Add("add", new HardCmd(3, 3, async (args, msg, s) =>
            {
                if (args[0].ToLower() == "csharp")
                {
                    bool check;
                    lock (data.AdvancedUsers) check = data.AdvancedUsers.Contains(msg.sender.Id);
                    if (check)
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
                        lock (data.AdvancedUsers)
                            data.AdvancedUsers.Add(ulong.Parse(args[1]));
                        SaveWhitelist();
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

            hardCmds.Add("remove", new HardCmd(1, 2, async (args, msg, s) =>
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

            hardCmds.Add("showchanges", new HardCmd(1, 1, async (args, msg, s) =>
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

            hardCmds.Add("inform", new HardCmd(2, 2, async (args, msg, s) =>
            {
                User u = FindUser(msg.server, args[0]);
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

            hardCmds.Add("help", new HardCmd(0, 2, async (args, msg, s) =>
            {
                if (args.Length > 0)
                {
                    string cmd = args[0].ToLowerInvariant();
                    if (hardCmds.ContainsKey(cmd))
                    {
                        HardCmd hcmd = hardCmds[cmd];
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
                    foreach (string cmd in new SortedSet<string>(hardCmds.Keys))
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

            hardCmds.Add("variable", new HardCmd(1, 2, async (args, msg, s) =>
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
        }

        private static bool UserActive(User u) { return u.Status == UserStatus.Online || u.Status == UserStatus.DoNotDisturb; }

        private async void UserUpdated(object sender, UserUpdatedEventArgs e)
        {
            if (!UserActive(e.Before) && UserActive(e.After))
            {
                Session s = CreateSession(e.Server);
                if (s.Msgs.ContainsKey(e.After.Id))
                {
                    Inbox i = s.Msgs[e.After.Id];
                    Channel toSend = await client.CreatePrivateChannel(e.After.Id);
                    i.Deliver(e.Server, toSend);
                    lock (s.Msgs) s.Msgs.Remove(e.After.Id);
                    s.SetPending();
                }
            }
        }
        
        private void MessageRecieved(object sender, MessageEventArgs e)
        {
            if (e.Message.RawText.StartsWith(prefix) && e.User.Id != client.CurrentUser.Id)
                Task.Run(async () =>
                {
                    string cmd = e.Message.RawText.Substring(1);
                    int ix = cmd.IndexOf(' ');
                    if (ix != -1) cmd = cmd.Remove(ix);
                    cmd = cmd.ToLowerInvariant();
                    Session s = CreateSession(e.Server);
                    Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(s.Language);
                    try
                    {
                        if (hardCmds.ContainsKey(cmd))
                        {
                            HardCmd hcmd = hardCmds[cmd];
                            if (hcmd.admin && (!e.User.ServerPermissions.Administrator || e.User.IsBot))
                                await e.Channel.SendMessage(Strings.err_notadmin);
                            else
                            {
                                string[] args = ix == -1 ? new string[0] : e.Message.RawText.Substring(ix + 2).Split(new char[] { ' ' }, hcmd.argsMax, StringSplitOptions.RemoveEmptyEntries);
                                if (args.Length >= hcmd.argsMin)
                                {
                                    await e.Channel.SendIsTyping();
                                    hcmd.func(args, new CmdParams(e), s);
                                }
                                else await e.Channel.SendMessage(Strings.err_params);
                            }
                        }
                        else if (s.Cmds.ContainsKey(cmd))
                        {
                            await e.Channel.SendIsTyping();
                            string res = s.Cmds[cmd].Run(new CmdParams(e));
                            await e.Channel.SendMessage(string.IsNullOrWhiteSpace(res) ? Strings.ret_empty_cmd : res);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex);
                        await e.Channel.SendMessage(Strings.err_generic + ": " + ex.Message);
                    }
                });
        }

        public static void Log(object sender, LogMessageEventArgs e)
        {
            Log(e.Severity, "[" + e.Source + "] " + e.Message);
        }

        public static void Log(Exception ex)
        {
            Log(LogSeverity.Error, ex + "\n");
        }

        public static void Log(LogSeverity severity, string text)
        {
            text = "[" + DateTime.UtcNow.ToShortTimeString() + "] [" + severity + "] " + text;
            using (StreamWriter sw = DuckData.LogFile.AppendText())
                sw.WriteLine(text);
            Console.WriteLine(text);
        }

        public static User FindUser(Server srv, string user)
        {
            if (!string.IsNullOrWhiteSpace(user))
                foreach (User u in srv.FindUsers(user)) return u;
            return null;
        }
    }
}