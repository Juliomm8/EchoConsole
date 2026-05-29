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
        connection.On<object>("ReceiveTelemetryUpdate", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", payload);
        });

        connection.On<object>("sessionStarted", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", new
            {
                eventType = "sessionStarted",
                payload
            });
        });

        connection.On<object>("sessionHeartbeat", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", new
            {
                eventType = "sessionHeartbeat",
                payload
            });
        });

        connection.On<object>("sessionEnded", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", new
            {
                eventType = "sessionEnded",
                payload
            });
        });

        connection.On<object>("sessionExpired", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", new
            {
                eventType = "sessionExpired",
                payload
            });
        });

        connection.On<object>("installationUpdated", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", new
            {
                eventType = "installationUpdated",
                payload
            });
        });

        connection.On<object>("newInstallation", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", new
            {
                eventType = "newInstallation",
                payload
            });
        });

        connection.On<object>("alertCreated", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", new
            {
                eventType = "alertCreated",
                payload
            });
        });

        connection.On<object>("alertUpdated", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", new
            {
                eventType = "alertUpdated",
                payload
            });
        });

        connection.On<object>("liveSessionsChanged", async payload =>
        {
            await BroadcastAsync("ReceiveTelemetryUpdate", new
            {
                eventType = "liveSessionsChanged",
                payload
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