using Discord;

namespace DuckBot
{
    public struct CmdParams
    {
        public string Args { get; private set; }
        public Server Server { get; private set; }
        public Channel Channel { get; private set; }
        public User Sender { get; private set; }

        public CmdParams(MessageEventArgs e)
        {
            int ix = e.Message.RawText.IndexOf(' ');
            Args = ix == -1 ? "" : e.Message.RawText.Substring(ix + 1);
            Server = e.Server;
            Channel = e.Channel;
            Sender = e.User;
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
