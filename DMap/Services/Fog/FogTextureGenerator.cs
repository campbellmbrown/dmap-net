using System;

using DMap.Models;

namespace DMap.Services.Fog;

/// <summary>
/// Generates a procedural BGR pixel buffer for textured fog types.
/// Implementations are expected to be deterministic so the DM and any connected players,
/// given the same <paramref name="seed"/>, produce identical textures.
/// </summary>
public interface IFogTextureGenerator
{
    /// <summary>
    /// Generates a row-major BGR (3 bytes per pixel) texture sized <paramref name="width"/> by
    /// <paramref name="height"/>, themed by <paramref name="type"/> and seeded by <paramref name="seed"/>.
    /// Returns <see langword="null"/> for <see cref="FogType.Color"/> — callers should use the
    /// flat colour path instead.
    /// </summary>
    byte[]? Generate(int width, int height, FogType type, Guid seed);
}

/// <summary>
/// Default <see cref="IFogTextureGenerator"/> implementation. Uses multi-octave value noise
/// (fractal Brownian motion) sampled from a seeded integer hash, then maps the [0, 1] noise
/// values through per-type colour palettes.
/// </summary>
public sealed class FogTextureGenerator : IFogTextureGenerator
{
    /// <inheritdoc/>
    public byte[]? Generate(int width, int height, FogType type, Guid seed)
    {
        if (type == FogType.Color)
            return null;

        var intSeed = SeedFromGuid(seed);
        var buffer = new byte[width * height * 3];

        switch (type)
        {
            case FogType.Cloud:
                FillCloud(buffer, width, height, intSeed);
                break;
            case FogType.Fog:
                FillFog(buffer, width, height, intSeed);
                break;
            case FogType.Earth:
                FillEarth(buffer, width, height, intSeed);
                break;
            case FogType.Forest:
                FillForest(buffer, width, height, intSeed);
                break;
            case FogType.Ocean:
                FillOcean(buffer, width, height, intSeed);
                break;
            case FogType.Stone:
                FillStone(buffer, width, height, intSeed);
                break;
            case FogType.Bricks:
                FillBricks(buffer, width, height, intSeed);
                break;
            case FogType.Void:
                FillVoid(buffer, width, height, intSeed);
                break;
            case FogType.Night:
                FillNight(buffer, width, height, intSeed);
                break;
        }

        return buffer;
    }

    static void FillCloud(byte[] buf, int w, int h, int seed)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var n = Fbm(x * 0.005f, y * 0.005f, seed, octaves: 5);
                var t = Smoothstep(n);
                Write(buf, x, y, w,
                    Lerp(160, 240, t),
                    Lerp(170, 245, t),
                    Lerp(180, 250, t));
            }
        }
    }

    static void FillFog(byte[] buf, int w, int h, int seed)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var n = Fbm(x * 0.003f, y * 0.003f, seed, octaves: 3);
                Write(buf, x, y, w,
                    Lerp(95, 155, n),
                    Lerp(100, 160, n),
                    Lerp(105, 165, n));
            }
        }
    }

    static void FillEarth(byte[] buf, int w, int h, int seed)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var n = Fbm(x * 0.012f, y * 0.012f, seed, octaves: 5);
                var grain = Fbm(x * 0.06f, y * 0.06f, seed + 1, octaves: 2) * 0.25f;
                var t = MathF.Min(1f, n + grain - 0.1f);
                if (t < 0)
                    t = 0;
                Write(buf, x, y, w,
                    Lerp(20, 70, t),     // B
                    Lerp(45, 110, t),    // G
                    Lerp(80, 165, t));   // R (warm tones)
            }
        }
    }

    static void FillForest(byte[] buf, int w, int h, int seed)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var canopy = Fbm(x * 0.018f, y * 0.018f, seed, octaves: 5);
                var leaves = Fbm(x * 0.07f, y * 0.07f, seed + 3, octaves: 2) * 0.3f;
                var t = MathF.Min(1f, canopy + leaves - 0.15f);
                if (t < 0)
                    t = 0;
                Write(buf, x, y, w,
                    Lerp(15, 50, t),    // B
                    Lerp(45, 120, t),   // G (green dominant)
                    Lerp(20, 60, t));   // R
            }
        }
    }

    static void FillOcean(byte[] buf, int w, int h, int seed)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                // Stretched horizontally to suggest gently rolling swells.
                var swell = Fbm(x * 0.006f, y * 0.012f, seed, octaves: 5);
                var ripple = Fbm(x * 0.04f, y * 0.06f, seed + 7, octaves: 2) * 0.2f;
                var t = MathF.Min(1f, swell + ripple - 0.1f);
                if (t < 0)
                    t = 0;

                float b = Lerp(95, 175, t);   // B (blue dominant)
                float g = Lerp(45, 125, t);   // G (teal hint)
                float r = Lerp(10, 40, t);    // R (low)

                // White foam blended in at the brightest peaks.
                if (t > 0.82f)
                {
                    var foam = Smoothstep((t - 0.82f) / 0.18f);
                    b += (230 - b) * foam;
                    g += (235 - g) * foam;
                    r += (220 - r) * foam;
                }

                Write(buf, x, y, w, (byte)b, (byte)g, (byte)r);
            }
        }
    }

    static void FillStone(byte[] buf, int w, int h, int seed)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var n = Fbm(x * 0.015f, y * 0.015f, seed, octaves: 6);
                var detail = Fbm(x * 0.08f, y * 0.08f, seed + 7, octaves: 2) * 0.2f;
                var t = MathF.Min(1f, n + detail - 0.1f);
                if (t < 0)
                    t = 0;
                var v = Lerp(55, 135, t);
                Write(buf, x, y, w, v, v, (byte)(v + 5 > 255 ? 255 : v + 5));
            }
        }
    }

    static void FillBricks(byte[] buf, int w, int h, int seed)
    {
        const int BrickW = 64;
        const int BrickH = 28;
        const int Mortar = 3;

        for (var y = 0; y < h; y++)
        {
            var row = y / BrickH;
            var rowOffset = (row & 1) * (BrickW / 2);
            var yInBrick = y - row * BrickH;

            for (var x = 0; x < w; x++)
            {
                var shifted = x + rowOffset;
                var col = shifted / BrickW;
                var xInBrick = shifted - col * BrickW;

                var isMortar = xInBrick < Mortar || yInBrick < Mortar;
                if (isMortar)
                {
                    var n = Fbm(x * 0.1f, y * 0.1f, seed + 11, octaves: 2);
                    var v = Lerp(35, 55, n);
                    Write(buf, x, y, w, v, v, v);
                }
                else
                {
                    var brickHash = Hash(col, row, seed + 17);
                    var variation = Fbm(x * 0.05f, y * 0.05f, seed + 5, octaves: 3);
                    var t = MathF.Min(1f, variation * 0.7f + brickHash * 0.3f);
                    var weather = Fbm(x * 0.2f, y * 0.2f, seed + 23, octaves: 2) * 0.15f;
                    t = MathF.Max(0f, MathF.Min(1f, t - weather));
                    var bb = Lerp(80, 150, t);
                    var gg = Lerp(78, 145, t);
                    var rr = Lerp(85, 155, t);
                    Write(buf, x, y, w, bb, gg, rr);
                }
            }
        }
    }

    static void FillVoid(byte[] buf, int w, int h, int seed)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var n = Fbm(x * 0.005f, y * 0.005f, seed, octaves: 4);
                Write(buf, x, y, w,
                    Lerp(30, 70, n),    // B (dark blue dominant)
                    Lerp(8, 18, n),     // G
                    Lerp(20, 55, n));   // R (purple tint)
            }
        }
    }

    static void FillNight(byte[] buf, int w, int h, int seed)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var n = Fbm(x * 0.004f, y * 0.004f, seed, octaves: 3);
                var b = Lerp(20, 35, n);
                var g = Lerp(8, 18, n);
                var r = Lerp(5, 15, n);

                // Sparse stars: pick the brightest hash hits as star pixels.
                var starHash = Hash(x, y, seed + 9001);
                if (starHash > 0.9985f)
                {
                    var brightness = (byte)Lerp(180, 255, (starHash - 0.9985f) / 0.0015f);
                    b = brightness;
                    g = brightness;
                    r = brightness;
                }
                else if (starHash > 0.997f)
                {
                    // Dim stars
                    b = 110;
                    g = 110;
                    r = 110;
                }

                Write(buf, x, y, w, b, g, r);
            }
        }
    }

    static void Write(byte[] buf, int x, int y, int width, byte b, byte g, byte r)
    {
        var idx = (y * width + x) * 3;
        buf[idx] = b;
        buf[idx + 1] = g;
        buf[idx + 2] = r;
    }

    static byte Lerp(int a, int b, float t)
    {
        var v = a + (b - a) * t;
        if (v < 0)
            v = 0;
        if (v > 255)
            v = 255;
        return (byte)v;
    }

    static float Smoothstep(float t)
    {
        if (t < 0)
            t = 0;
        else if (t > 1)
            t = 1;
        return t * t * (3f - 2f * t);
    }

    static int SeedFromGuid(Guid guid)
    {
        Span<byte> bytes = stackalloc byte[16];
        guid.TryWriteBytes(bytes);
        var seed = 0;
        for (var i = 0; i < 16; i += 4)
            seed ^= BitConverter.ToInt32(bytes.Slice(i, 4));
        return seed;
    }

    /// <summary>Deterministic [0, 1] hash of an integer lattice point.</summary>
    static float Hash(int x, int y, int seed)
    {
        var n = unchecked(x * 374761393 + y * 668265263 + seed * 982451653);
        n = unchecked((n ^ (n >> 13)) * 1274126177);
        n ^= n >> 16;
        return (n & 0x7FFFFFFF) / (float)int.MaxValue;
    }

    /// <summary>Bilinear-interpolated value noise sample with smoothstep easing.</summary>
    static float ValueNoise(float x, float y, int seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = Smoothstep(x - x0);
        var fy = Smoothstep(y - y0);
        var n00 = Hash(x0, y0, seed);
        var n10 = Hash(x0 + 1, y0, seed);
        var n01 = Hash(x0, y0 + 1, seed);
        var n11 = Hash(x0 + 1, y0 + 1, seed);
        var n0 = n00 + (n10 - n00) * fx;
        var n1 = n01 + (n11 - n01) * fx;
        return n0 + (n1 - n0) * fy;
    }

    /// <summary>Sums multiple octaves of <see cref="ValueNoise"/> for a fractal Brownian motion sample in [0, 1].</summary>
    static float Fbm(float x, float y, int seed, int octaves)
    {
        var total = 0f;
        var amp = 1f;
        var freq = 1f;
        var max = 0f;
        for (var i = 0; i < octaves; i++)
        {
            total += ValueNoise(x * freq, y * freq, seed + i) * amp;
            max += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return total / max;
    }
}
