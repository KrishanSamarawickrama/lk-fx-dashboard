using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Api.Security;

public class ApiKeyEndpointFilter(IOptions<SecurityOptions> options) : IEndpointFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var apiKey = options.Value.ApiKey;

        if (string.IsNullOrEmpty(apiKey))
        {
            return Results.Problem(
                detail: "API key not configured on server.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey)
            || !string.Equals(apiKey, providedKey, StringComparison.Ordinal))
        {
            return Results.Problem(
                detail: "Invalid or missing API key.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }
}
