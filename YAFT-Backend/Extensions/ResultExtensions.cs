using Microsoft.AspNetCore.Mvc;
using YAFT.Application.Common;

namespace YAFT.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Result<T> result,
        Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess is null ? controller.Ok(result.Value) : onSuccess(result.Value);

        return controller.ToProblem(result.Error!);
    }

    public static IActionResult ToActionResult(
        this ControllerBase controller,
        Result result,
        Func<IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess is null ? controller.NoContent() : onSuccess();

        return controller.ToProblem(result.Error!);
    }

    private static IActionResult ToProblem(this ControllerBase controller, Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation   => StatusCodes.Status400BadRequest,
            ErrorType.NotFound     => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Conflict     => StatusCodes.Status409Conflict,
            ErrorType.Unavailable  => StatusCodes.Status502BadGateway,
            _                      => StatusCodes.Status500InternalServerError
        };

        return controller.Problem(title: error.Code, detail: error.Description, statusCode: status);
    }
}
