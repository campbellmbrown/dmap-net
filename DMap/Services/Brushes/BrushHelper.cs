using DMap.Models;

namespace DMap.Services.Brushes;

internal static class BrushHelper
{
    internal static void ApplyPixel(FogMask mask, int px, int py, double coverage, bool erase, byte snapshotValue, float opacity)
    {
        var effectiveCoverage = coverage * opacity;
        var current = mask[px, py];

        if (erase)
        {
            var alpha = (byte)(snapshotValue * (1.0 - effectiveCoverage));
            if (alpha < current)
                mask[px, py] = alpha;
        }
        else
        {
            var alpha = (byte)(snapshotValue + (255 - snapshotValue) * effectiveCoverage);
            if (alpha > current)
                mask[px, py] = alpha;
        }
    }
}
