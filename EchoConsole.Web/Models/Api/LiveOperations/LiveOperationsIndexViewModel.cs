using EchoConsole.Web.Models.Api.LiveOperations;

namespace EchoConsole.Web.Models.LiveOperations;

public sealed class LiveOperationsIndexViewModel
{
    public bool IsAvailable { get; set; }

    public LiveOperationsSnapshotApiModel Snapshot { get; set; } =
        new();
}