using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MediaFlows.Web.Filters;

/// <summary>
/// Maps exceptions thrown by service-layer code into conventional HTTP responses:
///  - <see cref="UnauthorizedAccessException"/> → 401 (e.g. ApiBaseController.CurrentUserId
///    when the JWT lacks a 'sub' claim)
///  - <see cref="InvalidOperationException"/> / <see cref="ArgumentException"/> → 400
/// Other exception types fall through to the default error middleware.
/// </summary>
public class HandleServiceExceptionsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
        {
            context.Result = new ObjectResult(new { error = context.Exception.Message })
            {
                StatusCode = 401
            };
            context.ExceptionHandled = true;
            return;
        }

        if (context.Exception is InvalidOperationException or ArgumentException)
        {
            context.Result = new ObjectResult(new { error = context.Exception.Message })
            {
                StatusCode = 400
            };
            context.ExceptionHandled = true;
        }
    }
}
