using System;
using System.Text;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using System.IO;

namespace DuckBot
{
    internal sealed class Program
    {
        public static readonly Random Rand = new Random();
        internal static Program Inst = null;
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
            dClient.UsingCommands(x =>
            {
                x.PrefixChar = '>';
                x.AllowMentionPrefix = true;
                x.HelpMode = HelpMode.Public;
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
            CommandService svc = dClient.GetService<CommandService>();

            svc.CreateCommand("add")
                .Description("Adds a function definition")
                .Parameter("type", ParameterType.Required)
                .Parameter("name", ParameterType.Required)
                .Parameter("content", ParameterType.Unparsed)
                .Do(async e =>
                {
                    Session s = CreateSession(e.Server);
                    string cmd = e.Args[1].ToLowerInvariant();

                    string oldContent = " ";
                    if (e.Args[0].ToLower() == "csharp")
                    {
                        if (data.AdvancedUsers.Contains(e.User.Id))
                        {
                            if (e.User.Id != DuckData.SuperUserId && (e.Args[2].Contains("Assembly") || e.Args[2].Contains("System.IO") || e.Args[2].Contains("Environment")))
                            {
                                await e.Channel.SendMessage("Unpermitted keywords have been found in the code.");
                                return;
                            }
                            else s.Cmds.Add(cmd, new Command(e.Args[0], e.Args[2], e.User.Name));
                        }
                        else
                        {
                            await e.Channel.SendMessage("You don't have sufficent permissons.");
                            return;
                        }
                    }
                    else if (e.Args[0].ToLower() == "whitelist")
                    {
                        if (e.User.Id == DuckData.SuperUserId)
                        {
                            data.AdvancedUsers.Add(ulong.Parse(e.Args[1]));
                            SaveWhitelist();
                            await e.Channel.SendMessage("The ID has been added to whitelist!");
                        }
                        else await e.Channel.SendMessage("You don't have sufficent permissons.");
                        return;
                    }
                    else if (s.Cmds.ContainsKey(cmd))
                    {
                        oldContent = s.Cmds[cmd].Content;
                        s.Cmds[cmd] = new Command(e.Args[0], e.Args[2], e.User.Name);
                    }
                    else s.Cmds.Add(cmd, new Command(e.Args[0], e.Args[2], e.User.Name));
                    await s.SaveAsync();
                    string toSend;
                    if (s.ShowChanges) toSend = "Changed from : ```" + oldContent + "``` to ```" + s.Cmds[e.Args[1]].Content + "```";
                    else toSend = "Done!";
                    await e.Channel.SendMessage(toSend);
                });
            svc.CreateCommand("remove")
                .Description("Removes a function")
                .Parameter("name", ParameterType.Required)
                .Do(async e =>
                {
                    Session s = CreateSession(e.Server);
                    string cmd = e.Args[0].ToLowerInvariant();
                    if (s.Cmds.ContainsKey(cmd))
                    {
                        string log = s.ShowChanges ? "\nContent: ```" + s.Cmds[cmd].Content + "```" : "";
                        s.Cmds.Remove(cmd);
                        await e.Channel.SendMessage("Removed command `" + cmd + "`" + log);
                        await s.SaveAsync();
                    }
                    else await e.Channel.SendMessage("No command to remove!");
                });

            svc.CreateCommand("info")
               .Description("Info about a function")
               .Parameter("name", ParameterType.Required)
               .Do(async e =>
               {
                   Session s = CreateSession(e.Server);
                   string cmd = e.Args[0].ToLowerInvariant();
                   if (s.Cmds.ContainsKey(cmd))
                   {
                       Command c = s.Cmds[cmd];
                       string prefix = "";
                       if (c.Type == Command.CmdType.Lua) prefix = "lua";
                       else if (c.Type == Command.CmdType.CSharp) prefix = "cs";
                       await e.Channel.SendMessage("Command name: `" + cmd + "`\nCreated: `" + c.CreationDate.ToShortDateString() + "` by `" + c.Creator + "`\nType: `" + c.Type + "`\nContent: ```" + prefix + "\n" + c.Content + "```");
                   }
               });
            svc.CreateCommand("list")
               .Description("Lists all defined functions")
               .Do(async e =>
               {
                   Session s = CreateSession(e.Server);
                   string toSend = "```";
                   int i = 0;
                   foreach (string name in s.Cmds.Keys)
                   {
                       toSend += (++i) + ". " + name + "\n";
                       if (toSend.Length > 1900)
                       {
                           toSend += "```";
                           await e.Channel.SendMessage(toSend);
                           toSend = "```";
                       }
                   }
                   toSend += "```";
                   await e.Channel.SendMessage(toSend);
               });
            svc.CreateCommand("changelog")
               .Description("Enables/Disables showing changes to commands")
               .Parameter("action", ParameterType.Required)
               .Do(async e =>
               {
                   if (e.User.ServerPermissions.Administrator)
                   {
                       Session s = CreateSession(e.Server);
                       string arg = e.Args[0].ToLowerInvariant();
                       if (arg == "enable")
                       {
                           s.ShowChanges = true;
                           await e.Channel.SendMessage("Changelog enabled for server " + e.Server.Name);
                       }
                       else if (arg == "disable")
                       {
                           s.ShowChanges = false;
                           await e.Channel.SendMessage("Changelog disabled for server " + e.Server.Name);
                       }
                       await s.SaveAsync();
                   }
                   else await e.Channel.SendMessage("You don't have sufficent permissons.");
               });
            svc.CreateCommand("inform")
               .Description("Messages a specified user when they appear back online")
               .Parameter("user", ParameterType.Required)
               .Parameter("content", ParameterType.Unparsed)
               .Do(async e =>
               {
                   User u = FindUser(e.Server, e.Args[0]);
                   if (u != null)
                   {
                       Session s = CreateSession(e.Server);
                       string msg = e.Args[1];
                       if (!msg.StartsWith("`")) msg = "`" + msg + "`";
                       msg = "`[" + DateTime.UtcNow.ToShortDateString() + "] #" + e.Channel.Name + ":` " + msg;
                       string removed = s.AddMessage(e.User.Id, u.Id, msg);
                       await e.Channel.SendMessage("Okay, I'll deliver the message when " + u.Name + " goes online");
                       if (!string.IsNullOrWhiteSpace(removed))
                           await e.Channel.SendMessage("This message was removed from the inbox:\n" + removed);
                       await s.SaveAsync();
                   }
                   else await e.Channel.SendMessage("No user " + e.Args[0] + " found on this server!");
               });
        }

        private async void UserUpdated(object sender, UserUpdatedEventArgs e)
        {
            if ((e.Before.Status == UserStatus.Offline || e.Before.Status == UserStatus.Idle) && (e.After.Status == UserStatus.Online))
            {
                Session s = CreateSession(e.Server);
                if (s.Msgs.ContainsKey(e.After.Id))
                {
                    Inbox i = s.Msgs[e.After.Id];
                    Channel toSend = await dClient.CreatePrivateChannel(e.After.Id);
                    i.Deliver(e.Server, toSend);
                    s.Msgs.Remove(e.After.Id);
                    await s.SaveAsync();
                }
            }
        }

        private async void MessageRecieved(object sender, MessageEventArgs e)
        {
            if (e.Message.Text.StartsWith(">") && e.User.Id != dClient.CurrentUser.Id)
            {
                string command = e.Message.Text.Substring(1);
                int ix = command.IndexOf(' ');
                if (ix != -1) command = command.Remove(ix);
                command = command.ToLowerInvariant();
                Session s = CreateSession(e.Server);
                if (s.Cmds.ContainsKey(command))
                {
                    Command c = s.Cmds[command];
                    try
                    {
                        await e.Channel.SendIsTyping();
                        string result = await System.Threading.Tasks.Task.Run(() => c.Run(new CmdParams(e)));
                        await e.Channel.SendMessage(result);
                    }
                    catch (Exception ex)
                    {
                        await e.Channel.SendMessage("Ooops, seems like an exception happened: " + ex.Message);
                        Log(ex.ToString(), true);
                    }
                }
            }
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
                    sw.Write("[" + LogSeverity.Error + "] [" + DateTime.UtcNow.ToShortTimeString() + "] ");
                    sw.WriteLine(text);
                    sw.WriteLine();
                }
                else sw.WriteLine(text);
        }

        public static User FindUser(Server srv, string user)
        {
            if (!string.IsNullOrWhiteSpace(user))
                foreach (User u in srv.FindUsers(user))
                    return u;
            return null;
        }
    }
}