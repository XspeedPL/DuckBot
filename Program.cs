using System;
using System.Collections.Generic;
using Discord;
using System.IO;

namespace DuckBot
{
    internal sealed class Program
    {
        public static readonly Random Rand = new Random();
        internal static Program Inst = null;
        
        public struct HardCmd
        {
            public delegate void CmdAct(string[] args, CmdParams msg, Session s);

            public readonly CmdAct func;
            public byte argsMin, argsMax;

            public HardCmd(byte minArgs, byte maxArgs, CmdAct action)
            {
                argsMin = minArgs; argsMax = maxArgs; func = action;
            }
        }

        private readonly Dictionary<string, HardCmd> hardCmds = new Dictionary<string, HardCmd>();
        private readonly DiscordClient dClient;
        private readonly DuckData data;

        static void Main(string[] args)
        {
            Inst = new Program();
            Inst.LoadData();
            Inst.Start();
        }

        private Program()
        {
            dClient = new DiscordClient(x =>
            {
                x.AppName = "DuckBot";
                x.LogLevel = LogSeverity.Info;
                x.LogHandler = Log;
            });
            data = new DuckData();
            CreateCommands();
        }

        ~Program() { dClient.Dispose(); }

        private void SaveWhitelist()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(new FileStream(DuckData.WhitelistFile.FullName, FileMode.Create, FileAccess.Write)))
                    foreach (ulong u in data.AdvancedUsers)
                        sw.WriteLine(u.ToString());
            }
            catch (Exception ex) { Log(ex.ToString(), true); }
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
                catch (Exception ex) { Log(new Exception("Couldn't load whitelist file", ex).ToString(), true); }
            foreach (FileInfo fi in DuckData.SessionsDir.EnumerateFiles("session_*.dat"))
                try
                {
                    ulong id = ulong.Parse(fi.Name.Remove(fi.Name.Length - 4).Substring(8));
                    Session s = new Session(id);
                    using (BinaryReader br = new BinaryReader(fi.OpenRead()))
                        s.Load(br);
                    data.ServerSessions.Add(id, s);
                }
                catch (Exception ex) { Log(new Exception("Couldn't load " + fi.Name, ex).ToString(), true); }
        }

        private void Start()
        {
            File.AppendAllText(DuckData.LogFile.FullName, "[" + LogSeverity.Info + "] [" + DateTime.UtcNow.ToShortTimeString() + "] Starting DuckBot, hold on tight!");
            dClient.MessageReceived += MessageRecieved;
            dClient.UserUpdated += UserUpdated;
            dClient.ExecuteAndWait(async () =>
            {
                await dClient.Connect("MjY1MTEwNDEzNDIxNTc2MTky.C0qW1g.EkGzwhfFyVKI6qtBQOjFMGP0zNA", TokenType.Bot);
            });
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
                    if (data.AdvancedUsers.Contains(msg.sender.Id))
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
                        data.AdvancedUsers.Add(ulong.Parse(args[1]));
                        SaveWhitelist();
                        await msg.channel.SendMessage("The ID has been added to whitelist!");
                    }
                    else await msg.channel.SendMessage("You don't have sufficent permissons.");
                    return;
                }
                string cmd = args[1].ToLowerInvariant(), oldContent = " ";
                if (s.Cmds.ContainsKey(cmd))
                {
                    oldContent = s.Cmds[cmd].Content;
                    s.Cmds[cmd] = new Command(args[0], args[2], msg.sender.Name);
                }
                else s.Cmds.Add(cmd, new Command(args[0], args[2],msg.sender.Name));
                await s.SaveAsync();
                string toSend;
                if (s.ShowChanges) toSend = "Changed from : ```" + oldContent + "``` to ```" + s.Cmds[args[1]].Content + "```";
                else toSend = "Done!";
                await msg.channel.SendMessage(toSend);
            }));

            hardCmds.Add("remove", new HardCmd(1, 2, async (args, msg, s) =>
            {
                string cmd = args[0].ToLowerInvariant();
                if (s.Cmds.ContainsKey(cmd))
                {
                    string log = s.ShowChanges ? "\nContent: ```" + s.Cmds[cmd].Content + "```" : "";
                    s.Cmds.Remove(cmd);
                    await msg.channel.SendMessage("Removed command `" + cmd + "`" + log);
                    await s.SaveAsync();
                }
                else await msg.channel.SendMessage("No command to remove!");
            }));

            hardCmds.Add("info", new HardCmd(1, 2, async (args, msg, s) =>
            {
                string cmd = args[0].ToLowerInvariant();
                if (s.Cmds.ContainsKey(cmd))
                {
                    Command c = s.Cmds[cmd];
                    string prefix = "";
                    if (c.Type == Command.CmdType.Lua) prefix = "lua";
                    else if (c.Type == Command.CmdType.CSharp) prefix = "cs";
                    await msg.channel.SendMessage("Command name: `" + cmd + "`\nCreated: `" + c.CreationDate.ToShortDateString() + "` by `" + c.Creator + "`\nType: `" + c.Type + "`\nContent: ```" + prefix + "\n" + c.Content + "```");
                }
            }));

            hardCmds.Add("list", new HardCmd(0, 0, async (args, msg, s) =>
            {
                string toSend = "```";
                int i = 0;
                foreach (string name in s.Cmds.Keys)
                {
                    toSend += (++i) + ". " + name + "\n";
                    if (toSend.Length > 1900)
                    {
                        toSend += "```";
                        await msg.channel.SendMessage(toSend);
                        toSend = "```";
                    }
                }
                toSend += "```";
                await msg.channel.SendMessage(toSend);
            }));

            hardCmds.Add("changelog", new HardCmd(1, 1, async (args, msg, s) =>
            {
                if (msg.sender.ServerPermissions.Administrator)
                {
                    string arg = args[0].ToLowerInvariant();
                    if (arg == "enable")
                    {
                        s.ShowChanges = true;
                        await msg.channel.SendMessage("Changelog enabled for server " + msg.server.Name);
                    }
                    else if (arg == "disable")
                    {
                        s.ShowChanges = false;
                        await msg.channel.SendMessage("Changelog disabled for server " + msg.server.Name);
                    }
                    await s.SaveAsync();
                }
                else await msg.channel.SendMessage("You don't have sufficent permissons.");
            }));

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
                    await s.SaveAsync();
                }
                else await msg.channel.SendMessage("No user " + args[0] + " found on this server!");
            }));
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
                    await s.SaveAsync();
                }
            }
        }

        private void MessageRecieved(object sender, MessageEventArgs e)
        {
            if (e.Message.RawText.StartsWith(">") && e.User.Id != dClient.CurrentUser.Id)
                System.Threading.Tasks.Task.Run(async () =>
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
                            await e.Channel.SendIsTyping();
                            HardCmd hcmd = hardCmds[command];
                            string[] args = e.Message.RawText.Substring(ix + 2).Split(new char[] { ' ' }, hcmd.argsMax, StringSplitOptions.RemoveEmptyEntries);
                            if (args.Length >= hcmd.argsMin) hcmd.func(args, new CmdParams(e), s);
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
                        Log(ex.ToString(), true);
                        await e.Channel.SendMessage("Ooops, seems like an exception happened: " + ex.Message);
                    }
                });
        }

        public static void Log(object sender, LogMessageEventArgs e)
        {
            using (StreamWriter sw = DuckData.LogFile.AppendText())
                sw.WriteLine("[" + e.Severity + "] [" + DateTime.UtcNow.ToShortTimeString() + "] [" + e.Source + "] " + e.Message);
        }

        public static void Log(string text, bool isException)
        {
            using (StreamWriter sw = DuckData.LogFile.AppendText())
                if (isException)
                {
                    sw.WriteLine("[" + LogSeverity.Error + "] [" + DateTime.UtcNow.ToShortTimeString() + "] " + text);
                    sw.WriteLine();
                }
                else sw.WriteLine(text);
        }

        public static User FindUser(Server srv, string user)
        {
            if (!string.IsNullOrWhiteSpace(user))
                foreach (User u in srv.FindUsers(user)) return u;
            return null;
        }
    }
}