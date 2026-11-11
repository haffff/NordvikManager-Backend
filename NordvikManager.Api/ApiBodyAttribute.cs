namespace NordvikManager.Api
{
    /// <summary>
    /// An endpoint parameter of a simple type (a Guid, a string) taken from the JSON body, as
    /// [FromBody] on the controller action; complex types come from the body anyway.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ApiBodyAttribute : Attribute
    {
    }
}
