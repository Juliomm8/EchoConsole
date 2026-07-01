namespace EchoConsole.Web.Configuration;

public sealed class GitHubReleaseOptions
{
    public const string SectionName = "GitHubRelease";

    public string Owner { get; set; } = string.Empty;

    public string Repository { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string AssetName { get; set; } = string.Empty;

    public long MaximumAssetSizeBytes { get; set; } =
        256L * 1024L * 1024L;

    public int RequestTimeoutSeconds { get; set; } = 30;
}
