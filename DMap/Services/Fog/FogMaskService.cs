using System;

using Avalonia;

using DMap.Models;
using DMap.Services.Brushes;
using DMap.Services.Networking;

namespace DMap.Services.Fog;

public sealed class FogMaskService : IFogMaskService
{
    public FogMask? Mask { get; private set; }

    public event EventHandler<PixelRect>? MaskChanged;

    public void Initialize(int width, int height)
    {
        Mask = new FogMask(width, height);
    }

    public PixelRect ApplyBrush(IBrush brush, int centerX, int centerY, BrushSettings settings)
    {
        if (Mask is null)
            throw new InvalidOperationException("Fog mask not initialized.");

        var dirtyRect = brush.Apply(Mask, centerX, centerY, settings);
        MaskChanged?.Invoke(this, dirtyRect);
        return dirtyRect;
    }

    public PixelRect ApplyRectangle(int x1, int y1, int x2, int y2, float softness)
    {
        if (Mask is null)
            throw new InvalidOperationException("Fog mask not initialized.");

        var minX = Math.Max(0, Math.Min(x1, x2));
        var minY = Math.Max(0, Math.Min(y1, y2));
        var maxX = Math.Min(Mask.Width - 1, Math.Max(x1, x2));
        var maxY = Math.Min(Mask.Height - 1, Math.Max(y1, y2));

        var feather = softness * Math.Min(maxX - minX, maxY - minY) / 2.0f;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                byte alpha;
                if (feather > 0)
                {
                    var minDist = Math.Min(
                        Math.Min(x - minX, maxX - x),
                        Math.Min(y - minY, maxY - y));
                    var t = Math.Min(1.0f, minDist / feather);
                    alpha = (byte)(255 * t);
                }
                else
                {
                    alpha = 255;
                }

                if (alpha > Mask[x, y])
                    Mask[x, y] = alpha;
            }
        }

        var dirtyRect = new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        MaskChanged?.Invoke(this, dirtyRect);
        return dirtyRect;
    }

    public void Replace(FogMask mask)
    {
        Mask = mask;
        MaskChanged?.Invoke(this, new PixelRect(0, 0, mask.Width, mask.Height));
    }

    public void ApplyDelta(FogDelta delta)
    {
        if (Mask is null)
            throw new InvalidOperationException("Fog mask not initialized.");

        for (var dy = 0; dy < delta.Height; dy++)
        {
            for (var dx = 0; dx < delta.Width; dx++)
            {
                var mx = delta.X + dx;
                var my = delta.Y + dy;
                if (mx >= 0 && mx < Mask.Width && my >= 0 && my < Mask.Height)
                {
                    var value = delta.Data[dy * delta.Width + dx];
                    if (value > Mask[mx, my])
                        Mask[mx, my] = value;
                }
            }
        }

        MaskChanged?.Invoke(this, new PixelRect(delta.X, delta.Y, delta.Width, delta.Height));
    }
}
