using EchoConsole.Api.Contracts.Admin.Simulation;

namespace EchoConsole.Api.Services.Simulation;

public enum SimulationPhase
{
    Idle = 0,
    SoftStart = 1,
    Ramping = 2,
    Stable = 3
}

public sealed record SimulationModuleFlags(
    bool Sessions,
    bool Installations,
    bool Alerts,
    bool Events)
{
    public static SimulationModuleFlags From(
        SimulationModulesRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new SimulationModuleFlags(
            source.Sessions,
            source.Installations,
            source.Alerts,
            source.Events);
    }
}

public sealed record SimulationRuntimeSnapshot(
    long Revision,
    bool IsRunning,
    int TargetActiveSessions,
    bool EnableStochasticFlow,
    SimulationVolatility Volatility,
    SimulationModuleFlags Modules,
    bool SimulateEvents,
    SimulationPhase Phase,
    DateTimeOffset? NextChurnAtUtc);

public sealed record SimulationVolatilityProfile(
    TimeSpan MinimumTickDelay,
    TimeSpan MaximumTickDelay,
    TimeSpan MinimumChurnInterval,
    TimeSpan MaximumChurnInterval,
    double RampStepRatio,
    double OscillationRatio,
    double MinimumChurnRatio,
    double MaximumChurnRatio,
    double AbruptDropProbability)
{
    public TimeSpan NextTickDelay()
    {
        return RandomBetween(
            MinimumTickDelay,
            MaximumTickDelay);
    }

    public TimeSpan NextChurnDelay()
    {
        return RandomBetween(
            MinimumChurnInterval,
            MaximumChurnInterval);
    }

    public int GetRampBatchSize(int target)
    {
        if (target <= 0)
        {
            return 0;
        }

        return Math.Max(
            1,
            (int)Math.Ceiling(target * RampStepRatio));
    }

    public int GetOscillationLimit(int target)
    {
        if (target <= 0)
        {
            return 0;
        }

        return Math.Max(
            1,
            (int)Math.Ceiling(target * OscillationRatio));
    }

    public int GetChurnCount(int population)
    {
        if (population <= 0)
        {
            return 0;
        }

        var ratio =
            MinimumChurnRatio +
            Random.Shared.NextDouble() *
            (MaximumChurnRatio - MinimumChurnRatio);

        return Math.Clamp(
            (int)Math.Ceiling(population * ratio),
            1,
            population);
    }

    private static TimeSpan RandomBetween(
        TimeSpan minimum,
        TimeSpan maximum)
    {
        var minimumMilliseconds =
            Math.Max(1L, (long)minimum.TotalMilliseconds);

        var maximumMilliseconds =
            Math.Max(
                minimumMilliseconds,
                (long)maximum.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(
            Random.Shared.NextInt64(
                minimumMilliseconds,
                maximumMilliseconds + 1));
    }
}

public static class SimulationVolatilityProfiles
{
    public static SimulationVolatilityProfile For(
        SimulationVolatility volatility)
    {
        return volatility switch
        {
            SimulationVolatility.Low => new(
                MinimumTickDelay: TimeSpan.FromSeconds(8),
                MaximumTickDelay: TimeSpan.FromSeconds(12),
                MinimumChurnInterval: TimeSpan.FromSeconds(35),
                MaximumChurnInterval: TimeSpan.FromSeconds(50),
                RampStepRatio: 0.08,
                OscillationRatio: 0.03,
                MinimumChurnRatio: 0.05,
                MaximumChurnRatio: 0.07,
                AbruptDropProbability: 0.20),

            SimulationVolatility.Chaotic => new(
                MinimumTickDelay: TimeSpan.FromSeconds(2),
                MaximumTickDelay: TimeSpan.FromSeconds(5),
                MinimumChurnInterval: TimeSpan.FromSeconds(8),
                MaximumChurnInterval: TimeSpan.FromSeconds(16),
                RampStepRatio: 0.20,
                OscillationRatio: 0.12,
                MinimumChurnRatio: 0.10,
                MaximumChurnRatio: 0.15,
                AbruptDropProbability: 0.70),

            _ => new(
                MinimumTickDelay: TimeSpan.FromSeconds(5),
                MaximumTickDelay: TimeSpan.FromSeconds(8),
                MinimumChurnInterval: TimeSpan.FromSeconds(20),
                MaximumChurnInterval: TimeSpan.FromSeconds(35),
                RampStepRatio: 0.12,
                OscillationRatio: 0.07,
                MinimumChurnRatio: 0.07,
                MaximumChurnRatio: 0.11,
                AbruptDropProbability: 0.45)
        };
    }
}

public sealed class SimulationRuntimeState
{
    private readonly object _syncRoot = new();

    private long _revision;
    private long _ownerRotationCursor;

    private SimulationRuntimeSnapshot _current = new(
        Revision: 0,
        IsRunning: false,
        TargetActiveSessions: 0,
        EnableStochasticFlow: false,
        Volatility: SimulationVolatility.Medium,
        Modules: new SimulationModuleFlags(
            Sessions: true,
            Installations: true,
            Alerts: true,
            Events: true),
        SimulateEvents: true,
        Phase: SimulationPhase.Idle,
        NextChurnAtUtc: null);

    public static SimulationRuntimeState Shared { get; } = new();

    public SimulationRuntimeSnapshot Configure(
        SimulationTargetRequest request,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Modules);

        lock (_syncRoot)
        {
            var target = Math.Max(
                0,
                request.TargetActiveSessions);

            var isRunning =
                target > 0 &&
                request.Modules.Sessions;

            var targetChanged =
                target != _current.TargetActiveSessions;

            var volatilityChanged =
                request.Volatility != _current.Volatility;

            var phase = !isRunning
                ? SimulationPhase.Idle
                : !_current.IsRunning
                    ? SimulationPhase.SoftStart
                    : targetChanged
                        ? SimulationPhase.Ramping
                        : _current.Phase;

            DateTimeOffset? nextChurnAtUtc = null;

            if (isRunning && request.EnableStochasticFlow)
            {
                nextChurnAtUtc =
                    !_current.IsRunning ||
                    volatilityChanged ||
                    _current.NextChurnAtUtc is null
                        ? now.Add(
                            SimulationVolatilityProfiles
                                .For(request.Volatility)
                                .NextChurnDelay())
                        : _current.NextChurnAtUtc;
            }

            _current = new SimulationRuntimeSnapshot(
                Revision: ++_revision,
                IsRunning: isRunning,
                TargetActiveSessions: target,
                EnableStochasticFlow:
                    request.EnableStochasticFlow,
                Volatility: request.Volatility,
                Modules:
                    SimulationModuleFlags.From(request.Modules),
                SimulateEvents: request.SimulateEvents,
                Phase: phase,
                NextChurnAtUtc: nextChurnAtUtc);

            return _current;
        }
    }

    public SimulationRuntimeSnapshot Stop(
        DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            _current = _current with
            {
                Revision = ++_revision,
                IsRunning = false,
                TargetActiveSessions = 0,
                EnableStochasticFlow = false,
                Phase = SimulationPhase.Idle,
                NextChurnAtUtc = null
            };

            return _current;
        }
    }

    public SimulationRuntimeSnapshot Read()
    {
        lock (_syncRoot)
        {
            return _current;
        }
    }

    public void MarkPhase(
        long revision,
        SimulationPhase phase)
    {
        lock (_syncRoot)
        {
            if (_current.Revision != revision)
            {
                return;
            }

            _current = _current with
            {
                Phase = phase
            };
        }
    }

    public void ScheduleNextChurn(
        long revision,
        DateTimeOffset nextChurnAtUtc)
    {
        lock (_syncRoot)
        {
            if (_current.Revision != revision)
            {
                return;
            }

            _current = _current with
            {
                NextChurnAtUtc = nextChurnAtUtc
            };
        }
    }

    public int? SelectNextOwner(
        IReadOnlyList<int> ownerIds,
        double assignmentProbability)
    {
        ArgumentNullException.ThrowIfNull(ownerIds);

        if (ownerIds.Count == 0 ||
            Random.Shared.NextDouble() >=
            Math.Clamp(assignmentProbability, 0, 1))
        {
            return null;
        }

        lock (_syncRoot)
        {
            var index =
                (int)(_ownerRotationCursor % ownerIds.Count);

            _ownerRotationCursor++;

            return ownerIds[index];
        }
    }
}

public sealed class SimulationExecutionGate
{
    public static SimulationExecutionGate Shared { get; } = new();

    public SemaphoreSlim Mutex { get; } = new(1, 1);
}
