using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardPaySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SmsDueReminderEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SmsNewBillsEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SmsNotificationsEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SmsOverdueEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SmsPaymentConfirmationEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SmsDueReminderEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SmsNewBillsEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SmsNotificationsEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SmsOverdueEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SmsPaymentConfirmationEnabled",
                table: "AspNetUsers");
        }
    }
}
