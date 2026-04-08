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
    opt.InstanceName  = "AdminService:";
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

            ValidateIssuer   = true,
            ValidIssuer      = Environment.GetEnvironmentVariable("JWT_ISSUER"),

            ValidateAudience      = true,
            ValidateLifetime      = true,
            ClockSkew             = TimeSpan.FromSeconds(30),
            RequireExpirationTime = true,
        };
    });

builder.Services.AddAuthorization();

// ── DI ────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPartnerService, PartnerService>();

// ── Controllers + Swagger ─────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Admin Service – Users & Partners",
        Version     = "v1",
        Description = "Sprint 02 | Supabase PostgreSQL + Redis",
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        Description  = "Nhập Internal JWT token từ Gateway",
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            []
        }
    });
});

// ── CORS ──────────────────────────────────────────────────────
var originsRaw = Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "";
var origins    = originsRaw
    .Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(o => o.Trim())
    .ToArray();

builder.Services.AddCors(opt =>
    opt.AddPolicy("Frontend", p =>
        p.WithOrigins(origins)
         .AllowAnyMethod()
         .AllowAnyHeader()
         .WithExposedHeaders("Authorization")
    )
);

// ── Health check ──────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")!);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Admin Service v1")
);

app.MapHealthChecks("/health");
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<JwtAuthMiddleware>();
app.MapControllers();

app.Run();