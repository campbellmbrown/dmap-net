using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DMap.Models;

namespace DMap.Services.Networking;

public interface IPlayerClientService : IDisposable
{
    event EventHandler<MapSession>? SessionInfoReceived;
    event EventHandler<byte[]>? MapImageReceived;
    event EventHandler<FogDelta>? FogDeltaReceived;
    event EventHandler<FogMask>? FogFullReceived;
    event EventHandler? Disconnected;

    Task ConnectAsync(IPEndPoint endpoint, CancellationToken ct);
    Task DisconnectAsync();
}
