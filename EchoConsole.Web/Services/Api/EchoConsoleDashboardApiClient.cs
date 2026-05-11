using System.Net.Http.Json;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleDashboardApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleDashboardApiClient> _logger;

    public EchoConsoleDashboardApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleDashboardApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("EchoConsoleApi");
        _logger = logger;
    }

    public async Task<DashboardOverviewApiDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/api/admin/dashboard/overview", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "EchoConsole API overview request failed. StatusCode: {StatusCode}. Body: {Body}",
                response.StatusCode,
                body);

            response.EnsureSuccessStatusCode();
        }

        var data = await response.Content.ReadFromJsonAsync<DashboardOverviewApiDto>(cancellationToken: cancellationToken);

        return data ?? new DashboardOverviewApiDto();
    }

    public async Task<IReadOnlyList<LiveSessionApiDto>> GetLiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/api/admin/dashboard/live-sessions", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "EchoConsole API live-sessions request failed. StatusCode: {StatusCode}. Body: {Body}",
                response.StatusCode,
                body);

            response.EnsureSuccessStatusCode();
        }

        var data = await response.Content.ReadFromJsonAsync<List<LiveSessionApiDto>>(cancellationToken: cancellationToken);

        return data ?? new List<LiveSessionApiDto>();
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

    public object Status { get; set; } = string.Empty;
}