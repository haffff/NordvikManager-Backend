using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordvikManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecipientId",
                table: "Messages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipientId",
                table: "Messages");
        }
    }
}
