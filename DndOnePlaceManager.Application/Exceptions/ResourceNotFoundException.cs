namespace DndOnePlaceManager.Application.Exceptions
{
    public class ResourceNotFoundException : Exception
    {
        public ResourceNotFoundException(string resourceName) : base ("Resource is unavailable: " + resourceName) { }
    }
}
