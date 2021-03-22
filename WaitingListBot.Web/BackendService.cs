using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace WaitingListBot.Web
{
    public class BackendService
    {
        readonly Uri baseAddress;
        readonly IHttpClientFactory clientFactory;

        public BackendService(Uri baseAddress, IHttpClientFactory clientFactory)
        {
            this.baseAddress = baseAddress;
            this.clientFactory = clientFactory;
        }

        private HttpClient GetHttpClient()
        {
            var client = clientFactory.CreateClient();
            client.BaseAddress = baseAddress;
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            return client;
        }

        public async Task<int> GetNumberOfGuilds()
        {
            var guildIds = await GetGuildIds();
            return guildIds.Count;
        }

        public async Task<List<ulong>> GetGuildIds()
        {
            var client = GetHttpClient();
            var result = await client.GetStringAsync("Guild/List");

            return JsonConvert.DeserializeObject<List<ulong>>(result);
        }

        public async Task<List<UserInListWithCounter>> GetPlayers(ulong guildId)
        {
            var client = GetHttpClient();
            var result = await client.GetStringAsync($"Guild/{guildId}/List");

            return JsonConvert.DeserializeObject<List<UserInListWithCounter>>(result);
        }

        public async Task<GuildInformation> GetGuildInformation(ulong guildId)
        {
            var client = GetHttpClient();
            var result = await client.GetStringAsync($"Guild/{guildId}/Info");

            return JsonConvert.DeserializeObject<GuildInformation>(result);
        }
    }
}
