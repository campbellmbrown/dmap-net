using System;
using System.IO;
using System.IO.Compression;

using DMap.Models;

namespace DMap.Protocol;

/// <summary>
/// Initial player handshake payload containing the session metadata and a full fog mask snapshot.
/// </summary>
public sealed class SessionInfoPayload : IPayload
{
    const int SessionIdByteLength = 16;

    /// <summary>
    /// Session metadata for the active DM host.
    /// </summary>
    public required MapSession Session { get; init; }

    /// <summary>
    /// Full fog mask pixel data in row-major order.
    /// </summary>
    public byte[] FogData { get; init; } = [];

    public byte[] Serialize()
    {
        ValidateFogDataLength(Session.MapWidth, Session.MapHeight, FogData);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(Session.SessionId.ToByteArray());
        writer.Write(Session.MapWidth);
        writer.Write(Session.MapHeight);

        using (var deflate = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(FogData, 0, FogData.Length);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Reconstructs a <see cref="SessionInfoPayload"/> from the buffer produced by <see cref="Serialize"/>.
    /// </summary>
    public static SessionInfoPayload Deserialize(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);

        var sessionId = new Guid(reader.ReadBytes(SessionIdByteLength));
        var mapWidth = reader.ReadInt32();
        var mapHeight = reader.ReadInt32();

        var fogData = new byte[mapWidth * mapHeight];
        using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
        deflate.ReadExactly(fogData, 0, fogData.Length);

        return new SessionInfoPayload
        {
            Session = new MapSession(sessionId, mapWidth, mapHeight),
            FogData = fogData,
        };
    }

    /// <summary>
    /// Creates a payload from the current hosted session state.
    /// </summary>
    public static SessionInfoPayload FromSession(MapSession session, FogMask mask)
    {
        if (session.MapWidth != mask.Width || session.MapHeight != mask.Height)
            throw new ArgumentException("Session dimensions must match the fog mask dimensions.");

        return new SessionInfoPayload
        {
            Session = session,
            FogData = mask.Data,
        };
    }

    /// <summary>
    /// Creates the session metadata represented by this payload.
    /// </summary>
    public MapSession ToSession() => Session;

    /// <summary>
    /// Creates the full fog mask represented by this payload.
    /// </summary>
    public FogMask ToFogMask()
    {
        ValidateFogDataLength(Session.MapWidth, Session.MapHeight, FogData);
        return new FogMask(Session.MapWidth, Session.MapHeight, FogData);
    }

    static void ValidateFogDataLength(int mapWidth, int mapHeight, byte[] fogData)
    {
        if (fogData.Length != mapWidth * mapHeight)
            throw new ArgumentException("Fog data length must equal map width multiplied by map height.");
    }
}
