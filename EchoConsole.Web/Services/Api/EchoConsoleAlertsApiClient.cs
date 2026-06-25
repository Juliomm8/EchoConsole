using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleAlertsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleAlertsApiClient> _logger;

    public EchoConsoleAlertsApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleAlertsApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(
            EchoConsoleApiClientNames.Admin);
        _logger = logger;
    }

    public async Task<PagedResponse<SystemAlertApiDto>> GetAlertsAsync(
        string? severity,
        bool? isResolved,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new Dictionary<string, string?>
            {
                ["pageNumber"] = pageNumber.ToString(),
                ["pageSize"] = pageSize.ToString()
            };

            if (!string.IsNullOrWhiteSpace(severity))
            {
                query["severity"] = severity.Trim();
            }

            if (isResolved.HasValue)
            {
                query["isResolved"] =
                    isResolved.Value.ToString().ToLowerInvariant();
            }

            var url = QueryHelpers.AddQueryString(
                "/api/admin/alerts",
                query);

            using var response = await _httpClient.GetAsync(
                url,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    "Alerts request",
                    response,
                    cancellationToken);

                return new PagedResponse<SystemAlertApiDto>();
            }

            return await response.Content
                .ReadFromJsonAsync<PagedResponse<SystemAlertApiDto>>(
                    cancellationToken: cancellationToken)
                ?? new PagedResponse<SystemAlertApiDto>();
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
                "Unexpected error while reading alerts from EchoConsole.Api.");

            return new PagedResponse<SystemAlertApiDto>();
        }
    }

    public async Task<SystemAlertApiDto?> ResolveAlertAsync(
        int alertId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/admin/alerts/{alertId}/resolve");

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    $"Resolve alert {alertId}",
                    response,
                    cancellationToken);

                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<SystemAlertApiDto>(
                    cancellationToken: cancellationToken);
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
                "Unexpected error while resolving alert {AlertId}.",
                alertId);

            return null;
        }
    }

    public async Task<AlertAiTrendAnalysisApiDto?>
        RunAiTrendAnalysisAsync(
            string culture,
            CancellationToken cancellationToken = default)
    {
        try
        {
            var url = QueryHelpers.AddQueryString(
                "/api/admin/alerts/ai-trend-analysis",
                "culture",
                culture);

            using var response = await _httpClient.PostAsync(
                url,
                null,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    "AI trend analysis",
                    response,
                    cancellationToken);

                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<AlertAiTrendAnalysisApiDto>(
                    cancellationToken: cancellationToken);
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
                "Unexpected error while running the simulated AI trend analysis.");

            return null;
        }
    }

    public async Task<AlertDiscordBroadcastApiDto?>
        BroadcastDiscordAsync(
            CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsync(
                "/api/admin/alerts/broadcast-discord",
                null,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var failure = await response.Content
                    .ReadFromJsonAsync<AlertDiscordBroadcastApiDto>(
                        cancellationToken: cancellationToken);

                if (failure is not null)
                {
                    return failure;
                }

                await LogFailureAsync(
                    "Discord alert broadcast",
                    response,
                    cancellationToken);

                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<AlertDiscordBroadcastApiDto>(
                    cancellationToken: cancellationToken);
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
                "Unexpected error while broadcasting alerts to Discord.");

            return null;
        }
    }

    private async Task LogFailureAsync(
        string operation,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(
            cancellationToken);

        _logger.LogWarning(
            "{Operation} failed. StatusCode={StatusCode}, Body={Body}",
            operation,
            response.StatusCode,
            body);
    }
}

public sealed class SystemAlertApiDto
{
    public int Id { get; set; }

    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string? InstallationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool IsResolved { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public sealed class AlertAiTrendAnalysisApiDto
{
    public string Narrative { get; set; } = string.Empty;

    public string ActiveBuildVersion { get; set; } = string.Empty;

    public string DominantSource { get; set; } = string.Empty;

    public int RecentAlertCount { get; set; }

    public int OpenAlertCount { get; set; }

    public int RecentCriticalCount { get; set; }

    public int PreviousCriticalCount { get; set; }

    public decimal CriticalTrendPercent { get; set; }

    public DateTimeOffset GeneratedAtUtc { get; set; }
}

public sealed class AlertDiscordBroadcastApiDto
{
    public bool Sent { get; set; }

    public int AlertCount { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; set; }
}
