namespace LkFxDashboard.Web.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string Csp =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://static.cloudflareinsights.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "object-src 'none'; " +
        "connect-src 'self' http: ws: wss: https://cloudflareinsights.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "upgrade-insecure-requests";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers.Remove("Server");

            // CSP and framing headers only apply to document responses
            var contentType = context.Response.ContentType;
            if (contentType is null || contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                headers["Content-Security-Policy"] = Csp;
                headers["X-Frame-Options"] = "DENY";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
