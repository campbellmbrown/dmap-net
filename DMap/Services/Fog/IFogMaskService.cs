using System;

using Avalonia;

using DMap.Models;
using DMap.Services.Brushes;
using DMap.Services.Networking;

namespace DMap.Services.Fog;

/// <summary>
/// Manages the fog-of-war mask for the current map session: initialization, painting,
/// shape fills, undo/redo snapshots, and applying network-received deltas.
/// </summary>
public interface IFogMaskService
{
    /// <summary>
    /// The current fog mask, or <see langword="null"/> when no map has been loaded yet.
    /// </summary>
    FogMask? Mask { get; }

    /// <summary>
    /// Raised after any modification to the mask. The event argument is the bounding
    /// rectangle of the changed region, which can be used to limit bitmap updates and redraws.
    /// </summary>
    event EventHandler<PixelRect>? MaskChanged;

    /// <summary>
    /// Allocates a new fully-fogged mask matching the given dimensions.
    /// Resets any in-progress stroke snapshot.
    /// </summary>
    /// <param name="width">Map width in pixels.</param>
    /// <param name="height">Map height in pixels.</param>
    void Initialize(int width, int height);

    /// <summary>
    /// Captures a snapshot of the current mask so that subsequent brush applications
    /// during this stroke are computed relative to the stroke start state, preventing
    /// opacity from accumulating on repeated passes over the same area.
    /// </summary>
    void BeginStroke();

    /// <summary>
    /// Finalises the current stroke and returns an <see cref="IFogCommand"/> that can
    /// be pushed onto the undo stack. Returns <see langword="null"/> if no pixels changed.
    /// </summary>
    IFogCommand? EndStroke();

    /// <summary>
    /// Applies a brush stroke from (<paramref name="x1"/>, <paramref name="y1"/>) to
    /// (<paramref name="x2"/>, <paramref name="y2"/>) using the provided brush and settings,
    /// accumulating the dirty region into the current stroke rectangle.
    /// </summary>
    /// <returns>Bounding rectangle of pixels that were modified.</returns>
    PixelRect ApplyBrush(IBrush brush, int x1, int y1, int x2, int y2, BrushSettings settings);

    /// <summary>
    /// Fills (or erases) a rectangular region of the mask with optional edge feathering.
    /// </summary>
    /// <param name="x1">First corner X in map pixels.</param>
    /// <param name="y1">First corner Y in map pixels.</param>
    /// <param name="x2">Opposite corner X in map pixels.</param>
    /// <param name="y2">Opposite corner Y in map pixels.</param>
    /// <param name="softness">Edge feathering in [0, 1]; 0 = hard edge.</param>
    /// <param name="opacity">Maximum alpha to apply in [0, 1].</param>
    /// <param name="erase">When <see langword="true"/>, removes fog instead of adding it.</param>
    /// <returns>Bounding rectangle of pixels that were modified.</returns>
    PixelRect ApplyRectangle(int x1, int y1, int x2, int y2, float softness, float opacity, bool erase = false);

    /// <summary>
    /// Fills (or erases) an elliptical region of the mask with optional edge feathering.
    /// </summary>
    /// <param name="x1">First corner X of the bounding box in map pixels.</param>
    /// <param name="y1">First corner Y of the bounding box in map pixels.</param>
    /// <param name="x2">Opposite corner X of the bounding box in map pixels.</param>
    /// <param name="y2">Opposite corner Y of the bounding box in map pixels.</param>
    /// <param name="softness">Edge feathering in [0, 1]; 0 = hard edge.</param>
    /// <param name="opacity">Maximum alpha to apply in [0, 1].</param>
    /// <param name="erase">When <see langword="true"/>, removes fog instead of adding it.</param>
    /// <returns>Bounding rectangle of pixels that were modified.</returns>
    PixelRect ApplyEllipse(int x1, int y1, int x2, int y2, float softness, float opacity, bool erase = false);

    /// <summary>Sets every pixel in the mask to 255 (fully revealed).</summary>
    void RevealAll();

    /// <summary>Sets every pixel in the mask to 0 (fully fogged).</summary>
    void RefogAll();

    /// <summary>
    /// Replaces the current mask entirely with <paramref name="mask"/> and raises
    /// <see cref="MaskChanged"/> for the full map rectangle.
    /// </summary>
    void Replace(FogMask mask);

    /// <summary>
    /// Merges a network-received fog delta into the mask using a max-blend: only pixels
    /// that are more revealed in the delta override the local value, so fog can never be
    /// re-added via a network update.
    /// </summary>
    void ApplyDelta(FogDelta delta);
}
