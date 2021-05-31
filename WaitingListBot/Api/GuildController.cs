using Microsoft.AspNetCore.Mvc;
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
        readonly WaitingListDataContext dataContext;

        public GuildController(WaitingListDataContext dataContext)
        {
            this.dataContext = dataContext;
        }

        [HttpGet]
        [Route("List")]
        public IEnumerable<ulong> List()
        {
            return dataContext.GuildData.AsQueryable().Select(x => x.GuildId);
        }

        [HttpGet]
        [Route("{guildId}/List")]
        public IEnumerable<UserInGuild>? ListPlayers(ulong guildId)
        {
            var guildData = dataContext.GetGuild(guildId);

            return guildData?.GetSortedList();
        }

        [HttpGet]
        [Route("{guildId}/Info")]
        public GuildInformation? GetGuildInformation(ulong guildId)
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
