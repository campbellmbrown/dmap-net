using System;

using Avalonia;
using Avalonia.Input;

using DMap.Protocol;

namespace DMap.Controls.MapCanvas;

/// <summary>Owns DM-side player viewport selection hit testing, dragging, resizing, and cursor selection.</summary>
public sealed class PlayerViewportEditor
{
    const double MinSize = 16;

    PlayerViewportHandle _activeHandle;
    Point _dragStartMap;
    Rect _dragStartRect;

    public bool IsDragging { get; private set; }

    public PlayerViewportHandle ActiveHandle => _activeHandle;

    public Rect GetRect(ViewportPayload viewport) =>
        new(
            viewport.CenterMapX - viewport.WidthMap / 2.0,
            viewport.CenterMapY - viewport.HeightMap / 2.0,
            viewport.WidthMap,
            viewport.HeightMap);

    public bool TryBeginDrag(Point mapPosition, ViewportPayload? viewport, double zoomLevel)
    {
        if (viewport is null || !viewport.HasMapRect)
            return false;

        var rect = GetRect(viewport);
        if (!TryHitHandle(mapPosition, rect, zoomLevel, out var handle))
        {
            if (rect.Contains(mapPosition))
                handle = PlayerViewportHandle.Move;
            else
                return false;
        }

        IsDragging = true;
        _activeHandle = handle;
        _dragStartMap = mapPosition;
        _dragStartRect = rect;
        return true;
    }

    public ViewportPayload? UpdateDrag(Point mapPosition, ViewportPayload? current, Size mapSize)
    {
        if (!IsDragging || current is null)
            return null;

        var rect = _activeHandle == PlayerViewportHandle.Move
            ? MoveRect(mapPosition)
            : ResizeRect(mapPosition);

        return CreatePayload(ClampRect(rect, mapSize), current);
    }

    public PlayerViewportHandle GetHoverHandle(Point mapPosition, ViewportPayload? viewport, double zoomLevel)
    {
        if (viewport is null || !viewport.HasMapRect)
            return PlayerViewportHandle.None;

        var rect = GetRect(viewport);
        if (TryHitHandle(mapPosition, rect, zoomLevel, out var handle))
            return handle;

        return rect.Contains(mapPosition)
            ? PlayerViewportHandle.Move
            : PlayerViewportHandle.None;
    }

    public Cursor GetCursor(Point mapPosition, ViewportPayload? viewport, double zoomLevel)
    {
        var handle = IsDragging ? _activeHandle : GetHoverHandle(mapPosition, viewport, zoomLevel);
        return handle switch
        {
            PlayerViewportHandle.Move => new Cursor(StandardCursorType.SizeAll),
            PlayerViewportHandle.Top or PlayerViewportHandle.Bottom => new Cursor(StandardCursorType.SizeNorthSouth),
            PlayerViewportHandle.Left or PlayerViewportHandle.Right => new Cursor(StandardCursorType.SizeWestEast),
            PlayerViewportHandle.TopLeft or PlayerViewportHandle.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
            PlayerViewportHandle.TopRight or PlayerViewportHandle.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
            _ => Cursor.Default,
        };
    }

    public void EndDrag()
    {
        IsDragging = false;
        _activeHandle = PlayerViewportHandle.None;
    }

    public void Cancel() => EndDrag();

    Rect MoveRect(Point mapPosition)
    {
        var delta = mapPosition - _dragStartMap;
        return new Rect(
            _dragStartRect.X + delta.X,
            _dragStartRect.Y + delta.Y,
            _dragStartRect.Width,
            _dragStartRect.Height);
    }

    Rect ResizeRect(Point mapPosition)
    {
        var left = _dragStartRect.Left;
        var top = _dragStartRect.Top;
        var right = _dragStartRect.Right;
        var bottom = _dragStartRect.Bottom;

        switch (_activeHandle)
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

    static ViewportPayload CreatePayload(Rect rect, ViewportPayload current) =>
        new()
        {
            CenterMapX = rect.X + rect.Width / 2.0,
            CenterMapY = rect.Y + rect.Height / 2.0,
            ZoomLevel = current.ZoomLevel <= 0 ? 1.0 : current.ZoomLevel,
            RotationQuarterTurns = current.RotationQuarterTurns,
            WidthMap = rect.Width,
            HeightMap = rect.Height,
            PaddingPixels = Math.Max(0, current.PaddingPixels),
            IsPixelSharpnessEnabled = current.IsPixelSharpnessEnabled,
        };

    static Rect ClampRect(Rect rect, Size mapSize)
    {
        var width = Math.Clamp(rect.Width, Math.Min(MinSize, mapSize.Width), mapSize.Width);
        var height = Math.Clamp(rect.Height, Math.Min(MinSize, mapSize.Height), mapSize.Height);
        var x = Math.Clamp(rect.X, 0, Math.Max(0, mapSize.Width - width));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, mapSize.Height - height));
        return new Rect(x, y, width, height);
    }

    static bool TryHitHandle(Point mapPosition, Rect rect, double zoomLevel, out PlayerViewportHandle handle)
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
