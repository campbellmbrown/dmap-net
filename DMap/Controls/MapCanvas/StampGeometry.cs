using System;

using Avalonia;

using DMap.Dm;
using DMap.Models;

namespace DMap.Controls.MapCanvas;

/// <summary>Map-space stamp bounds, handle, hit-test, and transform helpers.</summary>
internal static class StampGeometry
{
    const double MinSize = 12;

    public static Rect GetRect(StampInstance stamp) =>
        new(stamp.X, stamp.Y, stamp.Width, stamp.Height);

    public static Point GetCenter(StampInstance stamp) =>
        new(stamp.X + stamp.Width / 2.0, stamp.Y + stamp.Height / 2.0);

    public static void ApplyRect(StampInstance stamp, Rect rect)
    {
        stamp.X = rect.X;
        stamp.Y = rect.Y;
        stamp.Width = rect.Width;
        stamp.Height = rect.Height;
    }

    public static Rect MoveRect(Rect dragStartRect, Point dragStartMap, Point mapPosition)
    {
        var delta = mapPosition - dragStartMap;
        return new Rect(
            dragStartRect.X + delta.X,
            dragStartRect.Y + delta.Y,
            dragStartRect.Width,
            dragStartRect.Height);
    }

    public static Rect ResizeRect(
        Rect dragStartRect,
        double dragStartRotationDegrees,
        StampHandle activeHandle,
        Point mapPosition,
        bool preserveAspect)
    {
        var (xSign, ySign) = GetHandleSigns(activeHandle);
        var affectsWidth = xSign != 0;
        var affectsHeight = ySign != 0;
        var center = dragStartRect.Center;
        var (xAxis, yAxis) = GetRotatedAxes(dragStartRotationDegrees);
        var anchor = Add(
            Add(center, Scale(xAxis, -xSign * dragStartRect.Width / 2.0)),
            Scale(yAxis, -ySign * dragStartRect.Height / 2.0));
        var pointerFromAnchor = mapPosition - anchor;
        var width = affectsWidth
            ? Math.Max(MinSize, xSign * Dot(pointerFromAnchor, xAxis))
            : dragStartRect.Width;
        var height = affectsHeight
            ? Math.Max(MinSize, ySign * Dot(pointerFromAnchor, yAxis))
            : dragStartRect.Height;

        if (preserveAspect && IsCornerHandle(activeHandle))
        {
            var aspect = dragStartRect.Width / Math.Max(MinSize, dragStartRect.Height);
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

    public static Rect ClampRect(Rect rect, Size? mapSize)
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

    public static Point GetHandlePoint(StampInstance stamp, StampHandle handle, double zoomLevel)
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

    public static bool TryHitHandle(Point mapPosition, StampInstance stamp, double zoomLevel, out StampHandle handle)
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

    public static bool Contains(StampInstance stamp, Point mapPosition) =>
        GetRect(stamp).Contains(UnrotatePoint(mapPosition, GetCenter(stamp), stamp.RotationDegrees));

    public static double GetRotationRadians(StampInstance stamp) =>
        DegreesToRadians(stamp.RotationDegrees);

    public static double GetPointerAngleDegrees(StampInstance stamp, Point mapPosition) =>
        GetAngleDegrees(GetCenter(stamp), mapPosition);

    public static double GetRotationAfterDrag(
        StampInstance stamp,
        Point mapPosition,
        double dragStartRotationDegrees,
        double dragStartPointerAngleDegrees)
    {
        var angle = GetPointerAngleDegrees(stamp, mapPosition);
        return NormalizeDegrees(dragStartRotationDegrees + angle - dragStartPointerAngleDegrees);
    }

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
