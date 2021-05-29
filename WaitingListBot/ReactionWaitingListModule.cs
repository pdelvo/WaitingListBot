using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using Discord.WebSocket;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WaitingListBot.Model;

namespace WaitingListBot
{
    [RequireContext(ContextType.Guild)]
    public class ReactionWaitingListModule : ModuleBase<SocketCommandContext>
    {
        readonly StorageFactory storageFactory;
        readonly CommandService commandService;
        Storage storage;
        IWaitingList waitingList;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public ReactionWaitingListModule(CommandService commandService, StorageFactory storageFactory)
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

        [Command("start")]
        [Summary("Starts the reaction based waiting list.")]
        [ModPermission]
        public async Task Start()
        {
            var message = await GetMessageAsync(Context.Guild, storage);

            if (message != null)
            {
                await Context.Message.ReplyAsync("Waiting list is already open");
                return;
            }

            await waitingList.SetUsersAsync(Array.Empty<IGuildUser>());

            var waitingListChannel = Context.Guild.GetTextChannel(storage.WaitingListChannelId);

            message = await waitingListChannel.SendMessageAsync("Join the waiting list now!");

            storage.ReactionMessageId = message.Id;
            storage.IsEnabled = true;
            storage.IsPaused = false;
            storage.Save();
            // await message.AddReactionAsync(storage.ReactionEmote);

            await UpdateReactionMessageAsync(waitingList, Context.Guild, storage);
            var modMessage = await Context.Message.ReplyAsync("Waiting list has been started");

            ComponentBuilder componentBuilder = new ComponentBuilder();
            componentBuilder.WithButton("Clear counters", customId: "clearcounters");

            await modMessage.ModifyAsync(p =>
            {
                p.Components = componentBuilder.Build();
            });

        }

        [Command("stop")]
        [Summary("Stops the reaction based waiting list.")]
        [ModPermission]
        public async Task Stop()
        {
            var message = await GetMessageAsync(Context.Guild, storage);

            if (message == null)
            {
                await Context.Message.ReplyAsync("Waiting list is not running");
                return;
            }

            storage.IsEnabled = false;
            storage.IsPaused = false;
            storage.Save();

            await message.DeleteAsync();

            await Context.Message.ReplyAsync("Waiting list has been stopped");
        }

        [Command("pause")]
        [Summary("Pauses the joining of the list.")]
        [ModPermission]
        public async Task Pause()
        {
            storage.IsPaused = true;
            storage.Save();

            await UpdateReactionMessageAsync(waitingList, Context.Guild, storage);

            var message = await Context.Message.ReplyAsync("Waiting list has been paused");
            ComponentBuilder componentBuilder = new ComponentBuilder();

            componentBuilder.WithButton("Unpause", customId: "unpause");

            await message.ModifyAsync(p =>
            {
                p.Components = componentBuilder.Build();
            });
        }

        [Command("unpause")]
        [Summary("Pauses the joining of the list.")]
        [ModPermission]
        public async Task Unpause()
        {
            storage.IsPaused = false;
            storage.Save();

            await UpdateReactionMessageAsync(waitingList, Context.Guild, storage);

            await Context.Message.ReplyAsync("Waiting list has been unpaused");
        }

        public static async Task UpdateReactionMessageAsync(IWaitingList waitingList, IGuild guild, Storage storage)
        {
            var message = await GetMessageAsync(guild, storage);
            if (message is not IUserMessage) return;

            var embedBuilder = new EmbedBuilder
            {
                Color = Color.Green,
                Title = $"Waiting list"
            };

            var sortedList = await waitingList.GetPlayerListAsync();
            var description = "";
            int counter = 0;

            foreach (var player in sortedList)
            {
                description += $"**{++counter}.** {player.Name} ({GetMentionWithId(player.Id)}) {(player.IsSub ? "(Sub) " : "")}";
                if (player.Counter > 0)
                {
                    description += $"(Played { player.Counter} time{ (player.Counter > 1 ? "s" : "")})";
                }
                description += "\r\n";
            }
            string url = "https://wl.pdelvo.com/WaitingList/" + guild.Id;

            embedBuilder.Description = description;
            embedBuilder.AddField("\u200B", "[View this list in real time](" + url + ")");

            ComponentBuilder componentBuilder = new ComponentBuilder();

            componentBuilder.WithButton("Join", customId: "join", disabled: storage.IsPaused);
            componentBuilder.WithButton("Leave", customId: "leave");

            // Waiting for an updated Discord.Net package for this to work
            // componentBuilder.WithButton("Website", style: ButtonStyle.Link, url: url);

            Embed embed = embedBuilder.Build();

            await message.ModifyAsync(p =>
            {
                p.Content = $"Join the waiting list now!:";
                p.Embed = embed;
                p.Components = componentBuilder.Build();
            });
        }

        private static string GetMentionWithId(ulong id)
        {
            return "<@" + id + ">";
        }

        public static async Task<bool> IsReactionBasedWaitingListActiveAsync(IGuild guild, Storage storage)
        {
            return (await GetMessageAsync(guild, storage)) != null;
        }

        private static async Task<IUserMessage?> GetMessageAsync(IGuild guild, Storage storage)
        {
            if (storage.ReactionMessageId == 0) return null;

            var waitingListChannel = await guild.GetTextChannelAsync(storage.WaitingListChannelId);
            if (waitingListChannel == null) return null;
            return await waitingListChannel.GetMessageAsync(storage.ReactionMessageId) as IUserMessage;
        }

        public static async Task RemoveReactionForPlayerAsync(SocketGuild guild, Storage storage, params UserInList[] players)
        {
            var message = await GetMessageAsync(guild, storage);

            if (message != null)
            {
                foreach (var player in players)
                {
                    await message.RemoveReactionAsync(storage.ReactionEmote, guild.GetUser(player.Id));
                }
            }
        }

        public static async Task RemoveAllPlayerReactionsAsync(IGuild guild, Storage storage)
        {
            var message = await GetMessageAsync(guild, storage);

            if (message != null)
            {
                await message.RemoveAllReactionsForEmoteAsync(storage.ReactionEmote);
                await message.AddReactionAsync(storage.ReactionEmote);
            }
        }

        public static async Task SetWaitingListMembers(IWaitingList waitingList, IGuild guild, Storage storage, IGuildUser[]? users = null)
        {
            if (users == null)
            {
                var message = await GetMessageAsync(guild, storage);

                if (message == null)
                {
                    return;
                }

                var reactionUsers = message.GetReactionUsersAsync(storage.ReactionEmote, 1000);

                var allUsers = await guild.GetUsersAsync();

                List<IGuildUser> reactionGuildUsers = new List<IGuildUser>();

                await foreach (var userList in reactionUsers)
                {
                    foreach (var user in userList)
                    {
                        if (!user.IsBot)
                        {
                            reactionGuildUsers.Add(allUsers.First(x => x.Id == user.Id));
                        }
                    }
                }

                users = reactionGuildUsers.ToArray();
            }

            List<UserInList> toRemove = new List<UserInList>();

            foreach (var user in await waitingList.GetPlayerListAsync())
            {
                if (!users.Any(x => x.Id == user.Id))
                {
                    toRemove.Add(user);
                }
            }

            foreach (var user in users)
            {
                await waitingList.AddUserAsync(user);
            }
            foreach (var user in toRemove)
            {
                await waitingList.RemoveUserAsync(user.Id);
            }

            await UpdateReactionMessageAsync(waitingList, guild, storage);
        }
    }
}
