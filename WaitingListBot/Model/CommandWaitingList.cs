using Discord;
using Discord.WebSocket;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WaitingListBot.Data;

namespace WaitingListBot.Model
{
    public class CommandWaitingList : IWaitingList
    {
        readonly WaitingListDataContext dataContext;
        readonly DiscordSocketRestClient restClient;
        readonly ulong guildId;
        GuildData guildData;

        public CommandWaitingList(WaitingListDataContext dataContext, DiscordSocketRestClient restClient, ulong guildId)
        {
            this.dataContext = dataContext;
            this.restClient = restClient;
            this.guildId = guildId;

            this.guildData = dataContext.GetGuild(guildId)!;
        }

        public async Task<CommandResult> AddUserAsync(IGuildUser guildUser)
        {
            if (!guildData.IsEnabled)
            {
                return CommandResult.FromError("Waiting list is not open");
            }

            if (guildData.UsersInList.Any(x => x.UserId == guildUser.Id))
            {
                return CommandResult.FromError("You are already on the waiting list!");
            }
            else
            {
                var userInList = guildData.GetOrCreateGuildUser(guildUser.Id, guildUser.Nickname ?? guildUser.Username);

                userInList.IsInWaitingList = true;
                userInList.JoinTime = DateTime.Now;
                userInList.IsSub = guildUser.RoleIds.Contains(guildData.SubRoleId);

                dataContext.Update(userInList);

                await dataContext.SaveChangesAsync();

                return CommandResult.FromSuccess($"Waiting list joined!");
            }
        }

        public async Task<(CommandResult commandResult, Invite? invite)> GetInvite(string[] arguments, int numberOfPlayers, ulong? inviteRole = null, bool? isInviteRolePositive = null, bool removeFromList = true)
        {
            IAsyncEnumerable<UserInGuild> asyncList = guildData.GetSortedList().ToAsyncEnumerable();

            if (inviteRole is ulong roleId)
            {
                asyncList = asyncList.WhereAwait(async x =>
                {
                    var restGuildUser = await restClient.GetGuildUserAsync(guildId, x.UserId);

                    var hasRole = restGuildUser.RoleIds.Contains(roleId);

                    return hasRole == isInviteRolePositive;
                });
            }

            var list = await asyncList.ToListAsync();

            if (list.Count < numberOfPlayers)
            {
                return (CommandResult.FromError($"Did not send invites. There are only {list.Count} players in the list."), null);
            }

            string message;

            try
            {
                message = string.Format(guildData.DMMessageFormat, arguments);
            }
            catch (Exception)
            {
                return (CommandResult.FromError("The arguments had the wrong format"), null);
            }

            var invite = new Invite
            {
                FormatData = arguments,
                Guild = guildData,
                InvitedUsers = new List<InvitedUser>(),
                InviteTime = DateTime.Now,
                NumberOfInvitedUsers = numberOfPlayers
            };

            dataContext.Invites.Add(invite);
            dataContext.SaveChanges();

            StringBuilder warnings = new StringBuilder();

            // Send invites
            for (int i = 0; i < numberOfPlayers; i++)
            {
                var player = list[i];

                player.IsInWaitingList = false;
                player.PlayCount++;

                dataContext.Update(player);
                dataContext.SaveChanges();

                var restGuildUser = await restClient.GetGuildUserAsync(guildId, player.UserId);
                try
                {
                    ComponentBuilder componentBuilder = new ComponentBuilder();
                    componentBuilder.WithButton("Yes", customId: $"joinYes;{invite.Id}");
                    componentBuilder.WithButton("No", customId: $"joinNo;{invite.Id}");


                    var userMessage = await restGuildUser.SendMessageAsync($"Are you ready to join? You have 1 minute to respond.", component: componentBuilder.Build());


                    InvitedUser invitedUser = new InvitedUser
                    {
                        Invite = invite,
                        InviteTime = DateTime.Now,
                        DmQuestionMessageId = userMessage.Id,
                        User = player
                    };

                    dataContext.InvitedUsers.Add(invitedUser);
                }
                catch (Exception ex)
                {
                    warnings.AppendLine($"Could not invite {restGuildUser?.Mention ?? player.Name}. Exception: {ex.Message}");
                }
            }

            this.dataContext.Update(invite);

            await this.dataContext.SaveChangesAsync();

            return (CommandResult.FromSuccess("Players have been invited." + (warnings.Length > 0 ? "\r\n" + warnings.ToString() : "")), invite);
        }

        public async Task<CommandResult> InviteNextPlayerAsync(Invite invite)
        {
            IAsyncEnumerable<UserInGuild> list = guildData.GetSortedList().ToAsyncEnumerable();
            
            if (invite.InviteRole is ulong roleId)
            {
                list = list.WhereAwait(async x =>
                {
                    var restGuildUser = await restClient.GetGuildUserAsync(guildId, x.UserId);

                    var hasRole = restGuildUser.RoleIds.Contains(roleId);

                    return hasRole == invite.IsInviteRolePositive;
                });
            }

            if (await list.AnyAsync())
            {
                return CommandResult.FromError($"Could not invite additional player. List is empty.");
            }

            string message;

            try
            {
                message = string.Format(guildData.DMMessageFormat, invite.FormatData);
            }
            catch (Exception)
            {
                return CommandResult.FromError("The arguments had the wrong format");
            }

            StringBuilder warnings = new StringBuilder();

            // Send invites
            var player = await list.FirstAsync();

            player.IsInWaitingList = false;
            player.PlayCount++;

            dataContext.Update(player);
            dataContext.SaveChanges();

            var restGuildUser = await restClient.GetGuildUserAsync(guildId, player.UserId);
            try
            {
                ComponentBuilder componentBuilder = new ComponentBuilder();
                componentBuilder.WithButton("Yes", customId: $"joinYes;{invite.Id}");
                componentBuilder.WithButton("No", customId: $"joinNo;{invite.Id}");


                var userMessage = await restGuildUser.SendMessageAsync($"Are you ready to join? You have 1 minute to respond.", component: componentBuilder.Build());


                invite.InvitedUsers.Add(new InvitedUser
                {
                    Invite = invite,
                    InviteTime = DateTime.Now,
                    DmQuestionMessageId = userMessage.Id,
                    User = player
                });

                dataContext.Update(invite);
                dataContext.SaveChanges();
            }
            catch (Exception ex)
            {
                warnings.AppendLine($"Could not invite {restGuildUser.Mention}. Exception: {ex.Message}");
            }

            await this.dataContext.SaveChangesAsync();

            return CommandResult.FromSuccess("Players have been invited." + (warnings.Length > 0 ? "\r\n" + warnings.ToString() : ""));
        }

        public async Task<CommandResult> ResendAsync(Invite invite, string[] arguments)
        {
            var list = invite.InvitedUsers;

            if (list == null)
            {
                return CommandResult.FromError("Cant resend. List does not exist.");
            }

            // Send invites

            invite.FormatData = arguments;
            StringBuilder warnings = new StringBuilder();

            foreach (var player in list)
            {
                if (player.InviteAccepted != true)
                {
                    continue;
                }

                var restGuildUser = await restClient.GetGuildUserAsync(guildId, player.User.UserId);
                try
                {
                    var message = string.Format(guildData.DMMessageFormat, arguments);

                    await restGuildUser.SendMessageAsync(message);
                }
                catch (Exception ex)
                {
                    warnings.AppendLine($"Could not invite {restGuildUser.Mention}. Exception: {ex.Message}");
                }
            }

            return CommandResult.FromSuccess("Players have been invited." + (warnings.Length > 0 ? "\r\n" + warnings.ToString() : ""));
        }

        public Task<UserInGuild[]> GetPlayerListAsync()
        {
            return Task.FromResult(guildData.GetSortedList().ToArray());
        }

        public Task<CommandResult> RemoveUserAsync(ulong guildUserId)
        {
            var user = guildData.GetUser(guildUserId);
            if (user == null)
            {
                return Task.FromResult(CommandResult.FromError("You are not on the waiting list!"));
            }
            else
            {
                user.IsInWaitingList = false;
                user.JoinTime = default;

                dataContext.Update(user);

                return Task.FromResult(CommandResult.FromSuccess("You left the waiting list!"));
            }
        }

        public void ClearUsers()
        {
            foreach (var userInGuild in guildData.UsersInGuild)
            {
                userInGuild.IsInWaitingList = false;
                userInGuild.JoinTime = default;
            }
        }
    }
}
