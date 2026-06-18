using EchoConsole.Api.Contracts.Admin.LiveOperations;

namespace EchoConsole.Api.Services.LiveOperations;

public interface ILiveOperationsService
{
    Task<LiveOperationsSnapshotDto> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}