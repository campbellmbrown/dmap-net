using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Networking;

public sealed class PlayerClientService : IPlayerClientService
{
    TcpClient? _client;
    CancellationTokenSource? _cts;

    public event EventHandler<MapSession>? SessionInfoReceived;
    public event EventHandler<byte[]>? MapImageReceived;
    public event EventHandler<FogDelta>? FogDeltaReceived;
    public event EventHandler<FogMask>? FogFullReceived;
    public event EventHandler? Disconnected;

    public async Task ConnectAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _client = new TcpClient();
        await _client.ConnectAsync(endpoint, _cts.Token);

        _ = Task.Run(() => ReceiveLoopAsync(_client.GetStream()), _cts.Token);
    }

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

    void HandleFogDelta(byte[] payload)
    {
        var delta = FogDelta.Deserialize(payload);
        FogDeltaReceived?.Invoke(this, delta);
    }

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

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        _client?.Dispose();
        _client = null;
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _client?.Dispose();
    }
}
