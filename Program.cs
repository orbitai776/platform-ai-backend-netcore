using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

Env.Load(".env");
builder.Configuration.AddEnvironmentVariables();

// chỉ override nếu có PORT
var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();

app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());


app.MapGet("/", () => "Service is running 🚀");

app.Run();