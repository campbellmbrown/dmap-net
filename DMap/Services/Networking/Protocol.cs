using System;
using System.IO;
using System.IO.Compression;

using DMap.Models;

namespace DMap.Services.Networking;

/// <summary>
/// Identifies the type of a framed network message exchanged between the DM host and player clients.
/// </summary>
public enum MessageType
{
    /// <summary>Initial session handshake carrying session ID, map dimensions, and the full fog mask.</summary>
    SessionInfo = 1,

    /// <summary>Raw image bytes for the map background, sent once per session.</summary>
    MapImage = 2,

    /// <summary>A compressed rectangular region of fog mask changes (incremental update).</summary>
    FogDelta = 3,

    /// <summary>A compressed full fog mask replacement.</summary>
    FogFull = 4,

    /// <summary>Fog overlay appearance (type, colour, and texture seed).</summary>
    FogAppearance = 5,

    /// <summary>Viewport camera state (map center and zoom) broadcast from the DM to players.</summary>
    Viewport = 6,

    /// <summary>Player-visible cursor state broadcast from the DM to players.</summary>
    Cursor = 7,
}

/// <summary>
/// Fog overlay appearance settings broadcast from DM to players.
/// Carries the selected <see cref="FogType"/>, the flat fog colour (used when type is
/// <see cref="FogType.Color"/>), and a texture seed so all clients generate identical noise.
/// </summary>
public sealed class FogAppearancePayload
{
    /// <summary>Selected fog type (flat colour or one of the textured variants).</summary>
    public FogType FogType { get; init; }

    /// <summary>Red channel of the flat fog colour.</summary>
    public byte R { get; init; }

    /// <summary>Green channel of the flat fog colour.</summary>
    public byte G { get; init; }

    /// <summary>Blue channel of the flat fog colour.</summary>
    public byte B { get; init; }

    /// <summary>Texture seed (typically the session ID) so DM and players share the same noise.</summary>
    public Guid Seed { get; init; }

    /// <summary>
    /// Serializes this payload to a fixed 20-byte buffer.
    /// Format: 1 byte type | 1 byte R | 1 byte G | 1 byte B | 16 byte Guid.
    /// </summary>
    public byte[] Serialize()
    {
        var bytes = new byte[20];
        bytes[0] = (byte)FogType;
        bytes[1] = R;
        bytes[2] = G;
        bytes[3] = B;
        Seed.TryWriteBytes(bytes.AsSpan(4, 16));
        return bytes;
    }

    /// <summary>Reconstructs a <see cref="FogAppearancePayload"/> from the buffer produced by <see cref="Serialize"/>.</summary>
    public static FogAppearancePayload Deserialize(byte[] bytes)
    {
        return new FogAppearancePayload
        {
            FogType = (FogType)bytes[0],
            R = bytes[1],
            G = bytes[2],
            B = bytes[3],
            Seed = new Guid(bytes.AsSpan(4, 16)),
        };
    }
}

/// <summary>
/// Camera state broadcast from the DM so players follow the DM viewport even when their
/// window size differs. The payload is expressed in map-space center coordinates plus zoom.
/// </summary>
public sealed class ViewportPayload
{
    /// <summary>Map-space X coordinate that should be centered in the viewport.</summary>
    public double CenterMapX { get; init; }

    /// <summary>Map-space Y coordinate that should be centered in the viewport.</summary>
    public double CenterMapY { get; init; }

    /// <summary>Zoom multiplier to apply around the centered map coordinate.</summary>
    public double ZoomLevel { get; init; }

    /// <summary>
    /// Serializes this payload to a fixed 24-byte buffer.
    /// Format: CenterMapX (8 bytes) | CenterMapY (8 bytes) | ZoomLevel (8 bytes).
    /// </summary>
    public byte[] Serialize()
    {
        var bytes = new byte[24];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 8), CenterMapX);
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 8), CenterMapY);
        BitConverter.TryWriteBytes(bytes.AsSpan(16, 8), ZoomLevel);
        return bytes;
    }

    /// <summary>Reconstructs a <see cref="ViewportPayload"/> from the buffer produced by <see cref="Serialize"/>.</summary>
    public static ViewportPayload Deserialize(byte[] bytes)
    {
        return new ViewportPayload
        {
            CenterMapX = BitConverter.ToDouble(bytes, 0),
            CenterMapY = BitConverter.ToDouble(bytes, 8),
            ZoomLevel = BitConverter.ToDouble(bytes, 16),
        };
    }
}

/// <summary>
/// Player-visible cursor state broadcast from the DM. Coordinates are in map pixels, while
/// size is in screen pixels so player windows render the cursor consistently at every zoom.
/// </summary>
public sealed class CursorPayload
{
    /// <summary>Map-space X coordinate for the cursor anchor.</summary>
    public double MapX { get; init; }

    /// <summary>Map-space Y coordinate for the cursor anchor.</summary>
    public double MapY { get; init; }

    /// <summary>Icon rendered for the cursor.</summary>
    public CursorType CursorType { get; init; }

    /// <summary>Cursor icon size in screen pixels.</summary>
    public int CursorSize { get; init; }

    /// <summary><see langword="true"/> when the cursor should be visible to players.</summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// Serializes this payload to a fixed 25-byte buffer.
    /// Format: MapX (8 bytes) | MapY (8 bytes) | CursorType (4 bytes) | CursorSize (4 bytes) | IsVisible (1 byte).
    /// </summary>
    public byte[] Serialize()
    {
        var bytes = new byte[25];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 8), MapX);
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 8), MapY);
        BitConverter.TryWriteBytes(bytes.AsSpan(16, 4), (int)CursorType);
        BitConverter.TryWriteBytes(bytes.AsSpan(20, 4), CursorSize);
        bytes[24] = IsVisible ? (byte)1 : (byte)0;
        return bytes;
    }

    /// <summary>Reconstructs a <see cref="CursorPayload"/> from the buffer produced by <see cref="Serialize"/>.</summary>
    public static CursorPayload Deserialize(byte[] bytes)
    {
        return new CursorPayload
        {
            MapX = BitConverter.ToDouble(bytes, 0),
            MapY = BitConverter.ToDouble(bytes, 8),
            CursorType = (CursorType)BitConverter.ToInt32(bytes, 16),
            CursorSize = BitConverter.ToInt32(bytes, 20),
            IsVisible = bytes[24] != 0,
        };
    }
}

/// <summary>
/// A rectangular region of fog mask data that can be serialized for network transmission.
/// Used for incremental fog updates so only the changed area is sent over the wire.
/// The payload contains authoritative mask bytes for the addressed region, not a reveal-only diff.
/// </summary>
public sealed class FogDelta
{
    /// <summary>Left edge of the region in map pixels.</summary>
    public int X { get; init; }

    /// <summary>Top edge of the region in map pixels.</summary>
    public int Y { get; init; }

    /// <summary>Width of the region in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Height of the region in pixels.</summary>
    public int Height { get; init; }

    /// <summary>
    /// Row-major fog pixel values for the region (Width * Height bytes).
    /// Each byte is the reveal value: 0 = fully fogged, 255 = fully revealed.
    /// </summary>
    public byte[] Data { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Extracts a rectangular region from <paramref name="mask"/> into a new <see cref="FogDelta"/>.
    /// Pixels outside mask bounds are written as 0.
    /// </summary>
    /// <param name="mask">Source fog mask.</param>
    /// <param name="x">Left edge of the region.</param>
    /// <param name="y">Top edge of the region.</param>
    /// <param name="width">Width of the region.</param>
    /// <param name="height">Height of the region.</param>
    public static FogDelta FromMask(FogMask mask, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return new FogDelta { X = x, Y = y, Width = 0, Height = 0 };

        var data = new byte[checked(width * height)];
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

    /// <summary>
    /// Serializes this delta to a byte array.
    /// Format: X (4 bytes) | Y (4 bytes) | Width (4 bytes) | Height (4 bytes) | Deflate-compressed pixel data.
    /// </summary>
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

    /// <summary>
    /// Reconstructs a <see cref="FogDelta"/> from the byte array produced by <see cref="Serialize"/>.
    /// </summary>
    /// <param name="bytes">Serialized delta bytes.</param>
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

/// <summary>
/// Utilities for framing and reading length-prefixed messages over a TCP stream.
/// Each frame has an 8-byte header: 4-byte <see cref="MessageType"/> (little-endian int32)
/// followed by a 4-byte payload length (little-endian int32), then the raw payload bytes.
/// </summary>
public static class ProtocolFraming
{
    /// <summary>
    /// Creates a framed byte array containing the 8-byte header and <paramref name="payload"/> concatenated.
    /// </summary>
    /// <param name="type">Message type written into bytes 0–3 of the header.</param>
    /// <param name="payload">The message body.</param>
    /// <returns>A single buffer ready to write to a stream.</returns>
    public static byte[] Frame(MessageType type, byte[] payload)
    {
        var frame = new byte[8 + payload.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 4), (int)type);
        BitConverter.TryWriteBytes(frame.AsSpan(4, 4), payload.Length);
        Buffer.BlockCopy(payload, 0, frame, 8, payload.Length);
        return frame;
    }

    /// <summary>
    /// Asynchronously writes a framed message to <paramref name="stream"/> and flushes it.
    /// The header and payload are sent as separate write calls to avoid large buffer allocations.
    /// </summary>
    /// <param name="stream">Writable network stream.</param>
    /// <param name="type">Message type.</param>
    /// <param name="payload">Message body.</param>
    /// <param name="ct">Cancellation token.</param>
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

    /// <summary>
    /// Asynchronously reads one framed message from <paramref name="stream"/>.
    /// Handles partial reads by looping until all expected bytes are received.
    /// </summary>
    /// <param name="stream">Readable network stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The decoded message type and payload, or <see langword="null"/> if the stream
    /// was closed before the full frame could be read.
    /// </returns>
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
