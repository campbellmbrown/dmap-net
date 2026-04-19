using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Networking;

public sealed class DiscoveryService : IDiscoveryService
{
    private const int DiscoveryPort = 19876;
    private static readonly byte[] _magic = "DMAP"u8.ToArray();

    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;

    public event EventHandler<DiscoveredDm>? DmDiscovered;

    public async Task StartBroadcastingAsync(MapSession session, int tcpPort, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _udpClient = new UdpClient();
        _udpClient.EnableBroadcast = true;

        var packet = BuildBroadcastPacket(session, tcpPort);
        var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await _udpClient.SendAsync(packet, endpoint, _cts.Token);
                    await Task.Delay(2000, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore transient UDP errors
                }
            }
        }, _cts.Token);

        await Task.CompletedTask;
    }

    public async Task StartListeningAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _udpClient = new UdpClient(DiscoveryPort);
        _udpClient.EnableBroadcast = true;

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(_cts.Token);
                    var dm = ParseBroadcastPacket(result.Buffer, result.RemoteEndPoint);
                    if (dm is not null)
                        DmDiscovered?.Invoke(this, dm);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore malformed packets
                }
            }
        }, _cts.Token);

        await Task.CompletedTask;
    }

    private static byte[] BuildBroadcastPacket(MapSession session, int tcpPort)
    {
        var name = Environment.MachineName;
        var nameBytes = Encoding.UTF8.GetBytes(name);

        var packet = new byte[_magic.Length + 16 + 4 + 4 + nameBytes.Length];
        var offset = 0;

        Buffer.BlockCopy(_magic, 0, packet, offset, _magic.Length);
        offset += _magic.Length;

        session.SessionId.TryWriteBytes(packet.AsSpan(offset));
        offset += 16;

        BitConverter.TryWriteBytes(packet.AsSpan(offset, 4), tcpPort);
        offset += 4;

        BitConverter.TryWriteBytes(packet.AsSpan(offset, 4), nameBytes.Length);
        offset += 4;

        Buffer.BlockCopy(nameBytes, 0, packet, offset, nameBytes.Length);

        return packet;
    }

    private static DiscoveredDm? ParseBroadcastPacket(byte[] data, IPEndPoint sender)
    {
        if (data.Length < _magic.Length + 16 + 4 + 4)
            return null;

        for (var i = 0; i < _magic.Length; i++)
        {
            if (data[i] != _magic[i])
                return null;
        }

        var offset = _magic.Length;

        var sessionId = new Guid(data.AsSpan(offset, 16));
        offset += 16;

        var tcpPort = BitConverter.ToInt32(data, offset);
        offset += 4;

        var nameLength = BitConverter.ToInt32(data, offset);
        offset += 4;

        if (offset + nameLength > data.Length)
            return null;

        var name = Encoding.UTF8.GetString(data, offset, nameLength);

        var tcpEndPoint = new IPEndPoint(sender.Address, tcpPort);
        return new DiscoveredDm(name, tcpEndPoint, sessionId);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _udpClient?.Dispose();
    }
}
