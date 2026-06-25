using System.Net.Http.Json;
using EchoConsole.Api.Configuration;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoConsole.Api.BackgroundServices;

public sealed class DiscordAlertDispatcher : BackgroundService
{
    public const string HttpClientName = "DiscordAlerts";

    private const int NeonRedColor = 16717636;

    // Temporary emergency fallback only.
    // Paste the Discord webhook URL between the quotes for a live demo,
    // then clear the value immediately after User Secrets is working.
    private const string TemporaryWebhookFallback = "";

    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<DiscordOptions> _optionsMonitor;
    private readonly ILogger<DiscordAlertDispatcher> _logger;

    private bool _configurationNoticeLogged;
    private bool _fallbackNoticeLogged;
    private bool _schemaWarningLogged;

    public DiscordAlertDispatcher(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<DiscordOptions> optionsMonitor,
        ILogger<DiscordAlertDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var webhookUrl = ResolveWebhookUrl(options);

            try
            {
                if (webhookUrl is null)
                {
                    if (!_configurationNoticeLogged)
                    {
                        _logger.LogWarning(
                            "Discord alert relay is inactive. Set DiscordAlerts:Enabled=true and configure DiscordAlerts:WebhookUrl, or temporarily populate TemporaryWebhookFallback.");

                        _configurationNoticeLogged = true;
                    }
                }
                else
                {
                    _configurationNoticeLogged = false;

                    await DispatchPendingMessagesAsync(
                        webhookUrl,
                        options,
                        stoppingToken);

                    _schemaWarningLogged = false;
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SqlException ex)
                when (ex.Number is 207 or 208)
            {
                if (!_schemaWarningLogged)
                {
                    _logger.LogWarning(
                        ex,
                        "Discord alert outbox is unavailable. Apply migration AddAlertTypeCatalogAndDiscordOutbox.");

                    _schemaWarningLogged = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Automatic Discord alert relay failed. The dispatcher will retry on the next cycle.");
            }

            var delay = TimeSpan.FromSeconds(
                Math.Clamp(
                    _optionsMonitor.CurrentValue.PollIntervalSeconds,
                    1,
                    60));

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private string? ResolveWebhookUrl(DiscordOptions options)
    {
        if (options.Enabled &&
            TryGetHttpsUrl(options.WebhookUrl, out var configuredUrl))
        {
            _fallbackNoticeLogged = false;
            return configuredUrl;
        }

        if (TryGetHttpsUrl(
                TemporaryWebhookFallback,
                out var fallbackUrl))
        {
            if (!_fallbackNoticeLogged)
            {
                _logger.LogWarning(
                    "DiscordAlertDispatcher is using TemporaryWebhookFallback. Move the webhook to User Secrets and clear the fallback constant after the demo.");

                _fallbackNoticeLogged = true;
            }

            return fallbackUrl;
        }

        return null;
    }

    private static bool TryGetHttpsUrl(
        string? value,
        out string validUrl)
    {
        validUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(
                value.Trim(),
                UriKind.Absolute,
                out var uri) ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        validUrl = uri.AbsoluteUri;
        return true;
    }

    private async Task DispatchPendingMessagesAsync(
        string webhookUrl,
        DiscordOptions options,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<EchoConsoleDbContext>();

        var now = DateTimeOffset.UtcNow;
        var batchSize = Math.Clamp(options.BatchSize, 1, 50);

        var pendingMessages = await dbContext.AlertDiscordOutboxMessages
            .Include(message => message.SystemAlert)
            .Where(message =>
                message.SentAtUtc == null &&
                message.NextAttemptUtc <= now)
            .OrderBy(message => message.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        var httpClient = _httpClientFactory.CreateClient(
            HttpClientName);

        foreach (var message in pendingMessages)
        {
            try
            {
                var payload = CreateDiscordPayload(
                    message.SystemAlert);

                using var response = await httpClient.PostAsJsonAsync(
                    webhookUrl,
                    payload,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content
                        .ReadAsStringAsync(cancellationToken);

                    throw new HttpRequestException(
                        $"Discord returned {(int)response.StatusCode}: {responseBody}");
                }

                message.SentAtUtc = DateTimeOffset.UtcNow;
                message.LastError = null;

                _logger.LogInformation(
                    "Critical alert delivered to Discord. AlertId={AlertId}, Severity={Severity}, ErrorType={ErrorTypeCode}",
                    message.SystemAlert.Id,
                    message.SystemAlert.Severity,
                    message.SystemAlert.ErrorTypeCode);
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException)
            {
                message.AttemptCount++;
                message.LastError = Truncate(ex.Message, 1000);
                message.NextAttemptUtc = DateTimeOffset.UtcNow.Add(
                    CalculateRetryDelay(message.AttemptCount));

                _logger.LogWarning(
                    ex,
                    "Discord delivery failed. OutboxId={OutboxId}, Attempt={AttemptCount}, NextAttemptUtc={NextAttemptUtc}",
                    message.Id,
                    message.AttemptCount,
                    message.NextAttemptUtc);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static object CreateDiscordPayload(SystemAlert alert)
    {
        var buildVersion = string.IsNullOrWhiteSpace(
            alert.BuildVersion)
                ? "UNKNOWN_BUILD"
                : alert.BuildVersion;

        var errorType = string.IsNullOrWhiteSpace(
            alert.ErrorTypeCode)
                ? "UNCLASSIFIED"
                : alert.ErrorTypeCode;

        return new
        {
            username = "Echo Console",
            allowed_mentions = new
            {
                parse = Array.Empty<string>()
            },
            embeds = new[]
            {
                new
                {
                    title = "🚨 ECHO CONSOLE - ALERTA CRÍTICA DETECTADA",
                    color = NeonRedColor,
                    fields = new object[]
                    {
                        new
                        {
                            name = "Origen / Source",
                            value = SanitizeDiscordValue(alert.Source),
                            inline = true
                        },
                        new
                        {
                            name = "Build del Juego",
                            value = SanitizeDiscordValue(buildVersion),
                            inline = true
                        },
                        new
                        {
                            name = "Fecha UTC",
                            value = alert.CreatedAtUtc.UtcDateTime
                                .ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                            inline = true
                        },
                        new
                        {
                            name = "Tipo de Error",
                            value = SanitizeDiscordValue(errorType),
                            inline = true
                        },
                        new
                        {
                            name = "Severidad",
                            value = alert.Severity
                                .ToString()
                                .ToUpperInvariant(),
                            inline = true
                        },
                        new
                        {
                            name = "Mensaje Técnico",
                            value = SanitizeDiscordValue(alert.Message),
                            inline = false
                        }
                    },
                    footer = new
                    {
                        text = "Sistema de Telemetría Automatizado - Cosmic Diner"
                    },
                    timestamp = alert.CreatedAtUtc.UtcDateTime
                        .ToString("O")
                }
            }
        };
    }

    private static string SanitizeDiscordValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "N/A";
        }

        return Truncate(value.Trim(), 1000);
    }

    private static string Truncate(
        string value,
        int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static TimeSpan CalculateRetryDelay(
        int attemptCount)
    {
        var seconds = Math.Min(
            300,
            Math.Pow(
                2,
                Math.Clamp(attemptCount, 1, 8)));

        return TimeSpan.FromSeconds(seconds);
    }
}
