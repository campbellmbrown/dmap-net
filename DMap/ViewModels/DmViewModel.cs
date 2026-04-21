using System;
using System.Collections.Generic;
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
    private readonly IDmHostService _hostService;
    private readonly IDiscoveryService _discoveryService;

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

    private double _brushOpacity = 1.0;
    public double BrushOpacity
    {
        get => _brushOpacity;
        set => this.RaiseAndSetIfChanged(ref _brushOpacity, value);
    }

    private double _shapeSoftness = 0.0;
    public double ShapeSoftness
    {
        get => _shapeSoftness;
        set => this.RaiseAndSetIfChanged(ref _shapeSoftness, value);
    }

    private double _shapeOpacity = 1.0;
    public double ShapeOpacity
    {
        get => _shapeOpacity;
        set => this.RaiseAndSetIfChanged(ref _shapeOpacity, value);
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
            this.RaisePropertyChanged(nameof(IsBrushSelected));
            this.RaisePropertyChanged(nameof(IsShapeSelected));
            this.RaisePropertyChanged(nameof(IsPanSelected));
        }
    }

    public bool IsBrushSelected => _selectedTool == ToolType.Brush;
    public bool IsShapeSelected => _selectedTool == ToolType.Shape;
    public bool IsPanSelected => _selectedTool == ToolType.Pan;

    private BrushShape _selectedBrushShape;
    public BrushShape SelectedBrushShape
    {
        get => _selectedBrushShape;
        set => this.RaiseAndSetIfChanged(ref _selectedBrushShape, value);
    }

    public IReadOnlyList<BrushShape> BrushShapes { get; } = Enum.GetValues<BrushShape>();

    private ShapeType _selectedShapeType;
    public ShapeType SelectedShapeType
    {
        get => _selectedShapeType;
        set => this.RaiseAndSetIfChanged(ref _selectedShapeType, value);
    }

    public IReadOnlyList<ShapeType> ShapeTypes { get; } = Enum.GetValues<ShapeType>();

    public ReactiveCommand<Unit, Unit> LoadMapCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomInCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomOutCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetViewCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectBrushCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectShapeCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectPanCommand { get; }

    public Interaction<Unit, string?> ShowOpenFileDialog { get; } = new();

    public event EventHandler<PixelRect>? FogUpdated;

    private byte[]? _mapImageBytes;
    private MapSession? _session;

    public DmViewModel(IFogMaskService fogService, IDmHostService hostService, IDiscoveryService discoveryService)
    {
        _fogService = fogService;
        _circleBrush = new CircleBrush();
        _squareBrush = new SquareBrush();
        _diamondBrush = new DiamondBrush();
        _hostService = hostService;
        _discoveryService = discoveryService;

        _hostService.PlayerCountChanged += (_, count) => ConnectedPlayers = count;

        LoadMapCommand = ReactiveCommand.CreateFromTask(LoadMapAsync);
        ZoomInCommand = ReactiveCommand.Create(() => { ZoomLevel = Math.Min(ZoomLevel * 1.2, 10.0); });
        ZoomOutCommand = ReactiveCommand.Create(() => { ZoomLevel = Math.Max(ZoomLevel / 1.2, 0.1); });
        ResetViewCommand = ReactiveCommand.Create(() => { ZoomLevel = 1.0; OffsetX = 0; OffsetY = 0; });
        SelectBrushCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Brush; });
        SelectShapeCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Shape; });
        SelectPanCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Pan; });
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

    public void BeginBrushStroke() => _fogService.BeginStroke();

    public void EndBrushStroke() => _fogService.EndStroke();

    public void OnShapeStroke(int x1, int y1, int x2, int y2, bool isErasing)
    {
        if (_fogService.Mask is null)
            return;

        var (cx1, cy1, cx2, cy2) = ConstrainToSquare(SelectedShapeType, x1, y1, x2, y2);

        var dirtyRect = (SelectedShapeType is ShapeType.Ellipse or ShapeType.Circle)
            ? _fogService.ApplyEllipse(cx1, cy1, cx2, cy2, (float)ShapeSoftness, (float)ShapeOpacity, isErasing)
            : _fogService.ApplyRectangle(cx1, cy1, cx2, cy2, (float)ShapeSoftness, (float)ShapeOpacity, isErasing);

        LastDirtyRect = dirtyRect;
        FogUpdated?.Invoke(this, dirtyRect);
        SendFogDelta(dirtyRect);
    }

    private static (int, int, int, int) ConstrainToSquare(ShapeType shapeType, int x1, int y1, int x2, int y2)
    {
        if (shapeType is not ShapeType.Square and not ShapeType.Circle)
            return (x1, y1, x2, y2);

        var side = Math.Min(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        return (x1, y1, x1 + Math.Sign(x2 - x1) * side, y1 + Math.Sign(y2 - y1) * side);
    }

    public void OnBrushStroke(int x1, int y1, int x2, int y2, bool isErasing)
    {
        if (_fogService.Mask is null)
            return;

        var brush = SelectedBrushShape switch
        {
            BrushShape.Square => _squareBrush,
            BrushShape.Diamond => _diamondBrush,
            _ => _circleBrush,
        };
        var settings = new BrushSettings(BrushDiameter, (float)BrushSoftness, (float)BrushOpacity, Erase: isErasing);
        var dirtyRect = _fogService.ApplyBrush(brush, x1, y1, x2, y2, settings);

        LastDirtyRect = dirtyRect;
        FogUpdated?.Invoke(this, dirtyRect);

        SendFogDelta(dirtyRect);
    }

    private void SendFogDelta(PixelRect dirtyRect)
    {
        if (_fogService.Mask is null)
            return;

        var delta = FogDelta.FromMask(
            _fogService.Mask, dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);

        _ = _hostService.SendFogDeltaAsync(delta, default);
    }

    private async Task StartHostingAsync()
    {
        if (_session is null)
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
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            (_hostService as IDisposable)?.Dispose();
            (_discoveryService as IDisposable)?.Dispose();
        }
    }
}
