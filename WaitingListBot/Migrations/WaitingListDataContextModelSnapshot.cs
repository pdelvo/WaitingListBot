﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WaitingListBot.Data;

namespace WaitingListBot.Migrations
{
    [DbContext(typeof(WaitingListDataContext))]
    partial class WaitingListDataContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 64)
                .HasAnnotation("ProductVersion", "5.0.6");

            modelBuilder.Entity("WaitingListBot.Data.GuildData", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("CommandPrefix")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("DMMessageFormat")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Description")
                        .HasColumnType("longtext");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("bigint unsigned");

                    b.Property<string>("IconUrl")
                        .HasColumnType("longtext");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsPaused")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<ulong>("PublicMessageId")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong>("SubRoleId")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong>("WaitingListChannelId")
                        .HasColumnType("bigint unsigned");

                    b.HasKey("Id");

                    b.ToTable("GuildData");
                });

            modelBuilder.Entity("WaitingListBot.Data.Invite", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("FormatDataJson")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("GuildId")
                        .HasColumnType("int");

                    b.Property<ulong>("InviteMessageChannelId")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong>("InviteMessageId")
                        .HasColumnType("bigint unsigned");

                    b.Property<DateTime>("InviteTime")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("NumberOfInvitedUsers")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("Invites");
                });

            modelBuilder.Entity("WaitingListBot.Data.InvitedUser", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<ulong>("DmQuestionMessageId")
                        .HasColumnType("bigint unsigned");

                    b.Property<bool?>("InviteAccepted")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("InviteId")
                        .HasColumnType("int");

                    b.Property<DateTime>("InviteTime")
                        .HasColumnType("datetime(6)");

                    b.Property<int?>("UserId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("InviteId");

                    b.HasIndex("UserId");

                    b.ToTable("InvitedUsers");
                });

            modelBuilder.Entity("WaitingListBot.Data.UserInGuild", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("GuildId")
                        .HasColumnType("int");

                    b.Property<bool>("IsInWaitingList")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsSub")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTime?>("JoinTime")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<int>("PlayCount")
                        .HasColumnType("int");

                    b.Property<ulong>("UserId")
                        .HasColumnType("bigint unsigned");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("UserInGuild");
                });

            modelBuilder.Entity("WaitingListBot.Data.Invite", b =>
                {
                    b.HasOne("WaitingListBot.Data.GuildData", "Guild")
                        .WithMany("Invites")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("WaitingListBot.Data.InvitedUser", b =>
                {
                    b.HasOne("WaitingListBot.Data.Invite", "Invite")
                        .WithMany("InvitedUsers")
                        .HasForeignKey("InviteId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("WaitingListBot.Data.UserInGuild", "User")
                        .WithMany()
                        .HasForeignKey("UserId");

                    b.Navigation("Invite");

                    b.Navigation("User");
                });

            modelBuilder.Entity("WaitingListBot.Data.UserInGuild", b =>
                {
                    b.HasOne("WaitingListBot.Data.GuildData", "Guild")
                        .WithMany("UsersInGuild")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("WaitingListBot.Data.GuildData", b =>
                {
                    b.Navigation("Invites");

                    b.Navigation("UsersInGuild");
                });

            modelBuilder.Entity("WaitingListBot.Data.Invite", b =>
                {
                    b.Navigation("InvitedUsers");
                });
#pragma warning restore 612, 618
        }
    }
}
