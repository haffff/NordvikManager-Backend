namespace DndOnePlaceManager.Application.Mapping
{
    /// <summary>
    /// Maps between DTOs and models (see <see cref="AppMapper"/>). A null source gives null,
    /// or an empty list for a list target; lists map item by item.
    /// </summary>
    public interface IMapper
    {
        T Map<T>(object? source);
    }
}
