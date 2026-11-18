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

            // The old parent column stays in the table, unmapped and empty. Dropping it (or its
            // foreign key) makes SQLite rebuild Addons, and EF runs that rebuild's
            // PRAGMA foreign_keys outside the transaction: a host stopped in between (a closed
            // browser tab) would be left with the schema changed and the migration unrecorded,
            // and fail to start. Databases that ran this migration before 0.0.3 don't have the
            // column, so a later migration must not drop it by name; the next rebuild of Addons
            // leaves it out by itself.
            migrationBuilder.Sql("UPDATE Addons SET AddonModelId = NULL;");

            migrationBuilder.DropIndex(
                name: "IX_Addons_AddonModelId",
                table: "Addons");

            migrationBuilder.DropIndex(
                name: "IX_Addons_GameModelId",
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

            // AddonModelId and its foreign key were never dropped (see Up).
            migrationBuilder.CreateIndex(
                name: "IX_Addons_AddonModelId",
                table: "Addons",
                column: "AddonModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Addons_GameModelId",
                table: "Addons",
                column: "GameModelId");
        }
    }
}
