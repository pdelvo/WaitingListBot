using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.EventLog;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot
{
    // https://discord.com/api/oauth2/authorize?client_id=813143877615484978&permissions=355392&scope=bot
    // Test: https://discord.com/api/oauth2/authorize?client_id=815372800407502902&permissions=355392&scope=bot
    class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
          Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<Worker>()
            .Configure<EventLogSettings>(config =>
                {
                    config.LogName = "Waiting List Bot";
                    config.SourceName = "Waiting List Bot";
                });
            });
    }
}
