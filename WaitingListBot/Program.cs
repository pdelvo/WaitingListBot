using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot
{
    class Program
    {
        private DiscordSocketClient _client;
        Storage _storage;

        public static async Task Main(string[] args)
        {
            var program = new Program();
            await program.RunAsync();
        }

        private async Task RunAsync()
        {
            _storage = Storage.FromFile();

            _client = new DiscordSocketClient();

            _client.Log += Log;

            var token = File.ReadAllText("token.txt");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            var commandService = new CommandService(new CommandServiceConfig { DefaultRunMode = RunMode.Sync, LogLevel = LogSeverity.Info });

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(typeof(Storage), _storage);
            serviceCollection.AddSingleton(typeof(CommandService), commandService);

            var handler = new CommandHandler(_client, commandService, serviceCollection.BuildServiceProvider());

            await handler.InstallCommandsAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }


        private Task Log(LogMessage logMessage)
        {
            Console.WriteLine(logMessage.ToString());
            return Task.CompletedTask;
        }
    }
}
