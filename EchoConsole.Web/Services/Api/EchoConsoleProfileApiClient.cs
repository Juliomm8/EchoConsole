using System.Net;
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
            _logger.LogError(ex, "Failed to retrieve profile for user {UserId}.", userId);
            return null;
        }
    }

    public async Task<ClaimInstallationResponseModel?> ClaimInstallationAsync(
        ClaimInstallationRequestModel request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/profile/installations/claim",
                request,
                cancellationToken);

            return await ReadProfileOperationResponseAsync<ClaimInstallationResponseModel>(
                response,
                "claim installation",
                request.InstallationId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to claim installation {InstallationId} for user {UserId}.",
                request.InstallationId,
                request.UserId);

            return new ClaimInstallationResponseModel
            {
                Success = false,
                Message = "The platform could not process the claim request.",
                InstallationId = request.InstallationId,
                OwnerUserId = null
            };
        }
    }

    public async Task<UnlinkInstallationResponseModel?> UnlinkInstallationAsync(
        UnlinkInstallationRequestModel request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/profile/installations/unlink",
                request,
                cancellationToken);

            return await ReadProfileOperationResponseAsync<UnlinkInstallationResponseModel>(
                response,
                "unlink installation",
                request.InstallationId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to unlink installation {InstallationId} for user {UserId}.",
                request.InstallationId,
                request.UserId);

            return new UnlinkInstallationResponseModel
            {
                Success = false,
                Message = "The platform could not process the unlink request.",
                InstallationId = request.InstallationId,
                PreviousOwnerUserId = null
            };
        }
    }

    private async Task<T?> ReadProfileOperationResponseAsync<T>(
        HttpResponseMessage response,
        string operationName,
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(
            cancellationToken: cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return payload;
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(
                "Profile operation {OperationName} was forbidden for Installation {InstallationId}.",
                operationName,
                installationId);

            return payload;
        }

        _logger.LogWarning(
            "Profile operation {OperationName} failed for Installation {InstallationId}. StatusCode={StatusCode}.",
            operationName,
            installationId,
            response.StatusCode);

        return payload;
    }
}