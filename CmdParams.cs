using Discord;

namespace DuckBot
{
    public struct CmdParams
    {
        public string Args { get; private set; }
        public IGuild Server { get; private set; }
        public ITextChannel Channel { get; private set; }
        public IGuildUser Sender { get; private set; }

        public CmdParams(IMessage msg)
        {
            int ix = msg.Content.IndexOf(' ');
            Args = ix == -1 ? "" : msg.Content.Substring(ix + 1);
            Channel = (ITextChannel)msg.Channel;
            Sender = (IGuildUser)msg.Author;
            Server = Sender.Guild;
        }

        public CmdParams(CmdParams copy, string text)
        {
            Args = text;
            Server = copy.Server;
            Channel = copy.Channel;
            Sender = copy.Sender;
        }
    }
}
