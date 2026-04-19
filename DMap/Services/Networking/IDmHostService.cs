using System;
using System.Threading;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Networking;

public interface IDmHostService : IDisposable
{
    int Port { get; }
    int ConnectedPlayerCount { get; }
    event EventHandler<int>? PlayerCountChanged;

    Task StartAsync(CancellationToken ct);
    Task SendMapImageAsync(byte[] imageBytes, CancellationToken ct);
    Task SendSessionInfoAsync(MapSession session, FogMask mask, CancellationToken ct);
    Task SendFogDeltaAsync(FogDelta delta, CancellationToken ct);
    Task SendFogFullAsync(FogMask mask, CancellationToken ct);
}
