using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Chat;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DNDOnePlaceManager.Data.Contexts
{
    public class DndOneContext : DbContext, IDbContext
    {
        public DndOneContext(DbContextOptions<DndOneContext> options) : base(options)
        {
            Database.EnsureCreated();
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
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Actions)
                .WithOne(a => a.Game)
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
    }
}
