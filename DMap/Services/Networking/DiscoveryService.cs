using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Networking;

/// <summary>
/// Default implementation of <see cref="IDiscoveryService"/> using UDP broadcast on a fixed port.
/// Packet format: "DMAP" magic (4 bytes) | SessionId (16 bytes) | TCP port (4 bytes) |
/// name byte length (4 bytes) | UTF-8 machine name.
/// </summary>
public sealed class DiscoveryService : IDiscoveryService
{
    /// <summary>UDP port used for both sending and receiving discovery broadcast packets.</summary>
    const int DiscoveryPort = 19876;

    static readonly byte[] _magic = "DMAP"u8.ToArray();

    UdpClient? _udpClient;
    CancellationTokenSource? _cts;

    /// <inheritdoc/>
    public event EventHandler<DiscoveredDm>? DmDiscovered;

    /// <inheritdoc/>
    public Task StartBroadcastingAsync(MapSession session, int tcpPort, CancellationToken ct)
    {
        StopCurrentSession();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _udpClient = new UdpClient();
        _udpClient.EnableBroadcast = true;
        var udpClient = _udpClient;
        var token = _cts.Token;

        var packet = BuildBroadcastPacket(session, tcpPort);
        var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await udpClient.SendAsync(packet, endpoint, token);
                    await Task.Delay(2000, token);
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
        }, token);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StartListeningAsync(CancellationToken ct)
    {
        StopCurrentSession();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _udpClient = CreateBroadcastListener();
        _udpClient.EnableBroadcast = true;
        var udpClient = _udpClient;
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await udpClient.ReceiveAsync(token);
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
        }, token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a UDP listener that can share the discovery port with other in-process
    /// player windows listening for the same DM broadcasts.
    /// </summary>
    static UdpClient CreateBroadcastListener()
    {
        var client = new UdpClient(AddressFamily.InterNetwork);
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
        return client;
    }

    /// <summary>
    /// Constructs a broadcast packet containing the magic bytes, session ID, TCP port, and hostname.
    /// </summary>
    static byte[] BuildBroadcastPacket(MapSession session, int tcpPort)
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

    /// <summary>
    /// Parses a received UDP datagram and returns a <see cref="DiscoveredDm"/> if the packet
    /// is valid, or <see langword="null"/> if the magic bytes are missing or the packet is truncated.
    /// </summary>
    static DiscoveredDm? ParseBroadcastPacket(byte[] data, IPEndPoint sender)
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

    /// <inheritdoc/>
    public void Dispose()
    {
        StopCurrentSession();
    }

    void StopCurrentSession()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _udpClient?.Dispose();
        _udpClient = null;
    }
}
