using System;

using DMap.Models;

namespace DMap.Protocol;

/// <summary>
/// Fog overlay appearance settings broadcast from DM to players.
/// </summary>
public sealed class FogAppearancePayload : IPayload
{
    const int FogTypeByteLength = 1;
    const int RByteLength = sizeof(byte);
    const int GByteLength = sizeof(byte);
    const int BByteLength = sizeof(byte);
    const int SeedByteLength = 16;
    const int PayloadLength = FogTypeByteLength + RByteLength + GByteLength + BByteLength + SeedByteLength;

    const int FogTypeOffset = 0;
    const int ROffset = FogTypeOffset + FogTypeByteLength;
    const int GOffset = ROffset + RByteLength;
    const int BOffset = GOffset + GByteLength;
    const int SeedOffset = BOffset + BByteLength;

    /// <summary>
    /// Selected fog type.
    /// </summary>
    public FogType FogType { get; init; }

    /// <summary>
    /// Red channel of the flat fog colour. Not applicable for non-<see cref="FogType.Color"/> fog types.
    /// </summary>
    public byte R { get; init; }

    /// <summary>
    /// Green channel of the flat fog colour. Not applicable for non-<see cref="FogType.Color"/> fog types.
    /// </summary>
    public byte G { get; init; }

    /// <summary>
    /// Blue channel of the flat fog colour. Not applicable for non-<see cref="FogType.Color"/> fog types.
    /// </summary>
    public byte B { get; init; }

    /// <summary>
    /// Texture seed (typically the session ID) so DM and players share the same noise.
    /// Not applicable for <see cref="FogType.Color"/> since it has no noise texture.
    /// </summary>
    public Guid Seed { get; init; }

    public byte[] Serialize()
    {
        var bytes = new byte[PayloadLength];
        bytes[FogTypeOffset] = (byte)FogType;
        bytes[ROffset] = R;
        bytes[GOffset] = G;
        bytes[BOffset] = B;
        Seed.TryWriteBytes(bytes.AsSpan(SeedOffset, SeedByteLength));
        return bytes;
    }

    /// <summary>
    /// Deserializes a payload from bytes.
    /// </summary>
    public static FogAppearancePayload Deserialize(byte[] bytes)
    {
        return new FogAppearancePayload
        {
            FogType = (FogType)bytes[FogTypeOffset],
            R = bytes[ROffset],
            G = bytes[GOffset],
            B = bytes[BOffset],
            Seed = new Guid(bytes.AsSpan(SeedOffset, SeedByteLength)),
        };
    }
}
