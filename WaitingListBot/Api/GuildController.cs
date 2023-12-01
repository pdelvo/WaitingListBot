using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;

using WaitingListBot.Data;

namespace WaitingListBot.Api
{
    [Route("[controller]")]
    [ApiController]
    public class GuildController : ControllerBase
    {
        readonly IServiceProvider serviceProvider;

        public GuildController(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        [HttpGet]
        [Route("List")]
        public IEnumerable<ulong> List()
        {
            using (var dataContext = serviceProvider.GetRequiredService<WaitingListDataContext>())
            {
                return dataContext.GuildData.AsQueryable().Select(x => x.GuildId);
            }
        }

        [HttpGet]
        [Route("{guildId}/List")]
        public IEnumerable<UserInGuild>? ListPlayers(ulong guildId)
        {
            using (var dataContext = serviceProvider.GetRequiredService<WaitingListDataContext>())
            {
                var guildData = dataContext.GetGuild(guildId);

                List<UserInGuild>? sortedList = guildData?.GetSortedList();

                // We dont want to leak this
                foreach (var item in sortedList!)
                {
                    item.UserId = 0;
                    item.Guild = null!;
                }

                return sortedList;
            }
        }

        [HttpGet]
        [Route("{guildId}/Info")]
        public GuildInformation? GetGuildInformation(ulong guildId)
        {
            using (var dataContext = serviceProvider.GetRequiredService<WaitingListDataContext>())
            {
                var guildData = dataContext.GetGuild(guildId);

                if (guildData == null)
                {
                    return null;
                }

                return new GuildInformation
                {
                    Name = guildData.Name,
                    Description = guildData.Description,
                    IconUrl = guildData.IconUrl
                };
            }
        }
    }
}
