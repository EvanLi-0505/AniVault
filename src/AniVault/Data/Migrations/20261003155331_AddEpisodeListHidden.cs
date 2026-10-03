using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AniVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEpisodeListHidden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EpisodeListHidden",
                table: "Media",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EpisodeListHidden",
                table: "Media");
        }
    }
}
