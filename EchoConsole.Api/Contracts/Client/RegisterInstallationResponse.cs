namespace EchoConsole.Api.Contracts.Client;

public sealed class RegisterInstallationResponse
{
    public Guid InstallationId { get; set; }

    public string GameCode { get; set; } = null!;

    public string BuildVersion { get; set; } = null!;

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; }

    public DateTimeOffset ServerTimeUtc { get; set; }
}