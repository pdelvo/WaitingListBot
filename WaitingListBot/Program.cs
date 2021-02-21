using Discord;
using Discord.WebSocket;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot
{
    class Program
    {
        private DiscordSocketClient _client;
        Storage _storage;

        public static async Task Main(string[] args)
        {
            var program = new Program();
            await program.RunAsync();
        }

        private async Task RunAsync()
        {
            _storage = Storage.FromFile();

            _client = new DiscordSocketClient();

            _client.Log += Log;

            //  You can assign your bot token to a string, and pass that in to connect.
            //  This is, however, insecure, particularly if you plan to have your code hosted in a public repository.
            var token = "ODEzMTQzODc3NjE1NDg0OTc4.YDLBPw.hUAJTHTkAMl3SCsIBNnNQJJmY_w";

            // Some alternative options would be to keep your token in an Environment Variable or a standalone file.
            // var token = Environment.GetEnvironmentVariable("NameOfYourEnvironmentVariable");
            // var token = File.ReadAllText("token.txt");
            // var token = JsonConvert.DeserializeObject<AConfigurationClass>(File.ReadAllText("config.json")).Token;

            _client.MessageReceived += MessageReceivedAsync;

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task MessageReceivedAsync(SocketMessage arg)
        {
            var guildUser = arg.Author as IGuildUser;
            if (guildUser != null)
            {
                if (guildUser.GuildPermissions.Has(GuildPermission.BanMembers))
                {
                    if (arg.Content == "!waitingchannel")
                    {
                        _storage.WaitingListChannelId = arg.Channel.Id;
                        _storage.Save();
                        await arg.Channel.SendMessageAsync("Channel has been set as waiting channel");
                    }
                    if (arg.Content == "!nuke")
                    {
                        _storage.PlayCounter.Clear();
                        _storage.List.Clear();
                        _storage.Save();
                        await arg.Channel.SendMessageAsync("List has been cleared");
                    }
                    if (arg.Content.StartsWith("!next"))
                    {
                        string rest = arg.Content.Substring(5);
                        string[] split = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        int numberOfPlayers;
                        if (split.Length != 2 || !int.TryParse(split[0], out numberOfPlayers))
                        {
                            await arg.Channel.SendMessageAsync("Usage: !next [number of players] [password]");
                            return;
                        }

                        var list = GetSortedList();

                        if (list.Count < numberOfPlayers)
                        {
                            await arg.Channel.SendMessageAsync($"Did not send invites. There are only {list.Count} players in the list.");
                            return;
                        }
                        // Send invites

                        void IncreasePlayCounter(ulong id)
                        {
                            var entry = _storage.PlayCounter.SingleOrDefault(x => x.Id == id);

                            if (entry == null)
                            {
                                _storage.PlayCounter.Add(new PlayCounter { Id = id, Counter = 1 });
                            }
                            else
                            {
                                entry.Counter++;
                            }
                        }

                        for (int i = 0; i < numberOfPlayers; i++)
                        {
                            var player = list[i];
                            _storage.List.Remove(player);
                            IncreasePlayCounter(player.Id);
                            _storage.Save();

                            var user = _client.GetUser(player.Id);

                            var message = "You are next in line to play!\r\n";
                            message += "Join the private match with the following details:\r\n";
                            message += "Name: Berry\r\n";
                            message += $"Password: {split[1]}\r\n";
                            message += "Please make sure that you have cross platform play enabled!";


                            await user.SendMessageAsync(message);
                        }
                    }
                }
            }

            if (arg.Channel.Id == _storage.WaitingListChannelId && arg.Content?.StartsWith("!") == true)
            {
                string command = arg.Content.Substring(1);

                if (command == "help")
                {
                    var message = "Hi :guhberWave:! I am the waiting list bot! This is what i can do: \r\n";
                    message += "!play - Join the waiting list.\r\n";
                    message += "!leave - If you don't have time to play you can leave the waiting list.\r\n";
                    message += "!list - Displays the order of the next players.\r\n";

                    message += "!help - If you have trouble reading I can repeat myself.";
                    await arg.Channel.SendMessageAsync(message);
                }
                else if (command == "play")
                {
                    if (_storage.List.Any(x => x.Id == arg.Author.Id))
                    {
                        await arg.Channel.SendMessageAsync($"{arg.Author.Mention} You are already on the waiting list!");
                    }
                    else
                    {
                        // Add user the the waiting list
                        SocketUser author = arg.Author;
                        UserInList userInList = new UserInList
                        {
                            Id = author.Id,
                            Name = guildUser.Nickname ?? author.Username,
                            JoinTime = DateTime.Now,
                            IsSub = guildUser.RoleIds.Contains(765730759095877632ul)
                        }; 


                        _storage.List.Add(userInList);
                        _storage.Save();

                        await arg.Channel.SendMessageAsync($"{arg.Author.Mention} You joined the waiting list!");
                        //TODO: Maybe give the user the spot in the list
                    }
                }
                else if (command == "leave")
                {
                    var entry = _storage.List.SingleOrDefault(x => x.Id == arg.Author.Id);
                    if (entry == null)
                    {
                        await arg.Channel.SendMessageAsync($"{arg.Author.Mention} You are not on the waiting list!");
                    }
                    else
                    {
                        _storage.List.Remove(entry);
                        _storage.Save();

                        await arg.Channel.SendMessageAsync($"{arg.Author.Mention} You left the waiting list!");
                    }
                }
                else if (command == "list")
                {
                    var embedBuilder = new EmbedBuilder();
                    embedBuilder.Color = Color.Green;
                    embedBuilder.Title = $"Waiting list {arg.Author.Mention}";
                    var sortedList = GetSortedList();
                    var description = "";
                    int counter = 0;
                    foreach (var player in sortedList)
                    {
                        description += $"**{++counter}.** {_client.GetUser(player.Id).Mention}\r\n";
                    }
                    embedBuilder.Description = description;

                    await arg.Channel.SendMessageAsync($"Here are the next players in line {_client.GetUser(arg.Author.Id).Mention}:", embed: embedBuilder.Build(), allowedMentions: AllowedMentions.None);
                }
            }
        }

        private List<UserInList> GetSortedList()
        {
            var counters = _storage.PlayCounter;
            int GetPlayCounterById(ulong id)
            {
                return counters.SingleOrDefault(x => x.Id == id)?.Counter ?? 0;
            }

            var newList = new List<UserInList>(_storage.List);
            newList.Sort((a, b) =>
            {
                if (GetPlayCounterById(a.Id) < GetPlayCounterById(b.Id))
                {
                    return -1;
                }
                else if (GetPlayCounterById(a.Id) > GetPlayCounterById(b.Id))
                {
                    return 1;
                }

                if (a.IsSub && !b.IsSub)
                {
                    return -1;
                }
                else if (!a.IsSub && b.IsSub)
                {
                    return 1;
                }

                return a.JoinTime.CompareTo(b.JoinTime);
            });

            return newList;
        }

        private Task Log(LogMessage logMessage)
        {
            Console.WriteLine(logMessage.ToString());
            return Task.CompletedTask;
        }
    }
}
