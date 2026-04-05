// AdminService.API/Program.cs
using AdminService.API.Middleware;
using AdminService.Application.Partners;
using AdminService.Application.Users;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Infrastructure.Data;
using platform_ai_backend_netcore.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ── PostgreSQL / Supabase ────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSQL"),
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(3),
            errorCodesToAdd: null)
    )
    .UseSnakeCaseNamingConvention() // map PascalCase → snake_case tự động
);

// ── DI ───────────────────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPartnerService, PartnerService>();

// ── Controllers ──────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Admin Service – Users & Partners",
        Version     = "v1",
        Description = "Sprint 02 | Supabase PostgreSQL",
    });

    c.AddSecurityDefinition("AdminId", new()
    {
        Type        = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In          = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name        = "X-Admin-Id",
        Description = "Admin ID — hardcode Sprint 02",
    });

    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() {
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                Id   = "AdminId"
            }},
            []
        }
    });
});

// ── CORS ─────────────────────────────────────────────────────
var origins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(opt =>
    opt.AddPolicy("Frontend", p =>
        p.WithOrigins(origins)
         .AllowAnyMethod()
         .AllowAnyHeader()          // ← phải có dòng này
         .AllowCredentials()        // ← thêm dòng này
         .WithExposedHeaders("Authorization")  // ← thêm dòng này
    )
);

// ── Health check ─────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL")!);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Admin Service v1")
);

app.MapHealthChecks("/health");
app.UseCors("Frontend");
app.UseMiddleware<JwtAuthMiddleware>();
app.MapControllers();

app.Run();