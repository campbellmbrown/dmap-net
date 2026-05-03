using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Networking;

/// <summary>
/// TCP server implementation of <see cref="IDmHostService"/>. Listens on an OS-assigned
/// ephemeral port and manages a list of connected player <see cref="TcpClient"/> instances.
/// Pending map image and session info are cached so newly connecting players receive the
/// current state without an explicit re-send from the DM.
/// </summary>
public sealed class DmHostService : IDmHostService
{
    TcpListener? _listener;
    readonly List<TcpClient> _clients = new();
    readonly Lock _clientsLock = new();
    CancellationTokenSource? _cts;

    /// <summary>Cached map image payload sent to new clients on connect, or <see langword="null"/> before the first map is loaded.</summary>
    byte[]? _pendingMapImage;

    /// <summary>Cached session info payload sent to new clients on connect, or <see langword="null"/> before hosting starts.</summary>
    byte[]? _pendingSessionInfo;

    /// <summary>Cached session metadata used to rebuild the late-join handshake after fog changes.</summary>
    MapSession? _pendingSession;

    /// <summary>Cached authoritative fog mask for players that connect after incremental updates.</summary>
    FogMask? _pendingFogMask;

    /// <summary>Cached fog appearance payload sent to new clients on connect, or <see langword="null"/> if never set.</summary>
    byte[]? _pendingFogAppearance;

    /// <summary>Cached viewport payload sent to new clients on connect, or <see langword="null"/> if never set.</summary>
    byte[]? _pendingViewport;

    /// <summary>Cached cursor payload sent to new clients on connect, or <see langword="null"/> if never set.</summary>
    byte[]? _pendingCursor;

    /// <inheritdoc/>
    public int Port { get; set; }

    /// <inheritdoc/>
    public int ConnectedPlayerCount => _clients.Count;

    /// <inheritdoc/>
    public event EventHandler<int>? PlayerCountChanged;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new TcpListener(IPAddress.Any, 0);
        _listener.Start();

        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _ = HandleClientAsync(client);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the lifetime of a single player connection: sends the cached session info and
    /// map image on join, then keeps the connection alive until the client disconnects or the
    /// service is cancelled.
    /// </summary>
    async Task HandleClientAsync(TcpClient client)
    {
        lock (_clientsLock)
        {
            _clients.Add(client);
        }

        PlayerCountChanged?.Invoke(this, _clients.Count);

        try
        {
            var stream = client.GetStream();

            // Snapshot the pending payloads under the lock so reads are race-free.
            byte[]? pendingSessionInfo;
            byte[]? pendingMapImage;
            byte[]? pendingFogAppearance;
            byte[]? pendingViewport;
            byte[]? pendingCursor;
            lock (_clientsLock)
            {
                pendingSessionInfo = _pendingSession is not null && _pendingFogMask is not null
                    ? SerializeSessionInfo(_pendingSession, _pendingFogMask)
                    : _pendingSessionInfo;
                pendingMapImage = _pendingMapImage;
                pendingFogAppearance = _pendingFogAppearance;
                pendingViewport = _pendingViewport;
                pendingCursor = _pendingCursor;
            }

            if (pendingSessionInfo is not null)
                await ProtocolFraming.WriteFrameAsync(stream, MessageType.SessionInfo, pendingSessionInfo, default);

            if (pendingFogAppearance is not null)
                await ProtocolFraming.WriteFrameAsync(stream, MessageType.FogAppearance, pendingFogAppearance, default);

            if (pendingMapImage is not null)
                await ProtocolFraming.WriteFrameAsync(stream, MessageType.MapImage, pendingMapImage, default);

            if (pendingViewport is not null)
                await ProtocolFraming.WriteFrameAsync(stream, MessageType.Viewport, pendingViewport, default);

            if (pendingCursor is not null)
                await ProtocolFraming.WriteFrameAsync(stream, MessageType.Cursor, pendingCursor, default);

            // Keep connection alive until cancelled or disconnected
            var buffer = new byte[1];
            while (client.Connected && (_cts is null || !_cts.Token.IsCancellationRequested))
            {
                try
                {
                    // Read to detect disconnect
                    var read = await stream.ReadAsync(buffer, _cts?.Token ?? default);
                    if (read == 0)
                        break;
                }
                catch
                {
                    break;
                }
            }
        }
        finally
        {
            lock (_clientsLock)
            {
                _clients.Remove(client);
            }

            client.Dispose();
            PlayerCountChanged?.Invoke(this, _clients.Count);
        }
    }

    /// <inheritdoc/>
    public Task SendMapImageAsync(byte[] imageBytes, CancellationToken ct)
    {
        lock (_clientsLock)
        {
            _pendingMapImage = imageBytes;
        }
        return BroadcastAsync(MessageType.MapImage, imageBytes, ct);
    }

    /// <inheritdoc/>
    public Task SendSessionInfoAsync(MapSession session, FogMask mask, CancellationToken ct)
    {
        var cachedMask = mask.Clone();
        var payload = SerializeSessionInfo(session, cachedMask);
        lock (_clientsLock)
        {
            _pendingSession = session;
            _pendingFogMask = cachedMask;
            _pendingSessionInfo = payload;
        }
        return BroadcastAsync(MessageType.SessionInfo, payload, ct);
    }

    /// <inheritdoc/>
    public Task SendFogDeltaAsync(FogDelta delta, CancellationToken ct)
    {
        var payload = delta.Serialize();
        lock (_clientsLock)
        {
            if (_pendingSession is not null && _pendingFogMask is not null)
                ApplyDeltaToPendingMask(delta);
        }

        return BroadcastAsync(MessageType.FogDelta, payload, ct);
    }

    /// <inheritdoc/>
    public Task SendFogAppearanceAsync(FogAppearancePayload appearance, CancellationToken ct)
    {
        var payload = appearance.Serialize();
        lock (_clientsLock)
        {
            _pendingFogAppearance = payload;
        }
        return BroadcastAsync(MessageType.FogAppearance, payload, ct);
    }

    /// <inheritdoc/>
    public Task SendViewportAsync(ViewportPayload viewport, CancellationToken ct)
    {
        var payload = viewport.Serialize();
        lock (_clientsLock)
        {
            _pendingViewport = payload;
        }

        return BroadcastAsync(MessageType.Viewport, payload, ct);
    }

    /// <inheritdoc/>
    public Task SendCursorAsync(CursorPayload cursor, CancellationToken ct)
    {
        var payload = cursor.Serialize();
        lock (_clientsLock)
        {
            _pendingCursor = payload;
        }

        return BroadcastAsync(MessageType.Cursor, payload, ct);
    }

    /// <inheritdoc/>
    public Task SendFogFullAsync(FogMask mask, CancellationToken ct)
    {
        lock (_clientsLock)
        {
            if (_pendingSession is not null)
            {
                _pendingFogMask = mask.Clone();
                _pendingSessionInfo = SerializeSessionInfo(_pendingSession, _pendingFogMask);
            }
        }

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(mask.Width);
        writer.Write(mask.Height);

        using (var deflate = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(mask.Data, 0, mask.Data.Length);
        }

        return BroadcastAsync(MessageType.FogFull, ms.ToArray(), ct);
    }

    /// <summary>Serializes session metadata plus a full fog mask for the initial player handshake.</summary>
    static byte[] SerializeSessionInfo(MapSession session, FogMask mask)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(session.SessionId.ToByteArray());
        writer.Write(session.MapWidth);
        writer.Write(session.MapHeight);

        using (var deflate = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(mask.Data, 0, mask.Data.Length);
        }

        return ms.ToArray();
    }

    /// <summary>Applies an outgoing fog delta to the cached full mask used for late joiners.</summary>
    void ApplyDeltaToPendingMask(FogDelta delta)
    {
        if (_pendingFogMask is null)
            return;

        for (var dy = 0; dy < delta.Height; dy++)
        {
            for (var dx = 0; dx < delta.Width; dx++)
            {
                var mx = delta.X + dx;
                var my = delta.Y + dy;
                if (mx >= 0 && mx < _pendingFogMask.Width && my >= 0 && my < _pendingFogMask.Height)
                    _pendingFogMask[mx, my] = delta.Data[dy * delta.Width + dx];
            }
        }
    }

    /// <summary>
    /// Sends a framed message to every currently connected player, taking a snapshot of the
    /// client list under the lock to avoid holding it during I/O. Failed writes are silently
    /// ignored because <see cref="HandleClientAsync"/> will clean up the disconnected client.
    /// </summary>
    async Task BroadcastAsync(MessageType type, byte[] payload, CancellationToken ct)
    {
        List<TcpClient> snapshot;
        lock (_clientsLock)
        {
            snapshot = new List<TcpClient>(_clients);
        }

        foreach (var client in snapshot)
        {
            try
            {
                if (client.Connected)
                    await ProtocolFraming.WriteFrameAsync(client.GetStream(), type, payload, ct);
            }
            catch
            {
                // Client will be cleaned up by HandleClientAsync
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _listener?.Stop();

        lock (_clientsLock)
        {
            foreach (var client in _clients)
                client.Dispose();
            _clients.Clear();
        }
    }
}
