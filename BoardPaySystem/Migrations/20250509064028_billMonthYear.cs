using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardPaySystem.Migrations
{
    /// <inheritdoc />
    public partial class billMonthYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "BillingMonth",
                table: "Bills",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BillingYear",
                table: "Bills",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingMonth",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "BillingYear",
                table: "Bills");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
