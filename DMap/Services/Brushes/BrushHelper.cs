using System;

using DMap.Models;

namespace DMap.Services.Brushes;

/// <summary>
/// Shared pixel-level blending logic used by all brush implementations.
/// </summary>
internal static class BrushHelper
{
    /// <summary>
    /// Computes the map-clipped bounds for a brush segment.
    /// </summary>
    internal static bool TryGetClippedStrokeBounds(
        FogMask mask, int x1, int y1, int x2, int y2, double radius,
        out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = Math.Max(0, (int)Math.Floor(Math.Min(x1, x2) - radius));
        minY = Math.Max(0, (int)Math.Floor(Math.Min(y1, y2) - radius));
        maxX = Math.Min(mask.Width - 1, (int)Math.Ceiling(Math.Max(x1, x2) + radius));
        maxY = Math.Min(mask.Height - 1, (int)Math.Ceiling(Math.Max(y1, y2) + radius));

        return minX <= maxX && minY <= maxY;
    }

    /// <summary>
    /// Writes the reveal value for a single fog pixel, respecting coverage, opacity, and erase mode.
    /// The operation only moves the pixel in the target direction (reveal or erase); it never
    /// reduces the reveal level when painting or increases it when erasing.
    /// </summary>
    /// <param name="mask">The fog mask to update.</param>
    /// <param name="px">X coordinate of the pixel.</param>
    /// <param name="py">Y coordinate of the pixel.</param>
    /// <param name="coverage">
    /// Geometric coverage for this pixel in the range [0, 1]. Values less than 1 are produced
    /// by the softness gradient at brush edges.
    /// </param>
    /// <param name="erase">
    /// <see langword="true"/> to remove fog (reduce reveal value);
    /// <see langword="false"/> to add fog reveal (increase reveal value).
    /// </param>
    /// <param name="snapshotValue">
    /// The pixel's reveal value at the moment the stroke began, used as the base for computing
    /// the target alpha so repeated passes do not stack beyond one application of opacity.
    /// </param>
    /// <param name="opacity">Maximum stroke opacity in the range [0, 1].</param>
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
