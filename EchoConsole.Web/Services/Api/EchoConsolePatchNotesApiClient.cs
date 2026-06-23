using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EchoConsole.Web.Services.Api;

public sealed class EchoConsolePatchNotesApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EchoConsolePatchNotesApiClient> _logger;

    public EchoConsolePatchNotesApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<EchoConsolePatchNotesApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PatchNoteApiDto>> GetPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("EchoConsoleApiPublic");

            var patchNotes = await client.GetFromJsonAsync<List<PatchNoteApiDto>>(
                "/api/patchnotes",
                cancellationToken);

            return patchNotes is null
                ? Array.Empty<PatchNoteApiDto>()
                : patchNotes;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve published patch notes from EchoConsole.Api.");

            return Array.Empty<PatchNoteApiDto>();
        }
    }

    public async Task<CreatePatchNoteApiResult> CreateAsync(
        CreatePatchNoteApiRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("EchoConsoleApiAdmin");

            using var response = await client.PostAsJsonAsync(
                "/api/patchnotes",
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var patchNote = await response.Content
                    .ReadFromJsonAsync<PatchNoteApiDto>(
                        cancellationToken: cancellationToken);

                return CreatePatchNoteApiResult.Success(patchNote);
            }

            var errorMessage = await ReadErrorMessageAsync(
                response,
                cancellationToken);

            _logger.LogWarning(
                "Create patch note request failed. StatusCode: {StatusCode}. Error: {ErrorMessage}",
                response.StatusCode,
                errorMessage);

            return CreatePatchNoteApiResult.Failure(
                errorMessage,
                response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while creating a patch note.");

            return CreatePatchNoteApiResult.Failure(
                "The patch note service is currently unavailable.");
        }
    }

    private static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return $"The API returned status {(int)response.StatusCode}.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty(
                    "message",
                    out var messageElement))
            {
                var message = messageElement.GetString();

                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }
            }

            if (document.RootElement.TryGetProperty(
                    "title",
                    out var titleElement))
            {
                var title = titleElement.GetString();

                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }
        }
        catch (JsonException)
        {
        }

        return body.Length <= 500
            ? body
            : body[..500];
    }
}

public sealed class PatchNoteApiDto
{
    public int Id { get; set; }

    public string Version { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Tone { get; set; } = "green";

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool IsPublished { get; set; }
}

public sealed class CreatePatchNoteApiRequest
{
    public string Version { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Tone { get; set; } = "green";

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;
}

public sealed class CreatePatchNoteApiResult
{
    private CreatePatchNoteApiResult(
        bool succeeded,
        PatchNoteApiDto? patchNote,
        string? errorMessage,
        HttpStatusCode? statusCode)
    {
        Succeeded = succeeded;
        PatchNote = patchNote;
        ErrorMessage = errorMessage;
        StatusCode = statusCode;
    }

    public bool Succeeded { get; }

    public PatchNoteApiDto? PatchNote { get; }

    public string? ErrorMessage { get; }

    public HttpStatusCode? StatusCode { get; }

    public static CreatePatchNoteApiResult Success(
        PatchNoteApiDto? patchNote)
    {
        return new CreatePatchNoteApiResult(
            true,
            patchNote,
            null,
            null);
    }

    public static CreatePatchNoteApiResult Failure(
        string errorMessage,
        HttpStatusCode? statusCode = null)
    {
        return new CreatePatchNoteApiResult(
            false,
            null,
            errorMessage,
            statusCode);
    }
}
