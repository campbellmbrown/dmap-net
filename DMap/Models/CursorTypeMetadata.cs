using System;

namespace DMap.Models;

/// <summary>
/// Shared metadata for cursor icon assets and render anchoring.
/// </summary>
public static class CursorTypeMetadata
{
    /// <summary>Returns the SVG asset file name for <paramref name="cursorType"/>.</summary>
    public static string GetIconFileName(CursorType cursorType) =>
        "Cursors/" + cursorType switch
        {
            CursorType.Crosshair => "crosshair.svg",
            CursorType.LaserPointer => "laser-pointer-dot-blur.svg",
            CursorType.LocateFixed => "locate-fixed.svg",
            CursorType.Scan => "scan.svg",
            CursorType.Circle => "circle.svg",
            CursorType.Plus => "plus.svg",
            _ => throw new ArgumentOutOfRangeException(nameof(cursorType), cursorType, null),
        };

    /// <summary>Returns the human-readable display name for <paramref name="cursorType"/>.</summary>
    public static string GetDisplayName(CursorType cursorType) =>
        cursorType switch
        {
            CursorType.Crosshair => "Crosshair",
            CursorType.LaserPointer => "Laser Pointer",
            CursorType.LocateFixed => "Locate Fixed",
            CursorType.Scan => "Scan",
            CursorType.Circle => "Circle",
            CursorType.Plus => "Plus",
            _ => throw new ArgumentOutOfRangeException(nameof(cursorType), cursorType, null),
        };
}
