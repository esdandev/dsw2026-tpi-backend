using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsw2026Tpi.Data.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddPatientNamesToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "ApplicationUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "ApplicationUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "ApplicationUsers");
        }
    }
}
