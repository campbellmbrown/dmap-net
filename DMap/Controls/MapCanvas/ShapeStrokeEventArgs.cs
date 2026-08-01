using System;

namespace DMap.Controls.MapCanvas;

/// <summary>
/// Event arguments for a completed shape drag gesture, carrying the bounding box corners
/// in map pixels and whether the shape is erasing fog.
/// </summary>
public class ShapeStrokeEventArgs : EventArgs
{
    /// <summary>First corner X in map pixels.</summary>
    public int MapX1 { get; init; }

    /// <summary>First corner Y in map pixels.</summary>
    public int MapY1 { get; init; }

    /// <summary>Opposite corner X in map pixels.</summary>
    public int MapX2 { get; init; }

    /// <summary>Opposite corner Y in map pixels.</summary>
    public int MapY2 { get; init; }

    /// <summary><see langword="true"/> when the shape is removing fog; <see langword="false"/> when revealing.</summary>
    public bool IsErasing { get; init; }
}
