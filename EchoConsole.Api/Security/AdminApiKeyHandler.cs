namespace EchoConsole.Web.Security;

public sealed class AdminApiKeyHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminApiKeyHandler> _logger;

    public AdminApiKeyHandler(
        IConfiguration configuration,
        ILogger<AdminApiKeyHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["AdminApiSecurity:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("AdminApiSecurity:ApiKey is not configured in EchoConsole.Web.");
        }

        if (!request.Headers.Contains("X-Admin-Api-Key"))
        {
            request.Headers.Add("X-Admin-Api-Key", apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}