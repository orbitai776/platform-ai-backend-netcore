using System.Text;
using AdminService.API.Middleware;
using AdminService.Application.Partners;
using AdminService.Application.Users;
using DotNetEnv;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using platform_ai_backend_netcore.Application.Organization.Interfaces;
using platform_ai_backend_netcore.Application.Tokens.Interfaces;
using platform_ai_backend_netcore.Infrastructure.Cache;
using platform_ai_backend_netcore.Infrastructure.Data;
using platform_ai_backend_netcore.Infrastructure.Services;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;

// ── Load .env trước mọi thứ ──────────────────────────────────
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// ── Map biến môi trường vào Configuration ────────────────────
builder.Configuration.AddEnvironmentVariables();

//OTEL
var oltpUrl = Environment.GetEnvironmentVariable("OLTP_URL")
                ?? throw new InvalidOperationException("OLTP_URL is required");
var oltpAuth = Environment.GetEnvironmentVariable("OLTP_AUTH")
                ?? throw new InvalidOperationException("OLTP_AUTH is required");
var serviceName = Environment.GetEnvironmentVariable("OLTP_SERVICE_NAME")
                ?? throw new InvalidOperationException("OLTP_SERVICE_NAME is required");
var oltpEnv = Environment.GetEnvironmentVariable("OLTP_ENVIROMENT")
                ?? builder.Environment.EnvironmentName.ToLower();
var serviceVersion = "1.0.0";

string otlpAuthHeader;
if (string.IsNullOrEmpty(oltpAuth))
    throw new InvalidOperationException("OLTP_AUTH is required");
else if (oltpAuth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    otlpAuthHeader = oltpAuth;
else if (oltpAuth.Contains(":"))
    otlpAuthHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(oltpAuth));
else otlpAuthHeader = "Basic " + oltpAuth;

var oltpBase = oltpUrl.TrimEnd('/');

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Mircosoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service.name", serviceName)
    .Enrich.WithProperty("service.version", serviceVersion)
    .Enrich.WithProperty("enviroment", oltpEnv)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {TraceId} | {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Debug)
    .WriteTo.OpenTelemetry(otel =>
    {
        otel.Endpoint = $"{oltpBase}/otlp/v1/logs";
        otel.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
        otel.Headers = new Dictionary<string, string>
        {
            ["Authorization"] = otlpAuthHeader,
        };
        otel.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = serviceName,
            ["service.version"] = serviceVersion,
            ["deployment.enviroment"] = oltpEnv,
        };
    })
    .CreateLogger();
builder.Host.UseSerilog();
// ── OpenTelemetry Resource
var otelResource = ResourceBuilder.CreateDefault()
.AddService(serviceName, serviceVersion: serviceVersion)
.AddAttributes(new Dictionary<string, object>
{
    ["deployment.enviroment"] = oltpEnv
});

// ── OpenTelemetry Tracing + Metrics ──────────────────────────
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(otelResource)
        .AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
            o.Filter = ctx =>
                !ctx.Request.Path.StartsWithSegments("/health") &&
                !ctx.Request.Path.StartsWithSegments("/metrics");
        })
        .AddEntityFrameworkCoreInstrumentation(o =>
        {
            o.SetDbStatementForText            = true;
            o.SetDbStatementForStoredProcedure = true;
        })
        .AddHttpClientInstrumentation()
        // Đẩy trace → Grafana Cloud — path /otlp/v1/traces khớp với Go: WithURLPath("/otlp/v1/traces")
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri($"{oltpBase}/otlp/v1/traces");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
            o.Headers  = $"Authorization={otlpAuthHeader}";
        }))
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(otelResource)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri($"{oltpBase}/otlp/v1/metrics");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
            o.Headers  = $"Authorization={otlpAuthHeader}";
        }));

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
//Redis
var redisConn = Environment.GetEnvironmentVariable("REDIS_URL")
                ?? throw new InvalidOperationException("REDIS_URL is required");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var config = ConfigurationOptions.Parse(redisConn);
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddStackExchangeRedisCache(otp =>
{
    otp.Configuration = redisConn;
    otp.InstanceName = "AdminService";
});
builder.Services.AddSingleton<ICacheService, RedisCacheService>();


// ── DI ────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPartnerService, PartnerService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<ITokenService, TokenService>();
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
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
        Console.WriteLine("[CORS] Development mode: AllowAll policy enabled");
    }
    else if (environment == "Staging")
    {
        var stagingGatewayUrls = Environment.GetEnvironmentVariable("STAGING_GATEWAY_URLS") ?? "";
        var allowedOrigins = stagingGatewayUrls
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim())
                            .ToArray();

        if (allowedOrigins.Length == 0)
        {
            Console.WriteLine("[ERROR] STAGING: STAGING_GATEWAY_URLS not configured!");
            throw new InvalidOperationException(
                "STAGING_GATEWAY_URLS environment variable is required in staging"
            );
        }

        opt.AddPolicy("StagingGatewayOnly", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials()
                  .WithExposedHeaders("Authorization", "X-RateLimit-Remaining");
        });

        Console.WriteLine($"[CORS] Staging mode: StagingGatewayOnly policy with origins: {string.Join(", ", allowedOrigins)}");
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
                  .AllowCredentials()
                  .WithExposedHeaders("Authorization", "X-RateLimit-Remaining");
        });

        Console.WriteLine($"[CORS] Production mode: GatewayOnly policy with origins: {string.Join(", ", allowedOrigins)}");
    }
});

// ── Health check ──────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")!)
    .AddRedis(redisConn, name: "redis");

var app = builder.Build();
// Kiểm tra Redis connection lúc startup — xóa sau khi confirm OK
var redisCheck = app.Services.GetRequiredService<IConnectionMultiplexer>();
try
{
    var pong = await redisCheck.GetDatabase().PingAsync();
    Log.Information("[REDIS] Connected ✓ | latency={Latency}ms", pong.TotalMilliseconds);
}
catch (Exception ex)
{
    Log.Error(ex, "[REDIS] Connection FAILED ✗ — cache will be disabled");
}
// ── Middleware Pipeline ────────────────────────────────────────
app.MapHealthChecks("/health");

if (builder.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}
else if (environment == "Staging")
{
    app.UseCors("StagingGatewayOnly");
}
else
{
    app.UseCors("GatewayOnly");
}

app.UseMiddleware<JwtAuthMiddleware>();

app.MapControllers();
Log.Information(
    "[STARTUP] {ServiceName} v{Version} | env={Environment} | otlp={OtlpBase}",
    serviceName, serviceVersion, oltpEnv, oltpBase);
app.Run();