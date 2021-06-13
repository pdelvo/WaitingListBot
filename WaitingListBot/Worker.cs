using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using WaitingListBot.Data;
using WaitingListBot.Model;

namespace WaitingListBot
{
    internal class Worker : IHostedService
    {
        readonly IServiceCollection serviceCollection;
        readonly DiscordSocketClient client;
        readonly string token;
        readonly CommandHandler handler;
        readonly ILogger logger;

        readonly Timer DMTimeout;
        bool timerRunning = false;

        public Worker(ILogger<Worker> logger)
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages 
                | GatewayIntents.GuildMessageReactions | GatewayIntents.DirectMessages | GatewayIntents.DirectMessageReactions,
                AlwaysDownloadUsers = true,
                AlwaysAcknowledgeInteractions = false
            };

            client = new DiscordSocketClient(config);

            client.Log += Log;
            client.GuildMemberUpdated += GuildMemberUpdated;
            client.GuildUpdated += Client_GuildUpdated;
            client.GuildAvailable += Client_GuildAvailable;
            client.InteractionCreated += Client_InteractionCreated;

            //#if DEBUG
            //            token = File.ReadAllText("token-dev.txt");
            //#else
            //            token = File.ReadAllText("token.txt");
            //#endif
            token = File.ReadAllText("token.txt");

            // Commands are not thread safe. So set the run mode to sync
            var commandService = new CommandService(new CommandServiceConfig { DefaultRunMode = RunMode.Sync, LogLevel = LogSeverity.Info });

            serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(typeof(CommandService), commandService);
            serviceCollection.AddDbContext<WaitingListDataContext>();

            using (var waitingListDataContext = new WaitingListDataContext())
            {
                waitingListDataContext.Database.Migrate();
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
            using (var waitingListDataContext = new WaitingListDataContext())
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
                                    await DeclineInviteAsync(guild, waitingList, invitedUser);

                                    var dmChannel = await client.GetUser(invitedUser.User.UserId).GetOrCreateDMChannelAsync();

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

                                await CommandWaitingListModule.UpdateInviteMessageAsync(guild, invitedUser.Invite);
                            }
                        }
                    }
                }
            }
            timerRunning = false;
        }

        private async Task Client_InteractionCreated(SocketInteraction arg)
        {
            using (var waitingListDataContext = new WaitingListDataContext())
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
                        if (customId == "join")
                        {
                            if ((await waitingList!.AddUserAsync((IGuildUser)parsedArg.User)).Success)
                            {
                                await parsedArg.RespondAsync("Joined Waiting list.", ephemeral: true);
                            }
                            else
                            {
                                // await arg.RespondAsync("Failed");
                                logger.LogError("Failed to join " + parsedArg.User);
                            }
                        }
                        else if (customId == "leave")
                        {
                            await waitingList!.RemoveUserAsync(parsedArg.User.Id);
                            await parsedArg.RespondAsync("Left waiting list.", ephemeral: true);
                        }

                        waitingListDataContext.SaveChanges();

                        await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList!, guild!, guildData);
                    }
                    else
                    {
                        if (customId == "unpause")
                        {
                            guildData!.IsPaused = false;
                            waitingListDataContext.Update(guildData);
                            waitingListDataContext.SaveChanges();

                            await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList, guild!, guildData);

                            await parsedArg.AcknowledgeAsync();

                            await parsedArg.Message.DeleteAsync();
                        }
                        else if (customId == "clearcounters")
                        {
                            foreach (var user in guildData!.UsersInGuild)
                            {
                                user.PlayCount = 0;
                                waitingListDataContext.Update(user);
                            }
                            waitingListDataContext.SaveChanges();

                            await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList!, guild!, guildData);

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

                            if (parts[0] == "joinYes")
                            {
                                await parsedArg.Message.DeleteAsync();
                                await parsedArg.Channel.SendMessageAsync(string.Format(guildData.DMMessageFormat, invite.FormatData));
                                invitedUser.InviteAccepted = true;
                                waitingListDataContext.Update(invitedUser);
                                waitingListDataContext.SaveChanges();
                                await CommandWaitingListModule.UpdateInviteMessageAsync(guild, invite);
                            }
                            else
                            {
                                await parsedArg.Message.DeleteAsync();
                                await parsedArg.Channel.SendMessageAsync("Invite has been declined");
                                await DeclineInviteAsync(guild, waitingList, invitedUser);

                                waitingListDataContext.Update(invitedUser);
                                waitingListDataContext.SaveChanges();
                                await CommandWaitingListModule.UpdateInviteMessageAsync(guild, invite);
                            }
                        }
                    }
                }
            }
        }

        private static async Task DeclineInviteAsync(IGuild? guild, CommandWaitingList? waitingList, InvitedUser invitedUser)
        {
            invitedUser.InviteAccepted = false;

            // Invite someone new
            var result = await waitingList.InviteNextPlayerAsync(invitedUser.Invite);

            if (!result.Success)
            {
                var textChannel = await guild.GetTextChannelAsync(invitedUser.Invite.InviteMessageChannelId);
                await textChannel.SendMessageAsync("Could not invite additional user. List is empty");
            }
            await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList!, guild!, invitedUser.User.Guild);
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
            // Try to migrate old data:
            using (var waitingListDataContext = new WaitingListDataContext())
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
                    await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList!, guild!, guildData);
                }
            }

            UpdateGuildInformation(guild);
        }

        private async Task Client_GuildUpdated(SocketGuild oldGuild, SocketGuild newGuild)
        {
            UpdateGuildInformation(newGuild);
        }

        private void UpdateGuildInformation(SocketGuild guild)
        {
            using (var waitingListDataContext = new WaitingListDataContext())
            {
                waitingListDataContext.GetOrCreateGuildData(guild);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            DMTimeout.Change(Timeout.Infinite, Timeout.Infinite);
            await client.LogoutAsync();
            await client.StopAsync();
        }

        private async Task GuildMemberUpdated(SocketGuildUser before, SocketGuildUser after)
        {
            // Try to update
            using (var waitingListDataContext = new WaitingListDataContext())
            {
                var guildData = waitingListDataContext.GetGuild(after.Guild.Id);

                foreach (var user in guildData.UsersInGuild)
                {
                    if (user.UserId == after.Id)
                    {
                        user.Name = after.Nickname ?? after.Username;
                        user.IsSub = after.Roles.Any(x => x.Id == guildData.SubRoleId);
                    }
                }
                var waitingList = new CommandWaitingList(waitingListDataContext, client.Rest, after.Guild.Id);
                await ButtonWaitingListModule.UpdatePublicMessageAsync(waitingList, after.Guild, guildData);
            }
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
