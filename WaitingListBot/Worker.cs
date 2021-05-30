using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using WaitingListBot.Model;

namespace WaitingListBot
{
    internal class Worker : IHostedService
    {
        readonly IServiceCollection serviceCollection;
        readonly DiscordSocketClient client;
        readonly StorageFactory storageFactory = new();
        readonly string token;
        readonly CommandHandler handler;
        readonly ILogger logger;

        public Worker(ILogger<Worker> logger)
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions,
                AlwaysDownloadUsers = true,
                AlwaysAcknowledgeInteractions = false
            };

            client = new DiscordSocketClient(config);

            client.Log += Log;
            client.GuildMemberUpdated += GuildMemberUpdated;
            client.GuildUpdated += Client_GuildUpdated;
            client.GuildAvailable += Client_GuildAvailable;
            client.InteractionCreated += Client_InteractionCreated;

#if DEBUG
            token = File.ReadAllText("token-dev.txt");
#else
            token = File.ReadAllText("token.txt");
#endif

            // Commands are not thread safe. So set the run mode to sync
            var commandService = new CommandService(new CommandServiceConfig { DefaultRunMode = RunMode.Sync, LogLevel = LogSeverity.Info });

            serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(typeof(CommandService), commandService);
            serviceCollection.AddSingleton(typeof(StorageFactory), storageFactory);

            handler = new CommandHandler(client, commandService, serviceCollection.BuildServiceProvider());
            this.logger = logger;
        }

        private async Task Client_InteractionCreated(SocketInteraction arg)
        {
            if (arg.Type == InteractionType.MessageComponent)
            {
                var parsedArg = (SocketMessageComponent)arg;

                var customId = parsedArg.Data.CustomId;

                var guild = ((IGuildChannel)parsedArg.Channel).Guild;
                var storage = storageFactory.GetStorage(guild.Id);
                var waitingList = new CommandWaitingList(storage, client.Rest, guild.Id);

                if (arg.User.IsBot)
                {
                    return;
                }
                if (parsedArg.Message.Id == storage.ReactionMessageId)
                {
                    if (customId == "join")
                    {
                        if (!(await waitingList.AddUserAsync((IGuildUser)parsedArg.User)).Success)
                        {
                            // await arg.RespondAsync("Failed");
                            logger.LogError("Failed to join " + parsedArg.User);
                        }
                        else
                        {
                            await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, guild, storage);
                        }
                    }
                    else if (customId == "leave")
                    {
                        await waitingList.RemoveUserAsync(parsedArg.User.Id);
                        await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, guild, storage);
                    }

                    await parsedArg.AcknowledgeAsync();
                }
                else
                {
                    if (customId == "unpause")
                    {
                        storage.IsPaused = false;
                        storage.Save();

                        await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, guild, storage);

                        await parsedArg.AcknowledgeAsync();

                        await parsedArg.Message.DeleteAsync();
                    }
                    else if (customId == "clearcounters")
                    {
                        storage.PlayCounter.Clear();
                        storage.Save();

                        await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, guild, storage);

                        await parsedArg.RespondAsync("Counters have been cleared");
                    }
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            await client.SetActivityAsync(new Game("wl.pdelvo.com", ActivityType.Watching));

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

        private async Task Client_GuildAvailable(SocketGuild guild)
        {
            UpdateGuildInformation(guild);
            var storage = storageFactory.GetStorage(guild.Id);
            var waitingList = new CommandWaitingList(storage, client.Rest, guild.Id);

            // await ReactionWaitingListModule.SetWaitingListMembers(waitingList, guild, storage);
        }

        private async Task Client_GuildUpdated(SocketGuild oldGuild, SocketGuild newGuild)
        {
            UpdateGuildInformation(newGuild);
            var storage = storageFactory.GetStorage(newGuild.Id);
            var waitingList = new CommandWaitingList(storage, client.Rest, newGuild.Id);

            // await ReactionWaitingListModule.SetWaitingListMembers(waitingList, newGuild, storage);
        }

        private void UpdateGuildInformation(SocketGuild guild)
        {
            var guildStorage = storageFactory.GetStorage(guild.Id);
            guildStorage.Information = new GuildInformation()
            {
                Name = guild.Name,
                IconUrl = $"https://cdn.discordapp.com/icons/{guild.Id}/{guild.IconId}.png",
                Description = guild.Description
            };
            guildStorage.Save();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return client.StopAsync();
        }

        private async Task GuildMemberUpdated(SocketGuildUser before, SocketGuildUser after)
        {
            // Try to update
            var storageFactory = serviceCollection.BuildServiceProvider().GetService<StorageFactory>();

            var storage = storageFactory!.GetStorage(after.Guild.Id);

            foreach (var user in storage.List)
            {
                if (user.Id == after.Id)
                {
                    user.Name = after.Nickname ?? after.Username;
                    user.IsSub = after.Roles.Any(x => x.Id == storage.SubRoleId);
                }
            }
            var waitingList = new CommandWaitingList(storage, client.Rest, after.Guild.Id);
            await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, after.Guild, storage);
        }

        private Task Log(LogMessage logMessage)
        {
            if (logMessage.Exception != null)
            {
                logger.LogError(logMessage.Exception, "Exception was thrown in a Discord.Net method: " + logMessage.Message);
            }

            switch (logMessage.Severity)
            {
                case LogSeverity.Critical:
                    break;
                case LogSeverity.Error:
                    logger.LogCritical(logMessage.Message);
                    break;
                case LogSeverity.Warning:
                    logger.LogWarning(logMessage.Message);
                    break;
                case LogSeverity.Info:
                    logger.LogInformation(logMessage.Message);
                    break;
                case LogSeverity.Verbose:
                    break;
                case LogSeverity.Debug:
                    logger.LogDebug(logMessage.Message);
                    break;
                default:
                    logger.LogInformation(logMessage.Message);
                    break;
            }
            return Task.CompletedTask;
        }
    }
}
