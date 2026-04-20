using System;

using Avalonia;

using DMap.Models;

namespace DMap.Services.Brushes;

public sealed class CircleBrush : IBrush
{
    public string Name => "Circle";

    public PixelRect Apply(FogMask mask, int x1, int y1, int x2, int y2, BrushSettings settings)
    {
        var radius = settings.Diameter / 2.0;
        if (radius < 1)
            return default;

        var innerRadius = radius * (1.0 - settings.Softness);

        var minX = Math.Max(0, (int)Math.Floor(Math.Min(x1, x2) - radius));
        var minY = Math.Max(0, (int)Math.Floor(Math.Min(y1, y2) - radius));
        var maxX = Math.Min(mask.Width - 1, (int)Math.Ceiling(Math.Max(x1, x2) + radius));
        var maxY = Math.Min(mask.Height - 1, (int)Math.Ceiling(Math.Max(y1, y2) + radius));

        var dx = (double)(x2 - x1);
        var dy = (double)(y2 - y1);
        var lenSq = dx * dx + dy * dy;

        for (var py = minY; py <= maxY; py++)
        {
            for (var px = minX; px <= maxX; px++)
            {
                double dist;
                if (lenSq < 1e-10)
                {
                    var ex = px - x1;
                    var ey = py - y1;
                    dist = Math.Sqrt((ex * ex) + (ey * ey));
                }
                else
                {
                    var t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / lenSq, 0.0, 1.0);
                    var cx = x1 + (t * dx) - px;
                    var cy = y1 + (t * dy) - py;
                    dist = Math.Sqrt((cx * cx) + (cy * cy));
                }

                if (dist > radius)
                    continue;

                byte alpha;
                if (dist <= innerRadius || innerRadius >= radius)
                {
                    alpha = 255;
                }
                else
                {
                    var tf = (dist - innerRadius) / (radius - innerRadius);
                    alpha = (byte)(255 * (1.0 - tf));
                }

                var current = mask[px, py];
                if (alpha > current)
                    mask[px, py] = alpha;
            }
        }

        return new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
