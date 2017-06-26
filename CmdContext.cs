using System;
using System.Resources;
using Discord;

namespace DuckBot
{
    public class CmdContext
    {
        public enum CmdError { None, NoAccess, ArgCount }

        public string Args { get; }
        public IGuild Server { get; }
        public ITextChannel Channel { get; }
        public IGuildUser Sender { get; }
        public Session Session { get; }
        public bool AdvancedUser { get; }
        
        public CmdError Result { get; set; }
        public bool Processed { get; set; }

        public CmdContext(IMessage message, Session session, bool advancedUser)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            int ix = message.Content.IndexOf(' ');
            Args = ix == -1 ? "" : message.Content.Substring(ix + 1);
            Channel = (ITextChannel)message.Channel;
            Sender = (IGuildUser)message.Author;
            Server = Sender.Guild;
            Session = session;
            Processed = false;
            AdvancedUser = advancedUser;
        }

        public CmdContext(CmdContext copy, string args)
        {
            if (copy == null) throw new ArgumentNullException(nameof(copy));
            Args = args;
            Server = copy.Server;
            Channel = copy.Channel;
            Sender = copy.Sender;
            Session = copy.Session;
            Processed = false;
            AdvancedUser = copy.AdvancedUser;
        }

        public string GetString(string name, params object[] args) => Session.GetString(name, args);
    }
}
