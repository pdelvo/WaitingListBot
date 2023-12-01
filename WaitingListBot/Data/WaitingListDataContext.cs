using Discord;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot.Data
{
    public class WaitingListDataContext : DbContext
    {
        ILogger<WaitingListDataContext> logger;
        WaitingListBotConfiguration configuration;
        public DbSet<Invite> Invites { get; set; }
        public DbSet<InvitedUser> InvitedUsers { get; set; }
        public DbSet<UserInGuild> UserInGuild { get; set; }
        public DbSet<GuildData> GuildData { get; set; }

        public WaitingListDataContext(WaitingListBotConfiguration configuration, ILogger<WaitingListDataContext> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        public GuildData UpdateGuild(ulong guildId, string name, string description, string iconUrl)
        {
            var guild = GetGuild(guildId);

            if (guild == null)
            {
                guild = new GuildData
                {
                    GuildId = guildId,
                    Name = name,
                    Description = description,
                    IconUrl = iconUrl,
                    UsersInGuild = new List<UserInGuild>()
                };

                GuildData.Add(guild);
            }
            else
            {
                guild.Name = name;
                guild.Description = description;
                guild.IconUrl = iconUrl;
            }

            SaveChanges();

            return GetGuild(guildId)!;
        }
        public GuildData GetOrCreateGuildData(IGuild guild)
        {
            return UpdateGuild(guild.Id, guild.Name, guild.Description, $"https://cdn.discordapp.com/icons/{guild.Id}/{guild.IconId}.png");
        }

        public GuildData? GetGuild(ulong guildId)
        {
            return GuildData.Include(g => g.UsersInGuild)
                            .Include(g => g.Invites)
                            .SingleOrDefault(x => x.GuildId == guildId);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            string connectionString = configuration.ConnectionString;
            try
            {
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex.InnerException, "Failed to connect to database: " + connectionString);

                throw;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GuildData>()
                .HasMany(g => g.Invites)
                .WithOne(i => i.Guild);
            modelBuilder.Entity<GuildData>()
                .HasMany(g => g.UsersInGuild)
                .WithOne(u => u.Guild);
            modelBuilder.Entity<Invite>()
                .HasMany(g => g.InvitedUsers)
                .WithOne(u => u.Invite);
            modelBuilder.Entity<InvitedUser>()
                .HasOne(i => i.User);
            base.OnModelCreating(modelBuilder);
        }
    }
}
