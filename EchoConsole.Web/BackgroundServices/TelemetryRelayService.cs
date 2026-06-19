using EchoConsole.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace EchoConsole.Web.BackgroundServices;

public sealed class TelemetryRelayService : BackgroundService
{
    private const string AdminApiKeyHeaderName = "X-Admin-Api-Key";

    private readonly IConfiguration _configuration;
    private readonly IHubContext<AdminTelemetryHub> _hubContext;
    private readonly ILogger<TelemetryRelayService> _logger;

    private HubConnection? _connection;

    public TelemetryRelayService(
        IConfiguration configuration,
        IHubContext<AdminTelemetryHub> hubContext,
        ILogger<TelemetryRelayService> logger)
    {
        _configuration = configuration;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];
        var apiKey = _configuration["AdminApiSecurity:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new InvalidOperationException("ApiSettings:BaseUrl is not configured in EchoConsole.Web.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("AdminApiSecurity:ApiKey is not configured in EchoConsole.Web.");
        }

        var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/telemetry";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Headers[AdminApiKeyHeaderName] = apiKey;
            })
            .WithAutomaticReconnect()
            .Build();

        RegisterRelayHandlers(_connection);

        _connection.Closed += async ex =>
        {
            _logger.LogWarning(ex, "Telemetry relay connection closed. Retrying soon.");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        };

        _connection.Reconnecting += ex =>
        {
            _logger.LogWarning(ex, "Telemetry relay reconnecting...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Telemetry relay reconnected. ConnectionId: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_connection.State == HubConnectionState.Disconnected)
                {
                    _logger.LogInformation("Connecting telemetry relay to API hub using server-to-server header authentication.");

                    await _connection.StartAsync(stoppingToken);

                    _logger.LogInformation("Telemetry relay connected successfully.");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telemetry relay failed to connect. Retrying...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private void RegisterRelayHandlers(HubConnection connection)
    {
        connection.On<object>(
            "ReceiveTelemetryUpdate",
            async payload =>
            {
                await BroadcastAsync(
                    "ReceiveTelemetryUpdate",
                    payload);

                await BroadcastAsync(
                    "LiveOperationsRefresh",
                    new
                    {
                        eventType = "telemetryUpdate"
                    });
            });

        RegisterOperationalHandler(
            connection,
            "sessionStarted");

        RegisterOperationalHandler(
            connection,
            "sessionHeartbeat");

        RegisterOperationalHandler(
            connection,
            "sessionEventRecorded");

        RegisterOperationalHandler(
            connection,
            "sessionEnded");

        RegisterOperationalHandler(
            connection,
            "sessionExpired");

        RegisterOperationalHandler(
            connection,
            "installationUpdated");

        RegisterOperationalHandler(
            connection,
            "newInstallation");

        RegisterOperationalHandler(
            connection,
            "alertCreated");

        RegisterOperationalHandler(
            connection,
            "alertUpdated");

        RegisterOperationalHandler(
            connection,
            "liveSessionsChanged");
    }

    private void RegisterOperationalHandler(
    HubConnection connection,
    string sourceEventName)
    {
        connection.On<object>(
            sourceEventName,
            async payload =>
            {
                var envelope = new
                {
                    eventType = sourceEventName,
                    payload
                };

                await BroadcastAsync(
                    "ReceiveTelemetryUpdate",
                    envelope);

                await BroadcastAsync(
                    "LiveOperationsRefresh",
                    new
                    {
                        eventType = sourceEventName
                    });
            });
    }

    private async Task BroadcastAsync(string eventName, object? payload)
    {
        await _hubContext.Clients.All.SendAsync(
            eventName,
            new
            {
                serverTimeUtc = DateTimeOffset.UtcNow,
                payload
            });
    }
}