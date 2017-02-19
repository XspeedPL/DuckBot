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
        internal static TextWriter StdOut;

        private readonly Dictionary<string, HardCmd> hardCmds;
        private readonly DiscordClient client;
        private readonly DuckData data;
        private readonly string token, prefix;
        private readonly Task bgSaver;
        private readonly CancellationTokenSource bgCancel;
        
        static void Main(string[] args)
        {
            Console.Title = "DuckBot";
            StdOut = Console.Out;
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
            hardCmds = HardCmd.CreateDefault();
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

        internal bool IsAdvancedUser(ulong id)
        {
            lock (data.AdvancedUsers)
                return data.AdvancedUsers.Contains(id);
        }

        internal void AddAdvancedUser(ulong id)
        {
            try
            {
                lock (data.AdvancedUsers)
                {
                    data.AdvancedUsers.Add(id);
                    using (StreamWriter sw = new StreamWriter(new FileStream(DuckData.WhitelistFile.FullName, FileMode.Create, FileAccess.Write)))
                        foreach (ulong u in data.AdvancedUsers)
                            sw.WriteLine(u.ToString());
                }
            }
            catch (Exception ex) { Log(ex); }
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
            StdOut.WriteLine(text);
        }

        public static User FindUser(Server srv, string user)
        {
            if (!string.IsNullOrWhiteSpace(user))
                foreach (User u in srv.FindUsers(user)) return u;
            return null;
        }
    }
}