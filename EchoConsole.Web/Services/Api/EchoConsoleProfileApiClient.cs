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

    public async Task<UserSessionHistoryPageApiModel?> GetSessionHistoryAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50);

        try
        {
            return await _httpClient.GetFromJsonAsync<UserSessionHistoryPageApiModel>(
                $"/api/profile/sessions/{userId}?page={page}&pageSize={pageSize}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve session history for user {UserId}. Page={Page}, PageSize={PageSize}.",
                userId,
                page,
                pageSize);

            return null;
        }
    }

    public async Task<UserSessionDetailApiModel?> GetSessionDetailAsync(
        int userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/profile/sessions/{userId}/{sessionId}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Session detail was not found or not owned by user. UserId={UserId}, SessionId={SessionId}.",
                    userId,
                    sessionId);

                return null;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogWarning(
                    "Session detail request was forbidden. UserId={UserId}, SessionId={SessionId}.",
                    userId,
                    sessionId);

                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Session detail request failed. UserId={UserId}, SessionId={SessionId}, StatusCode={StatusCode}.",
                    userId,
                    sessionId,
                    response.StatusCode);

                return null;
            }

            return await response.Content.ReadFromJsonAsync<UserSessionDetailApiModel>(
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve session detail for user {UserId}. SessionId={SessionId}.",
                userId,
                sessionId);

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

    public async Task<UpdateProfileResponseModel?> UpdateProfileAsync(
    int userId,
    UpdateProfileRequestModel request,
    CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/profile/settings/{userId}",
                request,
                cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<UpdateProfileResponseModel>(
                cancellationToken: cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return payload;
            }

            _logger.LogWarning(
                "Profile update failed for user {UserId}. StatusCode={StatusCode}.",
                userId,
                response.StatusCode);

            return payload ?? new UpdateProfileResponseModel
            {
                Success = false,
                Message = "The platform could not update your profile."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profile for user {UserId}.", userId);

            return new UpdateProfileResponseModel
            {
                Success = false,
                Message = "The platform could not process the profile update request."
            };
        }
    }
}