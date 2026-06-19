using System.Globalization;
using System.Net.Http.Json;
using EchoConsole.Web.Models.Api.SessionEventAnalytics;
using Microsoft.AspNetCore.WebUtilities;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleSessionEventAnalyticsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleSessionEventAnalyticsApiClient> _logger;

    public EchoConsoleSessionEventAnalyticsApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleSessionEventAnalyticsApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("EchoConsoleApiAdmin");
        _logger = logger;
    }

    public async Task<AdminSessionEventAnalyticsApiModel?> GetAnalyticsAsync(
        string? buildVersion,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        string? trendGranularity,
        CancellationToken cancellationToken = default)
    {
        var queryParameters = new Dictionary<string, string?>
        {
            ["buildVersion"] = NormalizeFilter(buildVersion),
            ["fromUtc"] = fromUtc?.ToString(
                "O",
                CultureInfo.InvariantCulture),
            ["toUtcExclusive"] = toUtcExclusive?.ToString(
                "O",
                CultureInfo.InvariantCulture),
            ["trendGranularity"] = string.Equals(
                trendGranularity,
                "hour",
                StringComparison.OrdinalIgnoreCase)
                    ? "hour"
                    : "day"
        };

        var requestUri = QueryHelpers.AddQueryString(
            "/api/admin/session-event-analytics",
            queryParameters);

        try
        {
            using var response = await _httpClient.GetAsync(
                requestUri,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Session event analytics request failed. StatusCode={StatusCode}.",
                    response.StatusCode);

                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<AdminSessionEventAnalyticsApiModel>(
                    cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve session event analytics.");

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