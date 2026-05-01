using System;

using Avalonia;

using DMap.Models;

namespace DMap.Services.Brushes;

/// <summary>
/// Brush tip that produces a diamond-shaped stroke by measuring each pixel's Manhattan (L1-norm)
/// distance to the nearest point on the stroke segment. Softness feathers the outer band of the brush.
/// </summary>
public sealed class DiamondBrush : IBrush
{
    /// <inheritdoc/>
    public string Name => "Diamond";

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

        for (var py = minY; py <= maxY; py++)
        {
            for (var px = minX; px <= maxX; px++)
            {
                var dist = ManhattanDistToSegment(px, py, x1, y1, dx, dy);

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

    // Minimises |rx - t*dx| + |ry - t*dy| over t ∈ [0,1].
    // The piecewise-linear minimum is always at an endpoint or a kink of one of the absolute-value terms.
    /// <summary>
    /// Returns the Manhattan (L1-norm) distance from point (<paramref name="px"/>, <paramref name="py"/>)
    /// to the segment starting at (ax, ay) with direction (dx, dy).
    /// The minimum is found analytically by evaluating the piecewise-linear objective at all critical t values.
    /// </summary>
    static double ManhattanDistToSegment(int px, int py, int ax, int ay, double dx, double dy)
    {
        var rx = px - ax;
        var ry = py - ay;

        if (Math.Abs(dx) < 1e-10 && Math.Abs(dy) < 1e-10)
            return Math.Abs(rx) + Math.Abs(ry);

        var best = double.MaxValue;

        void Try(double t)
        {
            t = Math.Clamp(t, 0, 1);
            var d = Math.Abs(rx - (t * dx)) + Math.Abs(ry - (t * dy));
            if (d < best) best = d;
        }

        Try(0);
        Try(1);
        if (Math.Abs(dx) > 1e-10) Try(rx / dx);
        if (Math.Abs(dy) > 1e-10) Try(ry / dy);

        return best;
    }
}
