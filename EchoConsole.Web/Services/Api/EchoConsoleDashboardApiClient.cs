using System.Net.Http.Json;
using System.Text.Json;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleDashboardApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleDashboardApiClient> _logger;

    public EchoConsoleDashboardApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleDashboardApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(EchoConsoleApiClientNames.Admin);
        _logger = logger;
    }

    public async Task<DashboardOverviewApiDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/admin/dashboard/overview", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Overview request failed. StatusCode: {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    body);

                return new DashboardOverviewApiDto();
            }

            var data = await response.Content.ReadFromJsonAsync<DashboardOverviewApiDto>(cancellationToken: cancellationToken);
            return data ?? new DashboardOverviewApiDto();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error while reading dashboard overview.");
            return new DashboardOverviewApiDto();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reading dashboard overview.");
            return new DashboardOverviewApiDto();
        }
    }

    public async Task<IReadOnlyList<LiveSessionApiDto>> GetLiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/admin/dashboard/live-sessions", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Live sessions request failed. StatusCode: {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    body);

                return new List<LiveSessionApiDto>();
            }

            var data = await response.Content.ReadFromJsonAsync<List<LiveSessionApiDto>>(cancellationToken: cancellationToken);
            return data ?? new List<LiveSessionApiDto>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error while reading live sessions.");
            return new List<LiveSessionApiDto>();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reading live sessions.");
            return new List<LiveSessionApiDto>();
        }
    }
}

public sealed class DashboardOverviewApiDto
{
    public int ActiveSessions { get; set; }

    public int RegisteredInstallations { get; set; }

    public DateTime ServerTimeUtc { get; set; }
}

public sealed class LiveSessionApiDto
{
    public Guid SessionId { get; set; }

    public Guid InstallationId { get; set; }

    public string BuildVersion { get; set; } = string.Empty;

    public string CurrentScene { get; set; } = string.Empty;

    public string CurrentGameState { get; set; } = string.Empty;

    public string? CurrentPhase { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime LastHeartbeatUtc { get; set; }

    public int Status { get; set; }
}