using System;

using Avalonia;

namespace DMap.Controls;

/// <summary>Generic geometry extensions for Avalonia primitives.</summary>
public static class GeometryExtensions
{
    /// <summary>
    /// Creates a rectangle from two points.
    /// </summary>
    /// <param name="start">The first corner of the rectangle.</param>
    /// <param name="end">The opposite corner of the rectangle.</param>
    /// <returns>A rectangle defined by the two points.</returns>
    public static Rect MakeRect(this Point start, Point end) =>
        new(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y)
        );

    /// <summary>
    /// Calculates the midpoint between two points.
    /// </summary>
    /// <param name="point">The first point.</param>
    /// <param name="other">The second point.</param>
    /// <returns>A new point representing the midpoint between the two points.</returns>
    public static Point Midpoint(this Point point, Point other) =>
        new(
            (point.X + other.X) / 2.0,
            (point.Y + other.Y) / 2.0
        );

    /// <summary>
    /// Determines if the point is within a square area defined by a center point and a threshold.
    /// </summary>
    /// <param name="point">The point to check.</param>
    /// <param name="center">The center point of the square area.</param>
    /// <param name="threshold">The distance from <paramref name="center"/> that defines the square area.</param>
    /// <returns>True if the point is within the square area; otherwise, false.</returns>
    public static bool IsWithinSquare(this Point point, Point center, double threshold) =>
        Math.Abs(point.X - center.X) <= threshold &&
        Math.Abs(point.Y - center.Y) <= threshold;
}
