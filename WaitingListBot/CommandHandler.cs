using Discord;
using Discord.Commands;
using Discord.WebSocket;

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using WaitingListBot.Data;

namespace WaitingListBot
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        // Retrieve client and CommandService instance via ctor
        public CommandHandler(DiscordSocketClient client, CommandService commands, IServiceProvider services)
        {
            _commands = commands;
            _client = client;
            this._services = services;
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.MessageReceived += HandleCommandAsync;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: _services);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            var dataContext = (WaitingListDataContext)_services.GetService(typeof(WaitingListDataContext))!;

            // Don't process the command if it was a system message
            if (messageParam is not SocketUserMessage message) return;

            if (message.Author.IsBot)
                return;

            var guild = (message.Channel as SocketTextChannel)?.Guild;
            if (guild == null)
            {
                await messageParam.Channel.SendMessageAsync("Sorry I dont work in private messages!");
                return;
            }

            var guildData = dataContext!.GetOrCreateGuildData(guild);

            var channel = message.Channel;

            // Only allow mods to issue commands
            var guildUser = message.Author as IGuildUser;
            if (!ModPermissionAttribute.HasModPermission(guildUser).IsSuccess)
            {
                return;
            }

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasStringPrefix(guildData?.CommandPrefix, ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos)))
                return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            var result = await _commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: _services);

            if (!result.IsSuccess)
            {
                if (result.Error != CommandError.UnknownCommand)
                {
                    await messageParam.Channel.SendMessageAsync("Could not complete command: " + result.ErrorReason);
                }
            }
        }
    }
}
