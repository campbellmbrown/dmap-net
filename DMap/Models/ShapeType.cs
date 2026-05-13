namespace DMap.Models;

/// <summary>
/// The geometric shape drawn by the Shape tool.
/// </summary>
public enum ShapeType
{
    /// <summary>Axis-aligned rectangle whose width and height may differ.</summary>
    Rectangle,

    /// <summary>Axis-aligned rectangle drawn outward from its center point.</summary>
    CenteredRectangle,

    /// <summary>Axis-aligned square (width == height, constrained during drag).</summary>
    Square,

    /// <summary>Axis-aligned square drawn outward from its center point.</summary>
    CenteredSquare,

    /// <summary>Ellipse whose horizontal and vertical radii may differ.</summary>
    Ellipse,

    /// <summary>Ellipse drawn outward from its center point.</summary>
    CenteredEllipse,

    /// <summary>Circle whose horizontal and vertical radii are equal (constrained during drag).</summary>
    Circle,

    /// <summary>Circle drawn outward from its center point.</summary>
    CenteredCircle,
}
