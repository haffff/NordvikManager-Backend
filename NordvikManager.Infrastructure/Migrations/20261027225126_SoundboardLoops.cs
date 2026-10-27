using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordvikManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SoundboardLoops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoopedResourceIds",
                table: "Playlists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoopedResourceIds",
                table: "Playlists");
        }
    }
}
