using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

using Newtonsoft.Json;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using WaitingListBot.Data;
using WaitingListBot.Model;

namespace WaitingListBot
{
    public class Worker : IHostedService
    {
        readonly IServiceCollection serviceCollection;
        static ServiceProvider serviceProvider;
        readonly DiscordSocketClient client;
        readonly CommandHandler handler;
        readonly ILogger logger;
        private readonly WaitingListBotConfiguration configuration;
        private ConcurrentDictionary<int, byte> inviteMessageUpdates = new ConcurrentDictionary<int, byte>();
        private ConcurrentDictionary<int, byte> publicMessageUpdates = new ConcurrentDictionary<int, byte>();

        readonly Timer DMTimeout;
        bool timerRunning = false;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages
                | GatewayIntents.GuildMessageReactions | GatewayIntents.DirectMessages | GatewayIntents.DirectMessageReactions
                | GatewayIntents.MessageContent,
                AlwaysDownloadUsers = true
            };

            client = new DiscordSocketClient(config);

            client.Log += Log;
            client.GuildMemberUpdated += GuildMemberUpdated;
            client.GuildUpdated += Client_GuildUpdated;
            client.GuildAvailable += Client_GuildAvailable;
            client.ButtonExecuted += Client_ButtonExecuted;

            this.configuration = configuration.Get<WaitingListBotConfiguration>();

            // Commands are not thread safe. So set the run mode to sync
            var commandService = new CommandService(new CommandServiceConfig { DefaultRunMode = RunMode.Async, LogLevel = LogSeverity.Info });

            serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(typeof(CommandService), commandService);
            serviceCollection.AddDbContext<WaitingListDataContext>();
            serviceCollection.AddSingleton(this.configuration);
            serviceCollection.AddSingleton(this);
            serviceCollection.AddLogging(b =>
            {
                b.AddConsole();
                b.AddEventSourceLogger();
            });

            serviceProvider = serviceCollection.BuildServiceProvider();
            using (var asyncScope = serviceProvider.CreateScope())
            {
                using (var waitingListDataContext = asyncScope.ServiceProvider.GetRequiredService<WaitingListDataContext>())
                {
                    waitingListDataContext.Database.Migrate();
                }
            }

            handler = new CommandHandler(client, commandService, serviceCollection.BuildServiceProvider());
            this.logger = logger;

            DMTimeout = new Timer(CheckForTimeout, null, 0, 1000);
        }

        private async void CheckForTimeout(object? state)
        {
            if (timerRunning)
            {
                return;
            }
            timerRunning = true;
            try
            {
                using (var asyncScope = serviceProvider.CreateScope())
                {
                    using (var waitingListDataContext = asyncScope.ServiceProvider.GetRequiredService<WaitingListDataContext>())
                    {
                        foreach (var invitedUser in waitingListDataContext.InvitedUsers
                            .Include(iu => iu.User).ThenInclude(iu => iu.Guild).Include(iu => iu.Invite).ToList())
                        {
                            if (invitedUser.InviteAccepted == null)
                            {
                                if (invitedUser.InviteTime + TimeSpan.FromMinutes(1) < DateTime.Now)
                                {
                                    var guild = client.GetGuild(invitedUser.User.Guild.GuildId);
                                    if (guild != null)
                                    {
                                        var waitingList = new CommandWaitingList(waitingListDataContext!, client.Rest, guild.Id);

                                        try
                                        {
                                            await DeclineInviteAsync(client.Rest, guild, waitingList, invitedUser);

                                            var dmChannel = await (await client.Rest.GetUserAsync(invitedUser.User.UserId)).CreateDMChannelAsync();

                                            var message = await dmChannel.GetMessageAsync(invitedUser.DmQuestionMessageId);

                                            await message.DeleteAsync();
                                            await dmChannel.SendMessageAsync("Time ran out. Invite has been declined");
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError("Error declining invite", ex);
                                        }

                                        waitingListDataContext.Update(invitedUser);

                                        waitingListDataContext.SaveChanges();
                                        AddInviteUpdateToQueue(invitedUser.Invite.Id);
                                    }
                                }
                            }
                        }
                        var ids = inviteMessageUpdates.Keys.ToArray();

                        foreach (var id in ids)
                        {
                            inviteMessageUpdates.TryRemove(id, out _);
                        }
                        var invites = waitingListDataContext.Invites.Include(x => x.Guild).Where(x => ids.Contains(x.Id)).ToArray();
                        foreach (var invite in invites)
                        {
                            var guild = client.GetGuild(invite.Guild.GuildId);
                            await CommandWaitingListModule.UpdateInviteMessageAsync(guild, invite);
                        }
                        ids = publicMessageUpdates.Keys.ToArray();

                        foreach (var id in ids)
                        {
                            publicMessageUpdates.TryRemove(id, out _);
                        }
                        var guilds = waitingListDataContext.GuildData.Where(x => ids.Contains(x.Id)).Include(x => x.UsersInGuild).ToArray();
                        foreach (var guild in guilds)
                        {
                            var discordGuild = client.GetGuild(guild.GuildId);
                            await ButtonWaitingListModule.UpdatePublicMessageAsync(discordGuild, guild);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Timeout timer failed to run");
            }
            timerRunning = false;
        }

        public void AddInviteUpdateToQueue(int id)
        {
            inviteMessageUpdates.AddOrUpdate(id, 0, (_, _) => 0);
        }

        public void AddPublicMessageUpdateToQueue(int guildId)
        {
            publicMessageUpdates.AddOrUpdate(guildId, 0, (_, _) => 0);
        }

        private async Task Client_ButtonExecuted(SocketInteraction arg)
        {
            logger.LogInformation("Button started " + DateTime.Now);
            try
            {
                using (var asyncScope = serviceProvider.CreateAsyncScope())
                {
                    using (var waitingListDataContext = asyncScope.ServiceProvider.GetRequiredService<WaitingListDataContext>())
                    {
                        if (arg.Type == InteractionType.MessageComponent)
                        {
                            var parsedArg = (SocketMessageComponent)arg;

                            var customId = parsedArg.Data.CustomId;

                            var guild = (parsedArg.Channel as IGuildChannel)?.Guild;
                            var guildData = guild != null ? waitingListDataContext.GetGuild(guild.Id) : null;
                            var waitingList = guild != null ? new CommandWaitingList(waitingListDataContext, client.Rest, guild.Id) : null;

                            if (arg.User.IsBot)
                            {
                                return;
                            }
                            if (parsedArg.Message.Id == guildData?.PublicMessageId)
                            {
                                if (guild == null || guildData == null || waitingList == null)
                                {
                                    logger.LogCritical("Guild or guildData was null in InteractionCreated");
                                    return;
                                }
                                if (customId == "join")
                                {
                                    logger.LogInformation("Button join " + DateTime.Now);
                                    if ((await waitingList.AddUserAsync((IGuildUser)parsedArg.User)).Success)
                                    {
                                        logger.LogInformation("Button defered " + DateTime.Now);
                                        await parsedArg.DeferAsync();
                                        // await parsedArg.RespondAsync("Joined Waiting list.", ephemeral: true);
                                    }
                                    else
                                    {
                                        await arg.RespondAsync("Failed to join", ephemeral: true);
                                        logger.LogError("Failed to join " + parsedArg.User);
                                    }
                                }
                                else if (customId == "leave")
                                {
                                    await waitingList.RemoveUserAsync(parsedArg.User.Id);

                                    await parsedArg.DeferAsync();
                                    //await parsedArg.RespondAsync("Left waiting list.", ephemeral: true);
                                }

                                waitingListDataContext.SaveChanges();
                                guildData = waitingListDataContext.GetGuild(guild.Id);

                                AddPublicMessageUpdateToQueue(guildData.Id);
                            }
                            else
                            {
                                if (customId == "unpause")
                                {
                                    guildData!.IsPaused = false;
                                    waitingListDataContext.Update(guildData);
                                    waitingListDataContext.SaveChanges();


                                    AddPublicMessageUpdateToQueue(guildData.Id);

                                    await parsedArg.DeferAsync();

                                    await parsedArg.Message.DeleteAsync();
                                }
                                else if (customId == "clearcounters")
                                {
                                    foreach (var user in guildData!.UsersInGuild.Where(x=> x.PlayCount > 0))
                                    {
                                        user.PlayCount = 0;
                                        waitingListDataContext.Update(user);
                                    }
                                    waitingListDataContext.SaveChanges();


                                    AddPublicMessageUpdateToQueue(guildData.Id);

                                    await parsedArg.RespondAsync("Counters have been cleared");
                                }
                                else if (customId.StartsWith("joinYes") || customId.StartsWith("joinNo"))
                                {
                                    var parts = customId.Split(new[] { ';' }, 2);

                                    var inviteId = int.Parse(parts[1]);

                                    var invite = waitingListDataContext.Invites.Include(i => i.Guild).Include(i => i.InvitedUsers).ThenInclude(iu => iu.User).Single(i => i.Id == inviteId);

                                    var invitedUser = invite.InvitedUsers.Last(x => x.User.UserId == parsedArg.User.Id);

                                    guildData = invite.Guild;
                                    waitingList = new CommandWaitingList(waitingListDataContext!, client.Rest, guildData.GuildId);

                                    guild = client.GetGuild(guildData.GuildId);

                                    if (invitedUser.InviteAccepted != null)
                                    {
                                        return;
                                    }

                                    await parsedArg.Message.DeleteAsync();
                                    if (parts[0] == "joinYes")
                                    {
                                        await parsedArg.Channel.SendMessageAsync(string.Format(guildData.DMMessageFormat, invite.FormatData ?? new string[0]));
                                        invitedUser.InviteAccepted = true;
                                    }
                                    else
                                    {
                                        await parsedArg.Channel.SendMessageAsync("Invite has been declined");
                                        await DeclineInviteAsync(client.Rest, guild, waitingList, invitedUser);
                                    }

                                    waitingListDataContext.Update(invitedUser);
                                    waitingListDataContext.SaveChanges();
                                    AddInviteUpdateToQueue(invite.Id);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Interaction failed to run for user{arg.User}.");
            }
        }

        private async Task DeclineInviteAsync(DiscordRestClient client, IGuild guild, CommandWaitingList waitingList, InvitedUser invitedUser)
        {
            invitedUser.InviteAccepted = false;

            // Invite someone new
            var result = await waitingList.InviteNextPlayerAsync(invitedUser.Invite);

            if (!result.Success)
            {

                var restGuild = await client.GetGuildAsync(guild.Id);
                var textChannel = await restGuild.GetTextChannelAsync(invitedUser.Invite.InviteMessageChannelId);
                await textChannel.SendMessageAsync("Could not invite additional user. List is empty");
            }

            AddPublicMessageUpdateToQueue(invitedUser.User.Guild.Id);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await client.LoginAsync(TokenType.Bot, configuration.DiscordToken);
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

        private async Task GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
        {
            // Try to update
            using (var asyncScope = serviceProvider.CreateScope())
            {
                using (var waitingListDataContext = asyncScope.ServiceProvider.GetRequiredService<WaitingListDataContext>())
                {
                    var guildData = waitingListDataContext.GetOrCreateGuildData(after.Guild);

                    foreach (var user in guildData.UsersInGuild)
                    {
                        if (user.UserId == after.Id)
                        {
                            user.Name = after.Nickname ?? after.Username;
                            user.IsSub = after.Roles.Any(x => x.Id == guildData.SubRoleId);
                        }
                    }

                    var waitingList = new CommandWaitingList(waitingListDataContext, client.Rest, after.Guild.Id);
                    AddPublicMessageUpdateToQueue(guildData.Id);
                    waitingListDataContext.Update(guildData);
                    await waitingListDataContext.SaveChangesAsync();
                }
            }
        }

        private async Task Client_GuildAvailable(SocketGuild guild)
        {
            using (var asyncScope = serviceProvider.CreateScope())
            {
                // Try to migrate old data:
                using (var waitingListDataContext = asyncScope.ServiceProvider.GetRequiredService<WaitingListDataContext>())
                {
                    if (waitingListDataContext.GetGuild(guild.Id) == null)
                    {
                        StorageFactory factory = new StorageFactory();

                        var storage = factory.GetStorage(guild.Id);
                        var guildData = waitingListDataContext.GetOrCreateGuildData(guild);

                        guildData.CommandPrefix = storage.CommandPrefix;
                        guildData.DMMessageFormat = storage.DMMessageFormat ?? "You have been invited to play!\n Name: {0}\nPassword: {1}";
                        guildData.SubRoleId = storage.SubRoleId;
                        guildData.WaitingListChannelId = storage.WaitingListChannelId;

                        waitingListDataContext.Update(guildData);

                        waitingListDataContext.SaveChanges();

                        var waitingList = new CommandWaitingList(waitingListDataContext!, client.Rest, guildData.GuildId);
                        AddPublicMessageUpdateToQueue(guildData.Id);
                    }
                }

                UpdateGuildInformation(guild);
            }
        }

        private async Task Client_GuildUpdated(SocketGuild oldGuild, SocketGuild newGuild)
        {
            UpdateGuildInformation(newGuild);
        }

        private void UpdateGuildInformation(SocketGuild guild)
        {
            using (var asyncScope = serviceProvider.CreateScope())
            {
                using (var waitingListDataContext = asyncScope.ServiceProvider.GetRequiredService<WaitingListDataContext>())
                {
                    waitingListDataContext.GetOrCreateGuildData(guild);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            DMTimeout.Change(Timeout.Infinite, Timeout.Infinite);
            await client.LogoutAsync();
            await client.StopAsync();
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
