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

        public GuildController()
        {
            dataContext = new WaitingListDataContext();
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

            List<UserInGuild>? sortedList = guildData?.GetSortedList();

            // We dont want to leak this
            foreach (var item in sortedList!)
            {
                item.UserId = 0;
                item.Guild = null!;
            }

            return sortedList;
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
