using System;

using Avalonia;

namespace DMap.Models;

/// <summary>
/// Shared drag-constraint logic for square and circle shape tools.
/// </summary>
internal static class ShapeConstraintHelper
{
    /// <summary>
    /// Constrains a screen-space shape drag endpoint so square and circle tools size against a
    /// 45 degree support line instead of the smaller axis delta.
    /// </summary>
    internal static Point ConstrainEndPoint(ShapeType shapeType, Point start, Point end)
    {
        if (shapeType is not ShapeType.Square and not ShapeType.Circle)
            return end;

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var side = (Math.Abs(dx) + Math.Abs(dy)) / 2.0;
        return new Point(
            start.X + GetConstrainedDelta(dx, dy, side),
            start.Y + GetConstrainedDelta(dy, dx, side));
    }

    /// <summary>
    /// Constrains a map-space shape drag so square and circle tools size against a 45 degree
    /// support line instead of the smaller axis delta.
    /// </summary>
    internal static (int X1, int Y1, int X2, int Y2) ConstrainBounds(ShapeType shapeType, int x1, int y1, int x2, int y2)
    {
        if (shapeType is not ShapeType.Square and not ShapeType.Circle)
            return (x1, y1, x2, y2);

        var dx = x2 - x1;
        var dy = y2 - y1;
        var side = (Math.Abs(dx) + Math.Abs(dy)) / 2;
        return (
            x1,
            y1,
            x1 + GetConstrainedDelta(dx, dy, side),
            y1 + GetConstrainedDelta(dy, dx, side));
    }

    static double GetConstrainedDelta(double primaryDelta, double secondaryDelta, double side)
    {
        var sign = Math.Sign(primaryDelta);
        if (sign == 0)
            sign = Math.Sign(secondaryDelta);

        return sign * side;
    }

    static int GetConstrainedDelta(int primaryDelta, int secondaryDelta, int side)
    {
        var sign = Math.Sign(primaryDelta);
        if (sign == 0)
            sign = Math.Sign(secondaryDelta);

        return sign * side;
    }
}
