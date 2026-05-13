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
        _httpClient = httpClientFactory.CreateClient("EchoConsoleApi");
        _logger = logger;
    }

    public async Task<PagedResponse<UserApiDto>> GetUsersAsync(
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

            var url = QueryHelpers.AddQueryString("/api/admin/users", query);

            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Users request failed. StatusCode: {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    body);

                return new PagedResponse<UserApiDto>();
            }

            var data = await response.Content.ReadFromJsonAsync<PagedResponse<UserApiDto>>(
                cancellationToken: cancellationToken);

            return data ?? new PagedResponse<UserApiDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reading users from EchoConsole.Api.");
            return new PagedResponse<UserApiDto>();
        }
    }
}

public sealed class UserApiDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}