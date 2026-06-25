using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleBuildsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleBuildsApiClient> _logger;

    public EchoConsoleBuildsApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleBuildsApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(EchoConsoleApiClientNames.Admin);
        _logger = logger;
    }

    public async Task<PagedResponse<GameBuildApiDto>> GetBuildsAsync(
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new Dictionary<string, string?>
            {
                ["pageNumber"] = pageNumber.ToString(),
                ["pageSize"] = pageSize.ToString()
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query["searchTerm"] = searchTerm.Trim();
            }

            var url = QueryHelpers.AddQueryString("/api/admin/builds", query);

            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    response,
                    "Builds request failed.",
                    cancellationToken);

                return new PagedResponse<GameBuildApiDto>();
            }

            var data = await response.Content.ReadFromJsonAsync<PagedResponse<GameBuildApiDto>>(
                cancellationToken: cancellationToken);

            return data ?? new PagedResponse<GameBuildApiDto>();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reading builds from EchoConsole.Api.");
            return new PagedResponse<GameBuildApiDto>();
        }
    }

    public async Task<GameBuildSummaryApiDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                "/api/admin/builds/summary",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    response,
                    "Build summary request failed.",
                    cancellationToken);

                return new GameBuildSummaryApiDto();
            }

            return await response.Content.ReadFromJsonAsync<GameBuildSummaryApiDto>(
                cancellationToken: cancellationToken)
                ?? new GameBuildSummaryApiDto();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reading the game build summary.");
            return new GameBuildSummaryApiDto();
        }
    }

    public async Task<GameBuildApiDto?> CreateBuildAsync(
        CreateGameBuildApiRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/api/admin/builds",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    response,
                    "Create build request failed.",
                    cancellationToken);

                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameBuildApiDto>(
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating a game build.");
            return null;
        }
    }

    public async Task<GameBuildApiDto?> ToggleBuildActiveAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/admin/builds/{id}/toggle-active");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    response,
                    $"Toggle build request failed. BuildId: {id}.",
                    cancellationToken);

                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameBuildApiDto>(
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
                "Unexpected error while toggling build active status. BuildId: {BuildId}",
                id);

            return null;
        }
    }

    public async Task<bool> SetBuildActiveAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/admin/builds/{id}/active")
            {
                Content = JsonContent.Create(new SetGameBuildActiveApiRequest
                {
                    IsActive = isActive
                })
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            await LogFailureAsync(
                response,
                $"Set build active request failed. BuildId: {id}.",
                cancellationToken);

            return false;
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
                "Unexpected error while updating build active status. BuildId: {BuildId}",
                id);

            return false;
        }
    }

    public async Task<GameBuildApiDto?> UpdateBuildAsync(
        int id,
        UpdateGameBuildApiRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PutAsJsonAsync(
                $"/api/admin/builds/{id}",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(
                    response,
                    $"Update build request failed. BuildId: {id}.",
                    cancellationToken);

                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameBuildApiDto>(
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
                "Unexpected error while updating a game build. BuildId: {BuildId}",
                id);

            return null;
        }
    }

    public async Task<DeleteGameBuildApiResult> DeleteBuildAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync(
                $"/api/admin/builds/{id}",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new DeleteGameBuildApiResult
                {
                    Status = DeleteGameBuildApiStatus.Deleted
                };
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new DeleteGameBuildApiResult
                {
                    Status = DeleteGameBuildApiStatus.NotFound
                };
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var error = await response.Content.ReadFromJsonAsync<BuildOperationApiError>(
                    cancellationToken: cancellationToken);

                return new DeleteGameBuildApiResult
                {
                    Status = error?.Code switch
                    {
                        "active_build_cannot_be_deleted" => DeleteGameBuildApiStatus.ActiveBuild,
                        "build_has_linked_telemetry" => DeleteGameBuildApiStatus.HasLinkedTelemetry,
                        _ => DeleteGameBuildApiStatus.Failed
                    },
                    LinkedInstallations = error?.LinkedInstallations ?? 0,
                    TotalSessions = error?.TotalSessions ?? 0
                };
            }

            await LogFailureAsync(
                response,
                $"Delete build request failed. BuildId: {id}.",
                cancellationToken);

            return new DeleteGameBuildApiResult
            {
                Status = DeleteGameBuildApiStatus.Failed
            };
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
                "Unexpected error while deleting a game build. BuildId: {BuildId}",
                id);

            return new DeleteGameBuildApiResult
            {
                Status = DeleteGameBuildApiStatus.Failed
            };
        }
    }

    private async Task LogFailureAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogWarning(
            "{Operation} StatusCode: {StatusCode}. Body: {Body}",
            operation,
            response.StatusCode,
            body);
    }
}

public sealed class GameBuildApiDto
{
    public int Id { get; set; }

    public string VersionNumber { get; set; } = string.Empty;

    public string? ReleaseNotes { get; set; }

    public DateTimeOffset ReleaseDateUtc { get; set; }

    public bool IsActive { get; set; }

    public string EngineVersion { get; set; } = string.Empty;

    public int LinkedInstallations { get; set; }

    public int TotalSessions { get; set; }
}

public sealed class GameBuildSummaryApiDto
{
    public int TotalBuilds { get; set; }

    public string ActiveVersion { get; set; } = string.Empty;

    public string BaseEngineVersion { get; set; } = string.Empty;
}

public sealed class CreateGameBuildApiRequest
{
    public string VersionNumber { get; set; } = string.Empty;

    public string? ReleaseNotes { get; set; }

    public DateTimeOffset ReleaseDateUtc { get; set; }

    public bool IsActive { get; set; }

    public string EngineVersion { get; set; } = string.Empty;
}

public sealed class UpdateGameBuildApiRequest
{
    public string VersionNumber { get; set; } = string.Empty;

    public string? ReleaseNotes { get; set; }

    public DateTimeOffset ReleaseDateUtc { get; set; }

    public string EngineVersion { get; set; } = string.Empty;
}

public sealed class SetGameBuildActiveApiRequest
{
    public bool IsActive { get; set; }
}

public sealed class BuildOperationApiError
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public int LinkedInstallations { get; set; }

    public int TotalSessions { get; set; }
}

public sealed class DeleteGameBuildApiResult
{
    public DeleteGameBuildApiStatus Status { get; set; }

    public int LinkedInstallations { get; set; }

    public int TotalSessions { get; set; }
}

public enum DeleteGameBuildApiStatus
{
    Failed = 0,
    Deleted = 1,
    NotFound = 2,
    ActiveBuild = 3,
    HasLinkedTelemetry = 4
}

public sealed class DPagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
