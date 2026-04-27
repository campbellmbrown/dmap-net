using System;

namespace DMap.Models;

/// <summary>
/// Identifies an active DM hosting session together with the dimensions of its map.
/// Transmitted to player clients during the initial handshake so they can allocate
/// a fog mask of the correct size.
/// </summary>
/// <param name="SessionId">Unique identifier for this session, generated when a map is loaded.</param>
/// <param name="MapWidth">Width of the map image in pixels.</param>
/// <param name="MapHeight">Height of the map image in pixels.</param>
public sealed record MapSession(Guid SessionId, int MapWidth, int MapHeight);
