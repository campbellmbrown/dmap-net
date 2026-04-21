using System;

using Avalonia;

using DMap.Models;

namespace DMap.Services.Brushes;

public sealed class CircleBrush : IBrush
{
    public string Name => "Circle";

    public PixelRect Apply(FogMask mask, int x1, int y1, int x2, int y2, BrushSettings settings, byte[]? snapshot = null)
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
                var dist = DistToSegment(px, py, x1, y1, dx, dy, lenSq);

                if (dist > radius)
                    continue;

                var coverage = (dist <= innerRadius || innerRadius >= radius)
                    ? 1.0
                    : 1.0 - (dist - innerRadius) / (radius - innerRadius);

                var snapshotValue = snapshot != null ? snapshot[py * mask.Width + px] : mask[px, py];
                BrushHelper.ApplyPixel(mask, px, py, coverage, settings.Erase, snapshotValue, settings.Opacity);
            }
        }

        return new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static double DistToSegment(int px, int py, int x1, int y1, double dx, double dy, double lenSq)
    {
        if (lenSq < 1e-10)
        {
            var ex = px - x1;
            var ey = py - y1;
            return Math.Sqrt((ex * ex) + (ey * ey));
        }

        var t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / lenSq, 0.0, 1.0);
        var cx = x1 + (t * dx) - px;
        var cy = y1 + (t * dy) - py;
        return Math.Sqrt((cx * cx) + (cy * cy));
    }
}
