using Discord;
using Discord.Commands;
using Discord.WebSocket;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WaitingListBot.Model;

namespace WaitingListBot
{
    [RequireContext(ContextType.Guild)]
    public class CommandWaitingListModule : ModuleBase<SocketCommandContext>
    {
        readonly StorageFactory storageFactory;
        readonly CommandService commandService;
        Storage storage;
        IWaitingList waitingList;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public CommandWaitingListModule(CommandService commandService, StorageFactory storageFactory)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            this.commandService = commandService;
            this.storageFactory = storageFactory;
            storage = new Storage();
        }

        protected override void BeforeExecute(CommandInfo command)
        {
            waitingList = new CommandWaitingList(storageFactory.GetStorage(Context.Guild.Id), Context.Client.Rest, Context.Guild.Id);
            storage = storageFactory.GetStorage(Context.Guild.Id);
            base.BeforeExecute(command);
        }

        [Command("setsubrole")]
        [Summary("Sets the Id for the sub role.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(false)]
        public async Task SetAsSubRole([Summary("The role of subscribers.")] IRole role)
        {
            storage.SubRoleId = role.Id;
            storage.Save();
            await Context.Message.ReplyAsync("Sub role has been set");
        }

        [Command("enable")]
        [Summary("Enables the waiting list.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(false)]
        [NotWhenReactionBasedWaitingListEnabled]
        public async Task Enable()
        {
            storage.IsEnabled = true;
            storage.Save();
            await Context.Message.ReplyAsync("Waiting list is enabled");


            if (Context.Client.GetChannel(storage.WaitingListChannelId) is ISocketMessageChannel channel)
            {
                _ = await channel.SendMessageAsync($"The waiting list is now open! Use {storage.CommandPrefix}play to join");
            }
        }

        [Command("disable")]
        [Summary("Disables the waiting list.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(true)]
        [NotWhenReactionBasedWaitingListEnabled]
        public async Task Disable()
        {
            storage.IsEnabled = false;
            storage.Save();
            await Context.Message.ReplyAsync("Waiting list is disabled");

            if (Context.Client.GetChannel(storage.WaitingListChannelId) is ISocketMessageChannel channel)
            {
                await channel.SendMessageAsync($"The waiting list is now closed. You can no longer join.");
            }
        }

        [Command("waitingchannel")]
        [Summary("Selects the channel as the waiting list channel.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(false)]
        public async Task MarkAsWaitingChannelAsync(IGuildChannel channel)
        {
            storage.WaitingListChannelId = channel.Id;
            storage.Save();
            await Context.Message.ReplyAsync("Channel has been set as waiting channel");
        }

        [Command("dmformat")]
        [Summary("Gets or sets the DM format.")]
        [ModPermission]
        public async Task DMFormatAsync([Remainder][Summary("The format string for the DM messages.")] string? format = null)
        {
            if (format == null)
            {
                await Context.Message.ReplyAsync(storage.DMMessageFormat ?? "");
            }
            else
            {
                bool formatOk = false;
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        _ = string.Format(format, new object[i]);

                        formatOk = true;
                        break;
                    }
                    catch (Exception)
                    {
                    }
                }

                if (!formatOk)
                {
                    await Context.Message.ReplyAsync("The DM message format was not valid.");
                    return;
                }

                storage.DMMessageFormat = format;
                storage.Save();
                await Context.Message.ReplyAsync("Message format has been changed.");
            }
        }

        [Command("prefix")]
        [Summary("Gets or sets the command prefix.")]
        [ModPermission]
        public async Task PrefixFormat([Remainder][Summary("The format string for the DM messages.")] string? prefix = null)
        {
            if (prefix == null)
            {
                await Context.Message.ReplyAsync("The prefix is: " + storage.CommandPrefix ?? "");
            }
            else
            {
                storage.CommandPrefix = prefix;
                storage.Save();
                await Context.Message.ReplyAsync("Prefix has been changed.");
            }
        }

        [Command("nuke")]
        [Summary("Clears the waiting list.")]
        [ModPermission]
        public async Task ClearWaitingListAsync()
        {
            storage.PlayCounter.Clear();
            storage.List.Clear();
            storage.Save();

            await ReactionWaitingListModule.RemoveAllPlayerReactionsAsync(Context.Guild, storage);

            await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, Context.Guild, storage);

            await Context.Channel.SendFileAsync("nuke.jpg", "List has been cleared");
        }

        [Command("clearcounters")]
        [Summary("Clears the play counters. Does not clear players in queue.")]
        [ModPermission]
        public async Task ClearCountersAsync()
        {
            storage.PlayCounter.Clear();

            await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, Context.Guild, storage);

            await Context.Message.ReplyAsync("Counters have been cleared");
        }

        [Command("next")]
        [Summary("Notifies the next players.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(true)]
        public async Task NextAsync([Summary("Number of players")]int numberOfPlayers, [Summary("Arguments")]params string[] arguments)
        {

            var (result, nextPlayers) = await waitingList.GetNextPlayersAsync(arguments, numberOfPlayers, true);

            if (!result.Success || nextPlayers == null)
            {
                await Context.Message.ReplyAsync(result.Message);
                return;
            }

            string playerString = "";

            for (int i = 0; i < numberOfPlayers; i++)
            {
                var (playerResult, player) = nextPlayers[i];

                var restGuildUser = await Context.Client.Rest.GetGuildUserAsync(Context.Guild.Id, player.Id);
                playerString += restGuildUser.Mention + " ";
                await ReactionWaitingListModule.RemoveReactionForPlayerAsync(Context.Guild, storage, player);

                if (!playerResult.Success)
                {
                    await Context.Message.ReplyAsync(playerResult.Message, allowedMentions: AllowedMentions.None);
                }
            }

            await Context.Message.ReplyAsync("All players have been invited. Invited players: " + playerString, allowedMentions: AllowedMentions.None);

            await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, Context.Guild, storage);
        }

        [Command("join")]
        [Summary("Enters the waiting list.")]
        [CheckIfWaitingListIsActive(true)]
        [NotWhenReactionBasedWaitingListEnabled]
        public Task JoinAsync() => PlayAsync();

        [Command("join")]
        [Summary("Enters the waiting list.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(true)]
        [NotWhenReactionBasedWaitingListEnabled]
        public Task JoinAsync(IGuildUser user) => PlayAsync(user);

        [Command("play")]
        [Summary("Enters the waiting list.")]
        [CheckIfWaitingListIsActive(true)]
        [NotWhenReactionBasedWaitingListEnabled]
        public Task PlayAsync()
        {
            return PlayAsync(Context.User as IGuildUser);
        }


        [Command("play")]
        [Summary("Enters the waiting list.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(true)]
        [NotWhenReactionBasedWaitingListEnabled]
        public async Task PlayAsync(IGuildUser? guildUser)
        {
            if (guildUser == null)
            {
                return;
            }

            if (!storage.IsEnabled)
            {
                await Context.Message.ReplyAsync("The waiting list is closed.");
                return;
            }

            if (storage.List.Any(x => x.Id == guildUser.Id))
            {
                await Context.Message.ReplyAsync("You are already on the waiting list!");
            }
            else
            {
                // Add user the the waiting list
                UserInList userInList = new()
                {
                    Id = guildUser.Id,
                    Name = guildUser.Nickname ?? guildUser.Username,
                    JoinTime = DateTime.Now,
                    IsSub = guildUser.RoleIds.Contains(storage.SubRoleId)
                };

                storage.List.Add(userInList);
                storage.Save();

                await Context.Message.ReplyAsync($"Waiting list joined!");
            }
        }

        [Command("leave")]
        [Summary("Leaves the waiting list.")]
        public Task LeaveAsync()
        {
            return LeaveAsync(Context.User as IGuildUser);
        }

        [Command("leave")]
        [Summary("Leaves the waiting list.")]
        public async Task LeaveAsync(IGuildUser? guildUser)
        {
            if (guildUser == null)
            {
                return;
            }

            var entry = storage.List.SingleOrDefault(x => x.Id == guildUser.Id);
            if (entry == null)
            {
                await Context.Message.ReplyAsync($"You are not on the waiting list!");
            }
            else
            {
                storage.List.Remove(entry);
                storage.Save();

                await Context.Message.ReplyAsync($"You left the waiting list!");
                await ReactionWaitingListModule.RemoveReactionForPlayerAsync((SocketGuild)guildUser.Guild, storage, entry);
            }

        }

        [Command("list")]
        [Summary("Shows the waiting list.")]
        [CheckIfWaitingListIsActive(true)]
        public async Task ListAsync()
        {
            var embedBuilder = new EmbedBuilder
            {
                Color = Color.Green,
                Title = $"Waiting list"
            };

            var sortedList = storage.GetSortedList();
            var description = "";
            int counter = 0;

            foreach (var player in sortedList)
            {
                IGuildUser guildUser = Context.Guild.GetUser(player.Id);
                description += $"**{++counter}.** {guildUser?.Mention} {(player.IsSub ? "(Sub) " : "")}";
                if (player.Counter > 0)
                {
                    description += $"(Played { player.Counter} time{ (player.Counter > 1 ? "s" : "")})";
                }
                description += "\r\n";
            }
            embedBuilder.Description = description;
            embedBuilder.AddField("\u200B", "[View this list in real time](https://wl.pdelvo.com/WaitingList/" + Context.Guild.Id + ")");

            Embed embed = embedBuilder.Build();
            await Context.Message.ReplyAsync($"Here are the next players in line:", embed: embed, allowedMentions: AllowedMentions.None);
        }

        [Command("help")]
        [Summary("Shows this help message.")]
        public async Task Help()
        {
            List<CommandInfo> commands = commandService.Commands.ToList();
            EmbedBuilder embedBuilder = new();

            foreach (CommandInfo command in commands)
            {
                if (command.Preconditions.Any (x => x is ModPermissionAttribute))
                {
                    continue;
                }
                // Get the command Summary attribute information
                string embedFieldText = command.Summary ?? "No description available\r\n";
                string title = $"{storage.CommandPrefix}{command.Name} ";

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
        [ModPermission]
        public async Task ModHelp()
        {
            List<CommandInfo> commands = commandService.Commands.ToList();
            EmbedBuilder embedBuilder = new();

            foreach (CommandInfo command in commands)
            {
                // Get the command Summary attribute information
                string embedFieldText = command.Summary ?? "No description available\r\n";
                string title = $"{storage.CommandPrefix}{command.Name} ";

                foreach (var item in command.Parameters)
                {
                    title += $" [{item.Summary}]";
                }

                embedBuilder.AddField(title, embedFieldText);
            }

            await Context.Message.ReplyAsync("Here's a list of commands and their description: ", false, embedBuilder.Build());
        }
    }
}
