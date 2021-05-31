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
                .HasAnnotation("ProductVersion", "5.0.6");

            modelBuilder.Entity("WaitingListBot.Data.GuildData", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("CommandPrefix")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("DMMessageFormat")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("IconUrl")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsPaused")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("PublicMessageId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("SubRoleId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("WaitingListChannelId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("GuildData");
                });

            modelBuilder.Entity("WaitingListBot.Data.Invite", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("FormatDataJson")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("InviteMessageChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("InviteMessageId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("InviteTime")
                        .HasColumnType("TEXT");

                    b.Property<int>("NumberOfInvitedUsers")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("Invites");
                });

            modelBuilder.Entity("WaitingListBot.Data.InvitedUser", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("DmQuestionMessageId")
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("InviteAccepted")
                        .HasColumnType("INTEGER");

                    b.Property<int>("InviteId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("InviteTime")
                        .HasColumnType("TEXT");

                    b.Property<int?>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("InviteId");

                    b.HasIndex("UserId");

                    b.ToTable("InvitedUsers");
                });

            modelBuilder.Entity("WaitingListBot.Data.UserInGuild", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsInWaitingList")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsSub")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("JoinTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<int>("PlayCount")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("UserId")
                        .HasColumnType("INTEGER");

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
