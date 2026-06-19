using System;

namespace DMap.Protocol;

/// <summary>
/// Camera state broadcast from the DM to players.
/// </summary>
public sealed class ViewportPayload : IPayload
{
    const int CenterMapXByteLength = sizeof(double);
    const int CenterMapYByteLength = sizeof(double);
    const int ZoomLevelByteLength = sizeof(double);
    const int RotationQuarterTurnsByteLength = sizeof(int);
    const int PayloadLength = CenterMapXByteLength + CenterMapYByteLength + ZoomLevelByteLength + RotationQuarterTurnsByteLength;

    const int CenterMapXOffset = 0;
    const int CenterMapYOffset = CenterMapXOffset + CenterMapXByteLength;
    const int ZoomLevelOffset = CenterMapYOffset + CenterMapYByteLength;
    const int RotationQuarterTurnsOffset = ZoomLevelOffset + ZoomLevelByteLength;

    /// <summary>
    /// Map-space X coordinate that should be centered in the viewport.
    /// </summary>
    public double CenterMapX { get; init; }

    /// <summary>
    /// Map-space Y coordinate that should be centered in the viewport.
    /// </summary>
    public double CenterMapY { get; init; }

    /// <summary>
    /// Zoom multiplier to apply around the centered map coordinate.
    /// </summary>
    public double ZoomLevel { get; init; }

    /// <summary>
    /// Clockwise rotation of the map in 90-degree increments, normalized to the range 0–3.
    /// </summary>
    public int RotationQuarterTurns { get; init; }

    public byte[] Serialize()
    {
        var bytes = new byte[PayloadLength];
        BitConverter.TryWriteBytes(bytes.AsSpan(CenterMapXOffset, CenterMapXByteLength), CenterMapX);
        BitConverter.TryWriteBytes(bytes.AsSpan(CenterMapYOffset, CenterMapYByteLength), CenterMapY);
        BitConverter.TryWriteBytes(bytes.AsSpan(ZoomLevelOffset, ZoomLevelByteLength), ZoomLevel);
        BitConverter.TryWriteBytes(bytes.AsSpan(RotationQuarterTurnsOffset, RotationQuarterTurnsByteLength), NormalizeRotation(RotationQuarterTurns));
        return bytes;
    }

    /// <summary>
    /// Deserializes a payload from bytes.
    /// </summary>
    public static ViewportPayload Deserialize(byte[] bytes)
    {
        return new ViewportPayload
        {
            CenterMapX = BitConverter.ToDouble(bytes, CenterMapXOffset),
            CenterMapY = BitConverter.ToDouble(bytes, CenterMapYOffset),
            ZoomLevel = BitConverter.ToDouble(bytes, ZoomLevelOffset),
            RotationQuarterTurns = NormalizeRotation(BitConverter.ToInt32(bytes, RotationQuarterTurnsOffset)),
        };
    }

    static int NormalizeRotation(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;
}
