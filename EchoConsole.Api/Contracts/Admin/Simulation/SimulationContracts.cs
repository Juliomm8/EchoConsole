using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EchoConsole.Api.Contracts.Admin.Simulation;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SimulationVolatility
{
    Low = 1,
    Medium = 2,
    Chaotic = 3
}

public sealed class SimulationModulesRequest
{
    public bool Sessions { get; set; } = true;

    public bool Installations { get; set; } = true;

    public bool Alerts { get; set; } = true;

    public bool Events { get; set; } = true;
}

public sealed class SimulationTargetRequest
{
    [Range(0, 250)]
    public int TargetActiveSessions { get; set; }

    public bool SimulateEvents { get; set; }

    public bool EnableStochasticFlow { get; set; } = true;

    public SimulationVolatility Volatility { get; set; } =
        SimulationVolatility.Medium;

    [Required]
    public SimulationModulesRequest Modules { get; set; } = new();
}

public sealed class SimulationCommandRequest
{
    public bool SimulateEvents { get; set; }

    [Required]
    public SimulationModulesRequest Modules { get; set; } = new();
}

public sealed class SimulationConfirmationRequest
{
    [Required]
    [StringLength(64)]
    public string Confirmation { get; set; } = string.Empty;
}

public sealed class SimulationStatusDto
{
    public int ActiveRealSessions { get; set; }

    public int ActiveSimulatedSessions { get; set; }

    public int SimulatedInstallations { get; set; }

    public int OpenSimulationAlerts { get; set; }

    public bool IsRunning { get; set; }

    public int TargetActiveSessions { get; set; }

    public string Phase { get; set; } = "Idle";

    public SimulationVolatility Volatility { get; set; } =
        SimulationVolatility.Medium;

    public bool StochasticFlowEnabled { get; set; }

    public DateTimeOffset? NextChurnAtUtc { get; set; }

    public DateTimeOffset ServerTimeUtc { get; set; }
}

public sealed class SimulationCommandResponse
{
    public string Message { get; set; } = string.Empty;

    public SimulationStatusDto Status { get; set; } = new();
}

public sealed class SimulationMaintenanceResponse
{
    public string Message { get; set; } = string.Empty;

    public int DeletedSessionEvents { get; set; }

    public int DeletedSessions { get; set; }

    public int DeletedAlerts { get; set; }

    public int DeletedInstallations { get; set; }

    public SimulationStatusDto Status { get; set; } = new();
}
