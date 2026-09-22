using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndOnePlaceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BannedUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CentralServerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    BannedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannedUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MasterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SystemPlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Password = table.Column<string>(type: "TEXT", nullable: true),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseMetric = table.Column<bool>(type: "INTEGER", nullable: false),
                    CentralSessionId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelID = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerID = table.Column<Guid>(type: "TEXT", nullable: false),
                    All = table.Column<bool>(type: "INTEGER", nullable: false),
                    Permission = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Addons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<string>(type: "TEXT", nullable: true),
                    Author = table.Column<string>(type: "TEXT", nullable: true),
                    Website = table.Column<string>(type: "TEXT", nullable: true),
                    ReleaseUrl = table.Column<string>(type: "TEXT", nullable: true),
                    RepositoryUrl = table.Column<string>(type: "TEXT", nullable: true),
                    License = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstallHookFired = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddonModelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GameModelId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Addons_Addons_AddonModelId",
                        column: x => x.AddonModelId,
                        principalTable: "Addons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Addons_Games_GameModelId",
                        column: x => x.GameModelId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BattleMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattleMaps_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Layouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameModelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Default = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Layouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Layouts_Games_GameModelId",
                        column: x => x.GameModelId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Maps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    GridSize = table.Column<int>(type: "INTEGER", nullable: false),
                    GridVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    GridUnitSize = table.Column<int>(type: "INTEGER", nullable: false),
                    GridColor = table.Column<string>(type: "TEXT", nullable: true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Maps_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    System = table.Column<bool>(type: "INTEGER", nullable: false),
                    User = table.Column<string>(type: "TEXT", nullable: true),
                    CentralServerUserId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    Image = table.Column<string>(type: "TEXT", nullable: true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    Shuffle = table.Column<bool>(type: "INTEGER", nullable: false),
                    Repeat = table.Column<bool>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playlists_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NextId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsFolder = table.Column<bool>(type: "INTEGER", nullable: false),
                    Head = table.Column<bool>(type: "INTEGER", nullable: true),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EntryType = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    Icon = table.Column<string>(type: "TEXT", nullable: true),
                    NewItem = table.Column<bool>(type: "INTEGER", nullable: true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreeEntries_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreeEntries_TreeEntries_NextId",
                        column: x => x.NextId,
                        principalTable: "TreeEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TreeEntries_TreeEntries_ParentId",
                        column: x => x.ParentId,
                        principalTable: "TreeEntries",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Hook = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddonModelId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Actions_Addons_AddonModelId",
                        column: x => x.AddonModelId,
                        principalTable: "Addons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Actions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    MainResource = table.Column<Guid>(type: "TEXT", nullable: true),
                    AdditionalResources = table.Column<string>(type: "TEXT", nullable: true),
                    IsCustomUi = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTemplate = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsR20Card = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstOpen = table.Column<bool>(type: "INTEGER", nullable: true),
                    Key = table.Column<string>(type: "TEXT", nullable: true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddonModelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AddonModelId1 = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cards_Addons_AddonModelId",
                        column: x => x.AddonModelId,
                        principalTable: "Addons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Cards_Addons_AddonModelId1",
                        column: x => x.AddonModelId1,
                        principalTable: "Addons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Cards_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Elements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Selectable = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: true),
                    Layer = table.Column<int>(type: "INTEGER", nullable: false),
                    MapId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Elements_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: true),
                    ThumbnailData = table.Column<byte[]>(type: "BLOB", nullable: true),
                    MimeType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: true),
                    Storage = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddonModelId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_Addons_AddonModelId",
                        column: x => x.AddonModelId,
                        principalTable: "Addons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Resources_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Resources_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ElementsDetail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementsDetail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElementsDetail_Elements_ElementId",
                        column: x => x.ElementId,
                        principalTable: "Elements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    EntityName = table.Column<string>(type: "TEXT", nullable: true),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    ParentID = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsProtected = table.Column<bool>(type: "INTEGER", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MapId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ElementId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CardId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Properties_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Properties_Elements_ElementId",
                        column: x => x.ElementId,
                        principalTable: "Elements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Properties_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Properties_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistModelResourceModel",
                columns: table => new
                {
                    PlaylistsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourcesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistModelResourceModel", x => new { x.PlaylistsId, x.ResourcesId });
                    table.ForeignKey(
                        name: "FK_PlaylistModelResourceModel_Playlists_PlaylistsId",
                        column: x => x.PlaylistsId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaylistModelResourceModel_Resources_ResourcesId",
                        column: x => x.ResourcesId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actions_AddonModelId",
                table: "Actions",
                column: "AddonModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Actions_GameId",
                table: "Actions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Addons_AddonModelId",
                table: "Addons",
                column: "AddonModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Addons_GameModelId",
                table: "Addons",
                column: "GameModelId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleMaps_GameId",
                table: "BattleMaps",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_AddonModelId",
                table: "Cards",
                column: "AddonModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_AddonModelId1",
                table: "Cards",
                column: "AddonModelId1");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_GameId",
                table: "Cards",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Key_GameId",
                table: "Cards",
                columns: new[] { "Key", "GameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Elements_MapId",
                table: "Elements",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementsDetail_ElementId_Key",
                table: "ElementsDetail",
                columns: new[] { "ElementId", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_Layouts_GameModelId",
                table: "Layouts",
                column: "GameModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_GameId",
                table: "Maps",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ModelID",
                table: "Permissions",
                column: "ModelID");

            migrationBuilder.CreateIndex(
                name: "IX_Players_GameId",
                table: "Players",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_User",
                table: "Players",
                column: "User");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistModelResourceModel_ResourcesId",
                table: "PlaylistModelResourceModel",
                column: "ResourcesId");

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_GameId",
                table: "Playlists",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_CardId",
                table: "Properties",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_ElementId",
                table: "Properties",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_GameId",
                table: "Properties",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_MapId",
                table: "Properties",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_ParentID_Name",
                table: "Properties",
                columns: new[] { "ParentID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_AddonModelId",
                table: "Resources",
                column: "AddonModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_GameId",
                table: "Resources",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Key_GameId",
                table: "Resources",
                columns: new[] { "Key", "GameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_PlayerId",
                table: "Resources",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TreeEntries_GameId",
                table: "TreeEntries",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_TreeEntries_NextId",
                table: "TreeEntries",
                column: "NextId");

            migrationBuilder.CreateIndex(
                name: "IX_TreeEntries_ParentId",
                table: "TreeEntries",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actions");

            migrationBuilder.DropTable(
                name: "BannedUsers");

            migrationBuilder.DropTable(
                name: "BattleMaps");

            migrationBuilder.DropTable(
                name: "ElementsDetail");

            migrationBuilder.DropTable(
                name: "Layouts");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "PlaylistModelResourceModel");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropTable(
                name: "TreeEntries");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Elements");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Addons");

            migrationBuilder.DropTable(
                name: "Maps");

            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
