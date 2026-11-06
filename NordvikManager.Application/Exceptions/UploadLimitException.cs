namespace NordvikManager.Application.Exceptions
{
    /// <summary>An upload over the server's upload limit (answered 413, with the message).</summary>
    public class UploadLimitException : Exception
    {
        public UploadLimitException(string message) : base(message) { }
    }
}
