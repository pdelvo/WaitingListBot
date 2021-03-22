using Discord;
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

        public Worker()
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions,
                AlwaysDownloadUsers = true
            };

            client = new DiscordSocketClient(config);

            client.Log += Log;
            client.GuildMemberUpdated += GuildMemberUpdated;
            client.GuildUpdated += Client_GuildUpdated;
            client.GuildAvailable += Client_GuildAvailable;
            client.ReactionAdded += Client_ReactionAdded;
            client.ReactionRemoved += Client_ReactionRemoved;

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
        }

        private async Task Client_ReactionRemoved(Cacheable<IUserMessage, ulong> oldMessage, Discord.WebSocket.ISocketMessageChannel messageChannel, Discord.WebSocket.SocketReaction reaction)
        {
            var guild = ((IGuildChannel)reaction.Channel).Guild;
            var storage = storageFactory.GetStorage(guild.Id);
            var waitingList = new CommandWaitingList(storage, client.Rest, guild.Id);
            if (reaction.User.Value.IsBot)
            {
                return;
            }
            if (reaction.MessageId == storage.ReactionMessageId)
            {
                await waitingList.RemoveUserAsync((IGuildUser)reaction.User.Value);
                await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, guild, storage);
            }
        }

        private async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> oldMessage, Discord.WebSocket.ISocketMessageChannel messageChannel, Discord.WebSocket.SocketReaction reaction)
        {
            var guild = ((IGuildChannel)reaction.Channel).Guild;
            var storage = storageFactory.GetStorage(guild.Id);
            var waitingList = new CommandWaitingList(storage, client.Rest, guild.Id);
            if (reaction.User.Value.IsBot)
            {
                return;
            }
            if (reaction.MessageId == storage.ReactionMessageId)
            {
                if (!(await waitingList.AddUserAsync((IGuildUser)reaction.User.Value)).Success)
                {
                    await reaction.Message.Value.RemoveReactionAsync(storage.ReactionEmote, reaction.User.Value);
                }
                else
                {
                    await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, guild, storage);
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
            await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, guild, storage);
        }

        private async Task Client_GuildUpdated(SocketGuild oldGuild, SocketGuild newGuild)
        {
            UpdateGuildInformation(newGuild);
            var storage = storageFactory.GetStorage(newGuild.Id);
            var waitingList = new CommandWaitingList(storage, client.Rest, newGuild.Id);
            await ReactionWaitingListModule.UpdateReactionMessageAsync(waitingList, newGuild, storage);
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
            Console.WriteLine(logMessage.ToString());
            return Task.CompletedTask;
        }
    }
}
