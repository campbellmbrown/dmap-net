using System;

namespace DMap.Models;

public sealed class FogMask
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Data { get; }

    public FogMask(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new byte[width * height];
    }

    public FogMask(int width, int height, byte[] data)
    {
        if (data.Length != width * height)
            throw new ArgumentException("Data length must equal width * height.");

        Width = width;
        Height = height;
        Data = data;
    }

    public byte this[int x, int y]
    {
        get => Data[y * Width + x];
        set => Data[y * Width + x] = value;
    }

    public FogMask Clone()
    {
        var copy = new byte[Data.Length];
        Buffer.BlockCopy(Data, 0, copy, 0, Data.Length);
        return new FogMask(Width, Height, copy);
    }
}
