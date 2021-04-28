using Discord;
using Discord.WebSocket;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot.Model
{
    public class CommandWaitingList : IWaitingList
    {
        readonly Storage storage;
        readonly DiscordSocketRestClient restClient;
        readonly ulong guildId;


        public CommandWaitingList(Storage storage, DiscordSocketRestClient restClient, ulong guildId)
        {
            this.storage = storage;
            this.restClient = restClient;
            this.guildId = guildId;
        }

        public Task<CommandResult> AddUserAsync(IGuildUser guildUser)
        {
            if (!storage.IsEnabled)
            {
                return Task.FromResult(CommandResult.FromError("Waiting list is not open"));
            }

            if (storage.List.Any(x => x.Id == guildUser.Id))
            {
                return Task.FromResult(CommandResult.FromError("You are already on the waiting list!"));
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

                return Task.FromResult(CommandResult.FromSuccess($"Waiting list joined!"));
            }
        }

        public async Task<(CommandResult commandResult, (CommandResult, UserInListWithCounter)[]? players)> GetNextPlayersAsync(object[] arguments, int numberOfPlayers, bool removeFromList = true)
        {
            var list = storage.GetSortedList();

            if (list.Count < numberOfPlayers)
            {
                return (CommandResult.FromError($"Did not send invites. There are only {list.Count} players in the list."), null);
            }
            // Send invites

            void IncreasePlayCounter(ulong id)
            {
                var entry = storage.PlayCounter.SingleOrDefault(x => x.Id == id);

                if (entry == null)
                {
                    storage.PlayCounter.Add(new PlayCounter { Id = id, Counter = 1 });
                }
                else
                {
                    entry.Counter++;
                }
            }

            List<(CommandResult, UserInListWithCounter)> invitedPlayers = new();

            for (int i = 0; i < numberOfPlayers; i++)
            {
                var player = list[i];
                storage.List.Remove(storage.List.Single(x => x.Id == player.Id));
                IncreasePlayCounter(player.Id);
                storage.Save();

                var restGuildUser = await restClient.GetGuildUserAsync(guildId, player.Id);
                try
                {
                    var message = string.Format(storage.DMMessageFormat, arguments);

                    await restGuildUser.SendMessageAsync(message);

                    invitedPlayers.Add((CommandResult.FromSuccess("Player invited"), player));
                }
                catch (FormatException)
                {
                    return (CommandResult.FromError("The arguments had the wrong format"), null);
                }
                catch (Exception ex)
                {
                    invitedPlayers.Add((CommandResult.FromError($"Could not invite {restGuildUser.Mention}. Exception: {ex.Message}"), player));
                }
            }

            return (CommandResult.FromSuccess("Players have been invited."), invitedPlayers.ToArray());
        }

        public async Task<(CommandResult commandResult, (CommandResult, UserInListWithCounter)[]? players)> ResendAsync(object[] arguments)
        {
            var list = storage.LastInvited;

            if (list == null)
            {
                return (CommandResult.FromError("Cant resend. List does not exist."), null);
            }

            // Send invites

            List<(CommandResult, UserInListWithCounter)> invitedPlayers = new();

            for (int i = 0; i < list.Count; i++)
            {
                var player = list[i];

                var restGuildUser = await restClient.GetGuildUserAsync(guildId, player.Id);
                try
                {
                    var message = string.Format(storage.DMMessageFormat, arguments);

                    await restGuildUser.SendMessageAsync(message);

                    invitedPlayers.Add((CommandResult.FromSuccess("Player invited"), player));
                }
                catch (FormatException)
                {
                    return (CommandResult.FromError("The arguments had the wrong format"), null);
                }
                catch (Exception ex)
                {
                    invitedPlayers.Add((CommandResult.FromError($"Could not invite {restGuildUser.Mention}. Exception: {ex.Message}"), player));
                }
            }

            return (CommandResult.FromSuccess("Players have been invited."), invitedPlayers.ToArray());
        }

        public Task<UserInListWithCounter[]> GetPlayerListAsync()
        {
            return Task.FromResult(storage.GetSortedList().ToArray());
        }

        public Task<CommandResult> RemoveUserAsync(ulong guildUserId)
        {
            var entry = storage.List.SingleOrDefault(x => x.Id == guildUserId);
            if (entry == null)
            {
                return Task.FromResult(CommandResult.FromError("You are not on the waiting list!"));
            }
            else
            {
                storage.List.Remove(entry);
                storage.Save();

                return Task.FromResult(CommandResult.FromSuccess("You left the waiting list!"));
            }
        }

        public async Task SetUsersAsync(IGuildUser[] guildUsers)
        {
            storage.List = storage.List.Where(x => guildUsers.Any(user => x.Id == user.Id)).ToList();

            foreach (var user in guildUsers)
            {
                await AddUserAsync(user);
            }
        }
    }
}
