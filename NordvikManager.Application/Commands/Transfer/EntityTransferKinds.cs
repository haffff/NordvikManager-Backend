using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Transfer
{
    /// <summary>What an export holds. Cards, templates and custom views are all cards, and
    /// playlists and soundboards both playlists, but each goes back into its own panel.</summary>
    public static class EntityTransferKinds
    {
        public const string Map = "map";
        public const string Card = "card";
        public const string Template = "template";
        public const string View = "view";
        public const string Playlist = "playlist";
        public const string Soundboard = "soundboard";

        public static bool IsKnown(string? kind) => IsMap(kind) || IsCard(kind) || IsPlaylist(kind);
        public static bool IsMap(string? kind) => kind == Map;
        public static bool IsCard(string? kind) => kind is Card or Template or View;
        public static bool IsPlaylist(string? kind) => kind is Playlist or Soundboard;

        public static string Of(CardModel card) => card.IsCustomUi ? View : card.IsTemplate ? Template : Card;
        public static string Of(PlaylistModel playlist) => playlist.Kind == PlaylistKind.Soundboard ? Soundboard : Playlist;

        public static PlaylistKind PlaylistKindOf(string kind) => kind == Soundboard ? PlaylistKind.Soundboard : PlaylistKind.Music;

        /// <summary>The folder tree it goes into (TreeEntry.EntryType).</summary>
        public static string TreeEntryType(string kind) => kind switch
        {
            Map => nameof(MapModel),
            Template => TreeEntryTypes.CardTemplate,
            View => TreeEntryTypes.CustomView,
            Card => TreeEntryTypes.Card,
            Soundboard => TreeEntryTypes.Soundboard,
            _ => TreeEntryTypes.Playlist,
        };
    }
}
