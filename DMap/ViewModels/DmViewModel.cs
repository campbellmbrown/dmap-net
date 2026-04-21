using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;

using DMap.Models;
using DMap.Services.Brushes;
using DMap.Services.Fog;
using DMap.Services.History;
using DMap.Services.Networking;

using ReactiveUI;

namespace DMap.ViewModels;

public class DmViewModel : ViewModelBase, IDisposable
{
    const int DefaultBrushDiameter = 50;
    const double DefaultBrushSoftness = 0.3;
    const double DefaultBrushOpacity = 1.0;

    const double DefaultShapeSoftness = 0.0;
    const double DefaultShapeOpacity = 1.0;

    const double DefaultZoomLevel = 1.0;
    const double MinZoomLevel = 0.1;
    const double MaxZoomLevel = 10.0;

    readonly IFogMaskService _fogService;
    readonly IUndoRedoService _undoRedo;
    readonly IBrush _circleBrush;
    readonly IBrush _squareBrush;
    readonly IBrush _diamondBrush;
    readonly IDmHostService _hostService;
    readonly IDiscoveryService _discoveryService;

    public Bitmap? MapImage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsMapLoaded
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public FogMask? FogMask
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int BrushDiameter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultBrushDiameter;

    public double BrushSoftness
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultBrushSoftness;

    public double BrushOpacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultBrushOpacity;

    public double ShapeSoftness
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultShapeSoftness;

    public double ShapeOpacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultShapeOpacity;

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
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ZoomPercent));
        }
    } = DefaultZoomLevel;

    public decimal ZoomPercent
    {
        get => (decimal)Math.Round(ZoomLevel * 100);
        set => ZoomLevel = Math.Clamp((double)value / 100.0, MinZoomLevel, MaxZoomLevel);
    }

    public int ConnectedPlayers
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ToolType SelectedTool
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsBrushSelected));
            this.RaisePropertyChanged(nameof(IsShapeSelected));
            this.RaisePropertyChanged(nameof(IsPanSelected));
        }
    }

    public bool IsBrushSelected => SelectedTool == ToolType.Brush;
    public bool IsShapeSelected => SelectedTool == ToolType.Shape;
    public bool IsPanSelected => SelectedTool == ToolType.Pan;

    public BrushShape SelectedBrushShape
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IReadOnlyList<ToolType> ToolTypes { get; } = Enum.GetValues<ToolType>();
    public IReadOnlyList<BrushShape> BrushShapes { get; } = Enum.GetValues<BrushShape>();

    public ShapeType SelectedShapeType
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IReadOnlyList<ShapeType> ShapeTypes { get; } = Enum.GetValues<ShapeType>();

    public bool CanUndo
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CanRedo
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<Unit, Unit> LoadMapCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomInCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomOutCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetViewCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectBrushCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectShapeCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectPanCommand { get; }
    public ReactiveCommand<Unit, Unit> RevealAllCommand { get; }
    public ReactiveCommand<Unit, Unit> RefogAllCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    public Interaction<Unit, string?> ShowOpenFileDialog { get; } = new();

    public event EventHandler<PixelRect>? FogUpdated;

    byte[]? _mapImageBytes;
    MapSession? _session;

    public DmViewModel(IFogMaskService fogService, IUndoRedoService undoRedo, IDmHostService hostService, IDiscoveryService discoveryService)
    {
        _fogService = fogService;
        _undoRedo = undoRedo;
        _circleBrush = new CircleBrush();
        _squareBrush = new SquareBrush();
        _diamondBrush = new DiamondBrush();
        _hostService = hostService;
        _discoveryService = discoveryService;

        _hostService.PlayerCountChanged += (_, count) => ConnectedPlayers = count;
        _undoRedo.StateChanged += (_, _) => { CanUndo = _undoRedo.CanUndo; CanRedo = _undoRedo.CanRedo; };

        LoadMapCommand = ReactiveCommand.CreateFromTask(LoadMapAsync);
        ZoomInCommand = ReactiveCommand.Create(() => { ZoomLevel = Math.Min(ZoomLevel * 1.2, 10.0); });
        ZoomOutCommand = ReactiveCommand.Create(() => { ZoomLevel = Math.Max(ZoomLevel / 1.2, 0.1); });
        ResetViewCommand = ReactiveCommand.Create(() => { ZoomLevel = 1.0; OffsetX = 0; OffsetY = 0; });
        SelectBrushCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Brush; });
        SelectShapeCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Shape; });
        SelectPanCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Pan; });

        var mapLoaded = this.WhenAnyValue(x => x.IsMapLoaded);
        RevealAllCommand = ReactiveCommand.Create(ExecuteRevealAll, mapLoaded);
        RefogAllCommand = ReactiveCommand.Create(ExecuteRefogAll, mapLoaded);
        ExitCommand = ReactiveCommand.Create(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        });

        var canUndo = this.WhenAnyValue(x => x.CanUndo);
        var canRedo = this.WhenAnyValue(x => x.CanRedo);
        UndoCommand = ReactiveCommand.Create(ExecuteUndo, canUndo);
        RedoCommand = ReactiveCommand.Create(ExecuteRedo, canRedo);
    }

    void ExecuteRevealAll()
    {
        if (_fogService.Mask is null)
            return;

        var before = FogDeltaCommand.CaptureFromMask(_fogService.Mask, new PixelRect(0, 0, _fogService.Mask.Width, _fogService.Mask.Height));
        _fogService.RevealAll();
        var after = FogDeltaCommand.CaptureFromMask(_fogService.Mask, new PixelRect(0, 0, _fogService.Mask.Width, _fogService.Mask.Height));
        var rect = new PixelRect(0, 0, _fogService.Mask.Width, _fogService.Mask.Height);
        _undoRedo.Push(new FogDeltaCommand(rect, before, after));
        FogUpdated?.Invoke(this, rect);
        SendFogDelta(rect);
    }

    void ExecuteRefogAll()
    {
        if (_fogService.Mask is null)
            return;

        var before = FogDeltaCommand.CaptureFromMask(_fogService.Mask, new PixelRect(0, 0, _fogService.Mask.Width, _fogService.Mask.Height));
        _fogService.RefogAll();
        var after = FogDeltaCommand.CaptureFromMask(_fogService.Mask, new PixelRect(0, 0, _fogService.Mask.Width, _fogService.Mask.Height));
        var rect = new PixelRect(0, 0, _fogService.Mask.Width, _fogService.Mask.Height);
        _undoRedo.Push(new FogDeltaCommand(rect, before, after));
        FogUpdated?.Invoke(this, rect);
        SendFogDelta(rect);
    }

    void ExecuteUndo()
    {
        if (_fogService.Mask is null)
            return;

        var command = _undoRedo.TakeUndo();
        if (command is null)
            return;

        command.Undo(_fogService.Mask);
        FogUpdated?.Invoke(this, command.DirtyRect);
        SendFogDelta(command.DirtyRect);
    }

    void ExecuteRedo()
    {
        if (_fogService.Mask is null)
            return;

        var command = _undoRedo.TakeRedo();
        if (command is null)
            return;

        command.Redo(_fogService.Mask);
        FogUpdated?.Invoke(this, command.DirtyRect);
        SendFogDelta(command.DirtyRect);
    }

    async Task LoadMapAsync()
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
        _undoRedo.Clear();
        IsMapLoaded = true;

        FogUpdated?.Invoke(this, new PixelRect(0, 0, pixelSize.Width, pixelSize.Height));

        await StartHostingAsync();
    }

    public void BeginBrushStroke() => _fogService.BeginStroke();

    public void EndBrushStroke()
    {
        var command = _fogService.EndStroke();
        if (command is not null)
            _undoRedo.Push(command);
    }

    public void OnShapeStroke(int x1, int y1, int x2, int y2, bool isErasing)
    {
        if (_fogService.Mask is null)
            return;

        var (cx1, cy1, cx2, cy2) = ConstrainToSquare(SelectedShapeType, x1, y1, x2, y2);

        var shapeRect = ComputeShapeRect(cx1, cy1, cx2, cy2);
        var before = FogDeltaCommand.CaptureFromMask(_fogService.Mask, shapeRect);

        var dirtyRect = (SelectedShapeType is ShapeType.Ellipse or ShapeType.Circle)
            ? _fogService.ApplyEllipse(cx1, cy1, cx2, cy2, (float)ShapeSoftness, (float)ShapeOpacity, isErasing)
            : _fogService.ApplyRectangle(cx1, cy1, cx2, cy2, (float)ShapeSoftness, (float)ShapeOpacity, isErasing);

        var after = FogDeltaCommand.CaptureFromMask(_fogService.Mask, shapeRect);
        _undoRedo.Push(new FogDeltaCommand(shapeRect, before, after));

        FogUpdated?.Invoke(this, dirtyRect);
        SendFogDelta(dirtyRect);
    }

    private PixelRect ComputeShapeRect(int x1, int y1, int x2, int y2)
    {
        var minX = Math.Max(0, Math.Min(x1, x2));
        var minY = Math.Max(0, Math.Min(y1, y2));
        var maxX = Math.Min(_fogService.Mask!.Width - 1, Math.Max(x1, x2));
        var maxY = Math.Min(_fogService.Mask!.Height - 1, Math.Max(y1, y2));
        return new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
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

        FogUpdated?.Invoke(this, dirtyRect);
        SendFogDelta(dirtyRect);
    }

    void SendFogDelta(PixelRect dirtyRect)
    {
        if (_fogService.Mask is null)
            return;

        var delta = FogDelta.FromMask(
            _fogService.Mask, dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);

        _ = _hostService.SendFogDeltaAsync(delta, default);
    }

    async Task StartHostingAsync()
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
