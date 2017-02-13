using System;
using System.Collections.Generic;
using Discord;
using System.IO;
using System.Threading.Tasks;

namespace DuckBot
{
    internal sealed class Program : IDisposable
    {
        public static readonly Random Rand = new Random();
        internal static Program Inst = null;
        
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
        }

        private readonly Dictionary<string, HardCmd> hardCmds;
        private readonly DiscordClient dClient;
        private readonly DuckData data;
        private readonly string token, prefix;
        private readonly Task bgSaver;
        private readonly System.Threading.CancellationTokenSource bgCancel;

        static void Main(string[] args)
        {
            Console.Title = "DuckBot";
            if (!DuckData.TokenFile.Exists) Log(LogSeverity.Error, "Token file hasn't been found!");
            else
            {
                string token = File.ReadAllText(DuckData.TokenFile.FullName);
                int ix = token.IndexOf(' ');
                if (ix < 1) Log(LogSeverity.Error, "Token file has wrong format!");
                else using (Inst = new Program(token.Substring(ix + 1), token.Remove(ix)))
                {
                    Inst.LoadData();
                    Inst.Start();
                    while (Console.ReadKey(true).Key != ConsoleKey.Q)
                        System.Threading.Thread.Sleep(100);
                }
            }
        }

        private Program(string userToken, string cmdPrefix)
        {
            dClient = new DiscordClient(x =>
            {
                x.AppName = "DuckBot";
                x.LogLevel = LogSeverity.Info;
                x.LogHandler = Log;
            });
            token = userToken;
            prefix = cmdPrefix;
            data = new DuckData();
            hardCmds = new Dictionary<string, HardCmd>();
            CreateCommands();
            bgCancel = new System.Threading.CancellationTokenSource();
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
            Log(LogSeverity.Info, "Shutting down DuckBot...");
            dClient.Disconnect().Wait();
            dClient.Dispose();
            bgCancel.Cancel();
            bgSaver.Wait();
            bgSaver.Dispose();
            bgCancel.Dispose();
            Log(LogSeverity.Info, "Exiting gracefully");
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
                catch (Exception ex) { Log(new Exception("Couldn't load whitelist file", ex)); }
            foreach (FileInfo fi in DuckData.SessionsDir.EnumerateFiles("session_*.dat"))
                try
                {
                    ulong id = ulong.Parse(fi.Name.Remove(fi.Name.Length - 4).Substring(8));
                    Session s = new Session(id);
                    using (BinaryReader br = new BinaryReader(fi.OpenRead()))
                        s.Load(br);
                    data.ServerSessions.Add(id, s);
                }
                catch (Exception ex) { Log(new Exception("Couldn't load " + fi.Name, ex)); }
        }

        private void Start()
        {
            if (!DuckData.LogFile.Exists) File.WriteAllText(DuckData.LogFile.FullName, "");
            Log(LogSeverity.Info, "Starting DuckBot, press 'Q' to quit!");
            dClient.MessageReceived += MessageRecieved;
            dClient.UserUpdated += UserUpdated;
            dClient.Connect(token, TokenType.Bot);
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

        public Command GetCommand(Server srv, string name)
        {
            Session s = data.ServerSessions[srv.Id];
            return s.Cmds.ContainsKey(name) ? s.Cmds[name] : null;
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
                            await msg.channel.SendMessage("Unpermitted keywords have been found in the code.");
                            return;
                        }
                    }
                    else
                    {
                        await msg.channel.SendMessage("You don't have sufficent permissons.");
                        return;
                    }
                }
                else if (args[0].ToLower() == "whitelist")
                {
                    if (msg.sender.Id == DuckData.SuperUserId)
                    {
                        lock (data.AdvancedUsers)
                            data.AdvancedUsers.Add(ulong.Parse(args[1]));
                        SaveWhitelist();
                        await msg.channel.SendMessage("The ID has been added to whitelist!");
                    }
                    else await msg.channel.SendMessage("You don't have sufficent permissons.");
                    return;
                }
                string cmd = args[1].ToLowerInvariant(), oldContent = " ";
                lock (s)
                    if (s.Cmds.ContainsKey(cmd))
                    {
                        oldContent = s.Cmds[cmd].AsCodeBlock();
                        s.Cmds[cmd] = new Command(args[0], args[2], msg.sender.Name);
                    }
                    else s.Cmds.Add(cmd, new Command(args[0], args[2], msg.sender.Name));
                s.SetPending();
                string toSend;
                if (s.ShowChanges) lock (s) toSend = "Changed from: " + oldContent + "\nTo: " + s.Cmds[cmd].AsCodeBlock();
                else toSend = "Done!";
                await msg.channel.SendMessage(toSend);
            }, "<type> <name> <content>`\nTypes: `csharp, lua, switch, text", true));

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
                        log = s.ShowChanges ? "\nContent: " + s.Cmds[cmd].AsCodeBlock() : "";
                        s.Cmds.Remove(cmd);
                    }
                    await msg.channel.SendMessage("Removed command `" + cmd + "`." + log);
                    s.SetPending();
                }
                else await msg.channel.SendMessage("No user-made command with specified name exists.");
            }, "<command>", true));

            hardCmds.Add("changelog", new HardCmd(1, 1, async (args, msg, s) =>
            {
                string arg = args[0].ToLowerInvariant();
                if (arg == "enable")
                {
                    lock (s) s.ShowChanges = true;
                    await msg.channel.SendMessage("Changelog enabled for server " + msg.server.Name);
                }
                else if (arg == "disable")
                {
                    lock (s) s.ShowChanges = false;
                    await msg.channel.SendMessage("Changelog disabled for server " + msg.server.Name);
                }
                else await msg.channel.SendMessage("Unknown subcommand specified.");
                s.SetPending();
            }, "<action>`\nActions: `enable, disable", true));

            hardCmds.Add("inform", new HardCmd(2, 2, async (args, msg, s) =>
            {
                User u = FindUser(msg.server, args[0]);
                if (u != null)
                {
                    string message = args[1];
                    if (!message.StartsWith("`")) message = "`" + message + "`";
                    message = "`[" + DateTime.UtcNow.ToShortDateString() + "] " + msg.channel.Mention + ":` " + message;
                    string removed = s.AddMessage(msg.sender.Id, u.Id, message);
                    await msg.channel.SendMessage("Okay, I'll deliver the message when " + u.Name + " goes online");
                    if (!string.IsNullOrWhiteSpace(removed))
                        await msg.channel.SendMessage("This message was removed from the inbox:\n" + removed);
                    s.SetPending();
                }
                else await msg.channel.SendMessage("No user " + args[0] + " found on this server!");
            }, "<user> <message>"));

            hardCmds.Add("help", new HardCmd(0, 2, async (args, msg, s) =>
            {
                if (args.Length > 0)
                {
                    string cmd = args[0].ToLowerInvariant();
                    if (hardCmds.ContainsKey(cmd))
                    {
                        HardCmd hcmd = hardCmds[cmd];
                        string ret = "Usage: `" + cmd + " " + hcmd.help + "`\n";
                        ret += "Notation: `\n";
                        if (hcmd.help.Contains("<")) ret += "< > - Required, ";
                        if (hcmd.help.Contains("[")) ret += "[ ] - Optional, ";
                        if (hcmd.help.Contains("*")) ret += "' ' - Literal string, ";
                        ret = ret.Remove(ret.Length - 2) + "`";
                        await msg.channel.SendMessage(ret);
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
                                Command c = s.Cmds[cmd];
                                ret = "Command name: `" + cmd + "`\nCreated: `" + c.CreationDate.ToShortDateString() + "` by `" + c.Creator + "`\nType: `" + c.Type + "`\nContent: " + c.AsCodeBlock();
                            }
                            await msg.channel.SendMessage(ret);
                        }
                        else await msg.channel.SendMessage("No command with specified name exists.");
                    }
                }
                else
                {
                    string ret = "Built-in commands list:\n``` ";
                    foreach (string cmd in new SortedSet<string>(hardCmds.Keys))
                        ret += cmd + ", ";
                    ret = ret.Remove(ret.Length - 2) + " ```\n";
                    lock (s)
                        if (s.Cmds.Count > 0)
                        {
                            ret += "User-made commands list:\n``` ";
                            foreach (string cmd in new SortedSet<string>(s.Cmds.Keys))
                                ret += cmd + ", ";
                            ret = ret.Remove(ret.Length - 2) + " ```\n";
                        }
                    await msg.channel.SendMessage(ret);
                }
            }, "[command]"));

            hardCmds.Add("variable", new HardCmd(1, 2, async (args, msg, s) =>
            {
                if (args[0] == "list")
                {
                    string ret = "Variable list:\n``` ";
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
                        await msg.channel.SendMessage(res ? "Removed variable `" + args[1] + "`." : "No variable with specified name exists.");
                    }
                    else await msg.channel.SendMessage("Insufficient amount of parameters specified.");
                }
                else await msg.channel.SendMessage("Unknown subcommand specified.");
            }, "<action> [variable]`\nActions: `list, remove", true));
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
                    Channel toSend = await dClient.CreatePrivateChannel(e.After.Id);
                    i.Deliver(e.Server, toSend);
                    lock (s.Msgs) s.Msgs.Remove(e.After.Id);
                    s.SetPending();
                }
            }
        }

        private void MessageRecieved(object sender, MessageEventArgs e)
        {
            if (e.Message.RawText.StartsWith(prefix) && e.User.Id != dClient.CurrentUser.Id)
                Task.Run(async () =>
                {
                    string command = e.Message.RawText.Substring(1);
                    int ix = command.IndexOf(' ');
                    if (ix != -1) command = command.Remove(ix);
                    command = command.ToLowerInvariant();
                    Session s = CreateSession(e.Server);
                    try
                    {
                        if (hardCmds.ContainsKey(command))
                        {
                            HardCmd hcmd = hardCmds[command];
                            if (hcmd.admin && (!e.User.ServerPermissions.Administrator || e.User.IsBot))
                                await e.Channel.SendMessage("Only human server administrators can use this command!");
                            else
                            {
                                string[] args = ix == -1 ? new string[0] : e.Message.RawText.Substring(ix + 2).Split(new char[] { ' ' }, hcmd.argsMax, StringSplitOptions.RemoveEmptyEntries);
                                if (args.Length >= hcmd.argsMin)
                                {
                                    await e.Channel.SendIsTyping();
                                    hcmd.func(args, new CmdParams(e), s);
                                }
                                else await e.Channel.SendMessage("Insufficient amount of parameters specified.");
                            }
                        }
                        else if (s.Cmds.ContainsKey(command))
                        {
                            await e.Channel.SendIsTyping();
                            string result = s.Cmds[command].Run(new CmdParams(e));
                            await e.Channel.SendMessage(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex);
                        await e.Channel.SendMessage("Ooops, seems like an exception happened: " + ex.Message);
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