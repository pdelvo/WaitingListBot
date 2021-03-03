using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot
{
    // https://discord.com/api/oauth2/authorize?client_id=813143877615484978&permissions=68608&scope=bot
    // Test: https://discord.com/api/oauth2/authorize?client_id=815372800407502902&permissions=68608&scope=bot
    class Program
    {
        static IServiceCollection serviceCollection;

        public static async Task Main()
        {
            var program = new Program();
            await program.RunAsync();
        }

        private async Task RunAsync()
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages,
                AlwaysDownloadUsers = true
            };

            var client = new DiscordSocketClient(config);

            client.Log += Log;
            client.GuildMemberUpdated += GuildMemberUpdated;

            var token = File.ReadAllText("token.txt");

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            await client.SetGameAsync("Rocket League");
            await client.SetActivityAsync(new Game("Rocket League", ActivityType.Watching));

            // Commands are not thread safe. So set the run mode to sync
            var commandService = new CommandService(new CommandServiceConfig { DefaultRunMode = RunMode.Sync, LogLevel = LogSeverity.Info });

            serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(typeof(CommandService), commandService);
            serviceCollection.AddSingleton(typeof(StorageFactory), new StorageFactory());

            var handler = new CommandHandler(client, commandService, serviceCollection.BuildServiceProvider());

            await handler.InstallCommandsAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
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
