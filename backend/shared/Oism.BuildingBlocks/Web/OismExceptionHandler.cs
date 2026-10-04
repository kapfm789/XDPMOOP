using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Oism.SharedKernel;

namespace Oism.BuildingBlocks.Web;

// Bảng ánh xạ: docs/conventions/backend.md mục "Lỗi".
// Exception không có trong bảng rơi xuống 500 mặc định, không lộ chi tiết.
internal sealed class OismExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = new ProblemDetails();
        switch (exception)
        {
            case ValidationException validation:
                problem.Status = StatusCodes.Status400BadRequest;
                problem.Title = "Dữ liệu không hợp lệ";
                problem.Extensions["code"] = "validation_failed";
                problem.Extensions["errors"] = validation.Errors
                    .GroupBy(failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName))
                    .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());
                break;

            case OismException business:
                problem.Status = business switch
                {
                    NotFoundException => StatusCodes.Status404NotFound,
                    UnauthenticatedException => StatusCodes.Status401Unauthorized,
                    _ => StatusCodes.Status409Conflict,
                };
                problem.Title = business.Message;
                problem.Extensions["code"] = business.Code;
                if (business.Details is not null)
                    problem.Extensions["details"] = business.Details;
                break;

            default:
                return false;
        }

        httpContext.Response.StatusCode = problem.Status.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }
}
