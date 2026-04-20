using System;

using Avalonia;

using DMap.Models;

namespace DMap.Services.Brushes;

public sealed class DiamondBrush : IBrush
{
    public string Name => "Diamond";

    public PixelRect Apply(FogMask mask, int centerX, int centerY, BrushSettings settings)
    {
        var radius = settings.Diameter / 2.0;
        if (radius < 1)
            return default;

        var innerRadius = radius * (1.0 - settings.Softness);

        var minX = Math.Max(0, (int)Math.Floor(centerX - radius));
        var minY = Math.Max(0, (int)Math.Floor(centerY - radius));
        var maxX = Math.Min(mask.Width - 1, (int)Math.Ceiling(centerX + radius));
        var maxY = Math.Min(mask.Height - 1, (int)Math.Ceiling(centerY + radius));

        for (var py = minY; py <= maxY; py++)
        {
            for (var px = minX; px <= maxX; px++)
            {
                var dx = Math.Abs(px - centerX);
                var dy = Math.Abs(py - centerY);
                var dist = dx + dy;

                if (dist > radius)
                    continue;

                byte alpha;
                if (dist <= innerRadius || innerRadius >= radius)
                {
                    alpha = 255;
                }
                else
                {
                    var t = (dist - innerRadius) / (radius - innerRadius);
                    alpha = (byte)(255 * (1.0 - t));
                }

                var current = mask[px, py];
                if (alpha > current)
                    mask[px, py] = alpha;
            }
        }

        return new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
