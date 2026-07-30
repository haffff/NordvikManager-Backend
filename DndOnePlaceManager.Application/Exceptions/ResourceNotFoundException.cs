namespace DndOnePlaceManager.Application.Exceptions
{
    public class ResourceNotFoundException : Exception
    {
        public ResourceNotFoundException(string resourceName) : base("Resource is unavailable: " + resourceName) { }

        public ResourceNotFoundException(string resourceName, object identifier) : base($"Resource is unavailable: {resourceName} (Id: {identifier})") { }
    }
}
