using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardPaySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBillApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Bills",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Bills");
        }
    }
}
