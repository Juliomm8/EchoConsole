using System.Net.Http.Json;
using EchoConsole.Web.Models.Api.Profile;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleProfileApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleProfileApiClient> _logger;

    public EchoConsoleProfileApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleProfileApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("EchoConsoleApiAdmin");
        _logger = logger;
    }

    public async Task<UserProfileApiModel?> GetProfileAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserProfileApiModel>(
                $"/api/profile/dashboard/{userId}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve premium profile for user {UserId}.", userId);
            return null;
        }
    }
}