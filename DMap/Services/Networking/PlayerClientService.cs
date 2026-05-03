using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Networking;

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
        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms);

        var guidBytes = reader.ReadBytes(16);
        var sessionId = new Guid(guidBytes);
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();

        var data = new byte[width * height];
        using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
        deflate.ReadExactly(data, 0, data.Length);

        var session = new MapSession(sessionId, width, height);
        SessionInfoReceived?.Invoke(this, session);

        var mask = new FogMask(width, height, data);
        FogFullReceived?.Invoke(this, mask);
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
