using EchoConsole.Api.Configuration;
using EchoConsole.Api.Contracts.Admin.Simulation;
using EchoConsole.Api.Security;
using EchoConsole.Api.Services.Simulation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EchoConsole.Api.Controllers.Admin;

[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
[ApiController]
[Route("api/admin/simulation")]
public sealed class SimulationAdminController : ControllerBase
{
    private const string PurgeConfirmation =
        "PURGE_PC_PLAYER_DATA";

    private const string WipeConfirmation =
        "WIPE_ALL_TELEMETRY";

    private readonly ISimulationOrchestratorService _service;
    private readonly SimulationOrchestratorOptions _options;

    public SimulationAdminController(
        ISimulationOrchestratorService service,
        IOptions<SimulationOrchestratorOptions> options)
    {
        _service = service;
        _options = options.Value;
    }

    [HttpGet("status")]
    public async Task<ActionResult<SimulationStatusDto>> GetStatus(
        CancellationToken cancellationToken)
    {
        var disabledResult = EnsureEnabled();

        if (disabledResult is not null)
        {
            return disabledResult;
        }

        return Ok(
            await _service.GetStatusAsync(
                cancellationToken));
    }

    [HttpPost("reconcile")]
    public async Task<ActionResult<SimulationCommandResponse>> Reconcile(
        [FromBody] SimulationTargetRequest request,
        CancellationToken cancellationToken)
    {
        var disabledResult = EnsureEnabled();

        if (disabledResult is not null)
        {
            return disabledResult;
        }

        if (request.TargetActiveSessions >
            _options.MaxTargetSessions)
        {
            return BadRequest(new
            {
                message =
                    $"The target cannot exceed {_options.MaxTargetSessions} simulated sessions."
            });
        }

        return Ok(
            await _service.ReconcileAsync(
                request,
                cancellationToken));
    }

    [HttpPost("pulse")]
    public async Task<ActionResult<SimulationCommandResponse>> Pulse(
        [FromBody] SimulationTargetRequest request,
        CancellationToken cancellationToken)
    {
        var disabledResult = EnsureEnabled();

        if (disabledResult is not null)
        {
            return disabledResult;
        }

        if (request.TargetActiveSessions >
            _options.MaxTargetSessions)
        {
            return BadRequest(new
            {
                message =
                    $"The target cannot exceed {_options.MaxTargetSessions} simulated sessions."
            });
        }

        return Ok(
            await _service.PulseAsync(
                request,
                cancellationToken));
    }

    [HttpPost("alerts/critical")]
    public async Task<ActionResult<SimulationCommandResponse>>
        InjectCriticalAlert(
            [FromBody] SimulationCommandRequest request,
            CancellationToken cancellationToken)
    {
        var disabledResult = EnsureEnabled();

        if (disabledResult is not null)
        {
            return disabledResult;
        }

        return Ok(
            await _service.InjectCriticalAlertAsync(
                request,
                cancellationToken));
    }

    [HttpPost("mass-drop")]
    public async Task<ActionResult<SimulationCommandResponse>> MassDrop(
        [FromBody] SimulationCommandRequest request,
        CancellationToken cancellationToken)
    {
        var disabledResult = EnsureEnabled();

        if (disabledResult is not null)
        {
            return disabledResult;
        }

        return Ok(
            await _service.MassDropAsync(
                request,
                cancellationToken));
    }

    [HttpDelete("simulated-data")]
    public async Task<ActionResult<SimulationMaintenanceResponse>>
        PurgeSimulatedData(
            [FromBody] SimulationConfirmationRequest request,
            CancellationToken cancellationToken)
    {
        var disabledResult = EnsureEnabled();

        if (disabledResult is not null)
        {
            return disabledResult;
        }

        if (!string.Equals(
                request.Confirmation,
                PurgeConfirmation,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message =
                    $"Confirmation must exactly match '{PurgeConfirmation}'."
            });
        }

        return Ok(
            await _service.PurgeSimulatedDataAsync(
                cancellationToken));
    }

    [HttpDelete("telemetry")]
    public async Task<ActionResult<SimulationMaintenanceResponse>>
        WipeTelemetry(
            [FromBody] SimulationConfirmationRequest request,
            CancellationToken cancellationToken)
    {
        var disabledResult = EnsureEnabled();

        if (disabledResult is not null)
        {
            return disabledResult;
        }

        if (!string.Equals(
                request.Confirmation,
                WipeConfirmation,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message =
                    $"Confirmation must exactly match '{WipeConfirmation}'."
            });
        }

        return Ok(
            await _service.WipeTelemetryAsync(
                cancellationToken));
    }

    private ObjectResult? EnsureEnabled()
    {
        if (_options.Enabled)
        {
            return null;
        }

        return Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Simulation orchestrator disabled",
            detail:
                "Enable SimulationOrchestrator:Enabled only in an authorized development or test environment.");
    }
}
