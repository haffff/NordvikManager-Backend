
using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Elements
{
    public class UpdateElementCommandHandler : HandlerBase<UpdateElementCommand, CommandResponse>
    {
        public UpdateElementCommandHandler(IMapper mapper, IDbContext ctx) : base(ctx, mapper)
        {
        }

        public async override Task<CommandResponse> Handle(UpdateElementCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            ElementModel model = mapper.Map<ElementModel>(request.Element);
            if (model == null)
            {
                throw new ResourceNotFoundException(nameof(model));
            }

            if (model != null && model.HasPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Control))
            {
                var origModel = dbContext.Elements.Include(x => x.Details).FirstOrDefault(x => x.Id == model.Id);
                if (origModel == null)
                {
                    throw new WrongArgumentsException(nameof(origModel));
                }
                origModel.IsPublic = request.Element.IsPublic ?? origModel.IsPublic;
                origModel.Layer = request.Element.Layer ?? origModel.Layer;

                foreach (var detail in model.Details)
                {
                    var origDetail = origModel.Details.FirstOrDefault(x => x.Key == detail.Key);
                    if (origDetail == null)
                    {
                        if (detail.Value != null)
                        {
                            origModel.Details.Add(detail);
                        }
                    }
                    else
                    {
                        if (detail.Value == null)
                        {
                            dbContext.Remove(origDetail);
                        }
                        else
                        {
                            origDetail.Value = detail.Value;
                        }
                    }
                }

                return dbContext.SaveChanges() > 0 ? CommandResponse.Ok : CommandResponse.NoChange;
            }
            throw new PermissionException(Permission.Edit);
        }
    }
}

