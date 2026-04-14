using EasySave.Protocol.Messages;
using EasySave.Protocol.Transport;

namespace EasySaveTest;

public class ProtocolSerializerTests
{
    [Test]
    public void SerializeDeserializeEnvelope_RoundTripsPayload()
    {
        var payload = new CommandRequestMessage(
            "req-1",
            "host-alpha",
            42,
            "Documents backup",
            CommandType.Pause,
            "console-a",
            DateTimeOffset.UtcNow);

        var envelope = ProtocolSerializer.CreateEnvelope(ProtocolMessageTypes.CommandRequest, "console-a", payload);
        var json = ProtocolSerializer.SerializeEnvelope(envelope);
        var deserialized = ProtocolSerializer.DeserializeEnvelope(json);

        Assert.That(deserialized, Is.Not.Null);
        var payloadRoundTrip = ProtocolSerializer.DeserializePayload<CommandRequestMessage>(deserialized!);
        Assert.That(payloadRoundTrip, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payloadRoundTrip!.RequestId, Is.EqualTo(payload.RequestId));
            Assert.That(payloadRoundTrip.TargetInstanceId, Is.EqualTo(payload.TargetInstanceId));
            Assert.That(payloadRoundTrip.Command, Is.EqualTo(CommandType.Pause));
        });
    }
}
