using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Networking;

/// <summary>
/// Represents a DM session discovered via UDP broadcast on the local network.
/// </summary>
/// <param name="Name">Human-readable name of the host machine (usually its hostname).</param>
/// <param name="TcpEndPoint">IP endpoint of the DM's TCP server that players connect to.</param>
/// <param name="SessionId">Unique identifier of the session, used to de-duplicate repeated broadcasts.</param>
public sealed record DiscoveredDm(string Name, IPEndPoint TcpEndPoint, Guid SessionId);

/// <summary>
/// UDP-based discovery service that allows DM hosts to announce their presence on the
/// local network and player clients to listen for those announcements.
/// </summary>
public interface IDiscoveryService : IDisposable
{
    /// <summary>
    /// Starts broadcasting session availability packets via UDP every two seconds.
    /// Should only be called by the DM host side.
    /// </summary>
    /// <param name="session">Session metadata to embed in each broadcast packet.</param>
    /// <param name="tcpPort">TCP port players should connect to.</param>
    /// <param name="ct">Cancellation token that stops broadcasting when cancelled.</param>
    Task StartBroadcastingAsync(MapSession session, int tcpPort, CancellationToken ct);

    /// <summary>
    /// Starts listening for DM broadcast packets on the discovery port.
    /// Should only be called by the player client side.
    /// </summary>
    /// <param name="ct">Cancellation token that stops listening when cancelled.</param>
    Task StartListeningAsync(CancellationToken ct);

    /// <summary>
    /// Raised each time a valid DM broadcast packet is received.
    /// Consumers are responsible for de-duplicating by <see cref="DiscoveredDm.SessionId"/>
    /// since the same DM broadcasts repeatedly.
    /// </summary>
    event EventHandler<DiscoveredDm>? DmDiscovered;
}
