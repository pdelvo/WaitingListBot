using Discord.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WaitingListBot.Data;

namespace WaitingListBot
{
    public class CheckIfWaitingListIsActiveAttribute : PreconditionAttribute
    {
        bool allowRunningIfListIsActive;

        public CheckIfWaitingListIsActiveAttribute(bool allowRunningIfListIsActive)
        {
            this.allowRunningIfListIsActive = allowRunningIfListIsActive;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var guild = context.Guild;

            if (guild != null)
            {
                var dataContext = (WaitingListDataContext)services.GetService(typeof(WaitingListDataContext))!;

                var guildData = dataContext.GetGuild(guild.Id);

                if (allowRunningIfListIsActive == guildData?.IsEnabled)
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }
                else
                {
                    if (allowRunningIfListIsActive)
                    {
                        return Task.FromResult(PreconditionResult.FromError("Cannot run this command if the waiting list is closed."));
                    }
                    else
                    {
                        return Task.FromResult(PreconditionResult.FromError("Cannot run this command if the waiting list is open."));
                    }
                }
            }

            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
