using Discord.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
                var storageProvider = (StorageFactory)services.GetService(typeof(StorageFactory))!;

                var storage = storageProvider.GetStorage(guild.Id);

                if (allowRunningIfListIsActive == storage.IsEnabled)
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
