using Microsoft.AspNetCore.SignalR;

namespace EchoConsole.Api.Security;

public sealed class TelemetryHubApiKeyFilter : IHubFilter
{
    private const string HeaderName = "X-Admin-Api-Key";

    private readonly IRealtimeApiKeyValidator _apiKeyValidator;
    private readonly ILogger<TelemetryHubApiKeyFilter> _logger;

    public TelemetryHubApiKeyFilter(
        IRealtimeApiKeyValidator apiKeyValidator,
        ILogger<TelemetryHubApiKeyFilter> logger)
    {
        _apiKeyValidator = apiKeyValidator;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        return await next(invocationContext);
    }

    public async Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next)
    {
        var httpContext = context.Context.GetHttpContext();

        if (httpContext is null)
        {
            _logger.LogWarning("TelemetryHub connection rejected because HttpContext was null.");
            throw new HubException("Unauthorized relay connection.");
        }

        var providedApiKey = httpContext.Request.Headers[HeaderName].FirstOrDefault();

        if (!_apiKeyValidator.IsValid(providedApiKey))
        {
            _logger.LogWarning(
                "TelemetryHub connection rejected from {RemoteIp}. Missing or invalid server-to-server header.",
                httpContext.Connection.RemoteIpAddress?.ToString());

            context.Context.Abort();
            throw new HubException("Unauthorized relay connection.");
        }

        await next(context);
    }

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        await next(context, exception);
    }
}