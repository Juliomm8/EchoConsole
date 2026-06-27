using EchoConsole.Api.Domain.Entities;

namespace EchoConsole.Web.Services.Accounts;

public interface IAccountEmailSender
{
    Task SendEmailConfirmationAsync(
        User user,
        string confirmationUrl,
        CancellationToken cancellationToken = default);
}
