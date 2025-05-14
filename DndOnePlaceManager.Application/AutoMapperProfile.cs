using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Chat;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Helpers;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Chat;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application
{
    internal class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<PlayerDTO, PlayerModel>();
            CreateMap<PlayerModel, PlayerDTO>();

            CreateMap<MapDTO, MapModel>();
            CreateMap<MapModel, MapDTO>();

            CreateMap<ElementDTO, ElementModel>().ForMember(x => x.Details, (opt) => opt.MapFrom(src => DetailsParseHelper.ParseFabricJSToElementDetals(src.Id, src.Object).Values));
            CreateMap<ElementModel, ElementDTO>().ForMember(x=>x.Object, (opt) => opt.MapFrom(src => DetailsParseHelper.ParseElementDetailsToFabricJS(src.Details)));

            CreateMap<PropertyDTO, PropertyModel>();
            CreateMap<PropertyModel, PropertyDTO>();

            CreateMap<LayoutDTO, LayoutModel>();
            CreateMap<LayoutModel, LayoutDTO>();

            CreateMap<GameModel, GameItemDTO>();

            CreateMap<MessageDTO, MessageModel>().ForMember(x=>x.Content,(opt)=> opt.MapFrom(src => src.Data));
            CreateMap<MessageModel, MessageDTO>().ForMember(x => x.Data, (opt) => opt.MapFrom(src => src.Content));

            CreateMap<BattleMapDto, BattleMapModel>();
            CreateMap<BattleMapModel, BattleMapDto>();

            CreateMap<CardModel, CardDto>().ForMember(x=>x.AdditionalResources, (opt) => opt.MapFrom(src => JsonConvert.DeserializeObject<List<Guid>>(src.AdditionalResources)));
            CreateMap<CardDto, CardModel>().ForMember(x => x.AdditionalResources, (opt) => opt.MapFrom(src => JsonConvert.SerializeObject(src.AdditionalResources)));

            CreateMap<ActionModel, ActionDto>();
            CreateMap<ActionDto, ActionModel>();

            CreateMap<AddonModel, AddonDto>();
            CreateMap<AddonDto, AddonModel>();

            CreateMap<ResourceModel, ResourceDTO>()
                .ForMember(x=>x.MimeType, (opt) => opt.MapFrom((dto,model) => dto.MimeType.GetDescriptionValue() ));
            CreateMap<ResourceDTO, ResourceModel>()
                .ForMember(x=>x.MimeType, (opt) => opt.MapFrom((dto,model) => dto.MimeType.ToEnumUsingDescriptionAttribute<MimeType>() ));

            CreateMap<TreeEntryDto, TreeEntryModel>()
                .ForMember(x=>x.Parent, (opt) => opt.MapFrom((dto,model) => model.Parent))
                .ForMember(x=>x.Next, (opt) => opt.MapFrom((dto,model) => model.Next));
            CreateMap<TreeEntryModel, TreeEntryDto>()
                .ForMember(x=>x.ParentId , (opt) => opt.MapFrom((model,dto) => model.Parent?.Id))
                .ForMember(x=>x.Next , (opt) => opt.MapFrom((model, dto) => model.Next?.Id));
        }
    }
}
