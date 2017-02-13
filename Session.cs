using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DuckBot
{
    public sealed class Session
    {
        private static readonly int Version = -1;

        public readonly ulong ServerID;
        public readonly Dictionary<string, Command> Cmds;
        public readonly Dictionary<string, string> Vars;
        internal readonly Dictionary<ulong, Inbox> Msgs;

        public bool ShowChanges { get; internal set; }

        public bool PendingSave { get; private set; }

        public int PesistVars
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
            ServerID = sid;
            Cmds = new Dictionary<string, Command>();
            Vars = new Dictionary<string, string>();
            Msgs = new Dictionary<ulong, Inbox>();
            ShowChanges = false;
            PendingSave = false;
        }

        public void SetPending() { PendingSave = true; }

        public void Load(BinaryReader br)
        {
            int ver = br.ReadInt32();
            int count = ver == Version ? br.ReadInt32() : ver;
            lock (this)
            {
                while (count-- > 0)
                {
                    string name = br.ReadString();
                    Command cmd = new Command();
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
                if (ver == Version)
                {
                    count = br.ReadInt32();
                    while (count-- > 0)
                    {
                        string name = br.ReadString();
                        string value = br.ReadString();
                        Vars.Add(name, value);
                    }
                }
            }
        }

        public void SaveAsync()
        {
            Task.Run((System.Action)Save);
        }

        public void Save()
        {
            string file = Path.Combine(DuckData.SessionsDir.FullName, "session_" + ServerID + ".dat");
            lock (this)
                using (BinaryWriter bw = new BinaryWriter(new FileStream(file, FileMode.Create, FileAccess.Write)))
                {
                    PendingSave = false;
                    bw.Write(Version);
                    bw.Write(Cmds.Count);
                    foreach (KeyValuePair<string, Command> kvp in Cmds)
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
                    bw.Write(PesistVars);
                    foreach (KeyValuePair<string, string> kvp in Vars)
                        if (!kvp.Key.StartsWith("_"))
                        {
                            bw.Write(kvp.Key);
                            bw.Write(kvp.Value);
                        }
                }
        }

        public string AddMessage(ulong sender, ulong recv, string msg)
        {
            Inbox i;
            lock (this)
                if (!Msgs.ContainsKey(recv))
                {
                    i = new Inbox();
                    Msgs.Add(recv, i);
                }
                else i = Msgs[recv];
            return i.AddMessage(sender, msg);
        }
    }
}