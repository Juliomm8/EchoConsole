namespace EchoConsole.Web.Models.Profile;

public sealed class ProfileStatisticsSnapshotViewModel
{
    public int LinkedNodeCount { get; set; }

    public int TotalSessions { get; set; }

    public long TotalPlayTimeMinutes { get; set; }

    public int LongestSessionMinutes { get; set; }

    public string FavoriteBuild { get; set; } = "N/A";

    public DateTimeOffset? LastActivityUtc { get; set; }

    public IReadOnlyList<ProfileDailyActivityPointViewModel>
        ActivityLastSevenDays { get; set; } =
            Array.Empty<ProfileDailyActivityPointViewModel>();

    public IReadOnlyList<ProfileRecentEventViewModel>
        RecentEvents { get; set; } =
            Array.Empty<ProfileRecentEventViewModel>();
}
