using EchoConsole.Api.Contracts.Admin.Simulation;

namespace EchoConsole.Api.Services.Simulation;

public interface ISimulationOrchestratorService
{
    Task<SimulationStatusDto> GetStatusAsync(
        CancellationToken cancellationToken);

    Task<SimulationCommandResponse> ReconcileAsync(
        SimulationTargetRequest request,
        CancellationToken cancellationToken);

    Task<SimulationCommandResponse> PulseAsync(
        SimulationTargetRequest request,
        CancellationToken cancellationToken);

    Task<SimulationCommandResponse> InjectCriticalAlertAsync(
        SimulationCommandRequest request,
        CancellationToken cancellationToken);

    Task<SimulationCommandResponse> MassDropAsync(
        SimulationCommandRequest request,
        CancellationToken cancellationToken);

    Task<SimulationMaintenanceResponse> PurgeSimulatedDataAsync(
        CancellationToken cancellationToken);

    Task<SimulationMaintenanceResponse> WipeTelemetryAsync(
        CancellationToken cancellationToken);
}
