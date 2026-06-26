using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using EchoConsole.Api.Domain.Entities;

namespace EchoConsole.Web.Services.Accounts;

public interface IOtpEmailSender
{
    Task SendOtpAsync(
        User user,
        string otpCode,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpEmailSender : IOtpEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _enableSsl;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IConfiguration configuration,
        ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;

        _host = RequireSetting(
            configuration,
            "Smtp:Host");

        _port = ParsePort(
            configuration["Smtp:Port"]);

        _username = RequireSetting(
            configuration,
            "Smtp:Username");

        _password = RequireSetting(
            configuration,
            "Smtp:Password");

        _fromEmail =
            configuration["Smtp:FromEmail"]?.Trim()
            ?? "no-reply@darkskystudios.dev";

        _fromName =
            configuration["Smtp:FromName"]?.Trim()
            ?? "Echo Console";

        _enableSsl =
            configuration.GetValue<bool?>(
                "Smtp:EnableSsl")
            ?? true;
    }

    public async Task SendOtpAsync(
        User user,
        string otpCode,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var recipientEmail = user.Email?.Trim();

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new InvalidOperationException(
                "The user does not have a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(otpCode) ||
            otpCode.Length != 6 ||
            !otpCode.All(char.IsDigit))
        {
            throw new ArgumentException(
                "The OTP code must contain exactly six digits.",
                nameof(otpCode));
        }

        using var message = BuildMessage(
            recipientEmail,
            user.Alias,
            otpCode,
            expiresAtUtc);

        using var smtpClient = new SmtpClient(
            _host,
            _port)
        {
            EnableSsl = _enableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
                _username,
                _password),
            DeliveryMethod =
                SmtpDeliveryMethod.Network
        };

        await smtpClient.SendMailAsync(
            message,
            cancellationToken);

        _logger.LogInformation(
            "OTP email dispatched through SMTP. UserId={UserId}, Email={Email}, ExpiresAtUtc={ExpiresAtUtc}.",
            user.Id,
            recipientEmail,
            expiresAtUtc);
    }

    private MailMessage BuildMessage(
        string recipientEmail,
        string? alias,
        string otpCode,
        DateTimeOffset expiresAtUtc)
    {
        var safeAlias = WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(alias)
                ? "PLAYER"
                : alias.Trim());

        var safeCode = WebUtility.HtmlEncode(
            otpCode);

        var expirationText =
            expiresAtUtc.UtcDateTime.ToString(
                "yyyy-MM-dd HH:mm:ss 'UTC'");

        var safeExpiration = WebUtility.HtmlEncode(
            expirationText);

        var message = new MailMessage
        {
            From = new MailAddress(
                _fromEmail,
                _fromName,
                Encoding.UTF8),
            Subject =
                "[ECHO CONSOLE] OTP AUTHORIZATION SEQUENCE",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true,
            Body = $"""
                <!doctype html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>Echo Console OTP</title>
                </head>
                <body style="margin:0;padding:24px;background:#020402;color:#b7f7c5;font-family:Consolas,Monaco,'Courier New',monospace;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px;margin:0 auto;border:1px solid #245c35;background:#000000;">
                        <tr>
                            <td style="padding:28px;">
                                <p style="margin:0 0 10px;color:#45d66b;font-size:11px;letter-spacing:3px;">
                                    DARK SKY STUDIOS // SECURE ENROLLMENT CHANNEL
                                </p>
                                <h1 style="margin:0 0 24px;color:#d9ffe2;font-size:24px;letter-spacing:2px;">
                                    ECHO CONSOLE AUTHORIZATION
                                </h1>
                                <p style="margin:0 0 18px;color:#8ca596;line-height:1.7;">
                                    OPERATOR: {safeAlias}<br>
                                    STATUS: OTP SEQUENCE GENERATED
                                </p>
                                <div style="margin:24px 0;padding:24px;border:1px solid #45d66b;background:#031008;text-align:center;">
                                    <div style="margin-bottom:10px;color:#70927a;font-size:11px;letter-spacing:2px;">
                                        SIX-DIGIT ACCESS KEY
                                    </div>
                                    <div style="color:#7cff9b;font-size:36px;font-weight:bold;letter-spacing:10px;">
                                        {safeCode}
                                    </div>
                                </div>
                                <p style="margin:0;color:#8ca596;line-height:1.7;">
                                    This sequence expires at {safeExpiration}.<br>
                                    If you did not request this code, ignore this transmission.
                                </p>
                                <p style="margin:26px 0 0;padding-top:18px;border-top:1px solid #16351f;color:#42664d;font-size:10px;letter-spacing:2px;">
                                    ECHO CONSOLE // COSMIC DINER TELEMETRY NETWORK
                                </p>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>
                """
        };

        message.To.Add(
            new MailAddress(
                recipientEmail));

        var plainTextBody = $"""
            DARK SKY STUDIOS // SECURE ENROLLMENT CHANNEL

            ECHO CONSOLE AUTHORIZATION

            OPERATOR: {alias ?? "PLAYER"}
            SIX-DIGIT ACCESS KEY: {otpCode}

            This sequence expires at {expirationText}.
            If you did not request this code, ignore this transmission.
            """;

        message.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(
                plainTextBody,
                Encoding.UTF8,
                MediaTypeNames.Text.Plain));

        return message;
    }

    private static string RequireSetting(
        IConfiguration configuration,
        string key)
    {
        var value = configuration[key]?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"SMTP setting '{key}' is not configured.");
        }

        return value;
    }

    private static int ParsePort(
        string? rawPort)
    {
        if (!int.TryParse(
                rawPort,
                out var port) ||
            port is <= 0 or > 65535)
        {
            throw new InvalidOperationException(
                "SMTP setting 'Smtp:Port' must contain a valid TCP port.");
        }

        return port;
    }
}
