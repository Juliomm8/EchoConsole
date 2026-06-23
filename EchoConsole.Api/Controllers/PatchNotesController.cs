using EchoConsole.Api.Contracts.PatchNotes;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence.Cms;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers;

[ApiController]
[Route("api/patchnotes")]
public sealed class PatchNotesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PatchNotesController> _logger;

    public PatchNotesController(
        ApplicationDbContext dbContext,
        ILogger<PatchNotesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PatchNoteDto>>> GetPublished(
        CancellationToken cancellationToken = default)
    {
        var patchNotes = await BuildPatchNotesQuery()
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return Ok(patchNotes);
    }

    [Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
    [HttpGet("admin")]
    public async Task<ActionResult<IReadOnlyList<PatchNoteDto>>> GetAll(
        CancellationToken cancellationToken = default)
    {
        var patchNotes = await BuildPatchNotesQuery()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return Ok(patchNotes);
    }

    [Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
    [HttpPost]
    public async Task<ActionResult<PatchNoteDto>> Create(
        [FromBody] CreatePatchNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedVersion = request.Version.Trim();
        var normalizedCategory = request.Category.Trim().ToUpperInvariant();
        var normalizedTone = request.Tone.Trim().ToLowerInvariant();
        var normalizedTitle = request.Title.Trim();
        var normalizedDescription = request.Description.Trim();

        if (string.IsNullOrWhiteSpace(normalizedVersion))
        {
            ModelState.AddModelError(
                nameof(request.Version),
                "Version is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedCategory))
        {
            ModelState.AddModelError(
                nameof(request.Category),
                "Category is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            ModelState.AddModelError(
                nameof(request.Title),
                "Title is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            ModelState.AddModelError(
                nameof(request.Description),
                "Description is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var versionExists = await _dbContext.PatchNotes
            .AsNoTracking()
            .AnyAsync(
                x => x.Version == normalizedVersion,
                cancellationToken);

        if (versionExists)
        {
            return Conflict(new
            {
                message = $"A patch note with version '{normalizedVersion}' already exists."
            });
        }

        var patchNote = new PatchNote
        {
            Version = normalizedVersion,
            Category = normalizedCategory,
            Tone = normalizedTone,
            Title = normalizedTitle,
            Description = normalizedDescription,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsPublished = request.IsPublished
        };

        _dbContext.PatchNotes.Add(patchNote);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create patch note version {Version}.",
                normalizedVersion);

            return Conflict(new
            {
                message = $"A patch note with version '{normalizedVersion}' could not be created."
            });
        }

        var response = MapPatchNote(patchNote);

        _logger.LogInformation(
            "Patch note created. Id: {PatchNoteId}, Version: {Version}, IsPublished: {IsPublished}",
            patchNote.Id,
            patchNote.Version,
            patchNote.IsPublished);

        return CreatedAtAction(
            nameof(GetPublished),
            null,
            response);
    }

    [Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken = default)
    {
        var patchNote = await _dbContext.PatchNotes
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (patchNote is null)
        {
            return NotFound(new
            {
                message = $"Patch note with id '{id}' was not found."
            });
        }

        _dbContext.PatchNotes.Remove(patchNote);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Patch note deleted. Id: {PatchNoteId}, Version: {Version}",
            patchNote.Id,
            patchNote.Version);

        return NoContent();
    }

    private IQueryable<PatchNoteDto> BuildPatchNotesQuery()
    {
        return _dbContext.PatchNotes
            .AsNoTracking()
            .Select(x => new PatchNoteDto
            {
                Id = x.Id,
                Version = x.Version,
                Category = x.Category,
                Tone = x.Tone,
                Title = x.Title,
                Description = x.Description,
                CreatedAtUtc = x.CreatedAtUtc,
                IsPublished = x.IsPublished
            });
    }

    private static PatchNoteDto MapPatchNote(
        PatchNote patchNote)
    {
        return new PatchNoteDto
        {
            Id = patchNote.Id,
            Version = patchNote.Version,
            Category = patchNote.Category,
            Tone = patchNote.Tone,
            Title = patchNote.Title,
            Description = patchNote.Description,
            CreatedAtUtc = patchNote.CreatedAtUtc,
            IsPublished = patchNote.IsPublished
        };
    }
}
