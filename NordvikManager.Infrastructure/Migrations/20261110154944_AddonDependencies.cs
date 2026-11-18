using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordvikManager.Infrastructure.Migrations
{
    /// <summary>
    /// An addon's dependencies are kept as JSON ({ key, version }[]) instead of the old
    /// self-reference, which allowed only one parent per addon and was never filled in.
    /// A game holds one copy of each addon (unique game + key), so two installs at once
    /// can't both add it.
    /// </summary>
    public partial class AddonDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Copies an older version added twice keep their data but get a key of their own,
            // so the index below can be made (the GM can uninstall the extra one).
            migrationBuilder.Sql(
                "UPDATE Addons SET Key = Key || '_duplicate_' || rowid " +
                "WHERE GameModelId IS NOT NULL AND rowid NOT IN " +
                "(SELECT MIN(rowid) FROM Addons WHERE GameModelId IS NOT NULL GROUP BY GameModelId, Key);");

            migrationBuilder.DropForeignKey(
                name: "FK_Addons_Addons_AddonModelId",
                table: "Addons");

            migrationBuilder.DropIndex(
                name: "IX_Addons_AddonModelId",
                table: "Addons");

            migrationBuilder.DropIndex(
                name: "IX_Addons_GameModelId",
                table: "Addons");

            migrationBuilder.DropColumn(
                name: "AddonModelId",
                table: "Addons");

            migrationBuilder.AddColumn<string>(
                name: "Dependencies",
                table: "Addons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Addons_GameModelId_Key",
                table: "Addons",
                columns: new[] { "GameModelId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Addons_GameModelId_Key",
                table: "Addons");

            migrationBuilder.DropColumn(
                name: "Dependencies",
                table: "Addons");

            migrationBuilder.AddColumn<Guid>(
                name: "AddonModelId",
                table: "Addons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Addons_AddonModelId",
                table: "Addons",
                column: "AddonModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Addons_GameModelId",
                table: "Addons",
                column: "GameModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Addons_Addons_AddonModelId",
                table: "Addons",
                column: "AddonModelId",
                principalTable: "Addons",
                principalColumn: "Id");
        }
    }
}
