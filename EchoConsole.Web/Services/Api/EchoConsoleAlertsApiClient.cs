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
        string status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["status"] = status,
            ["pageNumber"] = pageNumber.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        if (!string.IsNullOrWhiteSpace(severity))
        {
            query["severity"] = severity.Trim();
        }

        return await GetAsync(
            QueryHelpers.AddQueryString("/api/admin/alerts", query),
            new PagedResponse<SystemAlertApiDto>(),
            cancellationToken);
    }

    public Task<AlertOverviewMetricsApiDto> GetMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        return GetAsync(
            "/api/admin/alerts/metrics",
            new AlertOverviewMetricsApiDto(),
            cancellationToken);
    }

    public Task<IReadOnlyList<AlertTypeDefinitionApiDto>> GetAlertTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return GetAsync<IReadOnlyList<AlertTypeDefinitionApiDto>>(
            "/api/admin/alerts/types",
            Array.Empty<AlertTypeDefinitionApiDto>(),
            cancellationToken);
    }

    public async Task<SystemAlertApiDto?> ResolveAlertAsync(
        int alertId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/admin/alerts/{alertId}/resolve");

        return await SendAsync<SystemAlertApiDto>(
            request,
            cancellationToken);
    }

    public async Task<AlertTypeDefinitionApiDto?> UpdateAlertTypeAsync(
        int id,
        UpdateAlertTypeApiRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/admin/alerts/types/{id}")
        {
            Content = JsonContent.Create(request)
        };

        return await SendAsync<AlertTypeDefinitionApiDto>(
            message,
            cancellationToken);
    }

    public async Task<bool> DeleteAlertTypeAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync(
            $"/api/admin/alerts/types/{id}",
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        await LogFailureAsync(
            $"Delete alert type {id}",
            response,
            cancellationToken);

        return false;
    }

    public async Task<AlertAiTrendAnalysisApiDto?> RunAiTrendAnalysisAsync(
        string culture,
        CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString(
            "/api/admin/alerts/ai-trend-analysis",
            "culture",
            culture);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            url);

        return await SendAsync<AlertAiTrendAnalysisApiDto>(
            request,
            cancellationToken);
    }

    private async Task<T> GetAsync<T>(
        string url,
        T fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                url,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    "GET " + url,
                    response,
                    cancellationToken);

                return fallback;
            }

            return await response.Content.ReadFromJsonAsync<T>(
                cancellationToken: cancellationToken)
                ?? fallback;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API read failed. Url={Url}", url);
            return fallback;
        }
    }

    private async Task<T?> SendAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    request.RequestUri?.ToString() ?? "Alerts API request",
                    response,
                    cancellationToken);

                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alerts API write failed.");
            return default;
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
    public string Status { get; set; } = string.Empty;
    public string ErrorTypeCode { get; set; } = string.Empty;
    public string? BuildVersion { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? InstallationId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool IsResolved { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public sealed class AlertTypeDefinitionApiDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultSeverity { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int AlertCount { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class UpdateAlertTypeApiRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultSeverity { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class AlertOverviewMetricsApiDto
{
    public int ActiveNocCount { get; set; }
    public int MitigatedLast24Hours { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
}

public sealed class AlertAiTrendAnalysisApiDto
{
    public string Narrative { get; set; } = string.Empty;
    public string ActiveBuildVersion { get; set; } = string.Empty;
    public string DominantSource { get; set; } = string.Empty;
    public int RecentAlertCount { get; set; }
    public int OpenAlertCount { get; set; }
    public int MitigatedLast24Hours { get; set; }
    public int ActiveCriticalCount { get; set; }
    public int RecentCriticalCount { get; set; }
    public int PreviousCriticalCount { get; set; }
    public decimal CriticalTrendPercent { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
}

public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
