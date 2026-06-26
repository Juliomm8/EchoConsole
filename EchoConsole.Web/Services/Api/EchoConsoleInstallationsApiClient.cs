using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsoleInstallationsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EchoConsoleInstallationsApiClient> _logger;

    public EchoConsoleInstallationsApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsoleInstallationsApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(
            EchoConsoleApiClientNames.Admin);

        _logger = logger;
    }

    public async Task<PagedInstallationsApiResponse> GetInstallationsAsync(
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new Dictionary<string, string?>
            {
                ["page"] = pageNumber.ToString(),
                ["pageSize"] = pageSize.ToString()
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query["search"] = searchTerm.Trim();
            }

            var url = QueryHelpers.AddQueryString(
                "/api/admin/installations",
                query);

            using var response = await _httpClient.GetAsync(
                url,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                _logger.LogWarning(
                    "Installations request failed. StatusCode={StatusCode}, Body={Body}",
                    response.StatusCode,
                    body);

                return new PagedInstallationsApiResponse();
            }

            return await response.Content
                .ReadFromJsonAsync<PagedInstallationsApiResponse>(
                    cancellationToken: cancellationToken)
                ?? new PagedInstallationsApiResponse();
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
                "Unexpected error while reading installations.");

            return new PagedInstallationsApiResponse();
        }
    }

    public async Task<bool> UpdateMetadataAsync(
        Guid installationId,
        UpdateInstallationMetadataApiRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/admin/installations/{installationId:D}/metadata")
            {
                Content = JsonContent.Create(request)
            };

            using var response = await _httpClient.SendAsync(
                message,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            _logger.LogWarning(
                "Installation metadata update failed. InstallationId={InstallationId}, StatusCode={StatusCode}, Body={Body}",
                installationId,
                response.StatusCode,
                body);

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
                "Unexpected error while updating installation metadata. InstallationId={InstallationId}",
                installationId);

            return false;
        }
    }

    public async Task<bool> DeleteAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync(
                $"/api/admin/installations/{installationId:D}",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            _logger.LogWarning(
                "Installation delete failed. InstallationId={InstallationId}, StatusCode={StatusCode}, Body={Body}",
                installationId,
                response.StatusCode,
                body);

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
                "Unexpected error while deleting installation. InstallationId={InstallationId}",
                installationId);

            return false;
        }
    }
}

public sealed class PagedInstallationsApiResponse
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public IReadOnlyList<InstallationListItemApiDto> Items { get; set; } =
        Array.Empty<InstallationListItemApiDto>();
}

public sealed class InstallationListItemApiDto
{
    public int DatabaseId { get; set; }

    public Guid InstallationId { get; set; }

    public string GameCode { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string OSVersion { get; set; } = string.Empty;

    public string? Processor { get; set; }

    public string? Gpu { get; set; }

    public int? RamMb { get; set; }

    public string TelemetryStatus { get; set; } = string.Empty;

    public string? AdminAlias { get; set; }

    public string AdminStatus { get; set; } = string.Empty;

    public int? OwnerUserId { get; set; }

    public string? OwnerAlias { get; set; }

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastUpdateUtc { get; set; }
}

public sealed class UpdateInstallationMetadataApiRequest
{
    public string? AdminAlias { get; set; }

    public string AdminStatus { get; set; } = "Active";
}
