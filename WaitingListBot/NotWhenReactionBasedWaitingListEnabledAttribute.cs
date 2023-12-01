using Discord;
using Discord.Commands;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WaitingListBot.Data;

namespace WaitingListBot
{
    public class NotWhenReactionBasedWaitingListEnabledAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var guild = context.Guild;

            if (guild != null)
            {
                using (var dataContext = services.GetRequiredService<WaitingListDataContext>())
                {
                    var guildData = dataContext.GetOrCreateGuildData(guild);

                    if (guildData.IsEnabled == true)
                    {
                        return Task.FromResult(PreconditionResult.FromError("Cannot run this command while a reaction waiting list is active"));
                    }
                }
            }

            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}