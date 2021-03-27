using Discord;
using Discord.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot
{
    public class NotWhenReactionBasedWaitingListEnabledAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var guild = context.Guild;

            if (guild != null)
            {
                var storageProvider = (StorageFactory)services.GetService(typeof(StorageFactory))!;

                var storage = storageProvider.GetStorage(guild.Id);

                if (await ReactionWaitingListModule.IsReactionBasedWaitingListActiveAsync(guild, storage))
                {
                    return PreconditionResult.FromError("Cannot run this command while a reaction waiting list is active");
                }
            }

            return PreconditionResult.FromSuccess();
        }
    }
}
