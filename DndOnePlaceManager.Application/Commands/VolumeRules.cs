namespace DndOnePlaceManager.Application.Commands
{
    /// <summary>Volumes the GM sets (playlists, soundboards, sound files) are kept between 0 and 1.</summary>
    public static class VolumeRules
    {
        public static double Clamp(double volume) =>
            double.IsNaN(volume) ? 1 : Math.Clamp(volume, 0, 1);
    }
}
