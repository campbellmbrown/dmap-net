using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;
using DMap.Protocol;

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
    /// Raised when a <see cref="MessageType.Cursor"/> frame is received.
    /// The event argument carries the DM cursor position, icon, size, and visibility.
    /// </summary>
    event EventHandler<CursorPayload>? CursorReceived;

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

/// <summary>
/// Default implementation of <see cref="IPlayerClientService"/>. Connects to the DM's TCP
/// host and drives a background receive loop that deserializes incoming frames and fires the
/// appropriate events on the caller's thread.
/// </summary>
public sealed class PlayerClientService : IPlayerClientService
{
    TcpClient? _client;
    CancellationTokenSource? _cts;

    /// <inheritdoc/>
    public event EventHandler<MapSession>? SessionInfoReceived;

    /// <inheritdoc/>
    public event EventHandler<byte[]>? MapImageReceived;

    /// <inheritdoc/>
    public event EventHandler<FogDelta>? FogDeltaReceived;

    /// <inheritdoc/>
    public event EventHandler<FogMask>? FogFullReceived;

    /// <inheritdoc/>
    public event EventHandler<FogAppearancePayload>? FogAppearanceReceived;

    /// <inheritdoc/>
    public event EventHandler<ViewportPayload>? ViewportReceived;

    /// <inheritdoc/>
    public event EventHandler<CursorPayload>? CursorReceived;

    /// <inheritdoc/>
    public event EventHandler? Disconnected;

    /// <inheritdoc/>
    public async Task ConnectAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _client = new TcpClient();
        await _client.ConnectAsync(endpoint, _cts.Token);

        _ = Task.Run(() => ReceiveLoopAsync(_client.GetStream()), _cts.Token);
    }

    /// <summary>
    /// Continuously reads framed messages from <paramref name="stream"/> and dispatches
    /// them to the appropriate handler until the connection closes or the token is cancelled.
    /// Always fires <see cref="Disconnected"/> when the loop exits.
    /// </summary>
    async Task ReceiveLoopAsync(NetworkStream stream)
    {
        try
        {
            while (_cts is not null && !_cts.Token.IsCancellationRequested)
            {
                var frame = await ProtocolFraming.ReadFrameAsync(stream, _cts.Token);
                if (frame is null)
                    break;

                var (type, payload) = frame.Value;

                switch (type)
                {
                    case MessageType.SessionInfo:
                        HandleSessionInfo(payload);
                        break;
                    case MessageType.MapImage:
                        MapImageReceived?.Invoke(this, payload);
                        break;
                    case MessageType.FogDelta:
                        HandleFogDelta(payload);
                        break;
                    case MessageType.FogFull:
                        HandleFogFull(payload);
                        break;
                    case MessageType.FogAppearance:
                        FogAppearanceReceived?.Invoke(this, FogAppearancePayload.Deserialize(payload));
                        break;
                    case MessageType.Viewport:
                        ViewportReceived?.Invoke(this, ViewportPayload.Deserialize(payload));
                        break;
                    case MessageType.Cursor:
                        CursorReceived?.Invoke(this, CursorPayload.Deserialize(payload));
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch
        {
            // Connection lost
        }

        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deserializes a <see cref="MessageType.SessionInfo"/> payload, fires
    /// <see cref="SessionInfoReceived"/> with the session metadata, and then fires
    /// <see cref="FogFullReceived"/> with the embedded initial fog mask.
    /// </summary>
    void HandleSessionInfo(byte[] payload)
    {
        var sessionInfo = SessionInfoPayload.Deserialize(payload);
        SessionInfoReceived?.Invoke(this, sessionInfo.ToSession());
        FogFullReceived?.Invoke(this, sessionInfo.ToFogMask());
    }

    /// <summary>
    /// Deserializes a <see cref="MessageType.FogDelta"/> payload and fires <see cref="FogDeltaReceived"/>.
    /// </summary>
    void HandleFogDelta(byte[] payload)
    {
        var delta = FogDelta.Deserialize(payload);
        FogDeltaReceived?.Invoke(this, delta);
    }

    /// <summary>
    /// Deserializes a <see cref="MessageType.FogFull"/> payload and fires <see cref="FogFullReceived"/>.
    /// </summary>
    void HandleFogFull(byte[] payload)
    {
        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms);

        var width = reader.ReadInt32();
        var height = reader.ReadInt32();

        var data = new byte[width * height];
        using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
        deflate.ReadExactly(data, 0, data.Length);

        var mask = new FogMask(width, height, data);
        FogFullReceived?.Invoke(this, mask);
    }

    /// <inheritdoc/>
    public Task DisconnectAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _client?.Dispose();
        _client = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _client?.Dispose();
    }
}
