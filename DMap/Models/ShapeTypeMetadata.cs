using System;

namespace DMap.Models;

/// <summary>
/// Shared metadata and classification helpers for shape tools.
/// </summary>
public static class ShapeTypeMetadata
{
    /// <summary>Returns the SVG asset file name for <paramref name="shapeType"/>.</summary>
    public static string GetIconFileName(ShapeType shapeType) =>
        shapeType switch
        {
            ShapeType.Rectangle => "rectangle-horizontal.svg",
            ShapeType.CenteredRectangle => "rectangle-horizontal-dot.svg",
            ShapeType.Square => "square.svg",
            ShapeType.CenteredSquare => "square-dot.svg",
            ShapeType.Ellipse => "ellipse.svg",
            ShapeType.CenteredEllipse => "ellipse-dot.svg",
            ShapeType.Circle => "circle.svg",
            ShapeType.CenteredCircle => "circle-dot.svg",
            _ => throw new ArgumentOutOfRangeException(nameof(shapeType), shapeType, null),
        };

    /// <summary>Returns the human-readable display name for <paramref name="shapeType"/>.</summary>
    public static string GetDisplayName(ShapeType shapeType) =>
        shapeType switch
        {
            ShapeType.Rectangle => "Rectangle",
            ShapeType.CenteredRectangle => "Centered Rectangle",
            ShapeType.Square => "Square",
            ShapeType.CenteredSquare => "Centered Square",
            ShapeType.Ellipse => "Ellipse",
            ShapeType.CenteredEllipse => "Centered Ellipse",
            ShapeType.Circle => "Circle",
            ShapeType.CenteredCircle => "Centered Circle",
            _ => throw new ArgumentOutOfRangeException(nameof(shapeType), shapeType, null),
        };

    /// <summary>Returns <see langword="true"/> when the shape drag is center-anchored.</summary>
    public static bool IsCentered(ShapeType shapeType) =>
        shapeType is ShapeType.CenteredRectangle
            or ShapeType.CenteredSquare
            or ShapeType.CenteredEllipse
            or ShapeType.CenteredCircle;

    /// <summary>Returns <see langword="true"/> when width and height are constrained to match.</summary>
    public static bool UsesEqualAspectConstraint(ShapeType shapeType) =>
        shapeType is ShapeType.Square
            or ShapeType.CenteredSquare
            or ShapeType.Circle
            or ShapeType.CenteredCircle;

    /// <summary>Returns <see langword="true"/> when the shape uses ellipse filling/rendering.</summary>
    public static bool IsEllipse(ShapeType shapeType) =>
        shapeType is ShapeType.Ellipse
            or ShapeType.CenteredEllipse
            or ShapeType.Circle
            or ShapeType.CenteredCircle;

    /// <summary>Returns <see langword="true"/> when the shape is a rectangle or square variant.</summary>
    public static bool SupportsCornerRadius(ShapeType shapeType) =>
        shapeType is ShapeType.Rectangle
            or ShapeType.CenteredRectangle
            or ShapeType.Square
            or ShapeType.CenteredSquare;

    /// <summary>Clamps the requested corner radius so it fits within the current shape bounds.</summary>
    public static double GetEffectiveCornerRadius(double cornerRadius, double width, double height) =>
        Math.Clamp(cornerRadius, 0, Math.Min(width, height) / 2.0);
}
