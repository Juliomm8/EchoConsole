using System.Net.Http.Json;
using EchoConsole.Web.Models.Api;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleHomeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleHomeApiClient> _logger;

    public EchoConsoleHomeApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleHomeApiClient> logger)
    {
        // Usa el cliente público configurado en Program.cs
        _httpClient = httpClientFactory.CreateClient("EchoConsoleApiPublic");
        _logger = logger;
    }

    public async Task<PublicHomeOverviewModel> GetHomeOverviewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await _httpClient.GetFromJsonAsync<PublicHomeOverviewModel>(
                "/api/public/home/overview",
                cancellationToken);

            return data ?? CreateFallbackModel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve public home overview from EchoConsole.Api.");
            return CreateFallbackModel();
        }
    }

    private static PublicHomeOverviewModel CreateFallbackModel()
    {
        return new PublicHomeOverviewModel
        {
            TotalSessions = 0,
            ActivePlayersNow = 0,
            MonitoredBuilds = 0,
            OpenAlerts = 0,
            FeaturedBuildVersion = "N/A"
        };
    }
}