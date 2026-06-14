using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiBaseController : ControllerBase
{
    protected string CurrentUserId =>
        User.FindFirstValue("sub") ?? throw new UnauthorizedAccessException("Missing 'sub' claim in JWT");

    protected IActionResult ApiError(string message, int statusCode = 400)
    {
        return StatusCode(statusCode, new { error = message });
    }

    protected IActionResult ApiNotFound(string entity = "Resource")
    {
        return NotFound(new { error = $"{entity} not found" });
    }

    // Returns 400 with joined ModelState errors when invalid, null when valid.
    protected IActionResult? ValidateModelState()
    {
        if (ModelState.IsValid) return null;
        var errors = string.Join("; ",
            ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return ApiError(errors);
    }
}
