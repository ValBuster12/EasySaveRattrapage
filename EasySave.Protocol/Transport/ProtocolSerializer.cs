using System.Text;
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
        string messageType,
        string senderId,
        TPayload payload,
        Guid? correlationId = null,
        DateTimeOffset? timestampUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderId);

        var payloadElement = JsonSerializer.SerializeToElement(payload, SerializerOptions);

        return new ProtocolEnvelope(
            messageType,
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

    public static byte[] SerializeEnvelopeToUtf8(ProtocolEnvelope envelope)
    {
        return Encoding.UTF8.GetBytes(SerializeEnvelope(envelope));
    }

    public static ProtocolEnvelope? DeserializeEnvelope(string json)
    {
        return JsonSerializer.Deserialize<ProtocolEnvelope>(json, SerializerOptions);
    }

    public static ProtocolEnvelope? DeserializeEnvelope(ReadOnlySpan<byte> utf8Bytes)
    {
        return JsonSerializer.Deserialize<ProtocolEnvelope>(utf8Bytes, SerializerOptions);
    }

    public static TPayload? DeserializePayload<TPayload>(ProtocolEnvelope envelope)
    {
        return envelope.Payload.Deserialize<TPayload>(SerializerOptions);
    }
}
