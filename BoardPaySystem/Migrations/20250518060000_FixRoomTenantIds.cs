using Microsoft.EntityFrameworkCore.Migrations;

namespace BoardPaySystem.Migrations
{
    public partial class FixRoomTenantIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set TenantId in Rooms to the Id of the user whose RoomId matches
            migrationBuilder.Sql(@"
                UPDATE r
                SET r.TenantId = u.Id
                FROM Rooms r
                INNER JOIN AspNetUsers u ON r.RoomId = u.RoomId
                WHERE u.RoomId IS NOT NULL
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Optionally, clear TenantId (not strictly necessary)
            // migrationBuilder.Sql("UPDATE Rooms SET TenantId = NULL;");
        }
    }
} 