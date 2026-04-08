namespace DndOnePlaceManager.Application.Exceptions
{
    public class ActionProcessException : Exception
    {
        public ActionProcessException(string message) : base(message) { }
        public ActionProcessException(string message, Exception innerException) : base(message, innerException) { }
    }
}
