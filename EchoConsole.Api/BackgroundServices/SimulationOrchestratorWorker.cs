using EchoConsole.Api.Services.Simulation;

namespace EchoConsole.Api.BackgroundServices;

public sealed class SimulationOrchestratorWorker
    : BackgroundService
{
    private static readonly object StartupSyncRoot = new();
    private static SimulationOrchestratorWorker? _selfHostedInstance;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SimulationOrchestratorWorker> _logger;

    public SimulationOrchestratorWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SimulationOrchestratorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public static void EnsureStarted(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(applicationLifetime);

        lock (StartupSyncRoot)
        {
            if (_selfHostedInstance is not null)
            {
                return;
            }

            var worker = new SimulationOrchestratorWorker(
                scopeFactory,
                loggerFactory.CreateLogger<
                    SimulationOrchestratorWorker>());

            _selfHostedInstance = worker;

            worker.StartAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            applicationLifetime.ApplicationStopping.Register(
                () =>
                {
                    worker.StopAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                });

            applicationLifetime.ApplicationStopped.Register(
                worker.Dispose);
        }
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextDelay = TimeSpan.FromSeconds(1);

            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var service = scope.ServiceProvider
                    .GetRequiredService<
                        ISimulationOrchestratorService>();

                if (service is not SimulationOrchestratorService orchestrator)
                {
                    _logger.LogWarning(
                        "The registered simulation service does not expose the stochastic cycle implementation.");
                }
                else
                {
                    await orchestrator.RunCycleAsync(
                        stoppingToken);

                    nextDelay =
                        orchestrator.GetNextWorkerDelay();
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "The stochastic simulation cycle failed. The worker will retry on the next tick.");

                nextDelay = TimeSpan.FromSeconds(2);
            }

            try
            {
                await Task.Delay(
                    nextDelay,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
