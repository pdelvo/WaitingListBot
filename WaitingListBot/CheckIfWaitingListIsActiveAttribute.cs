using Discord.Commands;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WaitingListBot.Data;

using static System.Formats.Asn1.AsnWriter;

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
            using var scope = services.CreateAsyncScope();
            var scopedServices = scope.ServiceProvider;
            var guild = context.Guild;

            if (guild != null)
            {
                using (var waitingListDataContext = scopedServices.GetRequiredService<WaitingListDataContext>())
                {

                    var guildData = waitingListDataContext.GetGuild(guild.Id);

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
            }
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
