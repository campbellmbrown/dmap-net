using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using DMap.Models;
using DMap.Services.Brushes;
using DMap.Services.Fog;
using DMap.Services.History;
using DMap.Services.Networking;

using ReactiveUI;

using IBrush = DMap.Services.Brushes.IBrush;

namespace DMap.ViewModels;

/// <summary>
/// ViewModel for the Dungeon Master (DM) view. Owns the fog mask, undo/redo history,
/// networking services, and all toolbar commands. Bridges user input events from
/// <see cref="Controls.MapCanvas"/> to the underlying fog and networking services.
/// </summary>
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

    const double DefaultFogOpacity = 0.5;

    readonly IFogMaskService _fogService;
    readonly IUndoRedoService _undoRedo;
    readonly IBrush _circleBrush;
    readonly IBrush _squareBrush;
    readonly IBrush _diamondBrush;
    readonly IDmHostService _hostService;
    readonly IDiscoveryService _discoveryService;
    readonly Func<PlayerViewModel> _createPlayer;

    /// <summary>The decoded map background image, or <see langword="null"/> before a map is loaded.</summary>
    public Bitmap? MapImage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary><see langword="true"/> once a map image has been loaded and the fog mask has been initialised.</summary>
    public bool IsMapLoaded
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// The current fog mask, kept in sync with the fog service.
    /// Bound to the map canvas so it can rebuild its fog bitmap when the mask is replaced.
    /// </summary>
    public FogMask? FogMask
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Diameter of the brush in map pixels. Bound to the toolbar slider.</summary>
    public int BrushDiameter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultBrushDiameter;

    /// <summary>Brush edge softness in [0, 1]; 0 = hard edge, 1 = full gradient.</summary>
    public double BrushSoftness
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultBrushSoftness;

    /// <summary>Maximum brush opacity applied per stroke in [0, 1].</summary>
    public double BrushOpacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultBrushOpacity;

    /// <summary>Shape edge softness in [0, 1]; 0 = hard edge.</summary>
    public double ShapeSoftness
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultShapeSoftness;

    /// <summary>Maximum shape fill opacity in [0, 1].</summary>
    public double ShapeOpacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultShapeOpacity;

    /// <summary>Opacity of the fog overlay rendered on the canvas, in the range [0, 1].</summary>
    public double FogOpacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultFogOpacity;

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

    /// <summary>
    /// Current zoom multiplier applied to the canvas transform.
    /// Changing this value also raises <see cref="ZoomPercent"/> change notification.
    /// </summary>
    public double ZoomLevel
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ZoomPercent));
        }
    } = DefaultZoomLevel;

    /// <summary>
    /// <see cref="ZoomLevel"/> expressed as a percentage in [10, 1000], rounded to the nearest integer.
    /// Setting this property converts the value and clamps it within allowed bounds.
    /// </summary>
    public decimal ZoomPercent
    {
        get => (decimal)Math.Round(ZoomLevel * 100);
        set => ZoomLevel = Math.Clamp((double)value / 100.0, MinZoomLevel, MaxZoomLevel);
    }

    /// <summary>Number of player TCP connections currently open on the host service.</summary>
    public int ConnectedPlayers
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Current process memory usage formatted as a human-readable string (e.g. "42 MB"), updated every 2 seconds.</summary>
    public string MemoryUsage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>
    /// The currently active editing tool.
    /// Changing this value also raises change notifications for the derived
    /// <see cref="IsBrushSelected"/>, <see cref="IsShapeSelected"/>, <see cref="IsPanSelected"/>,
    /// and <see cref="IsFogSelected"/> properties.
    /// </summary>
    public ToolType SelectedTool
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsBrushSelected));
            this.RaisePropertyChanged(nameof(IsShapeSelected));
            this.RaisePropertyChanged(nameof(IsPanSelected));
            this.RaisePropertyChanged(nameof(IsFogSelected));
        }
    }

    /// <summary><see langword="true"/> when the Brush tool is active.</summary>
    public bool IsBrushSelected => SelectedTool == ToolType.Brush;

    /// <summary><see langword="true"/> when the Shape tool is active.</summary>
    public bool IsShapeSelected => SelectedTool == ToolType.Shape;

    /// <summary><see langword="true"/> when the Pan tool is active.</summary>
    public bool IsPanSelected => SelectedTool == ToolType.Pan;

    /// <summary><see langword="true"/> when the Fog tool is active.</summary>
    public bool IsFogSelected => SelectedTool == ToolType.Fog;

    /// <summary>The brush shape (circle, square, or diamond) used when the Brush tool is active.</summary>
    public BrushShape SelectedBrushShape
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>All available tool types, used to populate the toolbar's tool selector.</summary>
    public IReadOnlyList<ToolType> ToolTypes { get; } = Enum.GetValues<ToolType>();

    /// <summary>All available brush shapes, used to populate the brush shape selector.</summary>
    public IReadOnlyList<BrushShape> BrushShapes { get; } = Enum.GetValues<BrushShape>();

    /// <summary>The geometric shape drawn when the Shape tool is active.</summary>
    public ShapeType SelectedShapeType
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>All available shape types, used to populate the shape type selector.</summary>
    public IReadOnlyList<ShapeType> ShapeTypes { get; } = Enum.GetValues<ShapeType>();

    /// <summary>
    /// The fog overlay style. Changing this raises change notification for
    /// <see cref="IsFogColorSelected"/> and broadcasts the new appearance to connected players.
    /// </summary>
    public FogType SelectedFogType
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsFogColorSelected));
            BroadcastFogAppearance();
        }
    } = FogType.Color;

    /// <summary>
    /// The flat fog colour used when <see cref="SelectedFogType"/> is <see cref="FogType.Color"/>.
    /// Changing this broadcasts the new appearance to connected players.
    /// </summary>
    public Color FogColor
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            BroadcastFogAppearance();
        }
    } = Colors.Black;

    /// <summary>Texture seed shared with players so all clients render identical fog noise.</summary>
    public Guid FogSeed
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary><see langword="true"/> when the colour picker should be visible (i.e. flat-colour mode).</summary>
    public bool IsFogColorSelected => SelectedFogType == FogType.Color;

    /// <summary>All available fog types, used to populate the fog type selector.</summary>
    public IReadOnlyList<FogType> FogTypes { get; } = Enum.GetValues<FogType>();

    /// <summary><see langword="true"/> when at least one operation is available to undo.</summary>
    public bool CanUndo
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary><see langword="true"/> when at least one operation is available to redo.</summary>
    public bool CanRedo
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Opens a file picker and loads the selected image as the map background.</summary>
    public ReactiveCommand<Unit, Unit> LoadMapCommand { get; }

    /// <summary>Increases the zoom level by 20%.</summary>
    public ReactiveCommand<Unit, Unit> ZoomInCommand { get; }

    /// <summary>Decreases the zoom level by ~17%.</summary>
    public ReactiveCommand<Unit, Unit> ZoomOutCommand { get; }

    /// <summary>Resets zoom to 100% and clears the pan offset.</summary>
    public ReactiveCommand<Unit, Unit> ResetViewCommand { get; }

    /// <summary>Activates the Brush tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectBrushCommand { get; }

    /// <summary>Activates the Shape tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectShapeCommand { get; }

    /// <summary>Activates the Pan tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectPanCommand { get; }

    /// <summary>Activates the Fog tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectFogCommand { get; }

    /// <summary>Sets all fog mask pixels to 255 (fully revealed) and pushes an undo entry.</summary>
    public ReactiveCommand<Unit, Unit> RevealAllCommand { get; }

    /// <summary>Sets all fog mask pixels to 0 (fully fogged) and pushes an undo entry.</summary>
    public ReactiveCommand<Unit, Unit> RefogAllCommand { get; }

    /// <summary>Shuts down the application.</summary>
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    /// <summary>Undoes the most recent fog operation.</summary>
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }

    /// <summary>Redoes the most recently undone fog operation.</summary>
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    /// <summary>Opens the player discovery window so the DM can monitor connected players.</summary>
    public ReactiveCommand<Unit, Unit> OpenPlayerWindowCommand { get; }

    /// <summary>Toggles whether the map image is rendered or replaced with a white background for fog inspection.</summary>
    public ReactiveCommand<Unit, Unit> ToggleMapVisibilityCommand { get; }

    /// <summary>
    /// <see langword="true"/> when the map image is rendered; <see langword="false"/> when the map is hidden
    /// and replaced with a white background so the fog mask is easier to inspect.
    /// </summary>
    public bool IsMapVisible
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    /// <summary>Toggles whether fog updates are held locally or sent to connected players immediately.</summary>
    public ReactiveCommand<Unit, Unit> TogglePauseUpdatesCommand { get; }

    /// <summary>
    /// When <see langword="true"/>, fog changes are applied locally but not broadcast to players.
    /// Resuming sends a single full-mask delta to bring all players up to date.
    /// </summary>
    public bool IsUpdatesPaused
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// Interaction that requests the view to display a file picker and return the chosen path.
    /// Handled in <see cref="Views.DmView"/>.
    /// </summary>
    public Interaction<Unit, string?> ShowOpenFileDialog { get; } = new();

    /// <summary>
    /// Interaction that requests the view to display the player window for the given ViewModel.
    /// Handled in <see cref="Views.DmView"/>.
    /// </summary>
    public Interaction<PlayerViewModel, Unit> ShowPlayerWindow { get; } = new();

    /// <summary>
    /// Raised after any fog modification (brush, shape, undo, redo, reveal-all, refog-all).
    /// The event argument is the bounding rectangle of the changed region. The view uses
    /// this to update only the affected portion of the fog bitmap rather than rebuilding it entirely.
    /// </summary>
    public event EventHandler<PixelRect>? FogUpdated;

    byte[]? _mapImageBytes;
    MapSession? _session;
    bool _hasPendingUpdates;
    /// <summary>Fires every 2 seconds on the UI thread to refresh <see cref="MemoryUsage"/>.</summary>
    readonly DispatcherTimer _memoryTimer;

    /// <summary>
    /// Constructs the ViewModel and wires up all reactive commands and service event handlers.
    /// </summary>
    public DmViewModel(IFogMaskService fogService, IUndoRedoService undoRedo, IDmHostService hostService, IDiscoveryService discoveryService, Func<PlayerViewModel> createPlayer)
    {
        _fogService = fogService;
        _undoRedo = undoRedo;
        _circleBrush = new CircleBrush();
        _squareBrush = new SquareBrush();
        _diamondBrush = new DiamondBrush();
        _hostService = hostService;
        _discoveryService = discoveryService;
        _createPlayer = createPlayer;

        _hostService.PlayerCountChanged += (_, count) => ConnectedPlayers = count;
        _undoRedo.StateChanged += (_, _) => { CanUndo = _undoRedo.CanUndo; CanRedo = _undoRedo.CanRedo; };

        MemoryUsage = FormatMemoryUsage();
        _memoryTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => MemoryUsage = FormatMemoryUsage());
        _memoryTimer.Start();

        LoadMapCommand = ReactiveCommand.CreateFromTask(LoadMapAsync);
        ZoomInCommand = ReactiveCommand.Create(() => { ZoomLevel = Math.Min(ZoomLevel * 1.2, 10.0); });
        ZoomOutCommand = ReactiveCommand.Create(() => { ZoomLevel = Math.Max(ZoomLevel / 1.2, 0.1); });
        ResetViewCommand = ReactiveCommand.Create(() => { ZoomLevel = 1.0; OffsetX = 0; OffsetY = 0; });
        SelectBrushCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Brush; });
        SelectShapeCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Shape; });
        SelectPanCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Pan; });
        SelectFogCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Fog; });

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
        OpenPlayerWindowCommand = ReactiveCommand.CreateFromTask(OpenPlayerWindowAsync);
        ToggleMapVisibilityCommand = ReactiveCommand.Create(() => { IsMapVisible = !IsMapVisible; });
        TogglePauseUpdatesCommand = ReactiveCommand.Create(() =>
        {
            IsUpdatesPaused = !IsUpdatesPaused;
            if (!IsUpdatesPaused)
                FlushPendingUpdates();
        });
    }

    /// <summary>
    /// Creates a <see cref="PlayerViewModel"/>, starts discovery listening, and triggers the
    /// <see cref="ShowPlayerWindow"/> interaction so the view can open the player window.
    /// </summary>
    async Task OpenPlayerWindowAsync()
    {
        var playerVm = _createPlayer();
        await playerVm.StartDiscoveryAsync();
        await ShowPlayerWindow.Handle(playerVm);
    }

    /// <summary>
    /// Reveals the entire map, captures a before/after undo entry, and broadcasts the change.
    /// </summary>
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

    /// <summary>
    /// Re-fogs the entire map, captures a before/after undo entry, and broadcasts the change.
    /// </summary>
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

    /// <summary>
    /// Pops the most recent command from the undo stack, restores the before-state, and broadcasts the delta.
    /// </summary>
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

    /// <summary>
    /// Pops the most recently undone command from the redo stack, re-applies it, and broadcasts the delta.
    /// </summary>
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

    /// <summary>
    /// Opens the file picker via <see cref="ShowOpenFileDialog"/>, loads the chosen image as the
    /// map background, initialises the fog mask, and starts hosting.
    /// </summary>
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
        FogSeed = _session.SessionId;
        _undoRedo.Clear();
        IsMapLoaded = true;

        FogUpdated?.Invoke(this, new PixelRect(0, 0, pixelSize.Width, pixelSize.Height));

        await StartHostingAsync();
        BroadcastFogAppearance();
    }

    /// <summary>Returns the current process working set formatted as "<c>N MB</c>".</summary>
    static string FormatMemoryUsage() =>
        $"{Environment.WorkingSet / 1024 / 1024} MB";

    /// <summary>
    /// Called by the view when the user begins a brush stroke (pointer pressed).
    /// Snapshots the current mask for opacity-consistent painting.
    /// </summary>
    public void BeginBrushStroke() => _fogService.BeginStroke();

    /// <summary>
    /// Called by the view when the user ends a brush stroke (pointer released).
    /// Finalises the stroke and pushes a undo entry if any pixels changed.
    /// </summary>
    public void EndBrushStroke()
    {
        var command = _fogService.EndStroke();
        if (command is not null)
            _undoRedo.Push(command);
    }

    /// <summary>
    /// Called by the view when the user completes a shape drag gesture.
    /// Applies the selected shape to the fog mask, records an undo entry, and broadcasts the delta.
    /// </summary>
    /// <param name="x1">Start X in map pixels.</param>
    /// <param name="y1">Start Y in map pixels.</param>
    /// <param name="x2">End X in map pixels.</param>
    /// <param name="y2">End Y in map pixels.</param>
    /// <param name="isErasing"><see langword="true"/> to erase fog; <see langword="false"/> to reveal.</param>
    public void OnShapeStroke(int x1, int y1, int x2, int y2, bool isErasing)
    {
        if (_fogService.Mask is null)
            return;

        var (cx1, cy1, cx2, cy2) = ConstrainToSquare(SelectedShapeType, x1, y1, x2, y2);

        if (cx1 == cx2 && cy1 == cy2)
            return;

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

    /// <summary>
    /// Computes the clamped bounding rectangle of a shape defined by two corner points.
    /// </summary>
    PixelRect ComputeShapeRect(int x1, int y1, int x2, int y2)
    {
        var minX = Math.Max(0, Math.Min(x1, x2));
        var minY = Math.Max(0, Math.Min(y1, y2));
        var maxX = Math.Min(_fogService.Mask!.Width - 1, Math.Max(x1, x2));
        var maxY = Math.Min(_fogService.Mask!.Height - 1, Math.Max(y1, y2));
        return new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>
    /// For square and circle shape types, constrains the drag so the resulting shape has equal
    /// width and height (the smaller of the two extents is used). Other shape types are returned unchanged.
    /// </summary>
    static (int, int, int, int) ConstrainToSquare(ShapeType shapeType, int x1, int y1, int x2, int y2)
    {
        if (shapeType is not ShapeType.Square and not ShapeType.Circle)
            return (x1, y1, x2, y2);

        var side = Math.Min(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        return (x1, y1, x1 + Math.Sign(x2 - x1) * side, y1 + Math.Sign(y2 - y1) * side);
    }

    /// <summary>
    /// Called by the view for each pointer-move event while a brush stroke is in progress.
    /// Applies the selected brush between the previous and current map coordinates and broadcasts the change.
    /// </summary>
    /// <param name="x1">Previous map X in pixels.</param>
    /// <param name="y1">Previous map Y in pixels.</param>
    /// <param name="x2">Current map X in pixels.</param>
    /// <param name="y2">Current map Y in pixels.</param>
    /// <param name="isErasing"><see langword="true"/> to erase fog; <see langword="false"/> to reveal.</param>
    public void OnBrushStroke(int x1, int y1, int x2, int y2, bool isErasing)
    {
        if (_fogService.Mask is null)
            return;

        var brush = SelectedBrushShape switch
        {
            BrushShape.Circle => _circleBrush,
            BrushShape.Square => _squareBrush,
            BrushShape.Diamond => _diamondBrush,
            _ => throw new InvalidOperationException($"Unsupported brush shape: {SelectedBrushShape}")
        };
        var settings = new BrushSettings(BrushDiameter, (float)BrushSoftness, (float)BrushOpacity, Erase: isErasing);
        var dirtyRect = _fogService.ApplyBrush(brush, x1, y1, x2, y2, settings);

        FogUpdated?.Invoke(this, dirtyRect);
        SendFogDelta(dirtyRect);
    }

    /// <summary>
    /// Extracts the fog delta for <paramref name="dirtyRect"/> from the current mask and
    /// fires-and-forgets a broadcast to all connected players. When updates are paused the
    /// delta is discarded and a flag is set so <see cref="FlushPendingUpdates"/> knows to
    /// sync when updates resume.
    /// </summary>
    void SendFogDelta(PixelRect dirtyRect)
    {
        if (_fogService.Mask is null)
            return;

        if (IsUpdatesPaused)
        {
            _hasPendingUpdates = true;
            return;
        }

        var delta = FogDelta.FromMask(
            _fogService.Mask, dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);

        _ = _hostService.SendFogDeltaAsync(delta, default);
    }

    /// <summary>
    /// Fires-and-forgets a broadcast of the current fog appearance (type + colour + seed)
    /// to all connected players. Called whenever <see cref="SelectedFogType"/> or <see cref="FogColor"/>
    /// changes, and once per map load. Skipped before the host starts (no session yet).
    /// </summary>
    void BroadcastFogAppearance()
    {
        if (_session is null)
            return;

        var payload = new FogAppearancePayload
        {
            FogType = SelectedFogType,
            R = FogColor.R,
            G = FogColor.G,
            B = FogColor.B,
            Seed = FogSeed,
        };
        _ = _hostService.SendFogAppearanceAsync(payload, default);
    }

    /// <summary>
    /// Sends the full current fog mask to all players as a single delta, catching them up on
    /// every change made while updates were paused.
    /// </summary>
    void FlushPendingUpdates()
    {
        if (!_hasPendingUpdates || _fogService.Mask is null)
            return;

        _hasPendingUpdates = false;
        var mask = _fogService.Mask;
        var delta = FogDelta.FromMask(mask, 0, 0, mask.Width, mask.Height);
        _ = _hostService.SendFogDeltaAsync(delta, default);
    }

    /// <summary>
    /// Starts the TCP host, sends the current map image and session info to any already-connected
    /// players, and begins broadcasting the session over UDP for discovery.
    /// </summary>
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

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases managed resources when <paramref name="disposing"/> is <see langword="true"/>.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _memoryTimer.Stop();
            (_hostService as IDisposable)?.Dispose();
            (_discoveryService as IDisposable)?.Dispose();
        }
    }
}
