using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleUsersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleUsersApiClient> _logger;

    public EchoConsoleUsersApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleUsersApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(
            EchoConsoleApiClientNames.Admin);
        _logger = logger;
    }

    public async Task<PagedUsersApiResponse> GetUsersAsync(
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

            var url = QueryHelpers.AddQueryString(
                "/api/admin/users",
                query);

            using var response = await _httpClient.GetAsync(
                url,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                _logger.LogWarning(
                    "Users request failed. StatusCode={StatusCode}, Body={Body}",
                    response.StatusCode,
                    body);

                return new PagedUsersApiResponse();
            }

            return await response.Content
                .ReadFromJsonAsync<PagedUsersApiResponse>(
                    cancellationToken: cancellationToken)
                ?? new PagedUsersApiResponse();
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
                "Unexpected error while reading users from EchoConsole.Api.");

            return new PagedUsersApiResponse();
        }
    }
}

public sealed class PagedUsersApiResponse
{
    public IReadOnlyList<UserApiDto> Items { get; set; } =
        Array.Empty<UserApiDto>();

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public int AdminCount { get; set; }

    public int ViewerCount { get; set; }
}

public sealed class UserApiDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public int InstallationCount { get; set; }

    public DateTimeOffset? LastTelemetryUtc { get; set; }

    public IReadOnlyList<UserInstallationHardwareApiDto> Installations { get; set; } =
        Array.Empty<UserInstallationHardwareApiDto>();
}

public sealed class UserInstallationHardwareApiDto
{
    public Guid InstallationId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string? Cpu { get; set; }

    public string? Gpu { get; set; }

    public int? RamMb { get; set; }

    public string OSVersion { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string AdminStatus { get; set; } = string.Empty;

    public DateTimeOffset LastUpdateUtc { get; set; }

    public Guid? LastSessionId { get; set; }

    public string? LastSessionStatus { get; set; }

    public int? LastSessionDurationMinutes { get; set; }
}
