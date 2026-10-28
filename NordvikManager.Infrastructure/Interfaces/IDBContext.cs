using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Chat;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Domain.Entities.Auth;
using Microsoft.EntityFrameworkCore;


namespace NordvikManager.Infrastructure.Interfaces
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
        DbSet<BannedUserModel> BannedUsers { get; }
        DbSet<PlaylistModel> Playlists { get; }
        DbSet<TurnOrderModel> TurnOrders { get; }
        DbSet<TurnOrderEntryModel> TurnOrderEntries { get; }
    }
}
