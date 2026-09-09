using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AniVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OriginalTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AlternativeTitles = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AirYear = table.Column<int>(type: "INTEGER", nullable: true),
                    AirSeason = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: true),
                    RuntimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OfficialWebsite = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PosterPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    BackdropPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ProviderRating = table.Column<double>(type: "REAL", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MyRating = table.Column<double>(type: "REAL", nullable: true),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLiked = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Episodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsWatched = table.Column<bool>(type: "INTEGER", nullable: false),
                    WatchedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Episodes_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaExternalIds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaExternalIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaExternalIds_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaTags",
                columns: table => new
                {
                    MediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaTags", x => new { x.MediaId, x.TagId });
                    table.ForeignKey(
                        name: "FK_MediaTags_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_MediaId_EpisodeNumber",
                table: "Episodes",
                columns: new[] { "MediaId", "EpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Media_IsFavorite",
                table: "Media",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_Media_IsLiked",
                table: "Media",
                column: "IsLiked");

            migrationBuilder.CreateIndex(
                name: "IX_Media_MediaType",
                table: "Media",
                column: "MediaType");

            migrationBuilder.CreateIndex(
                name: "IX_Media_MediaType_AirYear_AirSeason",
                table: "Media",
                columns: new[] { "MediaType", "AirYear", "AirSeason" });

            migrationBuilder.CreateIndex(
                name: "IX_Media_Status",
                table: "Media",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Media_Title",
                table: "Media",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_MediaExternalIds_MediaId",
                table: "MediaExternalIds",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaExternalIds_Source_ExternalId",
                table: "MediaExternalIds",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaTags_TagId",
                table: "MediaTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalizedName",
                table: "Tags",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "Episodes");

            migrationBuilder.DropTable(
                name: "MediaExternalIds");

            migrationBuilder.DropTable(
                name: "MediaTags");

            migrationBuilder.DropTable(
                name: "Media");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
