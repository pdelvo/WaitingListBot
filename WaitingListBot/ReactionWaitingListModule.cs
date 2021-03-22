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
            await message.AddReactionAsync(storage.ReactionEmote);

            await UpdateReactionMessageAsync(waitingList, Context.Guild, storage);
            await Context.Message.ReplyAsync("Waiting list has been started");
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

            await message.DeleteAsync();

            await Context.Message.ReplyAsync("Waiting list has been stopped");
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
                IGuildUser guildUser = await guild.GetUserAsync(player.Id);
                description += $"**{++counter}.** {guildUser?.Mention} {(player.IsSub ? "(Sub) " : "")}";
                if (player.Counter > 0)
                {
                    description += $"(Played { player.Counter} time{ (player.Counter > 1 ? "s" : "")})";
                }
                description += "\r\n";
            }
            embedBuilder.Description = description;
            embedBuilder.AddField("\u200B", "[View this list in real time](https://wl.pdelvo.com/WaitingList/" + guild.Id + ")");

            Embed embed = embedBuilder.Build();

            await message.ModifyAsync(p =>
            {
                p.Content = $"Join the waiting list now!:";
                p.Embed = embed;
            });
        }

        private static async Task<IUserMessage?> GetMessageAsync(IGuild guild, Storage storage)
        {
            var waitingListChannel = await guild.GetTextChannelAsync(storage.WaitingListChannelId);
            if (waitingListChannel == null) return null;

            return await waitingListChannel.GetMessageAsync(storage.ReactionMessageId) as IUserMessage;
        }

        public static async Task RemoveReactionForPlayerAsync(SocketGuild guild, Storage storage, UserInListWithCounter player)
        {
            var message = await GetMessageAsync(guild, storage);

            if (message != null)
            {
                await message.RemoveReactionAsync(storage.ReactionEmote, guild.GetUser(player.Id));
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
    }
}
