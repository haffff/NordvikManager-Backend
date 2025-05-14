using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Extension
{
    public static class DbContextExtensions
    {
        public static List<IEntity> GetEntitiesList(this IDbContext context, Guid gameId, string entityType)
        {
            switch (entityType.ToLower())
            {
                case "mapmodel":
                    return context.Games.Find(gameId)?.Maps?.Select(x => x as IEntity).ToList();
                case "elementmodel":
                    return context.Games.Include(x=>x.Maps).ThenInclude(x=>x.Elements).FirstOrDefault(x=> x.Id == gameId).Maps.SelectMany(x=>x.Elements).Cast<IEntity>().ToList();
                case "propertymodel":
                    return context.Games.Find(gameId)?.Properties?.Select(x => x as IEntity).ToList();
                case "layoutmodel":
                    return context.Games.Find(gameId)?.Layouts?.Select(x => x as IEntity).ToList();
                case "cardmodel":
                    return context.Games.Find(gameId)?.Cards?.Select(x => x as IEntity).ToList();
                case "battlemapmodel":
                    return context.Games.Find(gameId)?.BattleMaps?.Select(x => x as IEntity).ToList();
                default:
                    return new List<IEntity>();
            }
        }
    }
}