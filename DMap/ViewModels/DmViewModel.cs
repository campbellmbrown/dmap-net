using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Media.Imaging;

using DMap.Models;
using DMap.Services.Brushes;
using DMap.Services.Fog;
using DMap.Services.Networking;

using ReactiveUI;

namespace DMap.ViewModels;

public class DmViewModel : ViewModelBase, IDisposable
{
    private readonly IFogMaskService _fogService;
    private readonly IBrush _circleBrush;
    private readonly IBrush _squareBrush;
    private readonly IBrush _diamondBrush;

    private Bitmap? _mapImage;
    public Bitmap? MapImage
    {
        get => _mapImage;
        private set => this.RaiseAndSetIfChanged(ref _mapImage, value);
    }

    private bool _isMapLoaded;
    public bool IsMapLoaded
    {
        get => _isMapLoaded;
        private set => this.RaiseAndSetIfChanged(ref _isMapLoaded, value);
    }

    private FogMask? _fogMask;
    public FogMask? FogMask
    {
        get => _fogMask;
        private set => this.RaiseAndSetIfChanged(ref _fogMask, value);
    }

    private int _brushDiameter = 50;
    public int BrushDiameter
    {
        get => _brushDiameter;
        set => this.RaiseAndSetIfChanged(ref _brushDiameter, value);
    }

    private double _brushSoftness = 0.3;
    public double BrushSoftness
    {
        get => _brushSoftness;
        set => this.RaiseAndSetIfChanged(ref _brushSoftness, value);
    }

    private double _rectangleSoftness = 0.0;
    public double RectangleSoftness
    {
        get => _rectangleSoftness;
        set => this.RaiseAndSetIfChanged(ref _rectangleSoftness, value);
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

    private int _connectedPlayers;
    public int ConnectedPlayers
    {
        get => _connectedPlayers;
        set => this.RaiseAndSetIfChanged(ref _connectedPlayers, value);
    }

    private PixelRect _lastDirtyRect;
    public PixelRect LastDirtyRect
    {
        get => _lastDirtyRect;
        private set => this.RaiseAndSetIfChanged(ref _lastDirtyRect, value);
    }

    private ToolType _selectedTool;
    public ToolType SelectedTool
    {
        get => _selectedTool;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTool, value);
            this.RaisePropertyChanged(nameof(IsCircleBrushSelected));
            this.RaisePropertyChanged(nameof(IsSquareBrushSelected));
            this.RaisePropertyChanged(nameof(IsDiamondBrushSelected));
            this.RaisePropertyChanged(nameof(IsAnyBrushSelected));
            this.RaisePropertyChanged(nameof(IsRectangleSelected));
        }
    }

    public bool IsCircleBrushSelected => _selectedTool == ToolType.CircleBrush;
    public bool IsSquareBrushSelected => _selectedTool == ToolType.SquareBrush;
    public bool IsDiamondBrushSelected => _selectedTool == ToolType.DiamondBrush;
    public bool IsAnyBrushSelected => _selectedTool is ToolType.CircleBrush or ToolType.SquareBrush or ToolType.DiamondBrush;
    public bool IsRectangleSelected => _selectedTool == ToolType.Rectangle;

    public ReactiveCommand<Unit, Unit> LoadMapCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomInCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomOutCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetViewCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectCircleBrushCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectSquareBrushCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectDiamondBrushCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectRectangleCommand { get; }

    // Interaction to request a file path from the view
    public Interaction<Unit, string?> ShowOpenFileDialog { get; } = new();

    // Event raised after fog is updated so the view can refresh the canvas
    public event EventHandler<PixelRect>? FogUpdated;

    private byte[]? _mapImageBytes;
    private MapSession? _session;
    private IDmHostService? _hostService;
    private IDiscoveryService? _discoveryService;

    public DmViewModel()
        : this(new FogMaskService())
    {
    }

    public DmViewModel(IFogMaskService fogService)
    {
        _fogService = fogService;
        _circleBrush = new CircleBrush();
        _squareBrush = new SquareBrush();
        _diamondBrush = new DiamondBrush();

        LoadMapCommand = ReactiveCommand.CreateFromTask(LoadMapAsync);
        ZoomInCommand = ReactiveCommand.Create(() => { ZoomLevel = Math.Min(ZoomLevel * 1.2, 10.0); });
        ZoomOutCommand = ReactiveCommand.Create(() => { ZoomLevel = Math.Max(ZoomLevel / 1.2, 0.1); });
        ResetViewCommand = ReactiveCommand.Create(() => { ZoomLevel = 1.0; OffsetX = 0; OffsetY = 0; });
        SelectCircleBrushCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.CircleBrush; });
        SelectSquareBrushCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.SquareBrush; });
        SelectDiamondBrushCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.DiamondBrush; });
        SelectRectangleCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Rectangle; });
    }

    public void InitializeNetworking(IDmHostService hostService, IDiscoveryService discoveryService)
    {
        _hostService = hostService;
        _discoveryService = discoveryService;

        _hostService.PlayerCountChanged += (_, count) => ConnectedPlayers = count;
    }

    private async Task LoadMapAsync()
    {
        var path = await ShowOpenFileDialog.Handle(Unit.Default);
        if (string.IsNullOrEmpty(path))
            return;

        _mapImageBytes = await File.ReadAllBytesAsync(path);
        using var stream = new MemoryStream(_mapImageBytes);
        MapImage = new Bitmap(stream);

        var pixelSize = MapImage.PixelSize;
        _fogService.Initialize(pixelSize.Width, pixelSize.Height);
        FogMask = _fogService.Mask;

        _session = new MapSession(Guid.NewGuid(), pixelSize.Width, pixelSize.Height);
        IsMapLoaded = true;

        FogUpdated?.Invoke(this, new PixelRect(0, 0, pixelSize.Width, pixelSize.Height));

        await StartHostingAsync();
    }

    public void OnRectangleStroke(int x1, int y1, int x2, int y2)
    {
        if (_fogService.Mask is null)
            return;

        var dirtyRect = _fogService.ApplyRectangle(x1, y1, x2, y2, (float)RectangleSoftness);
        LastDirtyRect = dirtyRect;
        FogUpdated?.Invoke(this, dirtyRect);
        SendFogDelta(dirtyRect);
    }

    public void OnBrushStroke(int mapX, int mapY)
    {
        if (_fogService.Mask is null)
            return;

        var brush = SelectedTool switch
        {
            ToolType.SquareBrush => _squareBrush,
            ToolType.DiamondBrush => _diamondBrush,
            _ => _circleBrush,
        };
        var settings = new BrushSettings(BrushDiameter, (float)BrushSoftness);
        var dirtyRect = _fogService.ApplyBrush(brush, mapX, mapY, settings);

        LastDirtyRect = dirtyRect;
        FogUpdated?.Invoke(this, dirtyRect);

        SendFogDelta(dirtyRect);
    }

    private void SendFogDelta(PixelRect dirtyRect)
    {
        if (_hostService is null || _fogService.Mask is null)
            return;

        var delta = FogDelta.FromMask(
            _fogService.Mask, dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);

        _ = _hostService.SendFogDeltaAsync(delta, default);
    }

    private async Task StartHostingAsync()
    {
        if (_hostService is null || _discoveryService is null || _session is null)
            return;

        await _hostService.StartAsync(default);

        if (_mapImageBytes != null)
            await _hostService.SendMapImageAsync(_mapImageBytes, default);

        if (_fogService.Mask != null)
            await _hostService.SendSessionInfoAsync(_session, _fogService.Mask, default);

        await _discoveryService.StartBroadcastingAsync(_session, _hostService.Port, default);
    }

    public void Dispose()
    {
        (_hostService as IDisposable)?.Dispose();
        (_discoveryService as IDisposable)?.Dispose();
    }
}
