using EchoConsole.Api.Security;
using EchoConsole.Api.Services.LiveOperations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/live-operations")]
[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
public sealed class LiveOperationsAdminController : ControllerBase
{
    private readonly ILiveOperationsService _liveOperationsService;

    public LiveOperationsAdminController(
        ILiveOperationsService liveOperationsService)
    {
        _liveOperationsService = liveOperationsService;
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot(
        CancellationToken cancellationToken)
    {
        var snapshot = await _liveOperationsService.GetSnapshotAsync(
            cancellationToken);

        return Ok(snapshot);
    }
}