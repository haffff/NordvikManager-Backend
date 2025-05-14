using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Interfaces
{
    public interface IBattleMapContext : IDbContextBase
    {
        DbSet<GameModel> Games { get; }
        DbSet<PlayerModel> Players{ get; }
        DbSet<MapModel> Maps { get; }
        DbSet<ElementModel> Elements { get; }
        DbSet<PropertyModel> Properties { get; }

    }
}
