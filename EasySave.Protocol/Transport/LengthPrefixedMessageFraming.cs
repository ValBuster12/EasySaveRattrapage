using System.Buffers.Binary;
using System.Text;

namespace EasySave.Protocol.Transport;

public static class LengthPrefixedMessageFraming
{
    public static async Task WriteFrameAsync(Stream stream, string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        var payload = Encoding.UTF8.GetBytes(message);
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<string?> ReadFrameAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[sizeof(int)];
        var headerRead = await ReadExactAsync(stream, header, cancellationToken);
        if (headerRead == 0)
            return null;

        if (headerRead != header.Length)
            throw new EndOfStreamException("Unexpected end of stream while reading frame header.");

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength < 0)
            throw new InvalidDataException("Frame size cannot be negative.");

        var payload = new byte[payloadLength];
        var payloadRead = await ReadExactAsync(stream, payload, cancellationToken);
        if (payloadRead != payloadLength)
            throw new EndOfStreamException("Unexpected end of stream while reading frame payload.");

        return Encoding.UTF8.GetString(payload);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
                return offset;

            offset += read;
        }

        return offset;
    }
}
