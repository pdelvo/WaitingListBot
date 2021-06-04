using Discord;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot.Data
{
    public class WaitingListDataContext : DbContext
    {
        public DbSet<Invite> Invites { get; set; }
        public DbSet<InvitedUser> InvitedUsers { get; set; }
        public DbSet<UserInGuild> UserInGuild { get; set; }
        public DbSet<GuildData> GuildData { get; set; }

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
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            var configuration = builder.Build();

            string connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
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
