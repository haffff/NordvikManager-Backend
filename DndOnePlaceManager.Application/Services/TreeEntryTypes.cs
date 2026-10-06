using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Services
{
    /// <summary>
    /// Folder-tree labels (TreeEntry.EntryType). A label only groups entries into one tree —
    /// nothing maps it to a model — so panels that show the same model (cards / templates /
    /// custom views are all CardModel; playlists / soundboards are both PlaylistModel) each
    /// get their own folders without separate models.
    /// </summary>
    public static class TreeEntryTypes
    {
        public const string Card = "CardModel";
        public const string CardTemplate = "CardTemplate";
        public const string CustomView = "CustomView";
        public const string Playlist = "Playlist";
        public const string Soundboard = "Soundboard";

        /// <summary>The tree a card belongs to. A custom view wins if both flags are set.</summary>
        public static string ForCard(bool isTemplate, bool isCustomUi) =>
            isCustomUi ? CustomView : isTemplate ? CardTemplate : Card;

        public static string ForPlaylist(PlaylistKind kind) =>
            kind == PlaylistKind.Soundboard ? Soundboard : Playlist;
    }
}
