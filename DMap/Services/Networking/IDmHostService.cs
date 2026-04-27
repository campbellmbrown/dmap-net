using System;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Networking;

/// <summary>
/// TCP server that the DM (Dungeon Master) runs to stream map and fog data to connected player clients.
/// </summary>
public interface IDmHostService : IDisposable
{
    /// <summary>
    /// The TCP port the listener is bound to. Only valid after <see cref="StartAsync"/> completes.
    /// The OS assigns an ephemeral port when 0 is requested.
    /// </summary>
    int Port { get; }

    /// <summary>Number of player TCP connections currently open.</summary>
    int ConnectedPlayerCount { get; }

    /// <summary>
    /// Raised whenever a player connects or disconnects.
    /// The event argument is the new total connected player count.
    /// </summary>
    event EventHandler<int>? PlayerCountChanged;

    /// <summary>
    /// Starts the TCP listener and begins accepting incoming player connections in the background.
    /// </summary>
    /// <param name="ct">Cancellation token that stops the listener when cancelled.</param>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Broadcasts the map image to all currently connected players and caches it so
    /// newly connecting players also receive it on join.
    /// </summary>
    /// <param name="imageBytes">Raw image file bytes (PNG, JPEG, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendMapImageAsync(byte[] imageBytes, CancellationToken ct);

    /// <summary>
    /// Broadcasts the session info and the full current fog mask to all connected players,
    /// and caches the payload for players that connect later.
    /// </summary>
    /// <param name="session">Session identifier and map dimensions.</param>
    /// <param name="mask">Current fog mask to include in the handshake payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendSessionInfoAsync(MapSession session, FogMask mask, CancellationToken ct);

    /// <summary>
    /// Broadcasts a fog delta (incremental update) to all connected players.
    /// Deltas are not cached because new players receive the full fog state via <see cref="SendSessionInfoAsync"/>.
    /// </summary>
    /// <param name="delta">The changed fog region to transmit.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendFogDeltaAsync(FogDelta delta, CancellationToken ct);

    /// <summary>
    /// Broadcasts a full fog mask replacement to all connected players.
    /// </summary>
    /// <param name="mask">The complete fog mask to send.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendFogFullAsync(FogMask mask, CancellationToken ct);
}
