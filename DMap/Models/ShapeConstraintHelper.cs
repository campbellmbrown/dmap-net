using System;

using Avalonia;

namespace DMap.Models;

/// <summary>
/// Shared drag normalization logic for centered and aspect-constrained shape tools.
/// </summary>
internal static class ShapeConstraintHelper
{
    /// <summary>
    /// Normalizes a screen-space shape drag into two opposing bounding-box corners.
    /// </summary>
    internal static (Point Start, Point End) NormalizeBounds(ShapeType shapeType, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;

        if (ShapeTypeMetadata.UsesEqualAspectConstraint(shapeType))
        {
            var side = (Math.Abs(dx) + Math.Abs(dy)) / 2.0;
            var originalDx = dx;
            var originalDy = dy;
            dx = GetConstrainedDelta(originalDx, originalDy, side);
            dy = GetConstrainedDelta(originalDy, originalDx, side);
        }

        return ShapeTypeMetadata.IsCentered(shapeType)
            ? (new Point(start.X - dx, start.Y - dy), new Point(start.X + dx, start.Y + dy))
            : (start, new Point(start.X + dx, start.Y + dy));
    }

    /// <summary>
    /// Normalizes a map-space shape drag into two opposing bounding-box corners.
    /// </summary>
    internal static (int X1, int Y1, int X2, int Y2) NormalizeBounds(ShapeType shapeType, int x1, int y1, int x2, int y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;

        if (ShapeTypeMetadata.UsesEqualAspectConstraint(shapeType))
        {
            var side = (Math.Abs(dx) + Math.Abs(dy)) / 2;
            var originalDx = dx;
            var originalDy = dy;
            dx = GetConstrainedDelta(originalDx, originalDy, side);
            dy = GetConstrainedDelta(originalDy, originalDx, side);
        }

        return ShapeTypeMetadata.IsCentered(shapeType)
            ? (x1 - dx, y1 - dy, x1 + dx, y1 + dy)
            : (x1, y1, x1 + dx, y1 + dy);
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
