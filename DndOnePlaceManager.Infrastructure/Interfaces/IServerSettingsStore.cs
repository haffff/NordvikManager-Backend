using DndOnePlaceManager.Infrastructure.Services.Settings;

namespace DndOnePlaceManager.Infrastructure.Interfaces
{
    public interface IServerSettingsStore
    {
        /// <summary>A copy of the saved settings; empty when there's no file or it can't be read.</summary>
        ServerSettings Load();

        /// <summary>Why the file couldn't be read, or null.</summary>
        string? LoadError { get; }

        /// <summary>Applies <paramref name="change"/> to the saved settings and saves them; throws ServerSettingsException when the file can't be read.</summary>
        ServerSettings Update(Action<ServerSettings> change);

        /// <summary>Raised after every save, with the new settings.</summary>
        event Action<ServerSettings>? Changed;
    }
}
