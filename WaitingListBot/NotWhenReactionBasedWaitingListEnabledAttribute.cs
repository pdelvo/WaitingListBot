using Discord;
using Discord.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WaitingListBot.Data;

namespace WaitingListBot
{
    public class NotWhenReactionBasedWaitingListEnabledAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var guild = context.Guild;

            if (guild != null)
            {
                var dataContext = (WaitingListDataContext)services.GetService(typeof(WaitingListDataContext))!;

                var guildData = dataContext.GetGuild(guild.Id);

                if (guildData.IsEnabled == true)
                {
                    return PreconditionResult.FromError("Cannot run this command while a reaction waiting list is active");
                }
            }

            return PreconditionResult.FromSuccess();
        }
    }
}
