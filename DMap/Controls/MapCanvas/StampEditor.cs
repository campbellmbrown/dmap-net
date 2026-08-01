using System;
using System.Collections.Generic;

using Avalonia;

using DMap.Models;

namespace DMap.Controls.MapCanvas;

/// <summary>Owns stamp hit testing, handle geometry, dragging, resizing, rotation, and bounds clamping.</summary>
public sealed class StampEditor
{
    const double MinSize = 12;

    StampDragMode _dragMode;
    StampHandle _activeHandle;
    Point _dragStartMap;
    Rect _dragStartRect;
    double _dragStartRotationDegrees;
    double _dragStartPointerAngleDegrees;

    public bool IsDragging { get; private set; }

    public StampDragMode DragMode => _dragMode;

    public Rect GetRect(StampInstance stamp) =>
        new(stamp.X, stamp.Y, stamp.Width, stamp.Height);

    public Point GetCenter(StampInstance stamp) =>
        new(stamp.X + stamp.Width / 2.0, stamp.Y + stamp.Height / 2.0);

    public double GetRotationRadians(StampInstance stamp) =>
        DegreesToRadians(stamp.RotationDegrees);

    public Point GetHandlePoint(StampInstance stamp, StampHandle handle, double zoomLevel)
    {
        var rect = GetRect(stamp);
        var rotateHandleOffset = 28 / Math.Max(zoomLevel, 0.01);
        var localPoint = handle switch
        {
            StampHandle.TopLeft => rect.TopLeft,
            StampHandle.Top => new Point(rect.X + rect.Width / 2.0, rect.Y),
            StampHandle.TopRight => rect.TopRight,
            StampHandle.Right => new Point(rect.Right, rect.Y + rect.Height / 2.0),
            StampHandle.BottomRight => rect.BottomRight,
            StampHandle.Bottom => new Point(rect.X + rect.Width / 2.0, rect.Bottom),
            StampHandle.BottomLeft => rect.BottomLeft,
            StampHandle.Left => new Point(rect.X, rect.Y + rect.Height / 2.0),
            StampHandle.Rotate => new Point(rect.X + rect.Width / 2.0, rect.Y - rotateHandleOffset),
            _ => GetCenter(stamp),
        };

        return RotatePoint(localPoint, GetCenter(stamp), stamp.RotationDegrees);
    }

    public StampInstance? HitTest(IList<StampInstance>? stamps, Point mapPosition)
    {
        if (stamps is null)
            return null;

        for (var i = stamps.Count - 1; i >= 0; i--)
        {
            var stamp = stamps[i];
            if (Contains(stamp, mapPosition))
                return stamp;
        }

        return null;
    }

    public bool TryBeginHandleDrag(StampInstance? selectedStamp, Point mapPosition, double zoomLevel)
    {
        if (selectedStamp is null || !TryHitHandle(selectedStamp, mapPosition, zoomLevel, out var handle))
            return false;

        BeginDrag(
            selectedStamp,
            mapPosition,
            handle == StampHandle.Rotate ? StampDragMode.Rotate : StampDragMode.Resize,
            handle);
        return true;
    }

    public void BeginMoveDrag(StampInstance stamp, Point mapPosition) =>
        BeginDrag(stamp, mapPosition, StampDragMode.Move, StampHandle.None);

    public void UpdateDrag(StampInstance? stamp, Point mapPosition, Size? mapSize, bool preserveAspect)
    {
        if (!IsDragging || stamp is null)
            return;

        if (_dragMode == StampDragMode.Rotate)
        {
            stamp.RotationDegrees = GetRotationAfterDrag(stamp, mapPosition);
            return;
        }

        var rect = _dragMode == StampDragMode.Resize
            ? ResizeRect(mapPosition, preserveAspect)
            : MoveRect(mapPosition);

        ApplyRect(stamp, ClampRect(rect, mapSize));
    }

    public void EndDrag()
    {
        IsDragging = false;
        _dragMode = StampDragMode.None;
        _activeHandle = StampHandle.None;
    }

    public void Cancel() => EndDrag();

    public Rect ClampRect(Rect rect, Size? mapSize)
    {
        var width = Math.Max(MinSize, rect.Width);
        var height = Math.Max(MinSize, rect.Height);
        var x = rect.X;
        var y = rect.Y;

        if (mapSize is { } size)
        {
            width = Math.Min(width, Math.Max(MinSize, size.Width));
            height = Math.Min(height, Math.Max(MinSize, size.Height));
            x = Math.Clamp(x, 0, Math.Max(0, size.Width - width));
            y = Math.Clamp(y, 0, Math.Max(0, size.Height - height));
        }

        return new Rect(x, y, width, height);
    }

    void BeginDrag(StampInstance stamp, Point mapPosition, StampDragMode mode, StampHandle handle)
    {
        IsDragging = true;
        _dragMode = mode;
        _activeHandle = handle;
        _dragStartMap = mapPosition;
        _dragStartRect = GetRect(stamp);
        _dragStartRotationDegrees = stamp.RotationDegrees;
        _dragStartPointerAngleDegrees = GetPointerAngleDegrees(stamp, mapPosition);
    }

    bool TryHitHandle(StampInstance stamp, Point mapPosition, double zoomLevel, out StampHandle handle)
    {
        var threshold = Math.Max(4, 10 / Math.Max(zoomLevel, 0.01));
        var handles = new[]
        {
            StampHandle.Rotate,
            StampHandle.TopLeft,
            StampHandle.Top,
            StampHandle.TopRight,
            StampHandle.Right,
            StampHandle.BottomRight,
            StampHandle.Bottom,
            StampHandle.BottomLeft,
            StampHandle.Left,
        };

        foreach (var candidate in handles)
        {
            if (mapPosition.IsWithinSquare(GetHandlePoint(stamp, candidate, zoomLevel), threshold))
            {
                handle = candidate;
                return true;
            }
        }

        handle = StampHandle.None;
        return false;
    }

    bool Contains(StampInstance stamp, Point mapPosition) =>
        GetRect(stamp).Contains(UnrotatePoint(mapPosition, GetCenter(stamp), stamp.RotationDegrees));

    void ApplyRect(StampInstance stamp, Rect rect)
    {
        stamp.X = rect.X;
        stamp.Y = rect.Y;
        stamp.Width = rect.Width;
        stamp.Height = rect.Height;
    }

    Rect MoveRect(Point mapPosition)
    {
        var delta = mapPosition - _dragStartMap;
        return new Rect(
            _dragStartRect.X + delta.X,
            _dragStartRect.Y + delta.Y,
            _dragStartRect.Width,
            _dragStartRect.Height);
    }

    Rect ResizeRect(Point mapPosition, bool preserveAspect)
    {
        var (xSign, ySign) = GetHandleSigns(_activeHandle);
        var affectsWidth = xSign != 0;
        var affectsHeight = ySign != 0;
        var center = _dragStartRect.Center;
        var (xAxis, yAxis) = GetRotatedAxes(_dragStartRotationDegrees);
        var anchor = Add(
            Add(center, Scale(xAxis, -xSign * _dragStartRect.Width / 2.0)),
            Scale(yAxis, -ySign * _dragStartRect.Height / 2.0));
        var pointerFromAnchor = mapPosition - anchor;
        var width = affectsWidth
            ? Math.Max(MinSize, xSign * Dot(pointerFromAnchor, xAxis))
            : _dragStartRect.Width;
        var height = affectsHeight
            ? Math.Max(MinSize, ySign * Dot(pointerFromAnchor, yAxis))
            : _dragStartRect.Height;

        if (preserveAspect && IsCornerHandle(_activeHandle))
        {
            var aspect = _dragStartRect.Width / Math.Max(MinSize, _dragStartRect.Height);
            if (width / height > aspect)
                width = height * aspect;
            else
                height = width / aspect;
        }

        var nextCenter = anchor;
        if (affectsWidth)
            nextCenter = Add(nextCenter, Scale(xAxis, xSign * width / 2.0));
        if (affectsHeight)
            nextCenter = Add(nextCenter, Scale(yAxis, ySign * height / 2.0));

        return new Rect(nextCenter.X - width / 2.0, nextCenter.Y - height / 2.0, width, height);
    }

    double GetRotationAfterDrag(StampInstance stamp, Point mapPosition)
    {
        var angle = GetPointerAngleDegrees(stamp, mapPosition);
        return NormalizeDegrees(_dragStartRotationDegrees + angle - _dragStartPointerAngleDegrees);
    }

    double GetPointerAngleDegrees(StampInstance stamp, Point mapPosition) =>
        GetAngleDegrees(GetCenter(stamp), mapPosition);

    static bool IsCornerHandle(StampHandle handle) =>
        handle is StampHandle.TopLeft or StampHandle.TopRight or StampHandle.BottomRight or StampHandle.BottomLeft;

    static (int X, int Y) GetHandleSigns(StampHandle handle) =>
        handle switch
        {
            StampHandle.TopLeft => (-1, -1),
            StampHandle.Top => (0, -1),
            StampHandle.TopRight => (1, -1),
            StampHandle.Right => (1, 0),
            StampHandle.BottomRight => (1, 1),
            StampHandle.Bottom => (0, 1),
            StampHandle.BottomLeft => (-1, 1),
            StampHandle.Left => (-1, 0),
            _ => (1, 1),
        };

    static double GetAngleDegrees(Point center, Point point) =>
        Math.Atan2(point.Y - center.Y, point.X - center.X) * 180.0 / Math.PI;

    static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    static double DegreesToRadians(double degrees) =>
        degrees * Math.PI / 180.0;

    static Point RotatePoint(Point point, Point center, double degrees)
    {
        var radians = DegreesToRadians(degrees);
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var x = point.X - center.X;
        var y = point.Y - center.Y;

        return new Point(
            center.X + x * cos - y * sin,
            center.Y + x * sin + y * cos);
    }

    static Point UnrotatePoint(Point point, Point center, double degrees) =>
        RotatePoint(point, center, -degrees);

    static (Vector XAxis, Vector YAxis) GetRotatedAxes(double degrees)
    {
        var radians = DegreesToRadians(degrees);
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return (new Vector(cos, sin), new Vector(-sin, cos));
    }

    static Point Add(Point point, Vector vector) =>
        new(point.X + vector.X, point.Y + vector.Y);

    static Vector Scale(Vector vector, double scale) =>
        new(vector.X * scale, vector.Y * scale);

    static double Dot(Vector left, Vector right) =>
        left.X * right.X + left.Y * right.Y;
}
