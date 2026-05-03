namespace DMap.Models;

/// <summary>
/// Cursor icon rendered by the DM cursor tool and mirrored to player windows.
/// </summary>
public enum CursorType
{
    /// <summary>Crosshair target cursor.</summary>
    Crosshair,

    /// <summary>Soft laser pointer dot.</summary>
    LaserPointer,

    /// <summary>Fixed-location target cursor.</summary>
    LocateFixed,

    /// <summary>Scan target cursor.</summary>
    Scan,

    /// <summary>Circle marker cursor.</summary>
    Circle,

    /// <summary>Plus marker cursor.</summary>
    Plus,
}
