using System.IO;
using System.Collections.Generic;
using Discord;

namespace DuckBot
{
    internal sealed class Inbox : IBinary
    {
        private readonly Dictionary<ulong, string[]> queue = new Dictionary<ulong, string[]>();

        public void Load(BinaryReader br)
        {
            int count = br.ReadInt32();
            lock (this)
                while (count-- > 0)
                {
                    ulong sender = br.ReadUInt64();
                    string[] msgs = new string[5];
                    for (int i = 0; i < 5; ++i)
                        msgs[i] = br.ReadString();
                    queue.Add(sender, msgs);
                }
        }

        public void Save(BinaryWriter bw)
        {
            lock (this)
            {
                bw.Write(queue.Count);
                foreach (KeyValuePair<ulong, string[]> kvp in queue)
                {
                    bw.Write(kvp.Key);
                    for (int i = 0; i < 5; ++i)
                        bw.Write(kvp.Value[i]);
                }
            }
        }

        public string AddMessage(ulong sender, string msg)
        {
            lock (this)
            {
                if (!queue.ContainsKey(sender))
                    queue.Add(sender, new string[] { "", "", "", "", "" });
                string[] arr = queue[sender];
                string removed = arr[0];
                System.Array.Copy(arr, 1, arr, 0, 4);
                arr[4] = msg.Trim();
                return removed;
            }
        }

        public void Deliver(Server srv, Channel recipent, bool remove = true)
        {
            lock (this)
                foreach (KeyValuePair<ulong, string[]> kvp in queue)
                {
                    string sender = srv.GetUser(kvp.Key).Name;
                    string msg = "Messages from `" + sender + "`:";
                    for (int i = 0; i < 5; ++i)
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Value[i]))
                            msg += "\n - " + kvp.Value[i];
                    }
                    recipent.SendMessage(msg);
                }
            if (remove) queue.Clear();
        }
    }
}
