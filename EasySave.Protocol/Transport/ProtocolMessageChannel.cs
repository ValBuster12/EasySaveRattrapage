using EasySave.Protocol.Messages;

namespace EasySave.Protocol.Transport;

public sealed class ProtocolMessageChannel
{
    private readonly Stream _stream;

    public ProtocolMessageChannel(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public async Task SendAsync(ProtocolEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var serialized = ProtocolSerializer.SerializeEnvelope(envelope);
        await LengthPrefixedMessageFraming.WriteFrameAsync(_stream, serialized, cancellationToken);
    }

    public async Task<ProtocolEnvelope?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var frame = await LengthPrefixedMessageFraming.ReadFrameAsync(_stream, cancellationToken);
        if (string.IsNullOrWhiteSpace(frame))
            return null;

        return ProtocolSerializer.DeserializeEnvelope(frame);
    }
}
