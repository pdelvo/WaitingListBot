using Discord;

using System;
using System.Threading.Tasks;

using WaitingListBot.Data;

namespace WaitingListBot.Model
{
    public interface IWaitingList
    {
        Task<CommandResult> AddUserAsync(IGuildUser guildUser);
        Task<CommandResult> RemoveUserAsync(ulong guildUserId);

        Task<(CommandResult commandResult, Invite invite)> GetInvite(string[] arguments, int numberOfPlayers, bool removeFromList = true);
        Task<CommandResult> ResendAsync(Invite invite, string[] arguments);

        Task<UserInGuild[]> GetPlayerListAsync();
        void ClearUsers();
    }

    public record CommandResult(bool Success, string? Message)
    {
        static readonly CommandResult success = new(true, null);

        public static CommandResult SuccessResult => success;

        public static CommandResult FromSuccess(string message) => new(true, message);
        public static CommandResult FromError(string message) => new(false, message);
    }
}
