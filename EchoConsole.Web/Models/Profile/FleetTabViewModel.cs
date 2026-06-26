namespace EchoConsole.Web.Models.Profile;

public sealed class FleetTabViewModel
{
    public int LinkedDeviceCount { get; set; }

    public IReadOnlyList<FleetDeviceViewModel> Devices { get; set; } =
        Array.Empty<FleetDeviceViewModel>();
}
