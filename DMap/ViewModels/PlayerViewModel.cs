using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;

using Avalonia;
using Avalonia.Media.Imaging;

using DMap.Models;
using DMap.Services.Fog;
using DMap.Services.Networking;

using ReactiveUI;

namespace DMap.ViewModels;

public class PlayerViewModel : ViewModelBase, IDisposable
{
    private readonly IFogMaskService _fogService;

    private IDiscoveryService? _discoveryService;
    private IPlayerClientService? _clientService;

    public ObservableCollection<DiscoveredDm> DiscoveredDms { get; } = new();

    private DiscoveredDm? _selectedDm;
    public DiscoveredDm? SelectedDm
    {
        get => _selectedDm;
        set => this.RaiseAndSetIfChanged(ref _selectedDm, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    private bool _isConnecting;
    public bool IsConnecting
    {
        get => _isConnecting;
        private set => this.RaiseAndSetIfChanged(ref _isConnecting, value);
    }

    private string _statusText = "Searching for DM sessions...";
    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private Bitmap? _mapImage;
    public Bitmap? MapImage
    {
        get => _mapImage;
        private set => this.RaiseAndSetIfChanged(ref _mapImage, value);
    }

    private FogMask? _fogMask;
    public FogMask? FogMask
    {
        get => _fogMask;
        private set => this.RaiseAndSetIfChanged(ref _fogMask, value);
    }

    private double _offsetX;
    public double OffsetX
    {
        get => _offsetX;
        set => this.RaiseAndSetIfChanged(ref _offsetX, value);
    }

    private double _offsetY;
    public double OffsetY
    {
        get => _offsetY;
        set => this.RaiseAndSetIfChanged(ref _offsetY, value);
    }

    private double _zoomLevel = 1.0;
    public double ZoomLevel
    {
        get => _zoomLevel;
        set => this.RaiseAndSetIfChanged(ref _zoomLevel, value);
    }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }

    public event EventHandler<PixelRect>? FogUpdated;

    public PlayerViewModel()
        : this(new FogMaskService())
    {
    }

    public PlayerViewModel(IFogMaskService fogService)
    {
        _fogService = fogService;

        var canConnect = this.WhenAnyValue(
            x => x.SelectedDm, x => x.IsConnected, x => x.IsConnecting,
            (dm, connected, connecting) => dm is not null && !connected && !connecting);

        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);

        var canDisconnect = this.WhenAnyValue(x => x.IsConnected);
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync, canDisconnect);
    }

    public void InitializeNetworking(IDiscoveryService discoveryService, IPlayerClientService clientService)
    {
        _discoveryService = discoveryService;
        _clientService = clientService;

        _discoveryService.DmDiscovered += OnDmDiscovered;

        _clientService.SessionInfoReceived += OnSessionInfoReceived;
        _clientService.MapImageReceived += OnMapImageReceived;
        _clientService.FogDeltaReceived += OnFogDeltaReceived;
        _clientService.FogFullReceived += OnFogFullReceived;
        _clientService.Disconnected += OnDisconnected;
    }

    public async System.Threading.Tasks.Task StartDiscoveryAsync()
    {
        if (_discoveryService is null)
            return;

        await _discoveryService.StartListeningAsync(default);
    }

    private void OnDmDiscovered(object? sender, DiscoveredDm dm)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var existing in DiscoveredDms)
            {
                if (existing.SessionId == dm.SessionId)
                    return;
            }

            DiscoveredDms.Add(dm);
        });
    }

    private async System.Threading.Tasks.Task ConnectAsync()
    {
        if (_clientService is null || SelectedDm is null)
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

    private async System.Threading.Tasks.Task DisconnectAsync()
    {
        if (_clientService is null)
            return;

        await _clientService.DisconnectAsync();
        IsConnected = false;
        MapImage = null;
        FogMask = null;
        StatusText = "Disconnected. Searching for DM sessions...";
    }

    private void OnSessionInfoReceived(object? sender, MapSession session)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _fogService.Initialize(session.MapWidth, session.MapHeight);
            FogMask = _fogService.Mask;
            FogUpdated?.Invoke(this, new PixelRect(0, 0, session.MapWidth, session.MapHeight));
        });
    }

    private void OnMapImageReceived(object? sender, byte[] imageBytes)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            using var stream = new MemoryStream(imageBytes);
            MapImage = new Bitmap(stream);
        });
    }

    private void OnFogDeltaReceived(object? sender, FogDelta delta)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _fogService.ApplyDelta(delta);
            FogUpdated?.Invoke(this, new PixelRect(delta.X, delta.Y, delta.Width, delta.Height));
        });
    }

    private void OnFogFullReceived(object? sender, FogMask mask)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _fogService.Replace(mask);
            FogMask = _fogService.Mask;
            FogUpdated?.Invoke(this, new PixelRect(0, 0, mask.Width, mask.Height));
        });
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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
