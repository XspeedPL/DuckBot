using System;
using Discord;

namespace DuckBot
{
    public class CmdContext
    {
        public string Args { get; private set; }
        public IGuild Server { get; private set; }
        public ITextChannel Channel { get; private set; }
        public IGuildUser Sender { get; private set; }
        public bool Processed { get; set; }

        public Session Session { get; private set; }

        public CmdContext(IMessage message, Session ss)
        {
            if (message == null) throw new ArgumentNullException("message");
            int ix = message.Content.IndexOf(' ');
            Args = ix == -1 ? "" : message.Content.Substring(ix + 1);
            Channel = (ITextChannel)message.Channel;
            Sender = (IGuildUser)message.Author;
            Server = Sender.Guild;
            Session = ss;
            Processed = false;
        }

        public CmdContext(CmdContext copy, string text)
        {
            if (copy == null) throw new ArgumentNullException("copy");
            Args = text;
            Server = copy.Server;
            Channel = copy.Channel;
            Sender = copy.Sender;
            Session = copy.Session;
            Processed = false;
        }
    }
}
