using System.Net.Http.Json;
using System.Text.Json;
using EchoConsole.Web.Models.Api.LiveOperations;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleLiveOperationsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleLiveOperationsApiClient> _logger;

    public EchoConsoleLiveOperationsApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleLiveOperationsApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(
            "EchoConsoleApiAdmin");

        _logger = logger;
    }

    public async Task<LiveOperationsSnapshotApiModel?> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                "/api/admin/live-operations/snapshot",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                _logger.LogWarning(
                    "Live operations snapshot request failed. StatusCode={StatusCode}. Body={Body}.",
                    response.StatusCode,
                    body);

                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<LiveOperationsSnapshotApiModel>(
                    cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize live operations snapshot.");

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve live operations snapshot.");

            return null;
        }
    }
}