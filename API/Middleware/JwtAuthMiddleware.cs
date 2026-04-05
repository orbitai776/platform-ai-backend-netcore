// AdminService.API/Middleware/JwtAuthMiddleware.cs
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AdminService.API.Middleware;

public class JwtAuthMiddleware(
    RequestDelegate next,
    IConfiguration config,
    ILogger<JwtAuthMiddleware> logger)
{
    private static readonly string[] _skipPaths = ["/swagger", "/health"];

    private readonly TokenValidationParameters _validationParams = new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Secret"]!)),

        ValidateIssuer   = true,
        ValidIssuer      = config["Jwt:Issuer"],

        ValidateAudience = true,
        ValidAudience    = config["Jwt:Audience"],

        ValidateLifetime      = true,
        ClockSkew             = TimeSpan.FromSeconds(30),
        RequireExpirationTime = true,
    };

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (_skipPaths.Any(p => ctx.Request.Path.StartsWithSegments(p)))
        {
            await next(ctx);
            return;
        }

        var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new
            {
                message = "Missing or invalid Authorization header. Expected: Bearer <token>"
            });
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();

        try
        {
            var handler   = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, _validationParams, out _);

            var adminId = principal.FindFirst("uid")?.Value;
            var roles   = principal.Claims
                .Where(c => c.Type == "roles")
                .Select(c => c.Value)
                .ToList();
            var email = principal.FindFirst("email")?.Value;
            var name  = principal.FindFirst("name")?.Value;

            // TODO Sprint 03: bật lại check role sau khi có child-admin design
            // var isAdmin = roles.Any(r => r == "admin" || r == "super_admin");
            // if (!isAdmin) { return 403 }

            ctx.Items["AdminId"]   = adminId;
            ctx.Items["AdminRole"] = string.Join(",", roles);
            ctx.Items["Email"]     = email;
            ctx.Items["Name"]      = name;

            await next(ctx);
        }
        catch (SecurityTokenExpiredException)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { message = "Token đã hết hạn" });
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { message = "Chữ ký token không hợp lệ" });
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning("JWT validation failed: {Message}", ex.Message);
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { message = "Token không hợp lệ" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during JWT validation");
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { message = "Internal server error" });
        }
    }
}