namespace EchoConsole.Api.Configuration;

public sealed class SessionEventIngestionOptions
{
    public const string SectionName = "SessionEventIngestion";

    public int PermitLimit { get; set; } = 120;

    public int WindowSeconds { get; set; } = 60;

    public int SegmentsPerWindow { get; set; } = 6;

    public void Validate()
    {
        if (PermitLimit < 1)
        {
            throw new InvalidOperationException(
                "SessionEventIngestion:PermitLimit must be greater than zero.");
        }

        if (WindowSeconds < 1)
        {
            throw new InvalidOperationException(
                "SessionEventIngestion:WindowSeconds must be greater than zero.");
        }

        if (SegmentsPerWindow < 1)
        {
            throw new InvalidOperationException(
                "SessionEventIngestion:SegmentsPerWindow must be greater than zero.");
        }

        if (SegmentsPerWindow > WindowSeconds)
        {
            throw new InvalidOperationException(
                "SessionEventIngestion:SegmentsPerWindow cannot exceed WindowSeconds.");
        }
    }
}