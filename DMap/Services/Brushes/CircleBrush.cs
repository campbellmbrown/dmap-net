using System;

using Avalonia;

using DMap.Models;

namespace DMap.Services.Brushes;

/// <summary>
/// Brush tip that produces a circular stroke by measuring each pixel's Euclidean distance
/// to the nearest point on the stroke segment. Softness feathers the outer annulus of the brush.
/// </summary>
public sealed class CircleBrush : IBrush
{
    /// <inheritdoc/>
    public string Name => "Circle";

    /// <inheritdoc/>
    public PixelRect Apply(FogMask mask, int x1, int y1, int x2, int y2, BrushSettings settings, byte[]? snapshot = null)
    {
        var radius = settings.Diameter / 2.0;
        if (radius < 1)
            return default;

        var innerRadius = radius * (1.0 - settings.Softness);

        if (!BrushHelper.TryGetClippedStrokeBounds(mask, x1, y1, x2, y2, radius, out var minX, out var minY, out var maxX, out var maxY))
            return default;

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

    /// <summary>
    /// Returns the Euclidean distance from point (<paramref name="px"/>, <paramref name="py"/>) to
    /// the segment from (x1, y1) with direction vector (dx, dy) and squared length <paramref name="lenSq"/>.
    /// </summary>
    static double DistToSegment(int px, int py, int x1, int y1, double dx, double dy, double lenSq)
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
