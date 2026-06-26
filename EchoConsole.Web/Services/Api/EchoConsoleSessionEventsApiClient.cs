using EchoConsole.Web.Models.Api.SessionEvents;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleSessionEventsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleSessionEventsApiClient> _logger;

    public EchoConsoleSessionEventsApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleSessionEventsApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(EchoConsoleApiClientNames.Admin);
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
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve recent admin session events.");

            return null;
        }
    }

    public async Task<PurgeSessionApiModel?> PurgeSessionAsync(
        Guid sessionId,
        string? eventType,
        string? buildVersion,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        var queryParameters = new Dictionary<string, string?>
        {
            ["eventType"] = NormalizeFilter(eventType),
            ["buildVersion"] = NormalizeFilter(buildVersion),
            ["fromUtc"] = fromUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["toUtcExclusive"] = toUtcExclusive?.ToString("O", CultureInfo.InvariantCulture)
        };

        var requestUri = QueryHelpers.AddQueryString(
            $"/api/admin/session-events/sessions/{sessionId}",
            queryParameters);

        try
        {
            using var response = await _httpClient.DeleteAsync(
                requestUri,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "Admin session purge target was not found. SessionId={SessionId}.",
                    sessionId);

                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                _logger.LogWarning(
                    "Admin session purge failed. SessionId={SessionId}, StatusCode={StatusCode}, Body={Body}.",
                    sessionId,
                    response.StatusCode,
                    body);

                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<PurgeSessionApiModel>(
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
                "Failed to purge admin session. SessionId={SessionId}.",
                sessionId);

            return null;
        }
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    public async Task<AdminSessionTimelineDetailApiModel?> GetSessionTimelineAsync(
    Guid sessionId,
    CancellationToken cancellationToken = default)
    {
        var requestUri =
            $"/api/admin/session-events/sessions/{sessionId}/timeline";

        try
        {
            using var response = await _httpClient.GetAsync(
                requestUri,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "Admin session timeline was not found. SessionId={SessionId}.",
                    sessionId);

                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                _logger.LogWarning(
                    "Admin session timeline request failed. SessionId={SessionId}, StatusCode={StatusCode}, Body={Body}.",
                    sessionId,
                    response.StatusCode,
                    body);

                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<AdminSessionTimelineDetailApiModel>(
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
                "Failed to retrieve admin session timeline. SessionId={SessionId}.",
                sessionId);

            return null;
        }
    }
}