using Microsoft.AspNetCore.Mvc;
using NordvikManager.Api;

namespace NordvikManager.Controllers
{
    /// <summary>A shared endpoint's <see cref="ApiResult"/> as the IActionResult the controller returned before.</summary>
    public static class ApiResultExtensions
    {
        public static IActionResult ToActionResult(this ApiResult result) => result.Kind switch
        {
            ApiResultKind.Ok => result.HasBody ? new OkObjectResult(result.Body) : new OkResult(),
            ApiResultKind.BadRequest => result.HasBody ? new BadRequestObjectResult(result.Body) : new BadRequestResult(),
            ApiResultKind.Unauthorized => result.HasBody ? new UnauthorizedObjectResult(result.Body) : new UnauthorizedResult(),
            ApiResultKind.Forbid => new ForbidResult(),
            ApiResultKind.NotFound => result.HasBody ? new NotFoundObjectResult(result.Body) : new NotFoundResult(),
            ApiResultKind.Conflict => new ConflictObjectResult(result.Body),
            ApiResultKind.NoContent => new NoContentResult(),
            ApiResultKind.Content => new ContentResult { Content = (string?)result.Body, ContentType = result.ContentType, StatusCode = result.StatusCode },
            _ => result.HasBody ? new ObjectResult(result.Body) { StatusCode = result.StatusCode } : new StatusCodeResult(result.StatusCode),
        };
    }
}
