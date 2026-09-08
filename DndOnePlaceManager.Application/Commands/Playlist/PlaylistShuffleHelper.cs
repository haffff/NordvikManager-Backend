namespace DndOnePlaceManager.Application.Commands.Playlist
{
    internal static class PlaylistShuffleHelper
    {
        internal static List<Guid> FisherYatesShuffle(List<Guid> ids)
        {
            var result = new List<Guid>(ids);
            for (var i = result.Count - 1; i > 0; i--)
            {
                var j = Random.Shared.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }
    }
}
