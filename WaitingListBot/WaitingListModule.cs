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
        Storage _storage;
        CommandService _commandService;

        public WaitingListModule(Storage storage, CommandService commandService)
        {
            _storage = storage;
            _commandService = commandService;
        }

        [Command("waitingchannel")]
        [Summary("Selects the current channel as the waiting list.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task MarkAsWaitingChannelAsync()
        {
            _storage.WaitingListChannelId = Context.Channel.Id;
            _storage.Save();
            await ReplyAsync("Channel has been set as waiting channel");
        }

        [Command("nuke")]
        [Summary("Clears the waiting list.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task ClearWaitingListAsync()
        {
            _storage.PlayCounter.Clear();
            _storage.List.Clear();
            _storage.Save();
            await ReplyAsync("List has been cleared");
        }

        [Command("next")]
        [Summary("Notifies the next players.")]
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You do not have permissions to use this command.")]
        public async Task NextAsync([Summary("Number of players")]int numberOfPlayers, [Summary("Password")]string password)
        {
            var list = GetSortedList();

            if (list.Count < numberOfPlayers)
            {
                await ReplyAsync($"Did not send invites. There are only {list.Count} players in the list.");
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

                var user = Context.Client.GetUser(player.Id);

                var message = "You are next in line to play!\r\n";
                message += "Join the private match with the following details:\r\n";
                message += "Name: Berry\r\n";
                message += $"Password: {password}\r\n";
                message += "Please make sure that you have cross platform play enabled!";


                await user.SendMessageAsync(message);
            }
        }

        [Command("play")]
        [Summary("Enters the waiting list.")]
        public async Task PlayAsync()
        {
            var guildUser = Context.User as IGuildUser;

            if (guildUser == null)
            {
                return;
            }

            if (_storage.List.Any(x => x.Id == Context.User.Id))
            {
                await Context.Message.ReplyAsync($"You are already on the waiting list!");
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
                    IsSub = guildUser.RoleIds.Contains(765730759095877632ul)
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
            var embedBuilder = new EmbedBuilder();
            embedBuilder.Color = Color.Green;
            embedBuilder.Title = $"Waiting list";
            var sortedList = GetSortedList();
            var description = "";
            int counter = 0;
            foreach (var player in sortedList)
            {
                description += $"**{++counter}.** {Context.Client.GetUser(player.Id).Mention} ({(player.IsSub ? "Sub " : "")}Played {GetPlayCounterById(player.Id)} times already)\r\n";
            }
            embedBuilder.Description = description;

            await Context.Message.ReplyAsync($"Here are the next players in line:", embed: embedBuilder.Build(), allowedMentions: AllowedMentions.None);
        }

        [Command("help")]
        [Summary("Shows this help message.")]
        public async Task Help()
        {
            List<CommandInfo> commands = _commandService.Commands.ToList();
            EmbedBuilder embedBuilder = new EmbedBuilder();

            foreach (CommandInfo command in commands)
            {
                if (!(await command.CheckPreconditionsAsync(Context)).IsSuccess)
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

            await ReplyAsync("Here's a list of commands and their description: ", false, embedBuilder.Build());
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
