namespace DndOnePlaceManager.Application.Exceptions
{
    public class WrongArgumentsException : Exception
    {
        public WrongArgumentsException(params string[] fields) : base("Following fields are wrong: " + string.Join(',', fields)) { } 
    }
}
