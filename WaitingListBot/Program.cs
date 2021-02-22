using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
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

            //  You can assign your bot token to a string, and pass that in to connect.
            //  This is, however, insecure, particularly if you plan to have your code hosted in a public repository.
            var token = "ODEzMTQzODc3NjE1NDg0OTc4.YDLBPw.hUAJTHTkAMl3SCsIBNnNQJJmY_w";

            // Some alternative options would be to keep your token in an Environment Variable or a standalone file.
            // var token = Environment.GetEnvironmentVariable("NameOfYourEnvironmentVariable");
            // var token = File.ReadAllText("token.txt");
            // var token = JsonConvert.DeserializeObject<AConfigurationClass>(File.ReadAllText("config.json")).Token;

            //_client.MessageReceived += MessageReceivedAsync;

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
