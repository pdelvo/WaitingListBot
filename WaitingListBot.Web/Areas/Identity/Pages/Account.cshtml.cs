using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Newtonsoft.Json;

using WaitingListBot.Web.Model;

namespace WaitingListBot.Web.Areas.Identity.Pages
{
    public class AccountModel : PageModel
    {
        HealthCheckService healthCheckService;
        BackendService backendService;
        public DiscordServer[] Servers { get; set; }

        public AccountModel(HealthCheckService healthCheckService, BackendService backendService)
        {
            this.healthCheckService = healthCheckService;
            this.backendService = backendService;
        }

        public async Task<IActionResult> OnGet()
        {
            if (!HttpContext.User.Identity.IsAuthenticated)
            {
                return Challenge(AspNet.Security.OAuth.Discord.DiscordAuthenticationDefaults.AuthenticationScheme);
            }

            if ((await healthCheckService.CheckHealthAsync()).Status == HealthStatus.Healthy)
            {
                var guildIds = await backendService.GetGuildIds();
                var userId = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

                var guildClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/guilds");

                if(guildClaim != null)
                {
                    var json = guildClaim.Value;
                    DiscordServer[] serverList = JsonConvert.DeserializeObject<DiscordServer[]>(json);
                    Servers = serverList.Join(guildIds, k => k.Id, k => k, (l, r) => l)
                        .Where(x => (x.Permissions & Permissions.BAN_MEMBERS) != 0 || userId == "367018778409566209").ToArray();
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnGetLogout()
        {
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                await HttpContext.SignOutAsync();
            }

            return RedirectToPage("Index");
        }
    }
}
