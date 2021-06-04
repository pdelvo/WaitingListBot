using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WaitingListBot.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GuildData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CommandPrefix = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DMMessageFormat = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WaitingListChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    SubRoleId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PublicMessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsPaused = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildData", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    InviteTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    NumberOfInvitedUsers = table.Column<int>(type: "int", nullable: false),
                    GuildId = table.Column<int>(type: "int", nullable: false),
                    FormatDataJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InviteMessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    InviteMessageChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserInGuild",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    GuildId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PlayCount = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSub = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsInWaitingList = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    JoinTime = table.Column<DateTime>(type: "datetime(6)", nullable: true)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InvitedUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    InviteId = table.Column<int>(type: "int", nullable: false),
                    InviteTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    InviteAccepted = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    DmQuestionMessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
