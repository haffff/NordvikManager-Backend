using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Chat;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Entities.Security;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Infrastructure.Interfaces
{
    public interface IDbContext : IDbContextBase
    {
        DbSet<GameModel> Games { get; }
        DbSet<PlayerModel> Players { get; }
        DbSet<MapModel> Maps { get; }
        DbSet<ElementModel> Elements { get; }
        DbSet<PropertyModel> Properties { get; }
        DbSet<ResourceModel> Resources { get; }
        DbSet<MessageModel> Messages { get; }
        DbSet<LayoutModel> Layouts { get; }
        DbSet<PermissionModel> Permissions { get; }
        DbSet<BattleMapModel> BattleMaps { get; }
        DbSet<CardModel> Cards { get; }
        DbSet<ActionModel> Actions { get; }
        DbSet<AddonModel>? Addons { get; set; }
        DbSet<TreeEntryModel> TreeEntries { get; }
        DbSet<ElementDetailModel> ElementsDetail { get; set; }
    }
}
