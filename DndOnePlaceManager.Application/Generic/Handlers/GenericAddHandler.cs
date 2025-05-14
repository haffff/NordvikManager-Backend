using AutoMapper;
using DndOnePlaceManager.Application.Commands;
using DndOnePlaceManager.Application.Commands.Security.CheckPermissions;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Generic.Handlers
{
    internal class GenericAddHandler<TCommand,TModel,TDto> : HandlerBase<TCommand, (CommandResponse, Guid)>
        where TCommand : GenericAddCommand<TDto>
        where TModel : class, IEntity
        where TDto : class, IGameDataTransferObject
    {
        public GenericAddHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public virtual bool CheckPermissions(GameModel game, TCommand request)
        {
            game.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Permission.Edit);
            return true;
        }

        public virtual TModel CreateModel(GameModel game, TCommand request)
        {
            return mapper.Map<TModel>(request.Dto);
        }

        public virtual void SetPermissions(GameModel game, TModel model, TCommand request)
        {
            model.SetGlobalPermission();
            model.SetPermissions(request.Player.Id ?? Guid.Empty, Permission.All);
            model.SetPermissions(game.SystemPlayerId, Permission.All);
        }

        public virtual void AddToGame(GameModel game, TModel model, TCommand request)
        {
            dbContext.Add(model);
        }

        /// <summary>
        /// It has to include players!
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public virtual GameModel GetGame(TCommand request)
        {
            return dbContext.Games.Include(x => x.Players).FirstOrDefault(x => x.Id == request.GameID);
        }

        public virtual TDto GetDefault()
        {
            return null;
        }

        public async override Task<(CommandResponse, Guid)> Handle(TCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = GetGame(request);

            if (game == null)
            {
                return (CommandResponse.WrongArguments, Guid.Empty);
            }

            if (!CheckPermissions(game, request))
            {
                return (CommandResponse.NoPermission, Guid.Empty);
            }

            request.Dto = request.Dto ?? GetDefault();

            if(request.Dto == null)
            {
                return (CommandResponse.WrongArguments, Guid.Empty);
            }

            var model = CreateModel(game, request);

            AddToGame(game, model, request);

            dbContext.SaveChanges();

            SetPermissions(game, model, request);

            return (CommandResponse.Ok, model.Id);
        }
    }
}
