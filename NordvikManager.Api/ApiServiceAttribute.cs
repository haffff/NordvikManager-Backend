namespace NordvikManager.Api
{
    /// <summary>
    /// An endpoint parameter taken from the request's services, as [FromServices] on the
    /// controller action; the controller passes its own [FromServices] argument.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ApiServiceAttribute : Attribute
    {
    }
}
