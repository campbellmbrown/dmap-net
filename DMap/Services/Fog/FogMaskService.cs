using System;

using Avalonia;

using DMap.Models;
using DMap.Services.Brushes;
using DMap.Services.History;
using DMap.Services.Networking;

namespace DMap.Services.Fog;

public sealed class FogMaskService : IFogMaskService
{
    private const string MaskNotInitialized = "Fog mask not initialized.";

    public FogMask? Mask { get; private set; }

    private byte[]? _snapshot;
    private PixelRect? _strokeDirtyRect;

    public event EventHandler<PixelRect>? MaskChanged;

    public void Initialize(int width, int height)
    {
        Mask = new FogMask(width, height);
        _snapshot = null;
        _strokeDirtyRect = null;
    }

    public void BeginStroke()
    {
        if (Mask is null)
            return;

        _snapshot = new byte[Mask.Data.Length];
        Buffer.BlockCopy(Mask.Data, 0, _snapshot, 0, Mask.Data.Length);
        _strokeDirtyRect = null;
    }

    public IFogCommand? EndStroke()
    {
        var snapshot = _snapshot;
        var strokeRect = _strokeDirtyRect;
        _snapshot = null;
        _strokeDirtyRect = null;

        if (Mask is null || snapshot is null || strokeRect is null)
            return null;

        var before = FogDeltaCommand.CaptureFromRaw(snapshot, Mask.Width, strokeRect.Value);
        var after = FogDeltaCommand.CaptureFromMask(Mask, strokeRect.Value);
        return new FogDeltaCommand(strokeRect.Value, before, after);
    }

    public PixelRect ApplyBrush(IBrush brush, int x1, int y1, int x2, int y2, BrushSettings settings)
    {
        if (Mask is null)
            throw new InvalidOperationException(MaskNotInitialized);

        var dirtyRect = brush.Apply(Mask, x1, y1, x2, y2, settings, _snapshot);
        if (dirtyRect.Width > 0 && dirtyRect.Height > 0)
            _strokeDirtyRect = _strokeDirtyRect.HasValue
                ? UnionRects(_strokeDirtyRect.Value, dirtyRect)
                : dirtyRect;
        MaskChanged?.Invoke(this, dirtyRect);
        return dirtyRect;
    }

    public PixelRect ApplyRectangle(int x1, int y1, int x2, int y2, float softness, float opacity, bool erase = false)
    {
        if (Mask is null)
            throw new InvalidOperationException(MaskNotInitialized);

        var minX = Math.Max(0, Math.Min(x1, x2));
        var minY = Math.Max(0, Math.Min(y1, y2));
        var maxX = Math.Min(Mask.Width - 1, Math.Max(x1, x2));
        var maxY = Math.Min(Mask.Height - 1, Math.Max(y1, y2));

        var feather = softness * Math.Min(maxX - minX, maxY - minY) / 2.0f;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var coverage = RectCoverage(x, y, minX, minY, maxX, maxY, feather);
                var snapshotValue = Mask[x, y];
                BrushHelper.ApplyPixel(Mask, x, y, coverage, erase, snapshotValue, opacity);
            }
        }

        var dirtyRect = new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        MaskChanged?.Invoke(this, dirtyRect);
        return dirtyRect;
    }

    public PixelRect ApplyEllipse(int x1, int y1, int x2, int y2, float softness, float opacity, bool erase = false)
    {
        if (Mask is null)
            throw new InvalidOperationException(MaskNotInitialized);

        var minX = Math.Max(0, Math.Min(x1, x2));
        var minY = Math.Max(0, Math.Min(y1, y2));
        var maxX = Math.Min(Mask.Width - 1, Math.Max(x1, x2));
        var maxY = Math.Min(Mask.Height - 1, Math.Max(y1, y2));

        var rx = (maxX - minX) / 2.0;
        var ry = (maxY - minY) / 2.0;

        if (rx < 0.5 || ry < 0.5)
            return new PixelRect(0, 0, 0, 0);

        var cx = minX + rx;
        var cy = minY + ry;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var nx = (x - cx) / rx;
                var ny = (y - cy) / ry;
                var dist = Math.Sqrt(nx * nx + ny * ny);

                if (dist > 1.0)
                    continue;

                var coverage = (softness > 0 && dist > 1.0 - softness)
                    ? (1.0 - dist) / softness
                    : 1.0;

                var snapshotValue = Mask[x, y];
                BrushHelper.ApplyPixel(Mask, x, y, coverage, erase, snapshotValue, opacity);
            }
        }

        var dirtyRect = new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        MaskChanged?.Invoke(this, dirtyRect);
        return dirtyRect;
    }

    public void RevealAll()
    {
        if (Mask is null)
            return;

        Array.Fill(Mask.Data, (byte)255);
        MaskChanged?.Invoke(this, new PixelRect(0, 0, Mask.Width, Mask.Height));
    }

    public void RefogAll()
    {
        if (Mask is null)
            return;

        Array.Fill(Mask.Data, (byte)0);
        MaskChanged?.Invoke(this, new PixelRect(0, 0, Mask.Width, Mask.Height));
    }

    public void Replace(FogMask mask)
    {
        Mask = mask;
        MaskChanged?.Invoke(this, new PixelRect(0, 0, mask.Width, mask.Height));
    }

    public void ApplyDelta(FogDelta delta)
    {
        if (Mask is null)
            throw new InvalidOperationException(MaskNotInitialized);

        for (var dy = 0; dy < delta.Height; dy++)
        {
            for (var dx = 0; dx < delta.Width; dx++)
            {
                var mx = delta.X + dx;
                var my = delta.Y + dy;
                if (mx >= 0 && mx < Mask.Width && my >= 0 && my < Mask.Height)
                {
                    var value = delta.Data[dy * delta.Width + dx];
                    if (value > Mask[mx, my])
                        Mask[mx, my] = value;
                }
            }
        }

        MaskChanged?.Invoke(this, new PixelRect(delta.X, delta.Y, delta.Width, delta.Height));
    }

    private static PixelRect UnionRects(PixelRect a, PixelRect b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new PixelRect(x, y, right - x, bottom - y);
    }

    private static double RectCoverage(int x, int y, int minX, int minY, int maxX, int maxY, float feather)
    {
        if (feather <= 0)
            return 1.0;

        var minDist = Math.Min(
            Math.Min(x - minX, maxX - x),
            Math.Min(y - minY, maxY - y));
        return Math.Min(1.0f, minDist / feather);
    }
}
