namespace EchoConsole.Api.Security;

public interface IRealtimeApiKeyValidator
{
    bool IsValid(string? providedApiKey);
}