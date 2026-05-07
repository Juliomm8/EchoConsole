using System.Security.Cryptography;
using System.Text;

namespace EchoConsole.Api.Security;

public sealed class SessionTokenService
{
    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }

    public string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public bool Matches(string token, string expectedHash)
    {
        var computedHash = HashToken(token);
        var left = Encoding.UTF8.GetBytes(computedHash);
        var right = Encoding.UTF8.GetBytes(expectedHash);

        return left.Length == right.Length &&
               CryptographicOperations.FixedTimeEquals(left, right);
    }
}