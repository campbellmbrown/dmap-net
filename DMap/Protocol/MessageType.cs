namespace DMap.Protocol;

/// <summary>
/// Type of framed message exchanged between the DM host and player clients.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Initial session handshake carrying session ID, map dimensions, and the full fog mask.
    /// </summary>
    SessionInfo = 1,

    /// <summary>
    /// Raw image bytes for the map background, sent once per session.
    /// </summary>
    MapImage = 2,

    /// <summary>
    /// A compressed rectangular region of fog mask changes (incremental update).
    /// </summary>
    FogDelta = 3,

    /// <summary>
    /// A compressed full fog mask replacement.
    /// </summary>
    FogFull = 4,

    /// <summary>
    /// Fog overlay appearance (type, colour, and texture seed).
    /// </summary>
    FogAppearance = 5,

    /// <summary>
    /// Viewport camera state (map center and zoom) broadcast from the DM to players.
    /// </summary>
    Viewport = 6,

    /// <summary>
    /// Player-visible cursor state broadcast from the DM to players.
    /// </summary>
    Cursor = 7,

    /// <summary>
    /// Grid overlay settings broadcast from the DM to players.
    /// </summary>
    GridSettings = 8,
}
