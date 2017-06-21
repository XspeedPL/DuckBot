using System;
using System.IO;
using System.Threading;
using System.Resources;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Discord;

namespace DuckBot
{
    internal sealed class Program : IDisposable
    {
        public static readonly Random Rand = new Random();
        internal static Program Inst = null;

        public static ResourceManager ResourceManager { get; private set; }

        private readonly Discord.WebSocket.DiscordSocketClient client;
        private readonly DuckData data;
        private readonly string token;
        private readonly ConfiguredTaskAwaitable loopTask;
        private readonly CancellationTokenSource bgCancel;
        private readonly Logger stateLog;

        internal Dictionary<string, HardCmd> HardCmds { get; private set; }

        public bool End { get; private set; }

        static void Main()
        {
            ResourceManager = new ResourceManager("DuckBot.Resources.Strings", typeof(Resources.Strings).Assembly);
            Console.Title = "DuckBot";
            if (DuckData.StateLogFile.Exists) DuckData.StateLogFile.Delete();
            if (!DuckData.TokenFile.Exists) Log(new FileNotFoundException(ResourceManager.GetString("start_err_notoken")));
            else
            {
                string token = File.ReadAllText(DuckData.TokenFile.FullName, System.Text.Encoding.UTF8);
                using (Inst = new Program(token))
                {
                    Inst.LoadData();
                    Inst.StartAsync();
                    while (!Inst.End) Thread.Sleep(666);
                }
            }
        }

        private Program(string userToken)
        {
            End = false;
            stateLog = new Logger(DuckData.StateLogFile.CreateText(), Console.Out);
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
            loopTask = AsyncSaver(bgCancel.Token).ConfigureAwait(false);
        }
        
        public async Task AsyncSaver(CancellationToken cancelToken)
        {
            int i = 0;
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, cancelToken);
                    await stateLog.Output();
                    if (++i % 120 == 0)
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
            Log(LogSeverity.Info, ResourceManager.GetString("exit_start"));
            lock (data.ServerSessions)
                foreach (Session s in data.ServerSessions.Values)
                    s.Dispose();
            client.LogoutAsync().GetAwaiter().GetResult();
            client.StopAsync().GetAwaiter().GetResult();
            client.Dispose();
            bgCancel.Cancel();
            loopTask.GetAwaiter().GetResult();
            bgCancel.Dispose();
            stateLog.Dispose();
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
                catch (Exception ex) { Log(new FileLoadException(string.Format(ResourceManager.GetString("start_err_fileload"), DuckData.WhitelistFile.Name), ex)); }
            foreach (FileInfo fi in DuckData.SessionsDir.EnumerateFiles("session_*.dat"))
                try
                {
                    ulong id = ulong.Parse(fi.Name.Remove(fi.Name.Length - 4).Substring(8));
                    Session s = new Session(id);
                    using (BinaryReader br = new BinaryReader(fi.OpenRead()))
                        s.Load(br);
                    data.ServerSessions.Add(id, s);
                }
                catch (Exception ex) { Log(new FileLoadException(string.Format(ResourceManager.GetString("start_err_fileload"), fi.Name), ex)); }
        }

        private async void StartAsync()
        {
            Log(LogSeverity.Info, string.Format(ResourceManager.GetString("start_info"), Console.Title));
            Console.CancelKeyPress += Console_CancelKeyPress;
            client.MessageReceived += MessageRecieved;
            client.GuildMemberUpdated += GuildMemberUpdated;
            client.GuildAvailable += (guild) =>
            {
                CreateSession(guild).AutoJoinAudioAsync(guild).ConfigureAwait(false);
                return Task.CompletedTask;
            };
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
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
                    IDMChannel toSend = await after.GetOrCreateDMChannelAsync();
                    i.Deliver(s, after.Guild, toSend);
                    lock (s.Msgs) s.Msgs.Remove(after.Id);
                    s.SetPending();
                }
            }
        }
        
        private Task MessageRecieved(IMessage msg)
        {
            Session ss = CreateSession(((IGuildChannel)msg.Channel).Guild);
            if (msg.Author.Id != client.CurrentUser.Id && msg.Content.StartsWith(ss.CommandPrefix))
                MessageReceivedInner(msg).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        private async Task MessageReceivedInner(IMessage msg)
        {
            Session session = CreateSession(((IGuildChannel)msg.Channel).Guild);
            string cmdName = msg.Content.Substring(session.CommandPrefix.Length);
            int ix = cmdName.IndexOf(' ');
            if (ix != -1) cmdName = cmdName.Remove(ix);
            cmdName = cmdName.ToLowerInvariant();
            try
            {
                ICmd cmd = HardCmds.ContainsKey(cmdName) ? (ICmd)HardCmds[cmdName] : session.Cmds.ContainsKey(cmdName) ? session.Cmds[cmdName] : null;
                if (cmd != null)
                {
                    await msg.Channel.TriggerTypingAsync();
                    CmdContext ctx = new CmdContext(msg, session, data.AdvancedUsers.Contains(msg.Author.Id));
                    string res = cmd.Run(ctx);
                    if (ctx.Result == CmdContext.CmdError.None)
                    {
                        bool empty = string.IsNullOrWhiteSpace(res);
                        if (!empty || !ctx.Processed)
                        {
                            if (!empty && res.Length > 2000) res = res.Remove(2000);
                            await ctx.Channel.SendMessageAsync(string.IsNullOrWhiteSpace(res) ? ctx.GetString("ret_empty_cmd") : res);
                        }
                    }
                    else if (ctx.Result == CmdContext.CmdError.NoAccess)
                        await msg.Channel.SendMessageAsync(ctx.GetString("err_notadmin"));
                    else if (ctx.Result == CmdContext.CmdError.ArgCount)
                        await msg.Channel.SendMessageAsync(ctx.GetString("err_params"));
                    //else if (ctx.Result == CmdContext.CmdError.BadFormat)
                    //    await msg.Channel.SendMessageAsync(ctx.GetString("err_badformat"));
                }
            }
            catch (Exception ex)
            {
                Log(ex);
                await msg.Channel.SendMessageAsync(session.GetString("err_generic") + ": " + ex.Message);
            }
        }

        public static Task Log(LogMessage e)
        {
            Inst.Log(e.Severity, "[" + e.Source + "] " + e.Message);
            return Task.CompletedTask;
        }

        public static void Log(Exception ex)
        {
            Inst.Log(LogSeverity.Error, ex + "\n");
        }

        public void Log(LogSeverity severity, string text)
        {
            text = "[" + DateTime.UtcNow.ToShortTimeString() + "] [" + severity + "] " + text;
            stateLog.Log(text);
        }
    }
}