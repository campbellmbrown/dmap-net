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
