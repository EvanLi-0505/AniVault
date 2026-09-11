using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AniVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShowOnHome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default true: an upgrade must not silently hide every existing item from Home.
            migrationBuilder.AddColumn<bool>(
                name: "ShowOnHome",
                table: "Media",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowOnHome",
                table: "Media");
        }
    }
}
