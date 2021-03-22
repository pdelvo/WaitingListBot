﻿using Microsoft.AspNetCore.Mvc;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot.Api
{
    [Route("[controller]")]
    [ApiController]
    public class GuildController : ControllerBase
    {
        readonly StorageFactory storageFactory;

        public GuildController(StorageFactory storageFactory)
        {
            this.storageFactory = storageFactory;
        }

        [HttpGet]
        [Route("List")]
        public IEnumerable<ulong> List()
        {
            return storageFactory.ListIds();
        }

        [HttpGet]
        [Route("{guildId}/List")]
        public IEnumerable<UserInListWithCounter>? ListPlayers(ulong guildId)
        {
            var storage = storageFactory.GetStorage(guildId);

            return storage?.GetSortedList();
        }

        [HttpGet]
        [Route("{guildId}/Info")]
        public GuildInformation GetGuildInformation(ulong guildId)
        {
            var storage = storageFactory.GetStorage(guildId);

            return storage.Information;
        }
    }
}
