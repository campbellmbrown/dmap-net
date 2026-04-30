using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Networking;

/// <summary>
/// TCP client used by the player to connect to a DM host and receive map and fog updates.
/// </summary>
public interface IPlayerClientService : IDisposable
{
    /// <summary>
    /// Raised when a <see cref="MessageType.SessionInfo"/> frame is received.
    /// The event argument contains the session ID and map dimensions.
    /// The full fog mask for the session is delivered separately via <see cref="FogFullReceived"/>.
    /// </summary>
    event EventHandler<MapSession>? SessionInfoReceived;

    /// <summary>
    /// Raised when a <see cref="MessageType.MapImage"/> frame is received.
    /// The event argument is the raw image file bytes suitable for decoding into a bitmap.
    /// </summary>
    event EventHandler<byte[]>? MapImageReceived;

    /// <summary>
    /// Raised when a <see cref="MessageType.FogDelta"/> frame is received.
    /// The event argument is the deserialized incremental fog region.
    /// </summary>
    event EventHandler<FogDelta>? FogDeltaReceived;

    /// <summary>
    /// Raised when a <see cref="MessageType.FogFull"/> frame is received, or when session
    /// info is first received (which includes the full fog state).
    /// The event argument is a fully reconstructed fog mask.
    /// </summary>
    event EventHandler<FogMask>? FogFullReceived;

    /// <summary>
    /// Raised when a <see cref="MessageType.FogAppearance"/> frame is received.
    /// The event argument carries the fog type, colour, and texture seed.
    /// </summary>
    event EventHandler<FogAppearancePayload>? FogAppearanceReceived;

    /// <summary>
    /// Raised when a <see cref="MessageType.Viewport"/> frame is received.
    /// The event argument carries the DM camera center and zoom.
    /// </summary>
    event EventHandler<ViewportPayload>? ViewportReceived;

    /// <summary>
    /// Raised when the connection to the DM is closed, either because the DM disconnected,
    /// the network failed, or <see cref="DisconnectAsync"/> was called.
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Opens a TCP connection to the DM at <paramref name="endpoint"/> and starts the
    /// background receive loop.
    /// </summary>
    /// <param name="endpoint">IP address and port of the DM host.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ConnectAsync(IPEndPoint endpoint, CancellationToken ct);

    /// <summary>
    /// Cancels the receive loop and closes the TCP connection.
    /// </summary>
    Task DisconnectAsync();
}
