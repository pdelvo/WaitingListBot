﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WaitingListBot.Data;
using WaitingListBot.Model;

namespace WaitingListBot
{
    [RequireContext(ContextType.Guild)]
    public class CommandWaitingListModule : ModuleBase<SocketCommandContext>
    {
        readonly CommandService commandService;
        private WaitingListDataContext dataContext;
        GuildData guildData;
        IWaitingList waitingList;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public CommandWaitingListModule(CommandService commandService)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            this.commandService = commandService;
        }

        protected override void BeforeExecute(CommandInfo command)
        {
            dataContext = new WaitingListDataContext();
            waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
            guildData = dataContext.GetGuild(Context.Guild.Id)!;
            base.BeforeExecute(command);
        }

        protected override void AfterExecute(CommandInfo command)
        {
            dataContext.SaveChanges();
            dataContext.Dispose();
            base.AfterExecute(command);
        }

        [Command("setsubrole")]
        [Summary("Sets the Id for the sub role.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(false)]
        public async Task SetAsSubRole([Summary("The role of subscribers.")] IRole role)
        {
            guildData.SubRoleId = role.Id;
            dataContext.Update(guildData);
            dataContext.SaveChanges();
            await Context.Message.ReplyAsync("Sub role has been set");
        }

        [Command("waitingchannel")]
        [Summary("Selects the channel as the waiting list channel.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(false)]
        public async Task MarkAsWaitingChannelAsync(IGuildChannel channel)
        {
            guildData.WaitingListChannelId = channel.Id;
            dataContext.Update(guildData);
            dataContext.SaveChanges();
            await Context.Message.ReplyAsync("Channel has been set as waiting channel");
        }

        [Command("dmformat")]
        [Summary("Gets or sets the DM format.")]
        [ModPermission]
        public async Task DMFormatAsync([Remainder][Summary("The format string for the DM messages.")] string? format = null)
        {
            if (format == null)
            {
                await Context.Message.ReplyAsync(guildData.DMMessageFormat ?? "");
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

                guildData.DMMessageFormat = format;
                dataContext.Update(guildData);
                dataContext.SaveChanges();
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
                await Context.Message.ReplyAsync("The prefix is: " + guildData.CommandPrefix ?? "");
            }
            else
            {
                guildData.CommandPrefix = prefix;
                dataContext.Update(guildData);
                dataContext.SaveChanges();
                await Context.Message.ReplyAsync("Prefix has been changed.");
            }
        }

        [Command("nuke")]
        [Summary("Clears the waiting list.")]
        [ModPermission]
        public async Task ClearWaitingListAsync()
        {
            foreach (var guildUser in guildData.UsersInGuild)
            {
                guildUser.IsInWaitingList = false;
                guildUser.PlayCount = 0;
                guildUser.JoinTime = default;
                dataContext.Update(guildUser);
            }
            dataContext.SaveChanges();

            await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList, Context.Guild, guildData);

            await Context.Channel.SendFileAsync("nuke.jpg", "List has been cleared");
        }

        [Command("clearcounters")]
        [Summary("Clears the play counters. Does not clear players in queue.")]
        [ModPermission]
        public async Task ClearCountersAsync()
        {
            foreach (var guildUser in guildData.UsersInList)
            {
                guildUser.PlayCount = 0;
                dataContext.Update(guildUser);
            }
            dataContext.SaveChanges();

            await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList, Context.Guild, guildData);

            await Context.Message.ReplyAsync("Counters have been cleared");
        }

        [Command("next")]
        [Summary("Notifies the next players.")]
        [ModPermission]
        public async Task NextAsync([Summary("Number of players")] int numberOfPlayers, [Summary("Arguments")] params string[] arguments)
        {
            try
            {
                var (result, invite) = await waitingList.GetInvite(arguments, numberOfPlayers, true);

                if (!result.Success || invite == null)
                {
                    await Context.Message.ReplyAsync(result.Message);
                    return;
                }

                string playerString = "";

                foreach (var invitedUser in invite.InvitedUsers)
                {
                    playerString += invitedUser.User.Name + " (" + MentionUtils.MentionUser(invitedUser.User.UserId) + ") ";
                }

                var inviteMessage = await Context.Message.ReplyAsync(result.Message + "\r\nInvited players: " + playerString, allowedMentions: AllowedMentions.None);

                invite.InviteMessageChannelId = inviteMessage.Channel.Id;
                invite.InviteMessageId = inviteMessage.Id;

                dataContext.Update(invite);
                dataContext.SaveChanges();

                await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList, Context.Guild, guildData);
                await UpdateInviteMessageAsync(Context.Guild, invite);
            }
            catch (Exception ex)
            {
                var user = await Context.Client.Rest.GetUserAsync(367018778409566209);
                var myDMChannel = await user.GetOrCreateDMChannelAsync();
                await myDMChannel.SendMessageAsync("Server: " + Context.Guild.Name);

                await myDMChannel.SendMessageAsync(ex.ToString());

                throw;
            }
        }

        public static async Task UpdateInviteMessageAsync(IGuild guild, Invite invite)
        {
            var channel = await guild.GetTextChannelAsync(invite.InviteMessageChannelId);

            if (channel == null)
            {
                return;
            }

            var message = (IUserMessage)await channel.GetMessageAsync(invite.InviteMessageId);
            if (message is not IUserMessage) return;

            var embedBuilder = new EmbedBuilder
            {
                Color = Color.Green,
                Title = $"Invite list " + invite.Id
            };

            var description = "";
            int counter = 0;

            foreach (var player in invite.InvitedUsers)
            {
                description += $"**{++counter}.** {player.User.Name} ({GetMentionWithId(player.User.UserId)}) {(player.User.IsSub ? "(Sub) " : "")}";
                description += " ";
                switch (player.InviteAccepted)
                {
                    case true:
                        description += ":white_check_mark:";
                        break;
                    case false:
                        description += ":x:";
                        break;
                    default:
                        description += ":question:";
                        break;
                }

                description += "\r\n";
            }

            embedBuilder.Description = description;

            Embed embed = embedBuilder.Build();

            await message.ModifyAsync(p =>
            {
                p.Content = $"Invited players:";
                p.Embed = embed;
            });
        }
        private static string GetMentionWithId(ulong id)
        {
            return "<@" + id + ">";
        }

        [Command("resend")]
        [Summary("Resends the information to the last batch of players.")]
        [ModPermission]
        public async Task ResendAsync([Summary("Invite list id")] int id, [Summary("Arguments")] params string[] arguments)
        {
            var invite = dataContext.Invites.Include(i => i.InvitedUsers).ThenInclude(iu => iu.User).Include(i => i.Guild).Single(i => i.Id == id);

            if (invite.Guild.GuildId != Context.Guild.Id)
            {
                await Context.Message.ReplyAsync("List is not from this server");
                return;
            }

            invite.FormatData = arguments;
            dataContext.Update(invite);
            dataContext.SaveChanges();


            foreach (var invitedUser in invite.InvitedUsers)
            {
                if (invitedUser.InviteAccepted == true)
                {
                    var user = Context.Client.GetUser(invitedUser.User.UserId);
                    var dmChannel = await user.GetOrCreateDMChannelAsync();

                    await dmChannel.SendMessageAsync(string.Format(guildData.DMMessageFormat, invite.FormatData));
                }
            }

            await Context.Message.ReplyAsync("Message has been sent again");
        }

        [Command("join")]
        [Summary("Enters the waiting list.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(true)]
        public Task JoinAsync(IGuildUser user) => PlayAsync(user);


        [Command("play")]
        [Summary("Enters the waiting list.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(true)]
        public async Task PlayAsync(IGuildUser guildUser)
        {
            if (guildUser == null)
            {
                return;
            }

            if (!guildData.IsEnabled)
            {
                await Context.Message.ReplyAsync("The waiting list is closed.");
                return;
            }

            var userInGuild = guildData.GetOrCreateGuildUser(guildUser.Id, guildUser.Nickname ?? guildUser.Username);

            if (userInGuild.IsInWaitingList)
            {
                await Context.Message.ReplyAsync("You are already on the waiting list!");
            }
            else
            {
                // Add user the the waiting list
                userInGuild.IsInWaitingList = true;
                userInGuild.JoinTime = DateTime.Now;
                userInGuild.IsSub = guildUser.RoleIds.Contains(guildData.SubRoleId);
                dataContext.Update(userInGuild);
                dataContext.SaveChanges();

                await Context.Message.ReplyAsync($"Waiting list joined!");
                await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList, Context.Guild, guildData);
            }
        }

        [Command("setcounter")]
        [Summary("Set the counter value for a user.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(true)]
        public async Task SetCounterAsync(IGuildUser guildUser, int counter)
        {
            if (guildUser == null)
            {
                return;
            }

            if (!guildData.IsEnabled)
            {
                await Context.Message.ReplyAsync("The waiting list is closed.");
                return;
            }

            var userInGuild = guildData.GetOrCreateGuildUser(guildUser.Id, guildUser.Nickname ?? guildUser.Username);
            // Add user the the waiting list
            userInGuild.PlayCount = counter;
            dataContext.Update(userInGuild);
            dataContext.SaveChanges();

            await Context.Message.ReplyAsync($"Updated counter for {userInGuild.Name}");
            await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList, Context.Guild, guildData);
        }

        [Command("leave")]
        [Summary("Leaves the waiting list.")]
        public async Task LeaveAsync(IGuildUser? guildUser)
        {
            if (guildUser == null)
            {
                return;
            }

            var userInGuild = guildData.GetUser(guildUser.Id);

            if (userInGuild == null)
            {
                await Context.Message.ReplyAsync($"User is not on the waiting list!");
            }
            else
            {
                userInGuild.IsInWaitingList = false;
                userInGuild.JoinTime = default;
                dataContext.Update(userInGuild);
                dataContext.SaveChanges();

                await Context.Message.ReplyAsync($"User left the waiting list!");
                await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList, Context.Guild, guildData);
            }

        }

        [Command("list")]
        [Summary("Shows the waiting list.")]
        public async Task ListAsync()
        {
            var embedBuilder = new EmbedBuilder
            {
                Color = Color.Green,
                Title = $"Waiting list{(guildData.IsEnabled ? "" : " (NOT ACTIVE)")}"
            };

            var sortedList = guildData.GetSortedList();
            var description = "";
            int counter = 0;

            foreach (var player in sortedList)
            {
                IGuildUser guildUser = Context.Guild.GetUser(player.UserId);
                description += $"**{++counter}.** {player.Name} ({guildUser?.Mention}) {(player.IsSub ? "(Sub) " : "")}";
                if (player.PlayCount > 0)
                {
                    description += $"(Played { player.PlayCount} time{ (player.PlayCount > 1 ? "s" : "")})";
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
        [ModPermission]
        public async Task Help()
        {
            List<CommandInfo> commands = commandService.Commands.ToList();
            EmbedBuilder embedBuilder = new();

            foreach (CommandInfo command in commands)
            {
                // Get the command Summary attribute information
                string embedFieldText = command.Summary ?? "No description available\r\n";
                string title = $"{guildData.CommandPrefix}{command.Name} ";

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
