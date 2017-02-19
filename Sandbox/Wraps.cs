using System;
using System.IO;
using System.Collections.Generic;

namespace DuckBot.Sandbox
{
    public sealed class Server : MarshalByRefObject
    {
        private readonly Discord.Server inst;

        public Server(Discord.Server srv) { inst = srv; }

        public static implicit operator Server(Discord.Server srv) => new Server(srv);

        public IEnumerable<Channel> FindChannels(string name, string type = null, bool exactMatch = false)
        {
            foreach (var channel in inst.FindChannels(name, type, exactMatch)) yield return channel;
        }

        public IEnumerable<User> FindUsers(string name, bool exactMatch = false)
        {
            foreach (var user in inst.FindUsers(name, exactMatch)) yield return user;
        }

        public override string ToString() { return inst.ToString(); }

        public int ChannelCount => inst.ChannelCount;
        public User CurrentUser => inst.CurrentUser;
        public ulong Id => inst.Id;
        public string Name => inst.Name;
        public User Owner => inst.Owner;
        public int UserCount => inst.UserCount;
    }

    public sealed class Channel : MarshalByRefObject
    {
        private readonly Discord.Channel inst;

        public Channel(Discord.Channel chn) { inst = chn; }

        public static implicit operator Channel(Discord.Channel chn) => new Channel(chn);

        public IEnumerable<User> FindUsers(string name, bool exactMatch = false)
        {
            foreach (var user in inst.FindUsers(name, exactMatch)) yield return user;
        }

        public Message SendFile(string filename, Stream stream) { return inst.SendFile(filename, stream).Result; }

        public void SendIsTyping() { inst.SendIsTyping(); }

        public Message SendMessage(string text) { return inst.SendMessage(text).Result; }

        public Message SendTTSMessage(string text) { return inst.SendTTSMessage(text).Result; }

        public override string ToString() { return inst.ToString(); }

        public ulong Id => inst.Id;
        public bool IsPrivate => inst.IsPrivate;
        public string Mention => inst.Mention;
        public IEnumerable<Message> Messages { get { foreach (var message in inst.Messages) yield return message; } }
        public string Name => inst.Name;
        public int Position => inst.Position;
        public User Recipient => inst.Recipient;
        public Server Server => inst.Server;
        public string Topic => inst.Topic;
        public string Type => inst.Type.Value;
        public IEnumerable<User> Users { get { foreach (var user in inst.Users) yield return user; } }
    }

    public sealed class User : MarshalByRefObject
    {
        private readonly Discord.User inst;

        public User(Discord.User usr) { inst = usr; }

        public static implicit operator User(Discord.User usr) => new User(usr);

        public Channel CreatePMChannel() { return inst.CreatePMChannel().Result; }

        public Message SendFile(string filename, Stream stream) { return inst.SendFile(filename, stream).Result; }

        public Message SendMessage(string text) { return inst.SendMessage(text).Result; }

        public override string ToString() { return inst.ToString(); }

        public string AvatarId => inst.AvatarId;
        public string AvatarUrl => inst.AvatarUrl;
        public IEnumerable<Channel> Channels { get { foreach (var channel in inst.Channels) yield return channel; } }
        public ushort Discriminator => inst.Discriminator;
        public ulong Id => inst.Id;
        public bool IsBot => inst.IsBot;
        public DateTime JoinedAt => inst.JoinedAt;
        public DateTime? LastActivityAt => inst.LastActivityAt;
        public DateTime? LastOnlineAt => inst.LastOnlineAt;
        public string Mention => inst.Mention;
        public string Name => inst.Name;
        public string Nickname => inst.Nickname;
        public string NicknameMention => inst.NicknameMention;
        public Channel PrivateChannel => inst.PrivateChannel;
        public Server Server => inst.Server;
        public string Status => inst.Status.Value;
        public Channel VoiceChannel => inst.VoiceChannel;
    }

    public sealed class Message : MarshalByRefObject
    {
        private readonly Discord.Message inst;

        public Message(Discord.Message msg) { inst = msg; }

        public static implicit operator Message(Discord.Message msg) => new Message(msg);

        public override string ToString() { return inst.ToString(); }

        public Channel Channel => inst.Channel;
        public DateTime? EditedTimestamp => inst.EditedTimestamp;
        public ulong Id => inst.Id;
        public bool IsAuthor => inst.IsAuthor;
        public bool IsTTS => inst.IsTTS;
        public IEnumerable<Channel> MentionedChannels { get { foreach (var channel in inst.MentionedChannels) yield return channel; } }
        public IEnumerable<User> MentionedUsers { get { foreach (var user in inst.MentionedUsers) yield return user; } }
        public string RawText => inst.RawText;
        public Server Server => inst.Server;
        public Discord.MessageState State => inst.State;
        public string Text => inst.Text;
        public DateTime Timestamp => inst.Timestamp;
        public User User => inst.User;
    }
}
