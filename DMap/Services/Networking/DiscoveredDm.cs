using System;
using System.Net;

namespace DMap.Services.Networking;

/// <summary>
/// Represents a DM session discovered via UDP broadcast on the local network.
/// </summary>
/// <param name="Name">Human-readable name of the host machine (usually its hostname).</param>
/// <param name="TcpEndPoint">IP endpoint of the DM's TCP server that players connect to.</param>
/// <param name="SessionId">Unique identifier of the session, used to de-duplicate repeated broadcasts.</param>
public sealed record DiscoveredDm(string Name, IPEndPoint TcpEndPoint, Guid SessionId);
