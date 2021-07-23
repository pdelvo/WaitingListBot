using Microsoft.EntityFrameworkCore.Migrations;

namespace WaitingListBot.Migrations
{
    public partial class AddInviteRole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "InviteRole",
                table: "Invites",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInviteRolePositive",
                table: "Invites",
                type: "tinyint(1)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InviteRole",
                table: "Invites");

            migrationBuilder.DropColumn(
                name: "IsInviteRolePositive",
                table: "Invites");
        }
    }
}
