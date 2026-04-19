using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DMap.Models;

namespace DMap.Services.Networking;

public sealed record DiscoveredDm(string Name, IPEndPoint TcpEndPoint, Guid SessionId);

public interface IDiscoveryService : IDisposable
{
    Task StartBroadcastingAsync(MapSession session, int tcpPort, CancellationToken ct);
    Task StartListeningAsync(CancellationToken ct);
    event EventHandler<DiscoveredDm>? DmDiscovered;
}
