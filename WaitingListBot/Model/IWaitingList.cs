using Discord;

using System;
using System.Threading.Tasks;

namespace WaitingListBot.Model
{
    public interface IWaitingList
    {
        Task<CommandResult> AddUserAsync(IGuildUser guildUser);
        Task<CommandResult> RemoveUserAsync(ulong guildUserId);
        Task SetUsersAsync (IGuildUser[] guildUsers);

        Task<(CommandResult commandResult, UserInListWithCounter[]? players)> GetNextPlayersAsync(object[] arguments, int numberOfPlayers, bool removeFromList = true);

        Task<UserInListWithCounter[]> GetPlayerListAsync();
    }

    public record CommandResult(bool Success, string? Message)
    {
        static readonly CommandResult success = new(true, null);

        public static CommandResult SuccessResult => success;

        public static CommandResult FromSuccess(string message) => new(true, message);
        public static CommandResult FromError(string message) => new(false, message);
    }
}
