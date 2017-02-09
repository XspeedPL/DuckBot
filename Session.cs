using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace DuckBot
{
    class Session : IBinary
    {
        public readonly ulong ServerID;
        public readonly Dictionary<string, Command> Cmds;
        public readonly Dictionary<ulong, Inbox> Msgs;

        public bool ShowChanges { get; internal set; }

        public Session(ulong sid)
        {
            ServerID = sid;
            Cmds = new Dictionary<string, Command>();
            Msgs = new Dictionary<ulong, Inbox>();
            ShowChanges = false;
        }

        public void Load(BinaryReader br)
        {
            int count = br.ReadInt32();
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
        }

        public void Save(BinaryWriter bw)
        {
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
        }

        public string AddMessage(ulong sender, ulong recv, string msg)
        {
            Inbox i;
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
