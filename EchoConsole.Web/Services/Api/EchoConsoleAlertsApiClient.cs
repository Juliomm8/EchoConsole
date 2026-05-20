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
        _httpClient = httpClientFactory.CreateClient("EchoConsoleApiAdmin");
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
                query["isResolved"] = isResolved.Value.ToString().ToLowerInvariant();
            }

            var url = QueryHelpers.AddQueryString("/api/admin/alerts", query);

            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Alerts request failed. StatusCode: {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    body);

                return new PagedResponse<SystemAlertApiDto>();
            }

            var data = await response.Content.ReadFromJsonAsync<PagedResponse<SystemAlertApiDto>>(
                cancellationToken: cancellationToken);

            return data ?? new PagedResponse<SystemAlertApiDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reading alerts from EchoConsole.Api.");
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

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Resolve alert request failed. AlertId: {AlertId}. StatusCode: {StatusCode}. Body: {Body}",
                    alertId,
                    response.StatusCode,
                    body);

                return null;
            }

            return await response.Content.ReadFromJsonAsync<SystemAlertApiDto>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while resolving alert {AlertId}.", alertId);
            return null;
        }
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