using System;

namespace DMap.Controls.MapCanvas;

/// <summary>
/// Event arguments for a brush stroke segment, carrying the start and end coordinates
/// in map pixels and whether the stroke is erasing fog.
/// </summary>
public class BrushStrokeEventArgs : EventArgs
{
    /// <summary>Start X coordinate in map pixels.</summary>
    public int MapX1 { get; init; }

    /// <summary>Start Y coordinate in map pixels.</summary>
    public int MapY1 { get; init; }

    /// <summary>End X coordinate in map pixels.</summary>
    public int MapX2 { get; init; }

    /// <summary>End Y coordinate in map pixels.</summary>
    public int MapY2 { get; init; }

    /// <summary><see langword="true"/> when the stroke is removing fog; <see langword="false"/> when revealing.</summary>
    public bool IsErasing { get; init; }
}
