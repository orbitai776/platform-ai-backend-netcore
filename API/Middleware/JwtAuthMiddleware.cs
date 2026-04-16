// AdminService.API/Middleware/JwtAuthMiddleware.cs
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace AdminService.API.Middleware;

public class JwtAuthMiddleware(
    RequestDelegate next,
    ILogger<JwtAuthMiddleware> logger)
{
    private static readonly string[] _skipPaths = ["/swagger", "/health"];

    private readonly TokenValidationParameters _validationParams = new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? throw new InvalidOperationException("JWT_SECRET is not configured"))),

        ValidateIssuer = true,
        ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
                       ?? throw new InvalidOperationException("JWT_ISSUER is not configured"),

        ValidateAudience = true,
        ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
                       ?? throw new InvalidOperationException("JWT_AUDIENCE is not configured"),

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
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
            var handler = new JwtSecurityTokenHandler();

            // Tắt mapping claims mặc định của .NET để giữ nguyên tên claim gốc (vd: "roles", "uid")
            handler.InboundClaimTypeMap.Clear();

            var principal = handler.ValidateToken(token, _validationParams, out _);

            // Trích xuất thông tin từ payload
            var adminId  = principal.FindFirst("uid")?.Value;
            var email    = principal.FindFirst("email")?.Value;
            var name     = principal.FindFirst("name")?.Value
                        ?? principal.FindFirst("full_name")?.Value;

            // Đọc roles — hỗ trợ JSON array hoặc multiple claims
            var roles = ExtractRoles(principal, logger);

            logger.LogDebug("User {Email} has roles: {Roles}", email, string.Join(", ", roles));

            // Kiểm tra quyền admin
            var isAdmin = roles.Any(r => r is "admin" or "super_admin");
            if (!isAdmin)
            {
                logger.LogWarning(
                    "Access denied for user {Email}. Required admin role but got: {Roles}",
                    email, string.Join(", ", roles));

                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    message    = "Access denied. Admin or Super Admin role required.",
                    your_roles = roles
                });
                return;
            }

            ctx.Items["AdminId"]   = adminId;
            ctx.Items["AdminRole"] = string.Join(",", roles);
            ctx.Items["Email"]     = email;
            ctx.Items["Name"]      = name;
            ctx.Items["Roles"]     = roles;

            await next(ctx);
        }
        catch (SecurityTokenExpiredException)
        {
            logger.LogWarning("Token expired");
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { message = "Token đã hết hạn" });
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            logger.LogWarning("Invalid token signature");
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

    /// <summary>
    /// Trích xuất roles từ claim "roles".
    ///
    /// JWT library (.NET) khi gặp "roles": ["a","b","c"] trong payload sẽ tự động
    /// tách thành NHIỀU claims riêng lẻ (mỗi value = 1 claim cùng type "roles").
    /// → principal.FindFirst("roles") chỉ trả về value đầu tiên ("user"), không phải JSON array.
    /// → Phải dùng FindAll("roles") để lấy hết.
    ///
    /// Nếu token được tạo theo cách khác (1 claim chứa JSON string "[...]"):
    /// → Thử deserialize string đó.
    /// </summary>
    private static List<string> ExtractRoles(
        System.Security.Claims.ClaimsPrincipal principal,
        ILogger logger)
    {
        // Lấy TẤT CẢ claims có type = "roles"
        var allRolesClaims = principal.Claims
            .Where(c => c.Type == "roles")
            .Select(c => c.Value)
            .ToList();

        if (allRolesClaims.Count == 0)
        {
            logger.LogDebug("No roles claim found in token");
            return [];
        }

        // Trường hợp 1 (phổ biến): JWT lib đã tách thành nhiều claims
        // Mỗi value là string đơn: "user", "partner", "admin"
        if (allRolesClaims.Count > 1 || !allRolesClaims[0].TrimStart().StartsWith('['))
        {
            logger.LogDebug("Roles parsed as {Count} individual claims", allRolesClaims.Count);
            return allRolesClaims;
        }

        // Trường hợp 2 (ít gặp): 1 claim chứa toàn bộ JSON array "[\"admin\",\"user\"]"
        try
        {
            var roles = JsonSerializer.Deserialize<List<string>>(allRolesClaims[0]);
            if (roles is { Count: > 0 })
            {
                logger.LogDebug("Roles parsed from JSON array string");
                return roles;
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Failed to parse roles as JSON array: {Message}", ex.Message);
        }

        logger.LogDebug("Roles claim is empty or unreadable");
        return [];
    }
}