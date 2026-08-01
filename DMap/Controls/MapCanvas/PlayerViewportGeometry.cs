using System;

using Avalonia;

using DMap.Dm;
using DMap.Protocol;

namespace DMap.Controls.MapCanvas;

/// <summary>Map-space rectangle math for editing the player viewport selection.</summary>
internal static class PlayerViewportGeometry
{
    const double MinSize = 16;

    public static Rect GetRect(ViewportPayload viewport) =>
        new(
            viewport.CenterMapX - viewport.WidthMap / 2.0,
            viewport.CenterMapY - viewport.HeightMap / 2.0,
            viewport.WidthMap,
            viewport.HeightMap);

    public static ViewportPayload CreatePayload(Rect rect, ViewportPayload current) =>
        new()
        {
            CenterMapX = rect.X + rect.Width / 2.0,
            CenterMapY = rect.Y + rect.Height / 2.0,
            ZoomLevel = current.ZoomLevel <= 0 ? 1.0 : current.ZoomLevel,
            RotationQuarterTurns = current.RotationQuarterTurns,
            WidthMap = rect.Width,
            HeightMap = rect.Height,
            PaddingPixels = Math.Max(0, current.PaddingPixels),
        };

    public static Rect MoveRect(Rect dragStartRect, Point dragStartMap, Point mapPosition)
    {
        var delta = mapPosition - dragStartMap;
        return new Rect(
            dragStartRect.X + delta.X,
            dragStartRect.Y + delta.Y,
            dragStartRect.Width,
            dragStartRect.Height);
    }

    public static Rect ResizeRect(Rect dragStartRect, PlayerViewportHandle handle, Point mapPosition)
    {
        var left = dragStartRect.Left;
        var top = dragStartRect.Top;
        var right = dragStartRect.Right;
        var bottom = dragStartRect.Bottom;

        switch (handle)
        {
            case PlayerViewportHandle.TopLeft:
                left = Math.Min(mapPosition.X, right - MinSize);
                top = Math.Min(mapPosition.Y, bottom - MinSize);
                break;
            case PlayerViewportHandle.Top:
                top = Math.Min(mapPosition.Y, bottom - MinSize);
                break;
            case PlayerViewportHandle.TopRight:
                right = Math.Max(mapPosition.X, left + MinSize);
                top = Math.Min(mapPosition.Y, bottom - MinSize);
                break;
            case PlayerViewportHandle.Right:
                right = Math.Max(mapPosition.X, left + MinSize);
                break;
            case PlayerViewportHandle.BottomRight:
                right = Math.Max(mapPosition.X, left + MinSize);
                bottom = Math.Max(mapPosition.Y, top + MinSize);
                break;
            case PlayerViewportHandle.Bottom:
                bottom = Math.Max(mapPosition.Y, top + MinSize);
                break;
            case PlayerViewportHandle.BottomLeft:
                left = Math.Min(mapPosition.X, right - MinSize);
                bottom = Math.Max(mapPosition.Y, top + MinSize);
                break;
            case PlayerViewportHandle.Left:
                left = Math.Min(mapPosition.X, right - MinSize);
                break;
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    public static Rect ClampRect(Rect rect, Size mapSize)
    {
        var width = Math.Clamp(rect.Width, Math.Min(MinSize, mapSize.Width), mapSize.Width);
        var height = Math.Clamp(rect.Height, Math.Min(MinSize, mapSize.Height), mapSize.Height);
        var x = Math.Clamp(rect.X, 0, Math.Max(0, mapSize.Width - width));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, mapSize.Height - height));
        return new Rect(x, y, width, height);
    }

    public static bool TryHitHandle(Point mapPosition, Rect rect, double zoomLevel, out PlayerViewportHandle handle)
    {
        var threshold = Math.Max(4, 10 / Math.Max(zoomLevel, 0.01));

        if (mapPosition.IsWithinSquare(rect.TopLeft, threshold))
            return SetHandle(PlayerViewportHandle.TopLeft, out handle);
        if (mapPosition.IsWithinSquare(new Point(rect.X + rect.Width / 2.0, rect.Y), threshold))
            return SetHandle(PlayerViewportHandle.Top, out handle);
        if (mapPosition.IsWithinSquare(rect.TopRight, threshold))
            return SetHandle(PlayerViewportHandle.TopRight, out handle);
        if (mapPosition.IsWithinSquare(new Point(rect.Right, rect.Y + rect.Height / 2.0), threshold))
            return SetHandle(PlayerViewportHandle.Right, out handle);
        if (mapPosition.IsWithinSquare(rect.BottomRight, threshold))
            return SetHandle(PlayerViewportHandle.BottomRight, out handle);
        if (mapPosition.IsWithinSquare(new Point(rect.X + rect.Width / 2.0, rect.Bottom), threshold))
            return SetHandle(PlayerViewportHandle.Bottom, out handle);
        if (mapPosition.IsWithinSquare(rect.BottomLeft, threshold))
            return SetHandle(PlayerViewportHandle.BottomLeft, out handle);
        if (mapPosition.IsWithinSquare(new Point(rect.X, rect.Y + rect.Height / 2.0), threshold))
            return SetHandle(PlayerViewportHandle.Left, out handle);

        var expanded = rect.Inflate(threshold);
        if (expanded.Contains(mapPosition))
        {
            if (Math.Abs(mapPosition.Y - rect.Top) <= threshold)
                return SetHandle(PlayerViewportHandle.Top, out handle);
            if (Math.Abs(mapPosition.X - rect.Right) <= threshold)
                return SetHandle(PlayerViewportHandle.Right, out handle);
            if (Math.Abs(mapPosition.Y - rect.Bottom) <= threshold)
                return SetHandle(PlayerViewportHandle.Bottom, out handle);
            if (Math.Abs(mapPosition.X - rect.Left) <= threshold)
                return SetHandle(PlayerViewportHandle.Left, out handle);
        }

        handle = PlayerViewportHandle.None;
        return false;
    }

    static bool SetHandle(PlayerViewportHandle value, out PlayerViewportHandle handle)
    {
        handle = value;
        return true;
    }

}
