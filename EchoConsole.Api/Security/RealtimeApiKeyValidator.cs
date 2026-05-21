using System.Security.Cryptography;
using System.Text;

namespace EchoConsole.Api.Security;

public sealed class RealtimeApiKeyValidator : IRealtimeApiKeyValidator
{
    private readonly IConfiguration _configuration;

    public RealtimeApiKeyValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsValid(string? providedApiKey)
    {
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return false;
        }

        var expectedApiKey = _configuration["AdminApiSecurity:ApiKey"];

        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            return false;
        }

        var left = Encoding.UTF8.GetBytes(providedApiKey);
        var right = Encoding.UTF8.GetBytes(expectedApiKey);

        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}