using System;

using Avalonia;

using DMap.Models;

namespace DMap.Services.History;

public sealed class FogDeltaCommand : IFogCommand
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    public PixelRect DirtyRect { get; }

    public FogDeltaCommand(PixelRect dirtyRect, byte[] before, byte[] after)
    {
        DirtyRect = dirtyRect;
        _before = before;
        _after = after;
    }

    public void Undo(FogMask mask) => ApplyRegion(mask, _before);
    public void Redo(FogMask mask) => ApplyRegion(mask, _after);

    private void ApplyRegion(FogMask mask, byte[] data)
    {
        for (var dy = 0; dy < DirtyRect.Height; dy++)
            Buffer.BlockCopy(data, dy * DirtyRect.Width,
                mask.Data, (DirtyRect.Y + dy) * mask.Width + DirtyRect.X,
                DirtyRect.Width);
    }

    public static byte[] CaptureFromMask(FogMask mask, PixelRect region)
    {
        var data = new byte[region.Width * region.Height];
        for (var dy = 0; dy < region.Height; dy++)
            Buffer.BlockCopy(mask.Data, (region.Y + dy) * mask.Width + region.X,
                data, dy * region.Width, region.Width);
        return data;
    }

    public static byte[] CaptureFromRaw(byte[] rawData, int maskWidth, PixelRect region)
    {
        var data = new byte[region.Width * region.Height];
        for (var dy = 0; dy < region.Height; dy++)
            Buffer.BlockCopy(rawData, (region.Y + dy) * maskWidth + region.X,
                data, dy * region.Width, region.Width);
        return data;
    }
}
