using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using Discord.WebSocket;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WaitingListBot.Data;
using WaitingListBot.Model;

namespace WaitingListBot
{
    [RequireContext(ContextType.Guild)]
    public class ButtonWaitingListModule : ModuleBase<SocketCommandContext>
    {
        WaitingListDataContext dataContext;
        GuildData guildData;
        readonly CommandService commandService;
        IWaitingList waitingList;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public ButtonWaitingListModule(CommandService commandService)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            this.commandService = commandService;
        }

        protected override void BeforeExecute(CommandInfo command)
        {
            dataContext = new WaitingListDataContext();
            waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
            guildData = dataContext.GetOrCreateGuildData(Context.Guild)!;
            base.BeforeExecute(command);
        }

        protected override void AfterExecute(CommandInfo command)
        {
            dataContext.SaveChanges();
            dataContext.Dispose();
            base.AfterExecute(command);
        }

        [Command("start")]
        [Summary("Starts the reaction based waiting list.")]
        [ModPermission]
        public async Task Start()
        {
            var message = await GetMessageAsync(Context.Guild, guildData);

            if (message != null)
            {
                await Context.Message.ReplyAsync("Waiting list is already open");
                return;
            }

            waitingList.ClearUsers();

            var waitingListChannel = Context.Guild.GetTextChannel(guildData.WaitingListChannelId);

            message = await waitingListChannel.SendMessageAsync("Join the waiting list now!");

            guildData.PublicMessageId = message.Id;
            guildData.IsEnabled = true;
            guildData.IsPaused = false;
            dataContext.Update(guildData);
            dataContext.SaveChanges();
            // await message.AddReactionAsync(storage.ReactionEmote);

            await UpdatePublicMessageAsync(waitingList, Context.Guild, guildData);
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
            var message = await GetMessageAsync(Context.Guild, guildData);

            if (message == null)
            {
                await Context.Message.ReplyAsync("Waiting list is not running");
                return;
            }

            guildData.IsEnabled = false;
            guildData.IsPaused = false;
            dataContext.Update(guildData);
            dataContext.SaveChanges();

            await message.DeleteAsync();

            await Context.Message.ReplyAsync("Waiting list has been stopped");
        }

        [Command("pause")]
        [Summary("Pauses the joining of the list.")]
        [ModPermission]
        public async Task Pause()
        {
            if (!guildData.IsEnabled)
            {
                await Context.Message.ReplyAsync("Waiting list has not open");
                return;
            }
            guildData.IsPaused = true;
            dataContext.Update(guildData);
            dataContext.SaveChanges();

            await UpdatePublicMessageAsync(waitingList, Context.Guild, guildData);
            ComponentBuilder componentBuilder = new ComponentBuilder();

            componentBuilder.WithButton("Unpause", customId: "unpause");

            await Context.Message.DeleteAsync();

            await Context.Channel.SendMessageAsync("Waiting list has been paused", component: componentBuilder.Build());
        }

        //[Command("unpause")]
        //[Summary("Pauses the joining of the list.")]
        //[ModPermission]
        //public async Task Unpause()
        //{
        //    storage.IsPaused = false;
        //    storage.Save();

        //    await UpdateReactionMessageAsync(waitingList, Context.Guild, storage);

        //    await Context.Message.ReplyAsync("Waiting list has been unpaused");
        //}

        public static async Task UpdatePublicMessageAsync(IWaitingList waitingList, IGuild guild, GuildData guildData)
        {
            var message = await GetMessageAsync(guild, guildData);
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
                description += $"**{++counter}.** {player.Name} ({GetMentionWithId(player.UserId)}) {(player.IsSub ? "(Sub) " : "")}";
                if (player.PlayCount > 0)
                {
                    description += $"(Played { player.PlayCount} time{ (player.PlayCount > 1 ? "s" : "")})";
                }
                description += "\r\n";
            }
            string url = "https://wl.pdelvo.com/WaitingList/" + guild.Id;

            embedBuilder.Description = description;
            embedBuilder.AddField("\u200B", "[View this list in real time](" + url + ")");

            ComponentBuilder componentBuilder = new ComponentBuilder();

            componentBuilder.WithButton("Join", customId: "join", disabled: guildData.IsPaused);
            componentBuilder.WithButton("Leave", customId: "leave");

            // Waiting for an updated Discord.Net package for this to work
            componentBuilder.WithButton("Website", null, style: ButtonStyle.Link, url: url);

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

        private static async Task<IUserMessage?> GetMessageAsync(IGuild guild, GuildData guildData)
        {
            if (guildData.PublicMessageId == 0) return null;

            var waitingListChannel = await guild.GetTextChannelAsync(guildData.WaitingListChannelId);
            if (waitingListChannel == null) return null;

            return await waitingListChannel.GetMessageAsync(guildData.PublicMessageId) as IUserMessage;
        }
    }
}
