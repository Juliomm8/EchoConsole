using System.Text;
using System.Text.Json;
using EchoConsole.Api.Contracts.Client;

namespace EchoConsole.Api.Validation;

public static class SessionEventPayloadValidator
{
    public static SessionEventPayloadValidationResult Validate(
        string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return SessionEventPayloadValidationResult.Success(null);
        }

        if (payloadJson.Length > SessionEventContract.MaxPayloadCharacters)
        {
            return SessionEventPayloadValidationResult.Failure(
                "payload_character_limit_exceeded",
                $"payloadJson cannot exceed {SessionEventContract.MaxPayloadCharacters} characters.");
        }

        var utf8Payload = Encoding.UTF8.GetBytes(payloadJson);

        if (utf8Payload.Length > SessionEventContract.MaxPayloadUtf8Bytes)
        {
            return SessionEventPayloadValidationResult.Failure(
                "payload_byte_limit_exceeded",
                $"payloadJson cannot exceed {SessionEventContract.MaxPayloadUtf8Bytes} UTF-8 bytes.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                utf8Payload,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = SessionEventContract.MaxPayloadJsonDepth
                });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return SessionEventPayloadValidationResult.Failure(
                    "payload_root_invalid",
                    "payloadJson must contain a JSON object.");
            }

            return SessionEventPayloadValidationResult.Success(
                payloadJson.Trim());
        }
        catch (JsonException)
        {
            return SessionEventPayloadValidationResult.Failure(
                "payload_json_invalid",
                "payloadJson must contain valid JSON.");
        }
    }
}

public readonly record struct SessionEventPayloadValidationResult(
    bool IsValid,
    string? NormalizedPayload,
    string ErrorCode,
    string ErrorMessage)
{
    public static SessionEventPayloadValidationResult Success(
        string? normalizedPayload)
    {
        return new SessionEventPayloadValidationResult(
            true,
            normalizedPayload,
            string.Empty,
            string.Empty);
    }

    public static SessionEventPayloadValidationResult Failure(
        string errorCode,
        string errorMessage)
    {
        return new SessionEventPayloadValidationResult(
            false,
            null,
            errorCode,
            errorMessage);
    }
}