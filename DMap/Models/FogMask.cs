using System;

namespace DMap.Models;

/// <summary>
/// A 2-D grayscale mask that tracks the fog-of-war state for every map pixel.
/// Each byte represents the revealed amount for one pixel: 0 = fully fogged,
/// 255 = fully revealed. Values in between produce a partial reveal.
/// </summary>
public sealed class FogMask
{
    /// <summary>Width of the mask in pixels, matching the loaded map image width.</summary>
    public int Width { get; }

    /// <summary>Height of the mask in pixels, matching the loaded map image height.</summary>
    public int Height { get; }

    /// <summary>
    /// Raw pixel data stored in row-major order (index = y * Width + x).
    /// Each byte is the reveal value for one pixel.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Creates a new mask of the given dimensions with all pixels set to 0 (fully fogged).
    /// </summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    public FogMask(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new byte[width * height];
    }

    /// <summary>
    /// Creates a mask backed by an existing data array.
    /// </summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="data">Pixel data; must have exactly <paramref name="width"/> * <paramref name="height"/> bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> length does not equal width * height.</exception>
    public FogMask(int width, int height, byte[] data)
    {
        if (data.Length != width * height)
            throw new ArgumentException("Data length must equal width * height.");

        Width = width;
        Height = height;
        Data = data;
    }

    /// <summary>
    /// Gets or sets the reveal value for the pixel at column <paramref name="x"/>, row <paramref name="y"/>.
    /// </summary>
    public byte this[int x, int y]
    {
        get => Data[y * Width + x];
        set => Data[y * Width + x] = value;
    }

    /// <summary>
    /// Creates a deep copy of this mask with independent pixel data.
    /// </summary>
    /// <returns>A new <see cref="FogMask"/> with identical dimensions and pixel values.</returns>
    public FogMask Clone()
    {
        var copy = new byte[Data.Length];
        Buffer.BlockCopy(Data, 0, copy, 0, Data.Length);
        return new FogMask(Width, Height, copy);
    }
}
