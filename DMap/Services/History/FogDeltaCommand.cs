using System;

using Avalonia;

using DMap.Models;

namespace DMap.Services.History;

/// <summary>
/// An <see cref="IFogCommand"/> that stores a rectangular snapshot of the fog mask
/// before and after an operation so the region can be restored in either direction.
/// </summary>
public sealed class FogDeltaCommand : IFogCommand
{
    readonly byte[] _before;
    readonly byte[] _after;

    /// <inheritdoc/>
    public PixelRect DirtyRect { get; }

    /// <summary>
    /// Creates a command that can flip the pixels inside <paramref name="dirtyRect"/>
    /// between two captured states.
    /// </summary>
    /// <param name="dirtyRect">The region covered by the before/after data.</param>
    /// <param name="before">Pixel values captured before the operation.</param>
    /// <param name="after">Pixel values captured after the operation.</param>
    public FogDeltaCommand(PixelRect dirtyRect, byte[] before, byte[] after)
    {
        DirtyRect = dirtyRect;
        _before = before;
        _after = after;
    }

    /// <inheritdoc/>
    public void Undo(FogMask mask) => ApplyRegion(mask, _before);

    /// <inheritdoc/>
    public void Redo(FogMask mask) => ApplyRegion(mask, _after);

    /// <summary>
    /// Copies <paramref name="data"/> (a row-major sub-image of width <see cref="PixelRect.Width"/>)
    /// into the corresponding rows of <paramref name="mask"/>.
    /// </summary>
    void ApplyRegion(FogMask mask, byte[] data)
    {
        for (var dy = 0; dy < DirtyRect.Height; dy++)
            Buffer.BlockCopy(data, dy * DirtyRect.Width,
                mask.Data, (DirtyRect.Y + dy) * mask.Width + DirtyRect.X,
                DirtyRect.Width);
    }

    /// <summary>
    /// Extracts the pixel values inside <paramref name="region"/> from a live fog mask.
    /// </summary>
    /// <param name="mask">The mask to read from.</param>
    /// <param name="region">The rectangle to capture.</param>
    /// <returns>A row-major byte array of size region.Width * region.Height.</returns>
    public static byte[] CaptureFromMask(FogMask mask, PixelRect region)
    {
        var data = new byte[region.Width * region.Height];
        for (var dy = 0; dy < region.Height; dy++)
            Buffer.BlockCopy(mask.Data, (region.Y + dy) * mask.Width + region.X,
                data, dy * region.Width, region.Width);
        return data;
    }

    /// <summary>
    /// Extracts the pixel values inside <paramref name="region"/> from a raw byte array
    /// that uses the same row-major layout as a fog mask with the given width.
    /// Used to capture the before-state from a pre-stroke snapshot.
    /// </summary>
    /// <param name="rawData">Raw pixel data with layout identical to <see cref="FogMask.Data"/>.</param>
    /// <param name="maskWidth">Width of the mask the raw data belongs to.</param>
    /// <param name="region">The rectangle to capture.</param>
    /// <returns>A row-major byte array of size region.Width * region.Height.</returns>
    public static byte[] CaptureFromRaw(byte[] rawData, int maskWidth, PixelRect region)
    {
        var data = new byte[region.Width * region.Height];
        for (var dy = 0; dy < region.Height; dy++)
            Buffer.BlockCopy(rawData, (region.Y + dy) * maskWidth + region.X,
                data, dy * region.Width, region.Width);
        return data;
    }
}
