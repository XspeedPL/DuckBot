using System.IO;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord.Audio;

namespace DuckBot
{
    public sealed class Session : System.IDisposable
    {
        private static readonly int Version = 2;

        public ulong ServerId { get; private set; }
        public Dictionary<string, SoftCmd> Cmds { get; private set; }
        public Dictionary<string, string> Vars { get; private set; }
        internal Dictionary<ulong, Inbox> Msgs { get; private set; }

        internal Audio.AudioStreamer AudioPlayer { get; set; }

        public bool ShowChanges { get; internal set; }
        public CultureInfo Language { get; private set; }
        public bool PendingSave { get; private set; }
        public string MusicChannel { get; internal set; }
        public string CommandPrefix { get; internal set; }

        public int PersistentVars
        {
            get
            {
                int ret = 0;
                lock (this)
                    foreach (string var in Vars.Keys)
                        if (!var.StartsWith("_")) ++ret;
                return ret;
            }
        }

        public Session(ulong sid)
        {
            ServerId = sid;
            Cmds = new Dictionary<string, SoftCmd>();
            Vars = new Dictionary<string, string>();
            Msgs = new Dictionary<ulong, Inbox>();
            ShowChanges = false;
            PendingSave = false;
            Language = new CultureInfo("en-US");
            MusicChannel = "";
            CommandPrefix = ">";
        }

        public void SetPending() { PendingSave = true; }

        public bool SetLanguage(string langCode)
        {
            if (Utils.IsCultureAvailable(langCode, out CultureInfo culture))
            {
                Language = culture;
                SetPending();
                return true;
            }
            else return false;
        }

        internal void Load(BinaryReader br)
        {
            int ver = br.ReadInt32();
            int count = br.ReadInt32();
            lock (this)
            {
                while (count-- > 0)
                {
                    string name = br.ReadString();
                    SoftCmd cmd = new SoftCmd();
                    cmd.Load(br);
                    Cmds.Add(name, cmd);
                }
                count = br.ReadInt32();
                while (count-- > 0)
                {
                    ulong user = br.ReadUInt64();
                    Inbox inb = new Inbox();
                    inb.Load(br);
                    Msgs.Add(user, inb);
                }
                ShowChanges = br.ReadBoolean();
                count = br.ReadInt32();
                while (count-- > 0)
                {
                    string name = br.ReadString();
                    string value = br.ReadString();
                    Vars.Add(name, value);
                }
                SetLanguage(br.ReadString());
                MusicChannel = ver >= 1 ? br.ReadString() : "";
                CommandPrefix = ver >= 2 ? br.ReadString() : ">";
            }
        }

        public void Save()
        {
            string file = Path.Combine(DuckData.SessionsDir.FullName, "session_" + ServerId + ".dat");
            lock (this)
                using (BinaryWriter bw = new BinaryWriter(File.Open(file, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    PendingSave = false;
                    bw.Write(Version);
                    bw.Write(Cmds.Count);
                    foreach (KeyValuePair<string, SoftCmd> kvp in Cmds)
                    {
                        bw.Write(kvp.Key);
                        kvp.Value.Save(bw);
                    }
                    bw.Write(Msgs.Count);
                    foreach (KeyValuePair<ulong, Inbox> kvp in Msgs)
                    {
                        bw.Write(kvp.Key);
                        kvp.Value.Save(bw);
                    }
                    bw.Write(ShowChanges);
                    bw.Write(PersistentVars);
                    foreach (KeyValuePair<string, string> kvp in Vars)
                        if (!kvp.Key.StartsWith("_"))
                        {
                            bw.Write(kvp.Key);
                            bw.Write(kvp.Value);
                        }
                    bw.Write(Language.Name);
                    bw.Write(MusicChannel);
                    bw.Write(CommandPrefix);
                }
        }

        public string AddMessage(ulong sender, ulong recipient, string message)
        {
            Inbox i;
            lock (this)
                if (!Msgs.ContainsKey(recipient))
                {
                    i = new Inbox();
                    Msgs.Add(recipient, i);
                }
                else i = Msgs[recipient];
            return i.AddMessage(sender, message);
        }

        public async Task JoinAudioAsync(Discord.IVoiceChannel channel)
        {
            await LeaveAudioAsync();
            IAudioClient client = await channel.ConnectAsync();
            if (AudioPlayer == null) AudioPlayer = new Audio.AudioStreamer(client);
            else
            {
                await AudioPlayer.StopAsync();
                AudioPlayer.AudioClient = client;
            }
        }

        public async Task LeaveAudioAsync()
        {
            if (AudioPlayer != null)
            {
                await AudioPlayer.DisposeAsync();
                AudioPlayer = null;
            }
        }

        internal async Task AutoJoinAudioAsync(Discord.IGuild srv)
        {
            await Task.Delay(666);
            foreach (Discord.IVoiceChannel c in await srv.GetVoiceChannelsAsync())
                if (c.Name.Equals(MusicChannel, System.StringComparison.OrdinalIgnoreCase))
                {
                    await JoinAudioAsync(c);
                    break;
                }
        }

        public async Task PlayAudioAsync(string stream)
        {
            await AudioPlayer.StopAsync();
            await AudioPlayer.PlayAsync(new System.Uri(stream)).ConfigureAwait(false);
        }

        public void Dispose()
        {
            try { if (AudioPlayer != null) AudioPlayer.DisposeAsync().GetAwaiter().GetResult(); }
            catch { }
        }

        public string GetString(string name) => Program.ResourceManager.GetString(name, Language);

        public string GetString(string name, params object[] args) => string.Format(GetString(name), args);
    }
}