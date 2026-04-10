// AdminService.API/Program.cs
using System.Text;
using AdminService.API.Middleware;
using AdminService.Application.Partners;
using AdminService.Application.Users;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using platform_ai_backend_netcore.Infrastructure.Data;
using platform_ai_backend_netcore.Infrastructure.Services;

// ── Load .env trước mọi thứ ──────────────────────────────────
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// ── Map biến môi trường vào Configuration ────────────────────
builder.Configuration
    .AddEnvironmentVariables();

// ── PostgreSQL / Supabase ─────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(
        Environment.GetEnvironmentVariable("POSTGRES_CONNECTION"),
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(3),
            errorCodesToAdd: null)
    )
    .UseSnakeCaseNamingConvention()
);

// ── Redis ─────────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration = Environment.GetEnvironmentVariable("REDIS_CONNECTION");
    opt.InstanceName = "AdminService:";
});

// ── JWT ───────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    Environment.GetEnvironmentVariable("JWT_SECRET")!)),

            ValidateIssuer = true,
            ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),

            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireExpirationTime = true,
        };
    });

builder.Services.AddAuthorization();

// ── DI ────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPartnerService, PartnerService>();

// ── Controllers─────────────────────────────────────
builder.Services.AddControllers();

// ── CORS Setup ────────────────────────────────────────────────
var environment = builder.Environment.EnvironmentName;
Console.WriteLine($"[CORS] Environment: {environment}");

builder.Services.AddCors(opt =>
{
    if (builder.Environment.IsDevelopment())
    {
        opt.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();

        });
        Console.WriteLine("[CORS] Development mode: AllowAll policy enabled");
    }
    else
    {
        var gatewayUrls = Environment.GetEnvironmentVariable("GATEWAY_URLS") ?? "";
        var allowedOrigins = gatewayUrls
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim())
                            .ToArray();
        if (allowedOrigins.Length == 0)
        {
            Console.WriteLine("[ERROR] PRODUCTION: GATEWAY_URLS not configured!");
            throw new InvalidOperationException(
                "GATEWAY_URLS environment variable is required in production"
            );
        }
        opt.AddPolicy("GatewayOnly", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .WithExposedHeaders("Authorization", "X-RateLimit-Remaining")
                  .AllowCredentials();
        });

        Console.WriteLine($"[CORS] Production mode: GatewayOnly policy with origins: {string.Join(", ", allowedOrigins)}");
    }
}
);
// ── Health check ──────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")!);

var app = builder.Build();
// ── Middleware Pipeline ───────────────────────────────────────
app.MapHealthChecks("/health");

// CORS
if (builder.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}
else
{
    app.UseCors("GatewayOnly");
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<JwtAuthMiddleware>();
app.MapControllers();

app.Run();