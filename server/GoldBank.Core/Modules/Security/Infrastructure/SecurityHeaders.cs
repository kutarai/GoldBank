using Microsoft.AspNetCore.Http;

namespace GoldBank.Core.Modules.Security.Infrastructure;

/// <summary>
/// Middleware that adds security response headers to all HTTP responses (STORY-075).
/// Implements OWASP recommended security headers for hardening against common web attacks.
/// </summary>
public sealed class SecurityHeaders
{
    private readonly RequestDelegate _next;

    public SecurityHeaders(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent MIME type sniffing
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // Enable XSS protection in older browsers
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Enforce HTTPS with HSTS (1 year, include subdomains)
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";

        // Content Security Policy - restrict resource loading
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'";

        // Control referrer information sent with requests
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions policy - disable unused browser features
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        await _next(context);
    }
}
