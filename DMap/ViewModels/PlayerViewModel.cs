using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using DMap.Models;
using DMap.Services.Fog;
using DMap.Services.Networking;

using ReactiveUI;

namespace DMap.ViewModels;

/// <summary>
/// ViewModel for the player view. Manages DM discovery, connection state, and incoming
/// map and fog updates from the DM host. All network callbacks are marshalled to the UI
/// thread via <see cref="Dispatcher.UIThread"/>.
/// </summary>
public class PlayerViewModel : ViewModelBase, IDisposable
{
    readonly IFogMaskService _fogService;
    readonly IDiscoveryService _discoveryService;
    readonly IPlayerClientService _clientService;

    /// <summary>
    /// Live collection of DM sessions discovered via UDP broadcast.
    /// De-duplicated by <see cref="DiscoveredDm.SessionId"/> before adding.
    /// </summary>
    public ObservableCollection<DiscoveredDm> DiscoveredDms { get; } = new();

    /// <summary>The DM session selected in the discovery list, or <see langword="null"/> if none is selected.</summary>
    public DiscoveredDm? SelectedDm
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary><see langword="true"/> when a TCP connection to the DM is open.</summary>
    public bool IsConnected
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary><see langword="true"/> while a connection attempt is in progress.</summary>
    public bool IsConnecting
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Human-readable status message shown in the player window (e.g. discovery state, connection result).</summary>
    public string StatusText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Searching for DM sessions...";

    /// <summary>The decoded map background image received from the DM, or <see langword="null"/> before the first map arrives.</summary>
    public Bitmap? MapImage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// The fog mask maintained by the local fog service, kept in sync with updates received from the DM.
    /// Bound to the map canvas to trigger fog bitmap rebuilds when the mask is replaced.
    /// </summary>
    public FogMask? FogMask
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Horizontal pan offset of the map canvas in screen pixels.</summary>
    public double OffsetX
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Vertical pan offset of the map canvas in screen pixels.</summary>
    public double OffsetY
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Current zoom multiplier applied to the canvas transform.</summary>
    public double ZoomLevel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 1.0;

    /// <summary>Fog overlay style as set by the DM. Defaults to flat black until the DM broadcasts an update.</summary>
    public FogType FogType
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Flat fog colour as set by the DM. Used when <see cref="FogType"/> is <see cref="FogType.Color"/>.</summary>
    public Color FogColor
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = Colors.Black;

    /// <summary>Texture seed broadcast by the DM so the player generates the same noise pattern.</summary>
    public Guid FogSeed
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Latest DM viewport received from the network. The player view applies this to the canvas and
    /// reapplies it after player-side layout changes so the camera stays aligned to the DM.
    /// </summary>
    public ViewportPayload? Viewport
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Initiates a connection to <see cref="SelectedDm"/>.
    /// Enabled only when a DM is selected and no connection is active or pending.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }

    /// <summary>
    /// Closes the current connection to the DM.
    /// Enabled only when <see cref="IsConnected"/> is <see langword="true"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }

    /// <summary>
    /// Raised after any fog update (initial session fog, incremental delta, or full replacement).
    /// The event argument is the bounding rectangle of the changed region.
    /// </summary>
    public event EventHandler<PixelRect>? FogUpdated;

    /// <summary>
    /// Constructs the ViewModel and subscribes to all discovery and client service events.
    /// </summary>
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
        _clientService.FogAppearanceReceived += OnFogAppearanceReceived;
        _clientService.ViewportReceived += OnViewportReceived;
        _clientService.Disconnected += OnDisconnected;

        var canConnect = this.WhenAnyValue(
            x => x.SelectedDm, x => x.IsConnected, x => x.IsConnecting,
            (dm, connected, connecting) => dm is not null && !connected && !connecting);

        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);

        var canDisconnect = this.WhenAnyValue(x => x.IsConnected);
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync, canDisconnect);
    }

    /// <summary>
    /// Starts listening for DM UDP broadcast packets. Should be called before showing the player window.
    /// </summary>
    public async Task StartDiscoveryAsync()
    {
        await _discoveryService.StartListeningAsync(default);
    }

    /// <summary>
    /// Adds the discovered DM to <see cref="DiscoveredDms"/> if its session ID has not already been seen.
    /// Marshalled to the UI thread because the discovery service fires on a background thread.
    /// </summary>
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

    /// <summary>
    /// Connects to <see cref="SelectedDm"/> and updates connection state and status text.
    /// </summary>
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

    /// <summary>
    /// Disconnects from the DM, clears the map and fog, and updates status text.
    /// </summary>
    async Task DisconnectAsync()
    {
        await _clientService.DisconnectAsync();
        IsConnected = false;
        MapImage = null;
        FogMask = null;
        Viewport = null;
        StatusText = "Disconnected. Searching for DM sessions...";
    }

    /// <summary>
    /// Initialises the fog service with the new map dimensions. The canvas rebuild and
    /// <see cref="FogUpdated"/> notification are deferred to the <see cref="FogFullReceived"/>
    /// event that <see cref="IPlayerClientService"/> always fires immediately after this one
    /// when processing a session-info frame, avoiding a redundant double rebuild.
    /// </summary>
    void OnSessionInfoReceived(object? sender, MapSession session)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _fogService.Initialize(session.MapWidth, session.MapHeight);
        });
    }

    /// <summary>Decodes the received image bytes into a <see cref="Bitmap"/> and assigns it to <see cref="MapImage"/>.</summary>
    void OnMapImageReceived(object? sender, byte[] imageBytes)
    {
        Dispatcher.UIThread.Post(() =>
        {
            using var stream = new MemoryStream(imageBytes);
            MapImage = new Bitmap(stream);
        });
    }

    /// <summary>Merges the received fog delta into the local mask and raises <see cref="FogUpdated"/> for the changed region.</summary>
    void OnFogDeltaReceived(object? sender, FogDelta delta)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _fogService.ApplyDelta(delta);
            FogUpdated?.Invoke(this, new PixelRect(delta.X, delta.Y, delta.Width, delta.Height));
        });
    }

    /// <summary>
    /// Replaces the local fog mask entirely with the received one and raises <see cref="FogUpdated"/>
    /// for the full map area.
    /// </summary>
    void OnFogFullReceived(object? sender, FogMask mask)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _fogService.Replace(mask);
            FogMask = _fogService.Mask;
            FogUpdated?.Invoke(this, new PixelRect(0, 0, mask.Width, mask.Height));
        });
    }

    /// <summary>Applies a fog appearance update from the DM (type, colour, and texture seed).</summary>
    void OnFogAppearanceReceived(object? sender, FogAppearancePayload appearance)
    {
        Dispatcher.UIThread.Post(() =>
        {
            FogType = appearance.FogType;
            FogColor = Color.FromRgb(appearance.R, appearance.G, appearance.B);
            FogSeed = appearance.Seed;
        });
    }

    /// <summary>Stores the latest DM viewport so the player view can apply it to the local canvas.</summary>
    void OnViewportReceived(object? sender, ViewportPayload viewport)
    {
        Dispatcher.UIThread.Post(() => Viewport = viewport);
    }

    /// <summary>Updates connection state and status text when the DM disconnects.</summary>
    void OnDisconnected(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = false;
            Viewport = null;
            StatusText = "Disconnected from DM. Searching for sessions...";
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        (_discoveryService as IDisposable)?.Dispose();
        (_clientService as IDisposable)?.Dispose();
    }
}
