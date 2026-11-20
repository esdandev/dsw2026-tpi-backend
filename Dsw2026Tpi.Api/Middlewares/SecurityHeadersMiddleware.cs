namespace Dsw2026Tpi.Api.Middlewares;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _includeCsp;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _includeCsp = environment.IsProduction();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // Swagger UI usa estilos y scripts inline: el CSP se aplica solo en producción.
        if (_includeCsp)
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        await _next(context);
    }
}
