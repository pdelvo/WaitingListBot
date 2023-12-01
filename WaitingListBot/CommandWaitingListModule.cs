using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        IServiceProvider scopedServices;
        IServiceProvider services;
        AsyncServiceScope scope;
        readonly CommandService commandService;
        private readonly ILogger<CommandWaitingList> logger;
        private readonly Worker worker;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public CommandWaitingListModule(Worker worker, CommandService commandService, ILogger<CommandWaitingList> logger, IServiceProvider services)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            this.commandService = commandService;
            this.services = services;
            this.logger = logger;
            this.worker = worker;
        }

        protected override void BeforeExecute(CommandInfo command)
        {
            scope = services.CreateAsyncScope();
            scopedServices = scope.ServiceProvider;
            base.BeforeExecute(command);
        }

        protected override void AfterExecute(CommandInfo command)
        {
            base.AfterExecute(command);

            scope.Dispose();
        }

        [Command("setsubrole")]
        [Summary("Sets the Id for the sub role.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(false)]
        public async Task SetAsSubRole([Summary("The role of subscribers.")] IRole role)
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
                guildData.SubRoleId = role.Id;
                dataContext.Update(guildData);
                dataContext.SaveChanges();
                await Context.Message.ReplyAsync("Sub role has been set");
            }
        }

        [Command("waitingchannel")]
        [Summary("Selects the channel as the waiting list channel.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(false)]
        public async Task MarkAsWaitingChannelAsync(IGuildChannel channel)
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
                guildData.WaitingListChannelId = channel.Id;
                dataContext.Update(guildData);
                dataContext.SaveChanges();
                await Context.Message.ReplyAsync("Channel has been set as waiting channel");
            }
        }

        [Command("dmformat")]
        [Summary("Gets or sets the DM format.")]
        [ModPermission]
        public async Task DMFormatAsync([Remainder][Summary("The format string for the DM messages.")] string? format = null)
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
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
        }

        [Command("prefix")]
        [Summary("Gets or sets the command prefix.")]
        [ModPermission]
        public async Task PrefixFormat([Remainder][Summary("The format string for the DM messages.")] string? prefix = null)
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
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
        }

        [Command("nuke")]
        [Summary("Clears the waiting list.")]
        [ModPermission]
        public async Task ClearWaitingListAsync()
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
                foreach (var guildUser in guildData.UsersInGuild)
                {
                    guildUser.IsInWaitingList = false;
                    guildUser.PlayCount = 0;
                    guildUser.JoinTime = default;
                    dataContext.Update(guildUser);
                }
                dataContext.SaveChanges();

                worker.AddPublicMessageUpdateToQueue(guildData.Id);

                await Context.Channel.SendFileAsync("nuke.jpg", "List has been cleared");
            }
        }

        [Command("clearcounters")]
        [Summary("Clears the play counters. Does not clear players in queue.")]
        [ModPermission]
        public async Task ClearCountersAsync()
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
                foreach (var guildUser in guildData.UsersInList)
                {
                    guildUser.PlayCount = 0;
                    dataContext.Update(guildUser);
                }
                dataContext.SaveChanges();
                worker.AddPublicMessageUpdateToQueue(guildData.Id);

                await Context.Message.ReplyAsync("Counters have been cleared");
            }
        }

        [Command("next")]
        [Summary("Notifies the next players.")]
        [ModPermission]
        public async Task NextAsync([Summary("Number of players")] int numberOfPlayers, [Summary("Arguments")] params string[] arguments)
        {
            await InviteNextUsersAsync(numberOfPlayers, arguments);
        }

        [Command("nextrole")]
        [Summary("Notifies the next players with a given role")]
        [ModPermission]
        public async Task NextRoleAsync([Summary("Number of players")] int numberOfPlayers, [Summary("The role to invite")] IRole role, [Summary("Arguments")] params string[] arguments)
        {
            await InviteNextUsersAsync(numberOfPlayers, arguments, role.Id, true);
        }

        [Command("nextnorole")]
        [Summary("Notifies the next players without a given role")]
        [ModPermission]
        public async Task NextNoRoleAsync([Summary("Number of players")] int numberOfPlayers, [Summary("The role to NOT invite")] IRole role, [Summary("Arguments")] params string[] arguments)
        {
            await InviteNextUsersAsync(numberOfPlayers, arguments, role.Id, false);
        }

        private async Task InviteNextUsersAsync(int numberOfPlayers, string[] arguments, ulong? inviteRole = null, bool? isInviteRolePositive = null)
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
                try
                {
                    var (result, invite) = await waitingList.GetInvite(arguments, numberOfPlayers, inviteRole, isInviteRolePositive, removeFromList: true);

                    logger.LogInformation(result.Message);

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

                    worker.AddPublicMessageUpdateToQueue(guildData.Id);
                    worker.AddInviteUpdateToQueue(invite.Id);
                    await UpdateInviteMessageAsync(Context.Guild, invite);
                }
                catch (Exception ex)
                {
                    var user = await Context.Client.Rest.GetUserAsync(367018778409566209);
                    var myDMChannel = await user.CreateDMChannelAsync();
                    await myDMChannel.SendMessageAsync("Server: " + Context.Guild.Name);

                    await myDMChannel.SendMessageAsync(ex.ToString());
                    logger.LogError(ex, "Failed to invite players");

                    throw;
                }
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
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
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
                        var dmChannel = await user.CreateDMChannelAsync();

                        await dmChannel.SendMessageAsync(string.Format(guildData.DMMessageFormat, invite.FormatData));
                    }
                }

                await Context.Message.ReplyAsync("Message has been sent again");
            }
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
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
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
                    worker.AddPublicMessageUpdateToQueue(guildData.Id);
                }
            }
        }

        [Command("setcounter")]
        [Summary("Set the counter value for a user.")]
        [ModPermission]
        [CheckIfWaitingListIsActive(true)]
        public async Task SetCounterAsync(IGuildUser guildUser, int counter)
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
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
                worker.AddPublicMessageUpdateToQueue(guildData.Id);
            }
        }

        [Command("leave")]
        [Summary("Leaves the waiting list.")]
        public async Task LeaveAsync(IGuildUser? guildUser)
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetGuild(Context.Guild.Id);
                if (guildUser == null || guildData == null)
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
                    worker.AddPublicMessageUpdateToQueue(guildData.Id);
                }
            }
        }

        [Command("list")]
        [Summary("Shows the waiting list.")]
        public async Task ListAsync()
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
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
        }

        [Command("help")]
        [Summary("Shows this help message.")]
        [ModPermission]
        public async Task Help()
        {
            using (var dataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
            {
                var waitingList = new CommandWaitingList(dataContext, Context.Client.Rest, Context.Guild.Id);
                var guildData = dataContext.GetOrCreateGuildData(Context.Guild);
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
}