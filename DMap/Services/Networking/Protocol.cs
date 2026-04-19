using System;
using System.IO;
using System.IO.Compression;
using DMap.Models;

namespace DMap.Services.Networking;

public enum MessageType
{
    SessionInfo = 1,
    MapImage = 2,
    FogDelta = 3,
    FogFull = 4
}

public sealed class FogDelta
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();

    public static FogDelta FromMask(FogMask mask, int x, int y, int width, int height)
    {
        var data = new byte[width * height];
        for (var dy = 0; dy < height; dy++)
        {
            for (var dx = 0; dx < width; dx++)
            {
                var mx = x + dx;
                var my = y + dy;
                if (mx >= 0 && mx < mask.Width && my >= 0 && my < mask.Height)
                    data[dy * width + dx] = mask[mx, my];
            }
        }

        return new FogDelta { X = x, Y = y, Width = width, Height = height, Data = data };
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Width);
        writer.Write(Height);

        using (var deflate = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(Data, 0, Data.Length);
        }

        return ms.ToArray();
    }

    public static FogDelta Deserialize(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);
        var x = reader.ReadInt32();
        var y = reader.ReadInt32();
        var w = reader.ReadInt32();
        var h = reader.ReadInt32();

        using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
        var data = new byte[w * h];
        deflate.ReadExactly(data, 0, data.Length);

        return new FogDelta { X = x, Y = y, Width = w, Height = h, Data = data };
    }
}

public static class ProtocolFraming
{
    public static byte[] Frame(MessageType type, byte[] payload)
    {
        var frame = new byte[8 + payload.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 4), (int)type);
        BitConverter.TryWriteBytes(frame.AsSpan(4, 4), payload.Length);
        Buffer.BlockCopy(payload, 0, frame, 8, payload.Length);
        return frame;
    }

    public static async System.Threading.Tasks.Task WriteFrameAsync(
        Stream stream, MessageType type, byte[] payload,
        System.Threading.CancellationToken ct = default)
    {
        var header = new byte[8];
        BitConverter.TryWriteBytes(header.AsSpan(0, 4), (int)type);
        BitConverter.TryWriteBytes(header.AsSpan(4, 4), payload.Length);
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async System.Threading.Tasks.Task<(MessageType Type, byte[] Payload)?> ReadFrameAsync(
        Stream stream, System.Threading.CancellationToken ct = default)
    {
        var header = new byte[8];
        var bytesRead = 0;
        while (bytesRead < 8)
        {
            var read = await stream.ReadAsync(header.AsMemory(bytesRead, 8 - bytesRead), ct)
                .ConfigureAwait(false);
            if (read == 0)
                return null;
            bytesRead += read;
        }

        var type = (MessageType)BitConverter.ToInt32(header, 0);
        var length = BitConverter.ToInt32(header, 4);

        var payload = new byte[length];
        bytesRead = 0;
        while (bytesRead < length)
        {
            var read = await stream.ReadAsync(payload.AsMemory(bytesRead, length - bytesRead), ct)
                .ConfigureAwait(false);
            if (read == 0)
                return null;
            bytesRead += read;
        }

        return (type, payload);
    }
}
