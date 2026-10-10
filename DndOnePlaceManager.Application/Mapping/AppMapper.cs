using System.Collections;
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
using Riok.Mapperly.Abstractions;

namespace DndOnePlaceManager.Application.Mapping
{
    /// <summary>
    /// Every DTO &lt;-&gt; model mapping, generated at compile time by Mapperly. A new
    /// mapping is a partial method here plus its pair in MappingSnapshotTests; a target
    /// member left unmapped fails the build (RMG012), so ignore it explicitly.
    /// Null collections (and byte arrays) become empty ones, as AutoMapper did. Only partial
    /// methods and [UserMapping] methods are mappings; the rest are helpers.
    /// </summary>
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, PropertyNameMappingStrategy = PropertyNameMappingStrategy.CaseInsensitive, AutoUserMappings = false)]
    internal partial class AppMapper : IMapper
    {
        public T Map<T>(object? source) => (T)MapTo(source, typeof(T))!;

        private object? MapTo(object? source, Type targetType)
        {
            if (ListItemType(targetType) is { } itemType)
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
                foreach (var item in (IEnumerable?)source ?? Array.Empty<object>())
                    list.Add(MapTo(item, itemType));
                return list;
            }
            return source == null ? null : MapObject(source, targetType);
        }

        // The item type of a List<T>/IEnumerable<T> target, null for any other target.
        private static Type? ListItemType(Type type) =>
            type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ? type.GetGenericArguments()[0]
                : null;

        // Dispatches to the mapping methods below by the source's runtime type.
        private partial object? MapObject(object? source, Type targetType);

        // Player
        [MapperIgnoreTarget(nameof(PlayerModel.User))]
        [MapperIgnoreTarget(nameof(PlayerModel.Resources))]
        [MapperIgnoreTarget(nameof(PlayerModel.Game))]
        private partial PlayerModel ToModel(PlayerDTO source);

        [MapperIgnoreTarget(nameof(PlayerDTO.IsOwner))]
        [MapperIgnoreTarget(nameof(PlayerDTO.Permission))]
        [MapperIgnoreTarget(nameof(PlayerDTO.Permissions))]
        private partial PlayerDTO ToDto(PlayerModel source);

        // Map
        [MapperIgnoreTarget(nameof(MapModel.Game))]
        private partial MapModel ToModel(MapDTO source);

        [MapPropertyFromSource(nameof(MapDTO.GameID), Use = nameof(GameIdOfMap))]
        [MapperIgnoreTarget(nameof(MapDTO.Permission))]
        private partial MapDTO ToDto(MapModel source);

        // Navigation properties are declared non-nullable but are null unless loaded.
        private static Guid? GameIdOfMap(MapModel source) => source.Game?.Id;

        // Element: the fabric.js object is stored as one detail row per top-level key.
        [MapPropertyFromSource(nameof(ElementModel.Details), Use = nameof(DetailsOf))]
        [MapperIgnoreTarget(nameof(ElementModel.Map))]
        private partial ElementModel ToModel(ElementDTO source);

        [MapPropertyFromSource(nameof(ElementDTO.Object), Use = nameof(ObjectOf))]
        [MapperIgnoreTarget(nameof(ElementDTO.Visible))]
        [MapperIgnoreTarget(nameof(ElementDTO.Permission))]
        private partial ElementDTO ToDto(ElementModel source);

        private static List<ElementDetailModel> DetailsOf(ElementDTO source) =>
            source.Object == null ? new() : DetailsParseHelper.ParseFabricJSToElementDetals(source.Id, source.Object).Values.ToList();

        private static string? ObjectOf(ElementModel source) =>
            source.Details == null ? null : DetailsParseHelper.ParseElementDetailsToFabricJS(source.Details);

        // Property
        [MapperIgnoreTarget(nameof(PropertyModel.Description))]
        [MapperIgnoreTarget(nameof(PropertyModel.Game))]
        [MapperIgnoreTarget(nameof(PropertyModel.Map))]
        [MapperIgnoreTarget(nameof(PropertyModel.Element))]
        [MapperIgnoreTarget(nameof(PropertyModel.Card))]
        private partial PropertyModel ToModel(PropertyDTO source);

        [MapperIgnoreTarget(nameof(PropertyDTO.Permission))]
        private partial PropertyDTO ToDto(PropertyModel source);

        // Layout
        [MapperIgnoreTarget(nameof(LayoutModel.Game))]
        private partial LayoutModel ToModel(LayoutDTO source);

        [MapperIgnoreTarget(nameof(LayoutDTO.Path))]
        [MapperIgnoreTarget(nameof(LayoutDTO.Permission))]
        private partial LayoutDTO ToDto(LayoutModel source);

        // Game (listing only)
        [MapperIgnoreTarget(nameof(GameItemDTO.Image))]
        [MapperIgnoreTarget(nameof(GameItemDTO.LongDescription))]
        [MapperIgnoreTarget(nameof(GameItemDTO.ShortDescription))]
        [MapperIgnoreTarget(nameof(GameItemDTO.Color))]
        [MapperIgnoreTarget(nameof(GameItemDTO.IsOwner))]
        [MapperIgnoreTarget(nameof(GameItemDTO.PasswordRequired))]
        private partial GameItemDTO ToDto(GameModel source);

        // Message: Data on the wire, Content in the database.
        [MapProperty(nameof(MessageDTO.Data), nameof(MessageModel.Content))]
        private partial MessageModel ToModel(MessageDTO source);

        [MapProperty(nameof(MessageModel.Content), nameof(MessageDTO.Data))]
        [MapperIgnoreTarget(nameof(MessageDTO.Permission))]
        private partial MessageDTO ToDto(MessageModel source);

        // Battle map
        [MapperIgnoreTarget(nameof(BattleMapModel.Path))]
        [MapperIgnoreTarget(nameof(BattleMapModel.Game))]
        private partial BattleMapModel ToModel(BattleMapDto source);

        [MapPropertyFromSource(nameof(BattleMapDto.GameId), Use = nameof(GameIdOfBattleMap))]
        [MapperIgnoreTarget(nameof(BattleMapDto.Permission))]
        private partial BattleMapDto ToDto(BattleMapModel source);

        private static Guid? GameIdOfBattleMap(BattleMapModel source) => source.Game?.Id;

        // Card: the additional resource ids are stored as a JSON list.
        [MapProperty(nameof(CardModel.AdditionalResources), nameof(CardDto.AdditionalResources), Use = nameof(ResourceIdsOf))]
        [MapProperty(nameof(CardModel.Properties), nameof(CardDto.Properties), Use = nameof(PropertyDtos))]
        [MapperIgnoreTarget(nameof(CardDto.TemplateId))]
        [MapperIgnoreTarget(nameof(CardDto.Owner))]
        [MapperIgnoreTarget(nameof(CardDto.Permission))]
        [MapperIgnoreTarget(nameof(CardDto.GenericPermission))]
        [MapperIgnoreTarget(nameof(CardDto.GmPermission))]
        private partial CardDto ToDto(CardModel source);

        [MapProperty(nameof(CardDto.AdditionalResources), nameof(CardModel.AdditionalResources), Use = nameof(ResourceIdsJson))]
        [MapProperty(nameof(CardDto.Properties), nameof(CardModel.Properties), Use = nameof(PropertyModels))]
        [MapperIgnoreTarget(nameof(CardModel.IsCustomUi))]
        [MapperIgnoreTarget(nameof(CardModel.IsTemplate))]
        [MapperIgnoreTarget(nameof(CardModel.IsR20Card))]
        [MapperIgnoreTarget(nameof(CardModel.GameId))]
        [MapperIgnoreTarget(nameof(CardModel.Game))]
        private partial CardModel ToModel(CardDto source);

        private static List<Guid> ResourceIdsOf(string? json) =>
            json == null ? new() : JsonConvert.DeserializeObject<List<Guid>>(json) ?? new();

        private static string ResourceIdsJson(List<Guid>? ids) => JsonConvert.SerializeObject(ids);

        // Action
        [MapperIgnoreTarget(nameof(ActionDto.Permission))]
        [MapperIgnoreTarget(nameof(ActionDto.GenericPermission))]
        [MapperIgnoreTarget(nameof(ActionDto.GmPermission))]
        private partial ActionDto ToDto(ActionModel source);

        [MapperIgnoreTarget(nameof(ActionModel.Game))]
        private partial ActionModel ToModel(ActionDto source);

        // Addon
        [MapperIgnoreTarget(nameof(AddonDto.Permission))]
        [MapperIgnoreTarget(nameof(AddonDto.Installed))]
        private partial AddonDto ToDto(AddonModel source);

        [MapperIgnoreTarget(nameof(AddonModel.InstallHookFired))]
        private partial AddonModel ToModel(AddonDto source);

        // Resource: the MIME type travels as its text ("image/png"), stored as the enum.
        [MapProperty(nameof(ResourceModel.MimeType), nameof(ResourceDTO.MimeType), Use = nameof(MimeTypeText))]
        [MapProperty(nameof(ResourceModel.Data), nameof(ResourceDTO.Data), Use = nameof(Bytes))]
        [MapPropertyFromSource(nameof(ResourceDTO.PlayerName), Use = nameof(PlayerNameOf))]
        private partial ResourceDTO ToDto(ResourceModel source);

        private static string? PlayerNameOf(ResourceModel source) => source.Player?.Name;

        [MapProperty(nameof(ResourceDTO.MimeType), nameof(ResourceModel.MimeType), Use = nameof(MimeTypeOf))]
        [MapProperty(nameof(ResourceDTO.Data), nameof(ResourceModel.Data), Use = nameof(Bytes))]
        [MapperIgnoreTarget(nameof(ResourceModel.Game))]
        [MapperIgnoreTarget(nameof(ResourceModel.GameId))]
        [MapperIgnoreTarget(nameof(ResourceModel.ThumbnailData))]
        [MapperIgnoreTarget(nameof(ResourceModel.ThumbnailSourceVersion))]
        [MapperIgnoreTarget(nameof(ResourceModel.ContentHash))]
        [MapperIgnoreTarget(nameof(ResourceModel.Player))]
        [MapperIgnoreTarget(nameof(ResourceModel.Playlists))]
        private partial ResourceModel ToModel(ResourceDTO source);

        private static string MimeTypeText(MimeType mimeType) => mimeType.GetDescriptionValue();

        private static MimeType MimeTypeOf(string? text) => text?.ToEnumUsingDescriptionAttribute<MimeType>() ?? default;

        // Playlist (its resources are filled by the handlers)
        [MapperIgnoreTarget(nameof(PlaylistDTO.Resources))]
        private partial PlaylistDTO ToDto(PlaylistModel source);

        // Tree entry: the DTO carries the parent's and next entry's ids; the handlers
        // resolve them to entries, so they stay unset on the model.
        [MapperIgnoreTarget(nameof(TreeEntryModel.Parent))]
        [MapperIgnoreTarget(nameof(TreeEntryModel.Next))]
        [MapperIgnoreTarget(nameof(TreeEntryModel.NewItem))]
        [MapperIgnoreTarget(nameof(TreeEntryModel.Game))]
        private partial TreeEntryModel ToModel(TreeEntryDto source);

        [MapProperty(nameof(TreeEntryModel.Parent) + "." + nameof(TreeEntryModel.Id), nameof(TreeEntryDto.ParentId))]
        [MapProperty(nameof(TreeEntryModel.Next) + "." + nameof(TreeEntryModel.Id), nameof(TreeEntryDto.Next))]
        [MapperIgnoreTarget(nameof(TreeEntryDto.AutoConnect))]
        private partial TreeEntryDto ToDto(TreeEntryModel source);

        // Nested lists: a null list maps to an empty one.
        [UserMapping]
        private List<ElementModel>? ElementModels(IEnumerable<ElementDTO>? source) => source?.Select(ToModel).ToList() ?? new();
        [UserMapping]
        private IEnumerable<ElementDTO>? ElementDtos(List<ElementModel>? source) => source?.Select(ToDto).ToList() ?? new();
        [UserMapping]
        private List<PropertyModel>? PropertyModels(IEnumerable<PropertyDTO>? source) => source?.Select(ToModel).ToList() ?? new();
        [UserMapping]
        private IEnumerable<PropertyDTO>? PropertyDtos(List<PropertyModel>? source) => source?.Select(ToDto).ToList() ?? new();
        [UserMapping]
        private List<AddonModel>? AddonModels(List<AddonDto>? source) => source?.Select(ToModel).ToList() ?? new();
        [UserMapping]
        private List<AddonDto>? AddonDtos(List<AddonModel>? source) => source?.Select(ToDto).ToList() ?? new();
        [UserMapping]
        private List<CardModel>? CardModels(List<CardDto>? source) => source?.Select(ToModel).ToList() ?? new();
        [UserMapping]
        private List<CardDto>? CardDtos(List<CardModel>? source) => source?.Select(ToDto).ToList() ?? new();
        [UserMapping]
        private List<ActionModel>? ActionModels(List<ActionDto>? source) => source?.Select(ToModel).ToList() ?? new();
        [UserMapping]
        private List<ActionDto>? ActionDtos(List<ActionModel>? source) => source?.Select(ToDto).ToList() ?? new();
        [UserMapping]
        private List<ResourceModel>? ResourceModels(List<ResourceDTO>? source) => source?.Select(ToModel).ToList() ?? new();
        [UserMapping]
        private List<ResourceDTO>? ResourceDtos(List<ResourceModel>? source) => source?.Select(ToDto).ToList() ?? new();

        private static byte[] Bytes(byte[]? source) => source ?? Array.Empty<byte>();
    }
}
