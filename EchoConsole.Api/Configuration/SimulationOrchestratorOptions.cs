namespace EchoConsole.Api.Configuration;

public sealed class SimulationOrchestratorOptions
{
    public const string SectionName = "SimulationOrchestrator";

    public bool Enabled { get; set; }

    public int MaxTargetSessions { get; set; } = 250;
}
