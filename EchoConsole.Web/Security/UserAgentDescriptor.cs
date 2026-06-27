namespace EchoConsole.Web.Security;

public sealed record UserAgentDescriptor(
    string Browser,
    string OperatingSystem)
{
    public string DeviceLabel => $"{Browser} on {OperatingSystem}";
}
