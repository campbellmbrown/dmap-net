using System;

namespace DMap.Protocol;

/// <summary>
/// Player viewport state broadcast from the DM to players.
/// </summary>
public sealed class ViewportPayload : IPayload
{
    const int CenterMapXByteLength = sizeof(double);
    const int CenterMapYByteLength = sizeof(double);
    const int ZoomLevelByteLength = sizeof(double);
    const int RotationQuarterTurnsByteLength = sizeof(int);
    const int WidthMapByteLength = sizeof(double);
    const int HeightMapByteLength = sizeof(double);
    const int PaddingPixelsByteLength = sizeof(double);
    const int PixelSharpnessEnabledByteLength = sizeof(byte);
    const int LegacyPayloadLength = CenterMapXByteLength + CenterMapYByteLength + ZoomLevelByteLength + RotationQuarterTurnsByteLength;
    const int PayloadLength = LegacyPayloadLength + WidthMapByteLength + HeightMapByteLength + PaddingPixelsByteLength + PixelSharpnessEnabledByteLength;

    const int CenterMapXOffset = 0;
    const int CenterMapYOffset = CenterMapXOffset + CenterMapXByteLength;
    const int ZoomLevelOffset = CenterMapYOffset + CenterMapYByteLength;
    const int RotationQuarterTurnsOffset = ZoomLevelOffset + ZoomLevelByteLength;
    const int WidthMapOffset = RotationQuarterTurnsOffset + RotationQuarterTurnsByteLength;
    const int HeightMapOffset = WidthMapOffset + WidthMapByteLength;
    const int PaddingPixelsOffset = HeightMapOffset + HeightMapByteLength;
    const int PixelSharpnessEnabledOffset = PaddingPixelsOffset + PaddingPixelsByteLength;

    /// <summary>
    /// Map-space X coordinate that should be centered in the viewport.
    /// </summary>
    public double CenterMapX { get; init; }

    /// <summary>
    /// Map-space Y coordinate that should be centered in the viewport.
    /// </summary>
    public double CenterMapY { get; init; }

    /// <summary>
    /// Legacy zoom multiplier to apply around the centered map coordinate.
    /// </summary>
    public double ZoomLevel { get; init; }

    /// <summary>
    /// Clockwise player view rotation in 90-degree increments, normalized to the range 0–3.
    /// </summary>
    public int RotationQuarterTurns { get; init; }

    /// <summary>
    /// Width of the selected player viewport in map pixels.
    /// </summary>
    public double WidthMap { get; init; }

    /// <summary>
    /// Height of the selected player viewport in map pixels.
    /// </summary>
    public double HeightMap { get; init; }

    /// <summary>
    /// Padding to leave around the fitted player viewport in screen pixels.
    /// </summary>
    public double PaddingPixels { get; init; }

    /// <summary>
    /// When <see langword="true"/>, scaled map and fog bitmaps render with nearest-neighbor interpolation.
    /// </summary>
    public bool IsPixelSharpnessEnabled { get; init; }

    /// <summary>
    /// <see langword="true"/> when this payload contains a valid map-space rectangle.
    /// </summary>
    public bool HasMapRect =>
        IsValidPositive(WidthMap)
        && IsValidPositive(HeightMap)
        && IsValidNonNegative(PaddingPixels);

    public byte[] Serialize()
    {
        var bytes = new byte[PayloadLength];
        BitConverter.TryWriteBytes(bytes.AsSpan(CenterMapXOffset, CenterMapXByteLength), CenterMapX);
        BitConverter.TryWriteBytes(bytes.AsSpan(CenterMapYOffset, CenterMapYByteLength), CenterMapY);
        BitConverter.TryWriteBytes(bytes.AsSpan(ZoomLevelOffset, ZoomLevelByteLength), ZoomLevel);
        BitConverter.TryWriteBytes(bytes.AsSpan(RotationQuarterTurnsOffset, RotationQuarterTurnsByteLength), NormalizeRotation(RotationQuarterTurns));
        BitConverter.TryWriteBytes(bytes.AsSpan(WidthMapOffset, WidthMapByteLength), WidthMap);
        BitConverter.TryWriteBytes(bytes.AsSpan(HeightMapOffset, HeightMapByteLength), HeightMap);
        BitConverter.TryWriteBytes(bytes.AsSpan(PaddingPixelsOffset, PaddingPixelsByteLength), Math.Max(0, PaddingPixels));
        bytes[PixelSharpnessEnabledOffset] = IsPixelSharpnessEnabled ? (byte)1 : (byte)0;
        return bytes;
    }

    /// <summary>
    /// Deserializes a payload from bytes.
    /// </summary>
    public static ViewportPayload Deserialize(byte[] bytes)
    {
        if (bytes.Length < LegacyPayloadLength)
            throw new ArgumentException("Viewport payload is shorter than the legacy payload length.", nameof(bytes));

        var widthMap = bytes.Length >= WidthMapOffset + WidthMapByteLength
            ? BitConverter.ToDouble(bytes, WidthMapOffset)
            : 0;
        var heightMap = bytes.Length >= HeightMapOffset + HeightMapByteLength
            ? BitConverter.ToDouble(bytes, HeightMapOffset)
            : 0;
        var paddingPixels = bytes.Length >= PaddingPixelsOffset + PaddingPixelsByteLength
            ? BitConverter.ToDouble(bytes, PaddingPixelsOffset)
            : 0;
        var isPixelSharpnessEnabled = bytes.Length >= PixelSharpnessEnabledOffset + PixelSharpnessEnabledByteLength
            ? bytes[PixelSharpnessEnabledOffset] != 0
            : false;

        return new ViewportPayload
        {
            CenterMapX = BitConverter.ToDouble(bytes, CenterMapXOffset),
            CenterMapY = BitConverter.ToDouble(bytes, CenterMapYOffset),
            ZoomLevel = BitConverter.ToDouble(bytes, ZoomLevelOffset),
            RotationQuarterTurns = NormalizeRotation(BitConverter.ToInt32(bytes, RotationQuarterTurnsOffset)),
            WidthMap = IsValidPositive(widthMap) ? widthMap : 0,
            HeightMap = IsValidPositive(heightMap) ? heightMap : 0,
            PaddingPixels = IsValidNonNegative(paddingPixels) ? paddingPixels : 0,
            IsPixelSharpnessEnabled = isPixelSharpnessEnabled,
        };
    }

    static bool IsValidPositive(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    static bool IsValidNonNegative(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

    static int NormalizeRotation(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;
}
