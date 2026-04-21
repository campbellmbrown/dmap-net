using System;

using Avalonia;

using DMap.Models;

namespace DMap.Services.Brushes;

public sealed class SquareBrush : IBrush
{
    public string Name => "Square";

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

        for (var py = minY; py <= maxY; py++)
        {
            for (var px = minX; px <= maxX; px++)
            {
                var dist = ChebyshevDistToSegment(px, py, x1, y1, dx, dy);

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

    // Minimises max(|rx - t*dx|, |ry - t*dy|) over t ∈ [0,1].
    // Critical points: endpoints, where the two terms are equal, and kinks of each term.
    private static double ChebyshevDistToSegment(int px, int py, int ax, int ay, double dx, double dy)
    {
        var rx = px - ax;
        var ry = py - ay;

        if (Math.Abs(dx) < 1e-10 && Math.Abs(dy) < 1e-10)
            return Math.Max(Math.Abs(rx), Math.Abs(ry));

        var best = double.MaxValue;

        void Try(double t)
        {
            t = Math.Clamp(t, 0, 1);
            var d = Math.Max(Math.Abs(rx - (t * dx)), Math.Abs(ry - (t * dy)));
            if (d < best) best = d;
        }

        Try(0);
        Try(1);

        if (Math.Abs(dx - dy) > 1e-10) Try((rx - ry) / (dx - dy));
        if (Math.Abs(dx + dy) > 1e-10) Try((rx + ry) / (dx + dy));
        if (Math.Abs(dx) > 1e-10) Try(rx / dx);
        if (Math.Abs(dy) > 1e-10) Try(ry / dy);

        return best;
    }
}
