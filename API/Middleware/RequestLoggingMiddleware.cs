using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.API.Middleware;
/// <summary>
/// Middleware log mọi request/response ra console với format rõ ràng cho DevOps.
/// Kết hợp với OpenTelemetry để tạo span cho từng request.
/// </summary>

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public const string ActivitySourceName = "platform-ai-backend";
    public const string MeterName = "platform-ai-backend";

    private static readonly ActivitySource _src = new(ActivitySourceName, "1.0.0");
    private static readonly Meter _meter = new(MeterName, "1.0.0");
    // Metrics
    private static readonly Counter<long> _reqTotal = _meter.CreateCounter<long>(
        "http_requests_total", description: "Total HTTP requests");

    private static readonly Histogram<double> _reqDuration = _meter.CreateHistogram<double>(
        "http_request_duration_ms", unit: "ms", description: "HTTP request duration");

    private static readonly Counter<long> _errTotal = _meter.CreateCounter<long>(
        "http_errors_total", description: "Total 4xx/5xx responses");

    // Paths bỏ qua — không log để tránh noise
    private static readonly HashSet<string> _ignoredPaths =
    [
        "/health", "/metrics", "/favicon.ico",
    ];

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "/";

        if (_ignoredPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(ctx);
            return;
        }

        var method = ctx.Request.Method;
        var sw = Stopwatch.StartNew();
        var requestId = ctx.TraceIdentifier;

        // Tạo OTEL span — Grafana Traces sẽ nhìn thấy span này
        using var activity = _src.StartActivity($"{method} {path}", ActivityKind.Server);
        activity?.SetTag("http.method", method);
        activity?.SetTag("http.url", path);
        activity?.SetTag("http.request_id", requestId);
        activity?.SetTag("http.user_agent", ctx.Request.Headers.UserAgent.ToString());

        // Gắn TraceId vào log context để mọi log trong request đều có TraceId
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = activity?.TraceId.ToString() ?? requestId,
            ["SpanId"] = activity?.SpanId.ToString() ?? "",
            ["RequestId"] = requestId,
        });

        logger.LogInformation(
            "[REQ] {Method} {Path} | id={RequestId}",
            method, path, requestId);

        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().Name);
            activity?.SetTag("exception.message", ex.Message);

            _errTotal.Add(1, new TagList
            {
                { "method", method },
                { "path",   NormalizePath(path) },
                { "status", 500 },
            });

            logger.LogError(ex,
                "[RES] {Method} {Path} | status=500 | elapsed={ElapsedMs}ms | id={RequestId} | UNHANDLED EXCEPTION: {ExMessage}",
                method, path, sw.ElapsedMilliseconds, requestId, ex.Message);

            throw;
        }

        sw.Stop();

        var status = ctx.Response.StatusCode;
        var elapsed = sw.Elapsed.TotalMilliseconds;
        var normPath = NormalizePath(path);

        activity?.SetTag("http.status_code", status);
        activity?.SetTag("duration_ms", elapsed);

        var tags = new TagList
        {
            { "method", method },
            { "path",   normPath },
            { "status", status },
        };

        _reqTotal.Add(1, tags);
        _reqDuration.Record(elapsed, tags);

        if (status >= 500)
        {
            _errTotal.Add(1, tags);
            activity?.SetStatus(ActivityStatusCode.Error, $"HTTP {status}");

            logger.LogError(
                "[RES] {Method} {Path} | status={Status} | elapsed={ElapsedMs}ms | id={RequestId}",
                method, path, status, elapsed, requestId);
        }
        else if (status >= 400)
        {
            _errTotal.Add(1, tags);

            logger.LogWarning(
                "[RES] {Method} {Path} | status={Status} | elapsed={ElapsedMs}ms | id={RequestId}",
                method, path, status, elapsed, requestId);
        }
        else
        {
            logger.LogInformation(
                "[RES] {Method} {Path} | status={Status} | elapsed={ElapsedMs}ms | id={RequestId}",
                method, path, status, elapsed, requestId);
        }

        // Cảnh báo DevOps nếu request quá chậm
        if (elapsed > 3000)
        {
            logger.LogWarning(
                "[SLOW REQUEST] {Method} {Path} took {ElapsedMs}ms — investigate DB queries or cache",
                method, path, elapsed);
        }
    }

    /// <summary>
    /// Normalize path params để metrics không explode cardinality.
    /// /users/abc-123 → /users/{id}
    /// /partners/abc/tokens → /partners/{id}/tokens
    /// </summary>
    private static string NormalizePath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (Guid.TryParse(segments[i], out _) || long.TryParse(segments[i], out _))
                segments[i] = "{id}";
        }
        return "/" + string.Join("/", segments);
    }
}
