namespace EchoConsole.Api.Seed;

public sealed class DemoSeedOptions
{
    public const string SectionName = "DemoSeed";

    public bool Enabled { get; set; }

    public bool ResetBeforeSeed { get; set; }
}