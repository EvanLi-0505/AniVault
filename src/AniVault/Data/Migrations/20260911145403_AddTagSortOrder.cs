using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AniVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTagSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Tags",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Every existing row gets defaultValue 0, which would tie every tag together and
            // fall back to Id order (arbitrary creation order) the first time SortOrder is read.
            // Backfill using the alphabetical order tags were shown in before this migration
            // (the app's old .OrderBy(t => t.Name)), so upgrading users see no visible reshuffle
            // until they actually drag a tag. Portable correlated-subquery form (no UPDATE...FROM,
            // no window functions) so it works on any SQLite version.
            migrationBuilder.Sql(
                """
                UPDATE Tags SET SortOrder = (
                    SELECT COUNT(*) FROM Tags AS t2
                    WHERE t2.Name < Tags.Name OR (t2.Name = Tags.Name AND t2.Id < Tags.Id)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Tags");
        }
    }
}
