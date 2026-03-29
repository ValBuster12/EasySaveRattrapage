using System.Text.Json;
using EasySave.Protocol.Messages;

namespace EasySave.Protocol.Transport;

public static class ProtocolSerializer
{
    public const string CurrentVersion = "1.0";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static ProtocolEnvelope CreateEnvelope<TPayload>(
        string type,
        string senderId,
        TPayload payload,
        Guid? correlationId = null,
        DateTimeOffset? timestampUtc = null)
    {
        var payloadElement = JsonSerializer.SerializeToElement(payload, SerializerOptions);

        return new ProtocolEnvelope(
            type,
            CurrentVersion,
            senderId,
            timestampUtc ?? DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            correlationId,
            payloadElement);
    }

    public static string SerializeEnvelope(ProtocolEnvelope envelope)
    {
        return JsonSerializer.Serialize(envelope, SerializerOptions);
    }

    public static ProtocolEnvelope? DeserializeEnvelope(string json)
    {
        return JsonSerializer.Deserialize<ProtocolEnvelope>(json, SerializerOptions);
    }

    public static TPayload? DeserializePayload<TPayload>(ProtocolEnvelope envelope)
    {
        return envelope.Payload.Deserialize<TPayload>(SerializerOptions);
    }
}
