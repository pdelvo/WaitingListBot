using Discord;
using Discord.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot
{
    public class ModPermissionAttribute : PreconditionAttribute
    {
        static readonly ulong[] GlobalModList = new ulong[]
        {
            367018778409566209 // Me
        };
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var guildUser = context.User as IGuildUser;
            if (guildUser == null)
            {
                return Task.FromResult(PreconditionResult.FromError("The command can only be run in a guild"));
            }

            if (GlobalModList.Contains(guildUser.Id))
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }

            if (guildUser.GuildPermissions.BanMembers)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }

            return Task.FromResult(PreconditionResult.FromError("You do not have permissions to use this command."));
        }
    }
}
