using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WaitingListBot.Web.Areas.Identity.Pages
{
    public class WaitingListModel : PageModel
    {
        HealthCheckService healthCheckService;
        BackendService backendService;

        public List<UserInListWithCounter> Users;

        [BindProperty(SupportsGet = true)]
        public bool Minimal { get; set; }

        public string GuildName { get; set; }

        public string GuildIconUrl { get; set; }

        public string GuildDescription { get; set; }

        public WaitingListModel(HealthCheckService healthCheckService, BackendService backendService)
        {
            this.healthCheckService = healthCheckService;
            this.backendService = backendService;
        }
        public ulong Id { get; set; }
        public async Task<IActionResult> OnGet(ulong id)
        {
            this.Id = id;
            var guildInformation = await backendService.GetGuildInformation(id);
            GuildName = guildInformation?.Name;
            GuildIconUrl = guildInformation?.IconUrl;
            GuildDescription = guildInformation?.Description;
            return Page();
        }
        public async Task<JsonResult> OnGetData(ulong id)
        {
            Id = id;

            if ((await healthCheckService.CheckHealthAsync()).Status == HealthStatus.Healthy)
            {
                var players = await backendService.GetPlayers(id);
                Users = players;
            }

            return new JsonResult(Users);
        }
    }
}