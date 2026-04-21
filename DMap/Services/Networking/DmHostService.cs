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

public sealed class DmHostService : IDmHostService
{
    TcpListener? _listener;
    readonly List<TcpClient> _clients = new();
    readonly Lock _clientsLock = new();
    CancellationTokenSource? _cts;

    byte[]? _pendingMapImage;
    byte[]? _pendingSessionInfo;

    public int Port { get; set; }
    public int ConnectedPlayerCount => _clients.Count;
    public event EventHandler<int>? PlayerCountChanged;

    public async Task StartAsync(CancellationToken ct)
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

        await Task.CompletedTask;
    }

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

            // Send pending data to new client
            if (_pendingSessionInfo is not null)
                await ProtocolFraming.WriteFrameAsync(stream, MessageType.SessionInfo, _pendingSessionInfo, default);

            if (_pendingMapImage is not null)
                await ProtocolFraming.WriteFrameAsync(stream, MessageType.MapImage, _pendingMapImage, default);

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

    public Task SendMapImageAsync(byte[] imageBytes, CancellationToken ct)
    {
        _pendingMapImage = imageBytes;
        return BroadcastAsync(MessageType.MapImage, imageBytes, ct);
    }

    public Task SendSessionInfoAsync(MapSession session, FogMask mask, CancellationToken ct)
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

        var payload = ms.ToArray();
        _pendingSessionInfo = payload;
        return BroadcastAsync(MessageType.SessionInfo, payload, ct);
    }

    public Task SendFogDeltaAsync(FogDelta delta, CancellationToken ct)
    {
        var payload = delta.Serialize();
        return BroadcastAsync(MessageType.FogDelta, payload, ct);
    }

    public Task SendFogFullAsync(FogMask mask, CancellationToken ct)
    {
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
