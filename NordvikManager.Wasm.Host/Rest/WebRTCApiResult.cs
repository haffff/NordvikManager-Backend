namespace NordvikManager.Wasm.Host.Rest
{
    /// <summary>
    /// Stand-in for IActionResult under the REST tunnel — a handler returns this instead
    /// of Ok(...)/NotFound()/etc.
    /// </summary>
    public record WebRTCApiResult(int Status, object? Body);
}
