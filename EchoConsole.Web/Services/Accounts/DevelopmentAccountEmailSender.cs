using EchoConsole.Api.Domain.Entities;

namespace EchoConsole.Web.Services.Accounts;

public sealed class DevelopmentAccountEmailSender : IAccountEmailSender
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DevelopmentAccountEmailSender> _logger;

    public DevelopmentAccountEmailSender(
        IWebHostEnvironment environment,
        ILogger<DevelopmentAccountEmailSender> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync(
        User user,
        string confirmationUrl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "A production email provider must be configured before email confirmation can be used outside Development.");
        }

        _logger.LogInformation(
            "Development email confirmation for UserId={UserId}, Email={Email}: {ConfirmationUrl}",
            user.Id,
            user.Email,
            confirmationUrl);

        return Task.CompletedTask;
    }
}
