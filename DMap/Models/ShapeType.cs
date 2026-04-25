namespace DMap.Models;

/// <summary>
/// The geometric shape drawn by the Shape tool.
/// </summary>
public enum ShapeType
{
    /// <summary>Axis-aligned rectangle whose width and height may differ.</summary>
    Rectangle,

    /// <summary>Axis-aligned square (width == height, constrained during drag).</summary>
    Square,

    /// <summary>Ellipse whose horizontal and vertical radii may differ.</summary>
    Ellipse,

    /// <summary>Circle whose horizontal and vertical radii are equal (constrained during drag).</summary>
    Circle,
}
