using Discord;
using Discord.Commands;
using Discord.WebSocket;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaitingListBot
{
    [RequireContext(ContextType.Guild)]
    public class WaitingListModule : ModuleBase<SocketCommandContext>
    {
        readonly StorageFactory _storageFactory;
        readonly CommandService _commandService;
        Storage _storage;

        public WaitingListModule(CommandService commandService, StorageFactory storageFactory)
        {
            _commandService = commandService;
            _storageFactory = storageFactory;
        }

        protected override void BeforeExecute(CommandInfo command)
        {
            _storage = _storageFactory.GetStorage(Context.Guild.Id);
            base.BeforeExecute(command);
        }

        [Command("setsubrole")]
        [Summary("Sets the Id for the sub role.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task SetAsSubRole([Summary("The role of subscribers.")] IRole role)
        {
            _storage.SubRoleId = role.Id;
            _storage.Save();
            await Context.Message.ReplyAsync("Sub role has been set");
        }

        [Command("enable")]
        [Summary("Enables the waiting list.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task Enable()
        {
            _storage.IsEnabled = true;
            _storage.Save();
            await Context.Message.ReplyAsync("Waiting list is enabled");


            if (Context.Client.GetChannel(_storage.WaitingListChannelId) is ISocketMessageChannel channel)
            {
                await channel.SendMessageAsync($"The waiting list is now open! Use {_storage.CommandPrefix}play to join");
            }
        }

        [Command("disable")]
        [Summary("Disables the waiting list.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task Disable()
        {
            _storage.IsEnabled = false;
            _storage.Save();
            await Context.Message.ReplyAsync("Waiting list is disabled");

            if (Context.Client.GetChannel(_storage.WaitingListChannelId) is ISocketMessageChannel channel)
            {
                await channel.SendMessageAsync($"The waiting list is now closed. You can no longer join.");
            }
        }

        [Command("waitingchannel")]
        [Summary("Selects the channel as the waiting list channel.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task MarkAsWaitingChannelAsync(IGuildChannel channel)
        {
            _storage.WaitingListChannelId = channel.Id;
            _storage.Save();
            await Context.Message.ReplyAsync("Channel has been set as waiting channel");
        }

        [Command("dmformat")]
        [Summary("Gets or sets the DM format.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task DMFormatAsync([Remainder][Summary("The format string for the DM messages.")] string format = null)
        {
            if (format == null)
            {
                await Context.Message.ReplyAsync(_storage.DMMessageFormat ?? "");
            }
            else
            {
                _storage.DMMessageFormat = format;
                _storage.Save();
                await Context.Message.ReplyAsync("Message format has been changed.");
            }
        }

        [Command("prefix")]
        [Summary("Gets or sets the command prefix.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task PrefixFormat([Remainder][Summary("The format string for the DM messages.")] string prefix = null)
        {
            if (prefix == null)
            {
                await Context.Message.ReplyAsync("The prefix is: " + _storage.CommandPrefix ?? "");
            }
            else
            {
                _storage.CommandPrefix = prefix;
                _storage.Save();
                await Context.Message.ReplyAsync("Prefix has been changed.");
            }
        }

        [Command("nuke")]
        [Summary("Clears the waiting list.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task ClearWaitingListAsync()
        {
            _storage.PlayCounter.Clear();
            _storage.List.Clear();
            _storage.Save();
            await Context.Message.ReplyAsync("List has been cleared");
        }

        [Command("next")]
        [Summary("Notifies the next players.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task NextAsync([Summary("Number of players")]int numberOfPlayers, [Summary("Arguments")]params string[] arguments)
        {
            var list = GetSortedList();

            if (list.Count < numberOfPlayers)
            {
                await Context.Message.ReplyAsync($"Did not send invites. There are only {list.Count} players in the list.");
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

            string playerString = "";

            var restClient = Context.Client.Rest;

            for (int i = 0; i < numberOfPlayers; i++)
            {
                var player = list[i];
                _storage.List.Remove(player);
                IncreasePlayCounter(player.Id);
                _storage.Save();

                var restGuildUser = await restClient.GetGuildUserAsync(Context.Guild.Id, player.Id);
                try
                {
                    var message =  string.Format(_storage.DMMessageFormat, arguments);

                    playerString += restGuildUser.Mention + " ";

                    await restGuildUser.SendMessageAsync(message);
                }
                catch (FormatException)
                {
                    await Context.Message.ReplyAsync("The arguments had the wrong format");
                    return;
                }
                catch (Exception ex)
                {
                    await Context.Message.ReplyAsync($"Could not invite {restGuildUser.Mention}. Exception: {ex.Message}");
                }
            }
            await Context.Message.ReplyAsync("All players have been invited. Invited players: " + playerString, allowedMentions: AllowedMentions.None);
        }

        [Command("join")]
        [Summary("Enters the waiting list.")]
        public Task JoinAsync() => PlayAsync();


        [Command("play")]
        [Summary("Enters the waiting list.")]
        public async Task PlayAsync()
        {
            if (Context.User is not IGuildUser guildUser)
            {
                return;
            }

            if (!_storage.IsEnabled)
            {
                await Context.Message.ReplyAsync("The waiting list is closed.");
                return;
            }

            if (_storage.List.Any(x => x.Id == Context.User.Id))
            {
                await Context.Message.ReplyAsync("You are already on the waiting list!");
            }
            else
            {
                // Add user the the waiting list
                SocketUser author = Context.User;
                UserInList userInList = new UserInList
                {
                    Id = author.Id,
                    Name = guildUser.Nickname ?? author.Username,
                    JoinTime = DateTime.Now,
                    IsSub = guildUser.RoleIds.Contains(_storage.SubRoleId)
                };


                _storage.List.Add(userInList);
                _storage.Save();

                await Context.Message.ReplyAsync($"You joined the waiting list!");
                //TODO: Maybe give the user the spot in the list
            }
        }

        [Command("leave")]
        [Summary("Leaves the waiting list.")]
        public async Task LeaveAsync()
        {
            var entry = _storage.List.SingleOrDefault(x => x.Id == Context.User.Id);
            if (entry == null)
            {
                await Context.Message.ReplyAsync($"You are not on the waiting list!");
            }
            else
            {
                _storage.List.Remove(entry);
                _storage.Save();

                await Context.Message.ReplyAsync($"You left the waiting list!");
            }
        }

        [Command("list")]
        [Summary("Shows the waiting list.")]
        public async Task ListAsync()
        {
            var embedBuilder = new EmbedBuilder
            {
                Color = Color.Green,
                Title = $"Waiting list"
            };

            var sortedList = GetSortedList();
            var description = "";
            int counter = 0;

            foreach (var player in sortedList)
            {
                IGuildUser guildUser = Context.Guild.GetUser(player.Id);
                description += $"**{++counter}.** {guildUser?.Mention} ({(player.IsSub ? "Sub, " : "")}Played {GetPlayCounterById(player.Id)} times already)\r\n";
            }
            embedBuilder.Description = description;

            Embed embed = embedBuilder.Build();
            await Context.Message.ReplyAsync($"Here are the next players in line:", embed: embed, allowedMentions: AllowedMentions.None);
        }

        [Command("help")]
        [Summary("Shows this help message.")]
        public async Task Help()
        {
            List<CommandInfo> commands = _commandService.Commands.ToList();
            EmbedBuilder embedBuilder = new EmbedBuilder();

            foreach (CommandInfo command in commands)
            {
                if (command.Preconditions.Any (x => x is RequireUserPermissionAttribute))
                {
                    continue;
                }
                // Get the command Summary attribute information
                string embedFieldText = command.Summary ?? "No description available\r\n";
                string title = $"!{command.Name} ";

                foreach (var item in command.Parameters)
                {
                    title += $" [{item.Summary}]";
                }

                embedBuilder.AddField(title, embedFieldText);
            }

            await Context.Message.ReplyAsync("Here's a list of commands and their description: ", false, embedBuilder.Build());
        }

        [Command("modhelp")]
        [Summary("Shows this help message.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task ModHelp()
        {
            List<CommandInfo> commands = _commandService.Commands.ToList();
            EmbedBuilder embedBuilder = new EmbedBuilder();

            foreach (CommandInfo command in commands)
            {
                // Get the command Summary attribute information
                string embedFieldText = command.Summary ?? "No description available\r\n";
                string title = $"!{command.Name} ";

                foreach (var item in command.Parameters)
                {
                    title += $" [{item.Summary}]";
                }

                embedBuilder.AddField(title, embedFieldText);
            }

            await Context.Message.ReplyAsync("Here's a list of commands and their description: ", false, embedBuilder.Build());
        }

        private List<UserInList> GetSortedList()
        {
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

        private int GetPlayCounterById(ulong id)
        {
            return _storage.PlayCounter.SingleOrDefault(x => x.Id == id)?.Counter ?? 0;
        }
    }
}
