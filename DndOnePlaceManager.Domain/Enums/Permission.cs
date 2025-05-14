namespace DndOnePlaceManager.Domain.Enums
{
    public enum Permission
    {
        NotSet = -1,
        None = 0,
        Read = 1,
        Execute = 2,
        Control = 4,
        Edit = 8,
        Remove = 16,
        All = 31,
    }
}
