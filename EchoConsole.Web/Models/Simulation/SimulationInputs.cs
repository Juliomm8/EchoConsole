using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Simulation;

public sealed class SimulationModulesInput
{
    public bool Sessions { get; set; } = true;

    public bool Installations { get; set; } = true;

    public bool Alerts { get; set; } = true;
}

public sealed class SimulationTargetInput
{
    [Range(0, 250)]
    public int TargetActiveSessions { get; set; }

    [Required]
    public SimulationModulesInput Modules { get; set; } = new();
}

public sealed class SimulationCommandInput
{
    [Required]
    public SimulationModulesInput Modules { get; set; } = new();
}

public sealed class SimulationConfirmationInput
{
    [Required]
    [StringLength(64)]
    public string Confirmation { get; set; } = string.Empty;
}
