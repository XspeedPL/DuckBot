using Discord;

namespace DuckBot
{
    public struct CmdParams
    {
        public readonly string args;
        public readonly Server server;
        public readonly Channel channel;
        public readonly User sender;

        public CmdParams(MessageEventArgs e)
        {
            int ix = e.Message.RawText.IndexOf(' ');
            args = ix == -1 ? "" : e.Message.RawText.Substring(ix + 1);
            server = e.Server;
            channel = e.Channel;
            sender = e.User;
        }

        public CmdParams(CmdParams copy, string text)
        {
            args = text;
            server = copy.server;
            channel = copy.channel;
            sender = copy.sender;
        }
    }
}
