using System.Text.Json;

namespace EasySave.Protocol.Messages;

public sealed record ProtocolEnvelope(
    string MessageType,
    string ProtocolVersion,
    string SenderId,
    DateTimeOffset TimestampUtc,
    Guid MessageId,
    Guid? CorrelationId,
    JsonElement Payload);
