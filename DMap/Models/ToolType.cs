namespace DMap.Models;

/// <summary>
/// The active editing tool on the DM canvas.
/// </summary>
public enum ToolType
{
    /// <summary>Free-hand brush that paints or erases fog along a stroke.</summary>
    Brush,

    /// <summary>Shape tool that draws a filled rectangle or ellipse in one drag gesture.</summary>
    Shape,

    /// <summary>Pan tool that scrolls the map viewport without modifying the fog.</summary>
    Pan,

    /// <summary>Fog appearance settings (type and colour). Does not modify the fog mask.</summary>
    Fog,
}
