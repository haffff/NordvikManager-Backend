using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Chat;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Data.Contexts
{
    public class DndOneContext : DbContext, IDbContext
    {
        public DndOneContext(DbContextOptions<DndOneContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        // EnsureCreated() only creates tables that don't exist yet — it never alters an
        // existing table, and this project has no EF migrations pipeline. Resources.Path/
        // Storage are new columns on a table that already existed for earlier users, so they
        // need an explicit ADD COLUMN instead of a DB wipe (existing Data blobs must survive
        // this). Deliberately NOT called from the constructor — DndOneContext is a scoped DI
        // service, so a new instance (and a fresh constructor call) is created per request/WS
        // message; running a migration check there means it fires constantly instead of once.
        // Call this once at app startup instead (see Startup.Configure). Column existence is
        // checked via schema introspection first (a query, not a failing command) so the ALTER
        // — and any resulting log noise — only ever runs on the one real upgrade, not on every
        // subsequent process start.
        public void EnsureResourceStorageColumns()
        {
            var existingColumns = GetResourceColumnNames();

            if (!existingColumns.Contains("Path"))
                TryExecuteSql("ALTER TABLE Resources ADD COLUMN Path TEXT NULL;");

            if (!existingColumns.Contains("Storage"))
                TryExecuteSql("ALTER TABLE Resources ADD COLUMN Storage INTEGER NOT NULL DEFAULT 0;");
        }

        private HashSet<string> GetResourceColumnNames()
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false;
                var sql = isSqlite
                    ? "PRAGMA table_info(Resources);"
                    : "SELECT COLUMN_NAME AS name FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Resources';";

                var connection = Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                using var reader = command.ExecuteReader();
                var nameOrdinal = reader.GetOrdinal("name");
                while (reader.Read())
                    columns.Add(reader.GetString(nameOrdinal));
            }
            catch
            {
                // Introspection itself failing is unexpected — fall through with an empty set so
                // EnsureResourceStorageColumns still attempts the ALTERs (safe: TryExecuteSql
                // swallows the "already exists" case either way).
            }
            return columns;
        }

        private void TryExecuteSql(string sql)
        {
            try
            {
                Database.ExecuteSqlRaw(sql);
            }
            catch
            {
                // Column already exists.
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Maps)
                .WithOne(m => m.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Players)
                .WithOne(p => p.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Layouts)
                .WithOne(l => l.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Resources)
                .WithOne(r => r.Game)
                .HasForeignKey(r => r.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.TreeEntries)
                .WithOne(t => t.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.BattleMaps)
                .WithOne(b => b.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Properties)
                .WithOne(p => p.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Cards)
                .WithOne(c => c.Game)
                .HasForeignKey(c => c.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            var cardKeyIndex = modelBuilder.Entity<CardModel>()
                .HasIndex(c => new { c.Key, c.GameId })
                .IsUnique();

            // SQLite allows multiple NULLs in unique indexes natively and does not support
            // filtered indexes. Apply the filter only on other providers (PostgreSQL, SQL Server).
            var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
            if (!isSqlite)
                cardKeyIndex.HasFilter("\"Key\" IS NOT NULL");

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Actions)
                .WithOne(a => a.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Addons)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MapModel>()
                .HasMany(m => m.Elements)
                .WithOne(e => e.Map)
                .HasForeignKey(e => e.MapId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MapModel>()
                .HasMany(m => m.Properties)
                .WithOne(p => p.Map)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ElementModel>()
                .HasMany(e => e.Properties)
                .WithOne(p => p.Element)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CardModel>()
                .HasMany(c => c.Properties)
                .WithOne(p => p.Card)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerModel>()
                .HasIndex(p => p.User)
                .IsUnique(false);

            modelBuilder.Entity<BattleMapModel>()
                .HasOne(b => b.Game)
                .WithMany(g => g.BattleMaps);

            modelBuilder.Entity<TreeEntryModel>()
                .HasOne(t => t.Parent);

            modelBuilder.Entity<TreeEntryModel>()
                .HasOne(t => t.Next);

            modelBuilder.Entity<PropertyModel>()
                .HasIndex(p => new { p.ParentID, p.Name })
                .IsUnique();

            modelBuilder.Entity<ResourceModel>()
                .HasOne(r => r.Game);

            modelBuilder.Entity<ResourceModel>()
                .HasOne(r => r.Player);

            modelBuilder.Entity<ResourceModel>()
                .HasIndex(r => new { r.Key, r.GameId })
                .IsUnique();

            modelBuilder.Entity<PermissionModel>()
                .HasIndex(p => p.ModelID)
                .IsUnique(false);

            modelBuilder.Entity<ElementDetailModel>().HasOne(e => e.Element).WithMany(e => e.Details).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ElementDetailModel>().HasIndex(e => new { e.ElementId, e.Key });

            modelBuilder.Entity<PlaylistModel>()
                .HasOne(p => p.Game)
                .WithMany(g => g.Playlists)
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlaylistModel>()
                .HasMany(p => p.Resources)
                .WithMany(r => r.Playlists);
        }

        public DbSet<GameModel>? Games { get; set; }
        public DbSet<MapModel>? Maps { get; set; }
        public DbSet<PlayerModel>? Players { get; set; }
        public DbSet<ElementModel>? Elements { get; set; }
        public DbSet<PropertyModel>? Properties { get; set; }
        public DbSet<PermissionModel>? Permissions { get; set; }
        public DbSet<ResourceModel>? Resources { get; set; }
        public DbSet<MessageModel>? Messages { get; set; }
        public DbSet<LayoutModel>? Layouts { get; set; }
        public DbSet<BattleMapModel>? BattleMaps { get; set; }
        public DbSet<CardModel>? Cards { get; set; }
        public DbSet<ActionModel>? Actions { get; set; }
        public DbSet<AddonModel>? Addons { get; set; }
        public DbSet<TreeEntryModel>? TreeEntries { get; set; }
        public DbSet<ElementDetailModel>? ElementsDetail { get; set; }
        public DbSet<BannedUserModel> BannedUsers { get; set; }
        public DbSet<PlaylistModel>? Playlists { get; set; }
    }
}
