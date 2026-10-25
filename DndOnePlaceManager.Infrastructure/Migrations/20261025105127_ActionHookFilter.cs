using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndOnePlaceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ActionHookFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HookFilter",
                table: "Actions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HookFilter",
                table: "Actions");
        }
    }
}
