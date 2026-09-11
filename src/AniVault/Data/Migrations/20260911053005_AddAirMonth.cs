using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AniVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAirMonth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AirMonth",
                table: "Media",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AirMonth",
                table: "Media");
        }
    }
}
