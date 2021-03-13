﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WaitingListBot
{
    internal class Worker : IHostedService
    {
        static IServiceCollection serviceCollection;
        DiscordSocketClient client;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages,
                AlwaysDownloadUsers = true
            };

            client = new DiscordSocketClient(config);

            client.Log += Log;
            client.GuildMemberUpdated += GuildMemberUpdated;

#if DEBUG
            var token = File.ReadAllText("token-dev.txt");
#else
            var token = File.ReadAllText("token.txt");
#endif

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            await client.SetActivityAsync(new Game("wl.pdelvo.com", ActivityType.Watching));

            // Commands are not thread safe. So set the run mode to sync
            var commandService = new CommandService(new CommandServiceConfig { DefaultRunMode = RunMode.Sync, LogLevel = LogSeverity.Info });

            serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(typeof(CommandService), commandService);
            serviceCollection.AddSingleton(typeof(StorageFactory), new StorageFactory());

            var handler = new CommandHandler(client, commandService, serviceCollection.BuildServiceProvider());

            await handler.InstallCommandsAsync();

            // Set up Api

            WebHost.CreateDefaultBuilder()
                .ConfigureServices(s =>
                {
                    s.AddControllers();
                    foreach (var service in serviceCollection)
                    {
                        s.Add(service);
                    }
                })
                .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(e =>
                {
                    e.MapControllers();
                });
            })
                .UseKestrel()
                .UseUrls("http://*:8123/")
                .Build().Run();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return client.StopAsync();
        }

        private Task GuildMemberUpdated(SocketGuildUser before, SocketGuildUser after)
        {
            // Try to update
            var storageFactory = serviceCollection.BuildServiceProvider().GetService<StorageFactory>();

            var storage = storageFactory.GetStorage(after.Guild.Id);

            foreach (var user in storage.List)
            {
                if (user.Id == after.Id)
                {
                    user.Name = after.Nickname ?? after.Username;
                    user.IsSub = after.Roles.Any(x => x.Id == storage.SubRoleId);
                }
            }

            return Task.CompletedTask;
        }

        private Task Log(LogMessage logMessage)
        {
            Console.WriteLine(logMessage.ToString());
            return Task.CompletedTask;
        }
    }
}
