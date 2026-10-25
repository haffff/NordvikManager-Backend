namespace DndOnePlaceManager.Application.Exceptions
{
    public class WrongArgumentsException : Exception
    {
        public WrongArgumentsException(params string[] fields) : base("Following fields are wrong: " + string.Join(',', fields)) { }

        private WrongArgumentsException(string message, bool _) : base(message) { }

        /// <summary>Wrong input, explained in a sentence for whoever entered it.</summary>
        public static WrongArgumentsException Because(string message) => new(message, true);
    }
}
