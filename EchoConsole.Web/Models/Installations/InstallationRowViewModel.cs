namespace EchoConsole.Web.Models.Installations;

public sealed class InstallationRowViewModel
{
    public string InstallationId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string OSVersion { get; set; } = string.Empty;

    public string Processor { get; set; } = string.Empty;

    public string Gpu { get; set; } = string.Empty;

    public string RamLabel { get; set; } = string.Empty;

    public string LastUpdateUtcLabel { get; set; } = string.Empty;
}