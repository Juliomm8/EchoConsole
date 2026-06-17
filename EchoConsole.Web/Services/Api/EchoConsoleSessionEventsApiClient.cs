using System.Globalization;
using System.Net.Http.Json;
using EchoConsole.Web.Models.Api.SessionEvents;
using Microsoft.AspNetCore.WebUtilities;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleSessionEventsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleSessionEventsApiClient> _logger;

    public EchoConsoleSessionEventsApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleSessionEventsApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("EchoConsoleApiAdmin");
        _logger = logger;
    }

    public async Task<AdminSessionEventsPageApiModel?> GetRecentEventsAsync(
        string? eventType,
        string? buildVersion,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var queryParameters = new Dictionary<string, string?>
        {
            ["eventType"] = NormalizeFilter(eventType),
            ["buildVersion"] = NormalizeFilter(buildVersion),
            ["fromUtc"] = fromUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["toUtcExclusive"] = toUtcExclusive?.ToString("O", CultureInfo.InvariantCulture),
            ["page"] = Math.Max(1, page).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(pageSize, 1, 100)
                .ToString(CultureInfo.InvariantCulture)
        };

        var requestUri = QueryHelpers.AddQueryString(
            "/api/admin/session-events",
            queryParameters);

        try
        {
            using var response = await _httpClient.GetAsync(
                requestUri,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Admin session events request failed. StatusCode={StatusCode}.",
                    response.StatusCode);

                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<AdminSessionEventsPageApiModel>(
                    cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve recent admin session events.");

            return null;
        }
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}