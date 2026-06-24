using System.Net.Http.Json;
using System.Text.Json;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleSimulationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleSimulationApiClient> _logger;

    public EchoConsoleSimulationApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleSimulationApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(
            EchoConsoleApiClientNames.Admin);

        _logger = logger;
    }

    public Task<SimulationApiResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Get,
            "/api/admin/simulation/status",
            null,
            cancellationToken);
    }

    public Task<SimulationApiResponse> ReconcileAsync(
        object request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Post,
            "/api/admin/simulation/reconcile",
            request,
            cancellationToken);
    }

    public Task<SimulationApiResponse> PulseAsync(
        object request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Post,
            "/api/admin/simulation/pulse",
            request,
            cancellationToken);
    }

    public Task<SimulationApiResponse> InjectCriticalAlertAsync(
        object request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Post,
            "/api/admin/simulation/alerts/critical",
            request,
            cancellationToken);
    }

    public Task<SimulationApiResponse> MassDropAsync(
        object request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Post,
            "/api/admin/simulation/mass-drop",
            request,
            cancellationToken);
    }

    public Task<SimulationApiResponse> PurgeSimulatedDataAsync(
        object request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Delete,
            "/api/admin/simulation/simulated-data",
            request,
            cancellationToken);
    }

    public Task<SimulationApiResponse> WipeTelemetryAsync(
        object request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Delete,
            "/api/admin/simulation/telemetry",
            request,
            cancellationToken);
    }

    private async Task<SimulationApiResponse> SendAsync(
        HttpMethod method,
        string requestUri,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                method,
                requestUri);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var content = await response.Content.ReadAsStringAsync(
                cancellationToken);

            var contentType =
                response.Content.Headers.ContentType?.ToString()
                ?? "application/json";

            return new SimulationApiResponse(
                (int)response.StatusCode,
                contentType,
                string.IsNullOrWhiteSpace(content)
                    ? "{}"
                    : content);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Simulation API request failed. Method={Method}, Uri={RequestUri}",
                method,
                requestUri);

            var fallback = JsonSerializer.Serialize(
                new
                {
                    message =
                        "The simulation API is unavailable. Confirm that EchoConsole.Api is running and that the admin API key is configured."
                });

            return new SimulationApiResponse(
                StatusCodes.Status503ServiceUnavailable,
                "application/json",
                fallback);
        }
    }
}

public sealed record SimulationApiResponse(
    int StatusCode,
    string ContentType,
    string Content);
