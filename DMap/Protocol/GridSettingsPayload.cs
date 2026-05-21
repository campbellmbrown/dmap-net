using System;
using System.IO;

using Avalonia.Media;

namespace DMap.Protocol;

/// <summary>
/// Grid overlay settings broadcast by the DM so player canvases can render the same map grid.
/// </summary>
public sealed class GridSettingsPayload : IPayload
{
    public bool IsVisible { get; init; }
    public double SquareSize { get; init; }
    public double LineWidth { get; init; }
    public double Opacity { get; init; }
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }
    public double OffsetX { get; init; }
    public double OffsetY { get; init; }

    public Color Color => Color.FromRgb(R, G, B);

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(IsVisible);
        writer.Write(SquareSize);
        writer.Write(LineWidth);
        writer.Write(Opacity);
        writer.Write(R);
        writer.Write(G);
        writer.Write(B);
        writer.Write(OffsetX);
        writer.Write(OffsetY);
        return ms.ToArray();
    }

    public static GridSettingsPayload Deserialize(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);
        return new GridSettingsPayload
        {
            IsVisible = reader.ReadBoolean(),
            SquareSize = reader.ReadDouble(),
            LineWidth = reader.ReadDouble(),
            Opacity = reader.ReadDouble(),
            R = reader.ReadByte(),
            G = reader.ReadByte(),
            B = reader.ReadByte(),
            OffsetX = reader.ReadDouble(),
            OffsetY = reader.ReadDouble(),
        };
    }
}
