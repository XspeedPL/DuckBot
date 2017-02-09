using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Net;
using Discord.API.Client;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using System.IO;

namespace DuckBot
{
    public class Program
    {

        static void Main(string[] args)
        {
            if (File.Exists(DuckData.saveFile))
            {
                using (StreamReader reader = new StreamReader(DuckData.saveFile))
                {
                    string toDeserialize = reader.ReadToEnd();
                    if (toDeserialize != "") DuckData.commandDB = JsonConvert.DeserializeObject<Dictionary<ulong, Dictionary<string, Command>>>(toDeserialize);
                    reader.Close();
                }

            }
            if (File.Exists(DuckData.changelogsFile))
            {
                using (StreamReader reader = new StreamReader(DuckData.changelogsFile))
                {
                    string toDeserialize = reader.ReadToEnd();
                    if (toDeserialize != "") DuckData.showChanges = JsonConvert.DeserializeObject<Dictionary<ulong, bool>>(toDeserialize);
                    reader.Close();
                }
            }
            else
            {
                File.Create(DuckData.changelogsFile);
            }
            new Program().Start();
        }
        private DiscordClient _client;
        public void Start()
        {
           
            _client = new DiscordClient(x =>
            {
                x.AppName = "DuckBot";
                x.LogLevel = LogSeverity.Info;
                x.LogHandler = Log;

            });
            _client.UsingCommands(x =>
            {
                x.PrefixChar = '>';
                x.AllowMentionPrefix = true;
                x.HelpMode = HelpMode.Public;
            });

            string token = "";
            CreateCommands();
            _client.ExecuteAndWait(async () =>
            {
                await _client.Connect(token, TokenType.Bot);
            });
        }
        public void CreateCommands()
        {
            CommandService cService = _client.GetService<CommandService>();
            _client.MessageReceived += MessageRecieved;
            _client.UserUpdated += UserUpdated;

            cService.CreateCommand("add")
                .Description("Adds a command")
                .Parameter("type", ParameterType.Required)
                .Parameter("name", ParameterType.Required)
                .Parameter("content", ParameterType.Unparsed)
                .Do(async e =>
                {
                    try
                    {
                        if (!DuckData.showChanges.ContainsKey(e.Server.Id))
                        {
                            DuckData.showChanges[e.Server.Id] = false;
                            string save = JsonConvert.SerializeObject(DuckData.showChanges, Formatting.Indented);
                            File.WriteAllText(DuckData.changelogsFile, save);
                        }
                        if (!DuckData.commandDB.ContainsKey(e.Server.Id))
                        {
                            DuckData.commandDB[e.Server.Id] = new Dictionary<string, Command>();
                        }

                        string previousCommand = "";
                        if (DuckData.commandDB.Count > 0)
                        {
                            previousCommand = DuckData.commandDB[e.Server.Id].ContainsKey(e.Args[1]) ? DuckData.commandDB[e.Server.Id][e.Args[1]].Content : "None.";
                            if (DuckData.commandDB[e.Server.Id].ContainsKey(e.Args[1]))
                            {
                                DuckData.commandDB[e.Server.Id].Remove(e.Args[1]);
                            }
                        }
                        if (e.Args[0].ToLower() == "csharp" || e.Args[0].ToLower() == "csharpscript")
                        {
                            if (DuckData.csharpCommandAdders.Contains(e.User.Id))
                            {
                                if (e.User.Id != 168285549088604160 && (e.Args[2].Contains("System.IO") || e.Args[2].Contains("Environment.Exit")))
                                {
                                    await e.Channel.SendMessage("Someting in this code is not permitted to use in DuckBot");
                                    return;
                                }
                                else DuckData.commandDB[e.Server.Id].Add(e.Args[1], new Command(e.Args[0], e.Args[2], e.User.Name));
                            }
                        }
                        else if (e.Args[0].ToLower() == "whitelist" && e.User.Id == 168285549088604160)
                        {
                            DuckData.csharpCommandAdders.Add(ulong.Parse(e.Args[1]));
                        }
                        else
                        {
                            DuckData.commandDB[e.Server.Id].Add(e.Args[1], new Command(e.Args[0], e.Args[2], e.User.Name));
                        }
                        string toSave = JsonConvert.SerializeObject(DuckData.commandDB, Formatting.Indented);

                        File.WriteAllText(DuckData.saveFile, toSave);

                        string toSend = "";

                        if (DuckData.showChanges[e.Server.Id] == true) toSend = $"Changed from : ```{previousCommand}``` to ```{DuckData.commandDB[e.Server.Id][e.Args[1]].Content}```";
                        else toSend = "Done";

                        await e.Channel.SendMessage(toSend);
                    }
                    catch(Exception exception)
                    {
                        Log(exception.ToString(), true);
                    }
                });
            cService.CreateCommand("remove")
                .Description("Removes a command")
                .Parameter("name", ParameterType.Required)
                .Do(async e =>
                {
                    if (!DuckData.showChanges.ContainsKey(e.Server.Id))
                    {
                        DuckData.showChanges[e.Server.Id] = false;
                        string toSave = JsonConvert.SerializeObject(DuckData.showChanges);
                        File.WriteAllText(DuckData.changelogsFile, toSave);
                    }
                    bool check = DuckData.commandDB[e.Server.Id].ContainsKey(e.Args[0]);

                    if (check)
                    {
                        string toSave = JsonConvert.SerializeObject(DuckData.commandDB, Formatting.Indented);
                        File.WriteAllText(DuckData.saveFile, toSave);
                        string log = DuckData.showChanges[e.Server.Id] ? $" Content : ```{DuckData.commandDB[e.Server.Id][e.Args[0]].Content}```" : "";
                        DuckData.commandDB[e.Server.Id].Remove(e.Args[0]);
                        await e.Channel.SendMessage($"Removed command `{e.Args[0]}`." + log);
                    }
                    else await e.Channel.SendMessage("No command to remove!");

                });
            
            cService.CreateCommand("info")
               .Description("Info about a command")
               .Parameter("name", ParameterType.Required)
               .Do(async e =>
               {
                   foreach (string name in DuckData.commandDB[e.Server.Id].Keys)
                   {
                       if (name == e.Args[0])
                       {
                           string prefix = "";
                           switch (DuckData.commandDB[e.Server.Id][name].Type)
                           {
                               case "lua":
                                   prefix = "lua";
                                   break;
                               case "csharpscript":
                                   prefix = "cs";
                                   break;
                               case "csharp":
                                   prefix = "cs";
                                   break;
                           }
                           await e.Channel.SendMessage($"Command name : `{name}` {Environment.NewLine} Created : `{DuckData.commandDB[e.Server.Id][name].CreationDate.ToShortDateString()}` by `{DuckData.commandDB[e.Server.Id][name].Creator}` {Environment.NewLine} Type : `{DuckData.commandDB[e.Server.Id][name].Type}` {Environment.NewLine} Content : ```{prefix}{Environment.NewLine}{DuckData.commandDB[e.Server.Id][name].Content}```");
                       }
                   }
               });
            cService.CreateCommand("list")
               .Description("Sends all commands in PM")
               .Do(async e =>
               {
                   string toSend = "```";

                   int number = 1;
                   foreach (string name in DuckData.commandDB[e.Server.Id].Keys)
                   {
                       toSend += number + ". " + name + Environment.NewLine;  
                       if(toSend.Length > 1900)
                       {
                           toSend += "```";
                           await e.Channel.SendMessage(toSend);
                           toSend = "```";
                       }
                       number++;               
                   }
                   toSend += "```";
                   await e.Channel.SendMessage(toSend);
               });
            cService.CreateCommand("changelog")
               .Description("Enables/Disables showing changes to commands")
               .Parameter("action", ParameterType.Required)
               .Do(async e =>
               {                  
                   if (e.User.ServerPermissions.Administrator)
                   {
                       if (e.Args[0].ToLower() == "enable")
                       {
                           DuckData.showChanges[e.Server.Id] = true;
                           await e.Channel.SendMessage($"Changelog enabled for server {e.Server.Name}");
                       }
                       else if (e.Args[0].ToLower() == "disable")
                       {
                           DuckData.showChanges[e.Server.Id] = false;
                           await e.Channel.SendMessage($"Changelog disabled for server {e.Server.Name}");
                       }
                       string toSave = JsonConvert.SerializeObject(DuckData.showChanges);
                       File.WriteAllText(DuckData.changelogsFile, toSave);
                   }
                   else await e.Channel.SendMessage("You don't have sufficent permissons.");
               });
            cService.CreateCommand("inform")
               .Description("Remembers a message to send to a specific user when they go online")
               .Parameter("user", ParameterType.Required)
               .Parameter("content", ParameterType.Unparsed)
               .Do(async e =>
               {
                   if (FindUser(e, e.Args[0]) != null)
                   {
                       if (!DuckData.messagesToDeliver.Keys.Contains(FindUser(e, e.Args[0]).Id))
                       {
                           DuckData.messagesToDeliver.Add(FindUser(e, e.Args[0]).Id, new List<string>());
                       }
                       DuckData.messagesToDeliver[FindUser(e, e.Args[0]).Id].Add($"From: `{e.User.Name}`\nIn channel: `{e.Server.Name}/{e.Channel.Name}`\nAt: `{DateTime.UtcNow.ToShortDateString()}` UTC\nContent: `{e.Args[1]}`");
                       await e.Channel.SendMessage($"Okay, I'll deliver the message when {e.Args[0]} goes online");
                   }
                   else await e.Channel.SendMessage($"No user {e.Args[0]} found on this server!");
               });
            /*cService.CreateCommand("execute")
               .Description("Executes a lua script")
               .Parameter("script", ParameterType.Unparsed)
               .Do(async e =>
               {
                   await e.Channel.SendMessage(lua.DoString($@"user, userAsMention = ""{e.User.Name}"", ""{e.User.Mention}"" {Environment.NewLine} {e.Args[0]}")[0].ToString());
               });*/
        }

        private void UserUpdated(object sender, UserUpdatedEventArgs e)
        {
            if((e.Before.Status == UserStatus.Offline || e.Before.Status == UserStatus.Idle) && (e.After.Status == UserStatus.Online || e.After.Status == UserStatus.DoNotDisturb))
            {
                if(DuckData.messagesToDeliver.Keys.Contains(e.After.Id))
                {
                    Discord.Channel toSend = _client.CreatePrivateChannel(e.After.Id).Result;
                    foreach (string message in DuckData.messagesToDeliver[e.After.Id])
                    {
                        toSend.SendMessage(message);
                    }
                    DuckData.messagesToDeliver[e.After.Id] = new List<string>();
                }
            }
        }

        private void MessageRecieved(object sender, MessageEventArgs e)
        {
            if(e.Message.Text.ToCharArray()[0] == '>' && e.User.Id != _client.CurrentUser.Id)// DuckBot ID 265110413421576192
            {
                string withoutCommandPrefix = e.Message.Text.Substring(1);
                string commandname = withoutCommandPrefix.Split( new char[] { ' ' } )[0];
                string arguments = withoutCommandPrefix.Substring(commandname.Length + 1);

                foreach (string name in DuckData.commandDB[e.Server.Id].Keys)
                {
                   if(name.ToLower() == commandname.ToLower())
                    {
                        try
                        {
                            e.Channel.SendIsTyping();
                            e.Channel.SendMessage(DuckData.commandDB[e.Server.Id][name].Run(e.User.Name, e.User.Mention, arguments, e));
                        }
                        catch(Exception exception)
                        {
                            e.Channel.SendMessage("Ooops, seems like an exception happened. Did you forget to input something?");
                            Log(exception.ToString(), true);
                        }
                        break;
                    }
                }
            }
        }

        public static void Log(object sender, LogMessageEventArgs e)
        {
            File.AppendAllText("/home/hasacz/DuckBotLog.txt", $"[{e.Severity}] [{e.Source}] [{e.Message}]{Environment.NewLine}");
        }
        public static void Log(string text, bool isException)
        {
            if (isException)
            {
                File.AppendAllText("/home/hasacz/DuckBotLog.txt", $"{Environment.NewLine}! EXCEPTION !{Environment.NewLine}{text}{Environment.NewLine}");
            }
            else
            {
                File.AppendAllText("/home/hasacz/DuckBotLog.txt", text + Environment.NewLine);
            }
        }
        public static Discord.User FindUser(CommandEventArgs e, string user)
        {
            Discord.User u = null;
            if (!string.IsNullOrWhiteSpace(user))
            {
                if (e.Message.MentionedUsers.Count() == 1)
                {
                    u = e.Message.MentionedUsers.FirstOrDefault();
                }
                else if (e.Server.FindUsers(user).Any())
                {
                    u = e.Server.FindUsers(user).FirstOrDefault();
                }
            }
            return u;
        }
    }
}
