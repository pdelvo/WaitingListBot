using Microsoft.EntityFrameworkCore.Migrations;

namespace WaitingListBot.Migrations
{
    public partial class AddDmQuestionMessageId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "DmQuestionMessageId",
                table: "InvitedUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DmQuestionMessageId",
                table: "InvitedUsers");
        }
    }
}
