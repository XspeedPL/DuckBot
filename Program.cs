using System;
using System.IO;
using System.Threading;
using System.Globalization;
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

        private readonly Discord.WebSocket.DiscordSocketClient client;
        private readonly DuckData data;
        private readonly string token;
        private readonly Task bgSaver;
        private readonly CancellationTokenSource bgCancel;
        private readonly StreamWriter log;

        internal Dictionary<string, HardCmd> HardCmds { get; private set; }

        public bool End { get; private set; }
        
        static void Main()
        {
            Console.Title = "DuckBot";
            if (!DuckData.LogFile.Exists) File.WriteAllText(DuckData.LogFile.FullName, "");
            if (!DuckData.TokenFile.Exists) Log(new FileNotFoundException(Strings.start_err_notoken));
            else
            {
                string token = File.ReadAllText(DuckData.TokenFile.FullName, System.Text.Encoding.UTF8);
                using (Inst = new Program(token))
                {
                    Inst.LoadData();
                    Inst.Start();
                }
            }
        }

        private Program(string userToken)
        {
            End = false;
            log = DuckData.LogFile.AppendText();
            client = new Discord.WebSocket.DiscordSocketClient(new Discord.WebSocket.DiscordSocketConfig()
            {
                DefaultRetryMode = RetryMode.AlwaysRetry,
                LogLevel = LogSeverity.Info
            });
            client.Log += Log;
            token = userToken;
            data = new DuckData();
            HardCmds = HardCmd.CreateDefault();
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
                            if (s.PendingSave) Task.Run((Action)s.Save);
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
            lock (data.ServerSessions)
                foreach (Session s in data.ServerSessions.Values)
                    s.Dispose();
            client.LogoutAsync().Wait();
            client.StopAsync();
            client.Dispose();
            bgCancel.Cancel();
            bgSaver.Wait();
            bgSaver.Dispose();
            bgCancel.Dispose();
            Log(LogSeverity.Info, Strings.exit_end);
            log.Close();
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
                catch (Exception ex) { Log(new FileLoadException(string.Format(Strings.start_err_fileload, DuckData.WhitelistFile.Name), ex)); }
            foreach (FileInfo fi in DuckData.SessionsDir.EnumerateFiles("session_*.dat"))
                try
                {
                    ulong id = ulong.Parse(fi.Name.Remove(fi.Name.Length - 4).Substring(8));
                    Session s = new Session(id);
                    using (BinaryReader br = new BinaryReader(fi.OpenRead()))
                        s.Load(br);
                    data.ServerSessions.Add(id, s);
                }
                catch (Exception ex) { Log(new FileLoadException(string.Format(Strings.start_err_fileload, fi.Name), ex)); }
        }

        private void Start()
        {
            Log(LogSeverity.Info, string.Format(Strings.start_info, Console.Title));
            Console.CancelKeyPress += Console_CancelKeyPress;
            client.MessageReceived += MessageRecieved;
            client.GuildMemberUpdated += GuildMemberUpdated;
            client.GuildAvailable += (guild) =>
            {
                Task.Run(() => CreateSession(guild).AutoJoinAudio(guild));
                return Task.CompletedTask;
            };
            client.LoginAsync(TokenType.Bot, token).Wait();
            client.StartAsync();
            while (!End) Thread.Sleep(666);
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            End = true;
            e.Cancel = true;
        }

        internal bool IsAdvancedUser(ulong id) => data.AdvancedUsers.Contains(id);

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
            catch (IOException ex) { Log(ex); }
        }

        internal Session CreateSession(IGuild srv)
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

        private async Task GuildMemberUpdated(IGuildUser before, IGuildUser after)
        {
            if (!before.UserActive() && after.UserActive())
            {
                Session s = CreateSession(after.Guild);
                if (s.Msgs.ContainsKey(after.Id))
                {
                    Inbox i = s.Msgs[after.Id];
                    IDMChannel toSend = await after.CreateDMChannelAsync();
                    i.Deliver(after.Guild, toSend);
                    lock (s.Msgs) s.Msgs.Remove(after.Id);
                    s.SetPending();
                }
            }
        }
        
        private Task MessageRecieved(IMessage msg)
        {
            Session ss = CreateSession(((IGuildChannel)msg.Channel).Guild);
            if (msg.Author.Id != client.CurrentUser.Id && msg.Content.StartsWith(ss.CommandPrefix))
                Task.Run(async () =>
                {
                    string cmd = msg.Content.Substring(ss.CommandPrefix.Length);
                    int ix = cmd.IndexOf(' ');
                    if (ix != -1) cmd = cmd.Remove(ix);
                    cmd = cmd.ToLowerInvariant();
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(ss.Language);
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentCulture;
                    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture;
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentCulture;
                    try
                    {
                        if (HardCmds.ContainsKey(cmd))
                        {
                            HardCmd hcmd = HardCmds[cmd];
                            IGuildUser guser = (IGuildUser)msg.Author;
                            if (hcmd.AdminOnly && (!guser.GuildPermissions.Administrator || msg.Author.IsBot) && !data.AdvancedUsers.Contains(msg.Author.Id))
                                await msg.Channel.SendMessageAsync(Strings.err_notadmin);
                            else
                            {
                                string[] args = ix == -1 ? new string[0] : msg.Content.Substring(ix + 2).Split(new char[] { ' ' }, hcmd.ArgsMax, StringSplitOptions.RemoveEmptyEntries);
                                if (args.Length >= hcmd.ArgsMin)
                                {
                                    await msg.Channel.TriggerTypingAsync();
                                    string res = hcmd.Func(args, new CmdContext(msg, ss));
                                    if (res != null)
                                        await msg.Channel.SendMessageAsync(string.IsNullOrWhiteSpace(res) ? Strings.ret_empty_cmd : res);
                                }
                                else await msg.Channel.SendMessageAsync(Strings.err_params);
                            }
                        }
                        else if (ss.Cmds.ContainsKey(cmd))
                        {
                            await msg.Channel.TriggerTypingAsync();
                            string res = ss.Cmds[cmd].Run(new CmdContext(msg, ss));
                            if (res != null)
                                await msg.Channel.SendMessageAsync(string.IsNullOrWhiteSpace(res) ? Strings.ret_empty_cmd : res);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex);
                        await msg.Channel.SendMessageAsync(Strings.err_generic + ": " + ex.Message);
                    }
                });
            return Task.CompletedTask;
        }

        public static Task Log(LogMessage e) => Task.Run(() => Inst.Log(e.Severity, "[" + e.Source + "] " + e.Message));

        public static void Log(Exception ex)
        {
            Inst.Log(LogSeverity.Error, ex + "\n");
        }

        public void Log(LogSeverity severity, string text)
        {
            text = "[" + DateTime.UtcNow.ToShortTimeString() + "] [" + severity + "] " + text;
            lock (log)
            {
                log.WriteLine(text);
                Console.WriteLine(text);
            }
        }
    }
}