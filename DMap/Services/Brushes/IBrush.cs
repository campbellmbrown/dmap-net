using Avalonia;

using DMap.Models;

namespace DMap.Services.Brushes;

/// <summary>
/// Defines a brush tip that can paint or erase fog along a line segment between two map coordinates.
/// Each implementation uses a different distance metric to produce a distinct brush shape.
/// </summary>
public interface IBrush
{
    /// <summary>Display name of the brush (e.g. "Circle", "Square", "Diamond").</summary>
    string Name { get; }

    /// <summary>
    /// Applies the brush stroke from map point (<paramref name="x1"/>, <paramref name="y1"/>) to
    /// (<paramref name="x2"/>, <paramref name="y2"/>) and updates <paramref name="mask"/> in place.
    /// </summary>
    /// <param name="mask">The fog mask to modify.</param>
    /// <param name="x1">Start X coordinate in map pixels.</param>
    /// <param name="y1">Start Y coordinate in map pixels.</param>
    /// <param name="x2">End X coordinate in map pixels.</param>
    /// <param name="y2">End Y coordinate in map pixels.</param>
    /// <param name="settings">Brush diameter, softness, opacity, and erase mode.</param>
    /// <param name="snapshot">
    /// Optional read-only snapshot of the mask taken at the start of the stroke. When provided,
    /// coverage is computed relative to the snapshot so repeated passes over the same area do not
    /// accumulate beyond a single application of the opacity.
    /// </param>
    /// <returns>The bounding rectangle of all pixels that were potentially modified.</returns>
    PixelRect Apply(FogMask mask, int x1, int y1, int x2, int y2, BrushSettings settings, byte[]? snapshot = null);
}
