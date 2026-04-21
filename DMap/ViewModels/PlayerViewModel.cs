using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using DMap.Models;
using DMap.Services.Fog;
using DMap.Services.Networking;

using ReactiveUI;

namespace DMap.ViewModels;

public class PlayerViewModel : ViewModelBase, IDisposable
{
    readonly IFogMaskService _fogService;
    readonly IDiscoveryService _discoveryService;
    readonly IPlayerClientService _clientService;

    public ObservableCollection<DiscoveredDm> DiscoveredDms { get; } = new();

    public DiscoveredDm? SelectedDm
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsConnected
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsConnecting
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string StatusText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Searching for DM sessions...";

    public Bitmap? MapImage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public FogMask? FogMask
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double OffsetX
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double OffsetY
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double ZoomLevel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 1.0;

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }

    public event EventHandler<PixelRect>? FogUpdated;

    public PlayerViewModel(IFogMaskService fogService, IDiscoveryService discoveryService, IPlayerClientService clientService)
    {
        _fogService = fogService;
        _discoveryService = discoveryService;
        _clientService = clientService;

        _discoveryService.DmDiscovered += OnDmDiscovered;
        _clientService.SessionInfoReceived += OnSessionInfoReceived;
        _clientService.MapImageReceived += OnMapImageReceived;
        _clientService.FogDeltaReceived += OnFogDeltaReceived;
        _clientService.FogFullReceived += OnFogFullReceived;
        _clientService.Disconnected += OnDisconnected;

        var canConnect = this.WhenAnyValue(
            x => x.SelectedDm, x => x.IsConnected, x => x.IsConnecting,
            (dm, connected, connecting) => dm is not null && !connected && !connecting);

        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);

        var canDisconnect = this.WhenAnyValue(x => x.IsConnected);
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync, canDisconnect);
    }

    public async Task StartDiscoveryAsync()
    {
        await _discoveryService.StartListeningAsync(default);
    }

    void OnDmDiscovered(object? sender, DiscoveredDm dm)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var existing in DiscoveredDms)
            {
                if (existing.SessionId == dm.SessionId)
                    return;
            }

            DiscoveredDms.Add(dm);
        });
    }

    async Task ConnectAsync()
    {
        if (SelectedDm is null)
            return;

        IsConnecting = true;
        StatusText = $"Connecting to {SelectedDm.Name}...";

        try
        {
            await _clientService.ConnectAsync(SelectedDm.TcpEndPoint, default);
            IsConnected = true;
            StatusText = $"Connected to {SelectedDm.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    async Task DisconnectAsync()
    {
        await _clientService.DisconnectAsync();
        IsConnected = false;
        MapImage = null;
        FogMask = null;
        StatusText = "Disconnected. Searching for DM sessions...";
    }

    void OnSessionInfoReceived(object? sender, MapSession session)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _fogService.Initialize(session.MapWidth, session.MapHeight);
            FogMask = _fogService.Mask;
            FogUpdated?.Invoke(this, new PixelRect(0, 0, session.MapWidth, session.MapHeight));
        });
    }

    void OnMapImageReceived(object? sender, byte[] imageBytes)
    {
        Dispatcher.UIThread.Post(() =>
        {
            using var stream = new MemoryStream(imageBytes);
            MapImage = new Bitmap(stream);
        });
    }

    void OnFogDeltaReceived(object? sender, FogDelta delta)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _fogService.ApplyDelta(delta);
            FogUpdated?.Invoke(this, new PixelRect(delta.X, delta.Y, delta.Width, delta.Height));
        });
    }

    void OnFogFullReceived(object? sender, FogMask mask)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _fogService.Replace(mask);
            FogMask = _fogService.Mask;
            FogUpdated?.Invoke(this, new PixelRect(0, 0, mask.Width, mask.Height));
        });
    }

    void OnDisconnected(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = false;
            StatusText = "Disconnected from DM. Searching for sessions...";
        });
    }

    public void Dispose()
    {
        (_discoveryService as IDisposable)?.Dispose();
        (_clientService as IDisposable)?.Dispose();
    }
}
