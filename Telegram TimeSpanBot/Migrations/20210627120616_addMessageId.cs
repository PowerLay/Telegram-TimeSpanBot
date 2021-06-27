using Microsoft.EntityFrameworkCore.Migrations;

namespace Telegram_TimeSpanBot.Migrations
{
    public partial class addMessageId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MessageId",
                table: "TimeSpans",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "TimeSpans");
        }
    }
}
