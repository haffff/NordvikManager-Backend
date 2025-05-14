
using AutoMapper;
using DndOnePlaceManager.Application.Commands.BattleMap.GetGame;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class GetGameCommandHandler : HandlerBase<GetGameCommand, GetGameCommandResponse>
    {
        public GetGameCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<GetGameCommandResponse> Handle(GetGameCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            GameModel game = await dbContext.Games
                .Include(players => players.Players)
                .FirstOrDefaultAsync(x => x.Id == request.GameID);

            var player = game.Players.FirstOrDefault(x => x.Id == request.PlayerID);

            if (game != null && player != null)
            {
                var fullGame = await dbContext.Games
                 .Include(players => players.Players)
                 .Include(maps => maps.Maps).ThenInclude(e => e.Elements).ThenInclude(e => e.Properties)
                 .Include(maps => maps.Layouts)
                 .Include(bms => bms.BattleMaps)
                 .FirstOrDefaultAsync(x => x.Id == request.GameID);
                return GetGameMap(fullGame, player);
            }
            return null;
        }

        private GetGameCommandResponse GetGameMap(GameModel fullGame, PlayerModel player)
        {
            var response = new GetGameCommandResponse()
            {
                Id = fullGame.Id,
                Name = fullGame.Name,
                RequirePassword = !String.IsNullOrEmpty(fullGame.Password),
                Master = mapper.Map<PlayerDTO>(fullGame.Players.First(x => x.Id == fullGame.MasterId)),
                Players = fullGame.Players.Select(x => mapper.Map<PlayerDTO>(x)).ToList(),
                DefaultLayout = mapper.Map<LayoutDTO>(fullGame.Layouts.FirstOrDefault(x => x.Default)),
                Layouts = fullGame.Layouts.WithPermission(player.Id).Select(x => mapper.Map<LayoutDTO>(x)).ToList(),
                Permission = fullGame.GetPermission(player.Id),
                BattleMaps = fullGame.BattleMaps.WithPermission(player.Id, Permission.Read).Select(x => new BattleMapDto() { Id = x.Id, Name = x.Name }).ToList(),
                Maps = fullGame.Maps.WithPermission(player.Id).Select(map => new MapDTO()
                {
                    Id = map.Id,
                    Name = map.Name,
                    Path = map.Path,
                }).ToList(),
            };

            return response;
        }
    }
}

