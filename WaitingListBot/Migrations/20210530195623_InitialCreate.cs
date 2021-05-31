using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WaitingListBot.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IconUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CommandPrefix = table.Column<string>(type: "TEXT", nullable: false),
                    DMMessageFormat = table.Column<string>(type: "TEXT", nullable: false),
                    WaitingListChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    SubRoleId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublicMessageId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    IsPaused = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InviteTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NumberOfInvitedUsers = table.Column<int>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<int>(type: "INTEGER", nullable: false),
                    FormatDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    InviteMessageId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invites_GuildData_GuildId",
                        column: x => x.GuildId,
                        principalTable: "GuildData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserInGuild",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    PlayCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IsSub = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsInWaitingList = table.Column<bool>(type: "INTEGER", nullable: false),
                    JoinTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInGuild", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInGuild_GuildData_GuildId",
                        column: x => x.GuildId,
                        principalTable: "GuildData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvitedUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InviteId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    InviteTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InviteAccepted = table.Column<bool>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitedUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvitedUsers_Invites_InviteId",
                        column: x => x.InviteId,
                        principalTable: "Invites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvitedUsers_UserInGuild_UserId",
                        column: x => x.UserId,
                        principalTable: "UserInGuild",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvitedUsers_InviteId",
                table: "InvitedUsers",
                column: "InviteId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitedUsers_UserId",
                table: "InvitedUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_GuildId",
                table: "Invites",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInGuild_GuildId",
                table: "UserInGuild",
                column: "GuildId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitedUsers");

            migrationBuilder.DropTable(
                name: "Invites");

            migrationBuilder.DropTable(
                name: "UserInGuild");

            migrationBuilder.DropTable(
                name: "GuildData");
        }
    }
}
