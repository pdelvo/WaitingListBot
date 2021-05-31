using Microsoft.EntityFrameworkCore.Migrations;

namespace WaitingListBot.Migrations
{
    public partial class InviteMessageChannelId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "InviteMessageChannelId",
                table: "Invites",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InviteMessageChannelId",
                table: "Invites");
        }
    }
}
