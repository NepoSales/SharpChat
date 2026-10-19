using Microsoft.AspNetCore.Mvc;
using SharpChat.Domain.Common;

namespace SharpChat.Api.Common;

internal static class ErrorResults
{
    /// <summary>Maps an application <see cref="Error"/> to a ProblemDetails response.</summary>
    public static ActionResult ToProblem(this ControllerBase controller, Error error)
    {
        if (error.Type == ErrorType.Validation && error.ValidationErrors is not null)
        {
            var details = new ValidationProblemDetails(error.ValidationErrors.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
            };
            details.Extensions["code"] = error.Code;
            return controller.BadRequest(details);
        }

        var status = error.Type switch
        {
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = controller.Problem(detail: error.Message, statusCode: status);
        ((ProblemDetails)problem.Value!).Extensions["code"] = error.Code;
        return problem;
    }
}
