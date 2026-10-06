using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndOnePlaceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResourceContentVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "Resources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailSourceVersion",
                table: "Resources",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "ThumbnailSourceVersion",
                table: "Resources");
        }
    }
}
