using System;

using DMap.Models;

namespace DMap.Protocol;

/// <summary>
/// Player-visible cursor state broadcast.
/// </summary>
public sealed class CursorPayload : IPayload
{
    const int MapXByteLength = sizeof(double);
    const int MapYByteLength = sizeof(double);
    const int CursorTypeByteLength = sizeof(int);
    const int CursorSizeByteLength = sizeof(int);
    const int IsVisibleByteLength = sizeof(byte);
    const int PayloadLength = MapXByteLength + MapYByteLength + CursorTypeByteLength + CursorSizeByteLength + IsVisibleByteLength;

    const int MapXOffset = 0;
    const int MapYOffset = MapXOffset + MapXByteLength;
    const int CursorTypeOffset = MapYOffset + MapYByteLength;
    const int CursorSizeOffset = CursorTypeOffset + CursorTypeByteLength;
    const int IsVisibleOffset = CursorSizeOffset + CursorSizeByteLength;

    /// <summary>
    /// Map-space X coordinate for the cursor anchor.
    /// </summary>
    public double MapX { get; init; }

    /// <summary>
    /// Map-space Y coordinate for the cursor anchor.
    /// </summary>
    public double MapY { get; init; }

    /// <summary>
    /// Icon rendered for the cursor.
    /// </summary>
    public CursorType CursorType { get; init; }

    /// <summary>
    /// Cursor icon size in screen pixels.
    /// </summary>
    public int CursorSize { get; init; }

    /// <summary>
    /// Whether the cursor should be visible to players.
    /// </summary>
    public bool IsVisible { get; init; }

    public byte[] Serialize()
    {
        var bytes = new byte[PayloadLength];
        BitConverter.TryWriteBytes(bytes.AsSpan(MapXOffset, MapXByteLength), MapX);
        BitConverter.TryWriteBytes(bytes.AsSpan(MapYOffset, MapYByteLength), MapY);
        BitConverter.TryWriteBytes(bytes.AsSpan(CursorTypeOffset, CursorTypeByteLength), (int)CursorType);
        BitConverter.TryWriteBytes(bytes.AsSpan(CursorSizeOffset, CursorSizeByteLength), CursorSize);
        bytes[IsVisibleOffset] = IsVisible ? (byte)1 : (byte)0;
        return bytes;
    }

    /// <summary>
    /// Deserializes a payload from bytes.
    /// </summary>
    public static CursorPayload Deserialize(byte[] bytes)
    {
        return new CursorPayload
        {
            MapX = BitConverter.ToDouble(bytes, MapXOffset),
            MapY = BitConverter.ToDouble(bytes, MapYOffset),
            CursorType = (CursorType)BitConverter.ToInt32(bytes, CursorTypeOffset),
            CursorSize = BitConverter.ToInt32(bytes, CursorSizeOffset),
            IsVisible = bytes[IsVisibleOffset] != 0,
        };
    }
}
