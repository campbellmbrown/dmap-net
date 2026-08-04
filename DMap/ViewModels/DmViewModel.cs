using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using DMap.Models;
using DMap.Protocol;
using DMap.Services.Brushes;
using DMap.Services.Fog;
using DMap.Services.History;
using DMap.Services.Maps;
using DMap.Services.Networking;
using DMap.ViewModels.ToolSettings;

using ReactiveUI;

using Serilog;

using IBrush = DMap.Services.Brushes.IBrush;

namespace DMap.ViewModels;

/// <summary>
/// ViewModel for the Dungeon Master (DM) view. Owns the fog mask, undo/redo history,
/// networking services, and all toolbar commands. Bridges user input events from
/// <see cref="Controls.MapCanvas"/> to the underlying fog and networking services.
/// </summary>
public class DmViewModel : ViewModelBase, IDisposable
{
    readonly IFogMaskService _fogService;
    readonly IUndoRedoService _undoRedo;
    readonly IBrush _circleBrush;
    readonly IBrush _squareBrush;
    readonly IBrush _diamondBrush;
    readonly IDmHostService _hostService;
    readonly IDiscoveryService _discoveryService;
    readonly IMapLoader _mapLoader;
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

    /// <summary>Settings for the Brush tool.</summary>
    public BrushToolSettingsViewModel BrushSettings { get; }

    /// <summary>Settings for the Shape tool.</summary>
    public ShapeToolSettingsViewModel ShapeSettings { get; }

    /// <summary>Settings for the Fog tool.</summary>
    public FogToolSettingsViewModel FogSettings { get; }

    /// <summary>Settings for the Cursor tool.</summary>
    public CursorToolSettingsViewModel CursorSettings { get; }

    /// <summary>Settings for the Grid tool.</summary>
    public GridToolSettingsViewModel GridSettings { get; }

    /// <summary>Settings placeholder for the Pan tool.</summary>
    public PanToolSettingsViewModel PanSettings { get; }

    /// <summary>Settings for the Stamp tool.</summary>
    public StampToolSettingsViewModel StampSettings { get; }

    /// <summary>Settings for the Player View tool.</summary>
    public PlayerViewToolSettingsViewModel PlayerViewSettings { get; }

    /// <summary>Placed stamps in map-space coordinates.</summary>
    public ObservableCollection<StampInstance> Stamps { get; } = new();

    /// <summary>The currently selected stamp, or <see langword="null"/> when none is selected.</summary>
    public StampInstance? SelectedStamp
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            StampSettings.HasSelectedStamp = value is not null;
            StampSettings.ApplySelectedStamp(value);
        }
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

    /// <summary>Loaded map dimensions formatted for the status bar.</summary>
    public string MapSize
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Map: -";

    /// <summary>
    /// The currently active editing tool.
    /// Changing this value also raises change notifications for the derived
    /// <see cref="IsBrushSelected"/>, <see cref="IsShapeSelected"/>, <see cref="IsPanSelected"/>,
    /// <see cref="IsFogSelected"/>, and <see cref="IsCursorSelected"/> properties.
    /// </summary>
    public ToolType SelectedTool
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsBrushSelected));
            this.RaisePropertyChanged(nameof(IsShapeSelected));
            this.RaisePropertyChanged(nameof(IsPanSelected));
            this.RaisePropertyChanged(nameof(IsFogSelected));
            this.RaisePropertyChanged(nameof(IsCursorSelected));
            this.RaisePropertyChanged(nameof(IsGridSelected));
            this.RaisePropertyChanged(nameof(IsStampSelected));
            this.RaisePropertyChanged(nameof(IsPlayerViewSelected));
            CurrentToolSettings = GetCurrentToolSettings(value);
            this.RaisePropertyChanged(nameof(HasCurrentToolSettings));
            this.RaisePropertyChanged(nameof(ShowToolSettingsRailToggle));
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

    /// <summary><see langword="true"/> when the Cursor tool is active.</summary>
    public bool IsCursorSelected => SelectedTool == ToolType.Cursor;

    /// <summary><see langword="true"/> when the Grid tool is active.</summary>
    public bool IsGridSelected => SelectedTool == ToolType.Grid;

    /// <summary><see langword="true"/> when the Stamp tool is active.</summary>
    public bool IsStampSelected => SelectedTool == ToolType.Stamp;

    /// <summary><see langword="true"/> when the Player View tool is active.</summary>
    public bool IsPlayerViewSelected => SelectedTool == ToolType.PlayerView;

    /// <summary>All available tool types, used to populate the toolbar's tool selector.</summary>
    public IReadOnlyList<ToolType> ToolTypes { get; } = Enum.GetValues<ToolType>();

    /// <summary>The child settings view model for the currently selected tool.</summary>
    public ToolSettingsViewModelBase CurrentToolSettings
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = null!;

    /// <summary><see langword="true"/> when the selected tool has a settings panel to display.</summary>
    public bool HasCurrentToolSettings => CurrentToolSettings.HasPanelContent;

    /// <summary>
    /// <see langword="true"/> when the tool rail should show the collapsed-state affordance for
    /// reopening the current tool settings panel.
    /// </summary>
    public bool ShowToolSettingsRailToggle => HasCurrentToolSettings && !IsToolSettingsPanelVisible;

    /// <summary>Texture seed shared with players so all clients render identical fog noise.</summary>
    public Guid FogSeed
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary><see langword="true"/> while this client is generating a textured fog bitmap.</summary>
    public bool IsFogGenerating
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

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

    /// <summary>Opens a file picker and loads the selected image or PDF as the map background.</summary>
    public ReactiveCommand<Unit, Unit> LoadMapCommand { get; }

    /// <summary>Activates the Brush tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectBrushCommand { get; }

    /// <summary>Activates the Shape tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectShapeCommand { get; }

    /// <summary>Activates the Pan tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectPanCommand { get; }

    /// <summary>Activates the Fog tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectFogCommand { get; }

    /// <summary>Activates the Cursor tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectCursorCommand { get; }

    /// <summary>Activates the Grid tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectGridCommand { get; }

    /// <summary>Activates the Stamp tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectStampCommand { get; }

    /// <summary>Activates the Player View tool.</summary>
    public ReactiveCommand<Unit, Unit> SelectPlayerViewCommand { get; }

    /// <summary>Sets all fog mask pixels to 255 (fully revealed) and pushes an undo entry.</summary>
    public ReactiveCommand<Unit, Unit> RevealAllCommand { get; }

    /// <summary>Sets all fog mask pixels to 0 (fully fogged) and pushes an undo entry.</summary>
    public ReactiveCommand<Unit, Unit> RefogAllCommand { get; }

    /// <summary>Shuts down the application.</summary>
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    /// <summary>Opens the application log directory in the platform file manager.</summary>
    public ReactiveCommand<Unit, Unit> OpenLogDirectoryCommand { get; }

    /// <summary>Undoes the most recent fog operation.</summary>
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }

    /// <summary>Redoes the most recently undone fog operation.</summary>
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    /// <summary>Opens the player discovery window so the DM can monitor connected players.</summary>
    public ReactiveCommand<Unit, Unit> OpenPlayerWindowCommand { get; }

    /// <summary>Opens the application information window.</summary>
    public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; }

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

    /// <summary>Toggles whether the floating tool/settings overlay is shown over the map.</summary>
    public ReactiveCommand<Unit, Unit> ToggleOverlayVisibilityCommand { get; }

    /// <summary>
    /// <see langword="true"/> when the floating tool palette and contextual settings panel are visible
    /// over the DM map view.
    /// </summary>
    public bool IsOverlayVisible
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    /// <summary>
    /// <see langword="true"/> when the contextual tool settings panel is visible beside the
    /// tool rail. The state remains unchanged across tool switches.
    /// </summary>
    public bool IsToolSettingsPanelVisible
    {
        get;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ShowToolSettingsRailToggle));
        }
    } = true;

    /// <summary>Toggles whether the contextual tool settings panel is shown beside the tool rail.</summary>
    public ReactiveCommand<Unit, Unit> ToggleToolSettingsPanelVisibilityCommand { get; }

    /// <summary>Toggles whether fog and viewport updates are held locally or sent to connected players immediately.</summary>
    public ReactiveCommand<Unit, Unit> TogglePauseUpdatesCommand { get; }

    /// <summary>
    /// When <see langword="true"/>, fog and player-facing changes are applied locally but not broadcast to players.
    /// Resuming sends a single full-mask delta and latest player viewport to bring all players up to date.
    /// </summary>
    public bool IsUpdatesPaused
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>The map-space rectangle and display transform players should see.</summary>
    public ViewportPayload? PlayerViewport
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
    /// Interaction that requests the view to display the application information dialog.
    /// Handled in <see cref="Views.DmView"/>.
    /// </summary>
    public Interaction<Unit, Unit> ShowAboutDialog { get; } = new();

    /// <summary>
    /// Interaction that requests the view to open a local directory path in the platform shell.
    /// Handled in <see cref="Views.DmView"/>.
    /// </summary>
    public Interaction<string, Unit> OpenDirectory { get; } = new();

    /// <summary>
    /// Raised after any fog modification (brush, shape, undo, redo, reveal-all, refog-all).
    /// The event argument is the bounding rectangle of the changed region. The view uses
    /// this to update only the affected portion of the fog bitmap rather than rebuilding it entirely.
    /// </summary>
    public event EventHandler<PixelRect>? FogUpdated;

    byte[]? _mapImageBytes;
    MapSession? _session;
    bool _hasPendingUpdates;
    bool _hasPendingViewportUpdate;
    bool _hasPendingCursorUpdate;
    bool _hasPendingFogAppearanceUpdate;
    bool _hasPendingGridUpdate;
    bool _hasPendingStampUpdate;
    ViewportPayload? _latestViewport;
    CursorPayload? _latestCursor;
    bool _isViewportBroadcastQueued;
    bool _isCursorBroadcastQueued;

    /// <summary>Fires every 2 seconds on the UI thread to refresh <see cref="MemoryUsage"/>.</summary>
    readonly DispatcherTimer _memoryTimer;

    /// <summary>
    /// Constructs the ViewModel and wires up all reactive commands and service event handlers.
    /// </summary>
    public DmViewModel(IFogMaskService fogService, IUndoRedoService undoRedo, IDmHostService hostService, IDiscoveryService discoveryService, IMapLoader mapLoader, Func<PlayerViewModel> createPlayer)
    {
        _fogService = fogService;
        _undoRedo = undoRedo;
        _circleBrush = new CircleBrush();
        _squareBrush = new SquareBrush();
        _diamondBrush = new DiamondBrush();
        _hostService = hostService;
        _discoveryService = discoveryService;
        _mapLoader = mapLoader;
        _createPlayer = createPlayer;

        BrushSettings = new BrushToolSettingsViewModel();
        ShapeSettings = new ShapeToolSettingsViewModel();
        FogSettings = new FogToolSettingsViewModel();
        CursorSettings = new CursorToolSettingsViewModel();
        GridSettings = new GridToolSettingsViewModel();
        PanSettings = new PanToolSettingsViewModel();
        StampSettings = new StampToolSettingsViewModel(
            () => ReorderSelectedStampToFront(),
            () => ReorderSelectedStampBy(1),
            () => ReorderSelectedStampBy(-1),
            () => ReorderSelectedStampToBack(),
            DuplicateSelectedStamp,
            DeleteSelectedStamp,
            SetSelectedStampAngle);
        PlayerViewSettings = new PlayerViewToolSettingsViewModel(RotatePlayerViewportClockwise, ResetPlayerViewportRotation);
        CurrentToolSettings = GetCurrentToolSettings(SelectedTool);

        FogSettings.PropertyChanged += OnFogSettingsPropertyChanged;
        GridSettings.PropertyChanged += OnGridSettingsPropertyChanged;
        PlayerViewSettings.PropertyChanged += OnPlayerViewSettingsPropertyChanged;

        _hostService.PlayerCountChanged += (_, count) => ConnectedPlayers = count;
        _undoRedo.StateChanged += (_, _) => { CanUndo = _undoRedo.CanUndo; CanRedo = _undoRedo.CanRedo; };

        MemoryUsage = FormatMemoryUsage();
        _memoryTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => MemoryUsage = FormatMemoryUsage());
        _memoryTimer.Start();

        LoadMapCommand = ReactiveCommand.CreateFromTask(LoadMapAsync);
        SelectBrushCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Brush; });
        SelectShapeCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Shape; });
        SelectPanCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Pan; });
        SelectFogCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Fog; });
        SelectCursorCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Cursor; });
        SelectGridCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Grid; });
        SelectStampCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.Stamp; });
        SelectPlayerViewCommand = ReactiveCommand.Create(() => { SelectedTool = ToolType.PlayerView; });

        var mapLoaded = this.WhenAnyValue(x => x.IsMapLoaded);
        RevealAllCommand = ReactiveCommand.Create(ExecuteRevealAll, mapLoaded);
        RefogAllCommand = ReactiveCommand.Create(ExecuteRefogAll, mapLoaded);
        ExitCommand = ReactiveCommand.Create(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        });
        OpenLogDirectoryCommand = ReactiveCommand.CreateFromTask(OpenLogDirectoryAsync);

        var canUndo = this.WhenAnyValue(x => x.CanUndo);
        var canRedo = this.WhenAnyValue(x => x.CanRedo);
        UndoCommand = ReactiveCommand.Create(ExecuteUndo, canUndo);
        RedoCommand = ReactiveCommand.Create(ExecuteRedo, canRedo);
        OpenPlayerWindowCommand = ReactiveCommand.CreateFromTask(OpenPlayerWindowAsync);
        ShowAboutCommand = ReactiveCommand.CreateFromTask(async () => await ShowAboutDialog.Handle(Unit.Default));
        ToggleMapVisibilityCommand = ReactiveCommand.Create(() => { IsMapVisible = !IsMapVisible; });
        ToggleOverlayVisibilityCommand = ReactiveCommand.Create(() => { IsOverlayVisible = !IsOverlayVisible; });
        ToggleToolSettingsPanelVisibilityCommand = ReactiveCommand.Create(() => { IsToolSettingsPanelVisible = !IsToolSettingsPanelVisible; });
        TogglePauseUpdatesCommand = ReactiveCommand.Create(() =>
        {
            IsUpdatesPaused = !IsUpdatesPaused;

            Log.Information("Player updates {State}.", IsUpdatesPaused ? "paused" : "resumed");
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
        Log.Information("Opening player window.");
        var playerVm = _createPlayer();
        await playerVm.StartDiscoveryAsync();
        await ShowPlayerWindow.Handle(playerVm);
    }

    /// <summary>Opens the shared application log directory using the view-layer shell interaction.</summary>
    async Task OpenLogDirectoryAsync()
    {
        Log.Information("Opening log directory {LogDirectory}.", AppDiagnostics.LogDirectory);
        await OpenDirectory.Handle(AppDiagnostics.LogDirectory);
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

        Log.Information("Loading map from {MapPath}.", path);

        try
        {
            var loadedMap = await _mapLoader.LoadAsync(path);
            _mapImageBytes = loadedMap.EncodedBytes;
            MapImage = loadedMap.Image;

            var pixelSize = loadedMap.PixelSize;
            MapSize = $"Map: {pixelSize.Width} x {pixelSize.Height}";
            _fogService.Initialize(pixelSize.Width, pixelSize.Height);
            FogMask = _fogService.Mask;

            _session = new MapSession(Guid.NewGuid(), pixelSize.Width, pixelSize.Height);
            FogSeed = _session.SessionId;
            _undoRedo.Clear();
            SelectedStamp = null;
            Stamps.Clear();
            SetPlayerViewport(CreateInitialPlayerViewport(pixelSize.Width, pixelSize.Height), queueBroadcast: false);
            IsMapLoaded = true;

            FogUpdated?.Invoke(this, new PixelRect(0, 0, pixelSize.Width, pixelSize.Height));

            await StartHostingAsync();
            BroadcastFogAppearance();
            BroadcastGridSettings();
            BroadcastStamps();
            QueueViewportBroadcast();

            Log.Information(
                "Loaded map {MapFileName} with dimensions {Width}x{Height}.",
                Path.GetFileName(path),
                pixelSize.Width,
                pixelSize.Height);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load map from {MapPath}.", path);
            throw;
        }
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

        var shapeType = ShapeSettings.SelectedShapeType;
        var (cx1, cy1, cx2, cy2) = ShapeConstraintHelper.NormalizeBounds(shapeType, x1, y1, x2, y2);

        if (cx1 == cx2 && cy1 == cy2)
            return;

        var shapeRect = ComputeShapeRect(cx1, cy1, cx2, cy2);
        if (IsEmpty(shapeRect))
            return;

        var before = FogDeltaCommand.CaptureFromMask(_fogService.Mask, shapeRect);

        var dirtyRect = ShapeTypeMetadata.IsEllipse(shapeType)
            ? _fogService.ApplyEllipse(cx1, cy1, cx2, cy2, (float)ShapeSettings.Softness, (float)ShapeSettings.Opacity, isErasing)
            : _fogService.ApplyRectangle(cx1, cy1, cx2, cy2, (float)ShapeSettings.Softness, (float)ShapeSettings.Opacity, ShapeSettings.CornerRadius, isErasing);

        if (IsEmpty(dirtyRect))
            return;

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
        if (minX > maxX || minY > maxY)
            return default;

        return new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
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

        var brush = BrushSettings.SelectedBrushShape switch
        {
            BrushShape.Circle => _circleBrush,
            BrushShape.Square => _squareBrush,
            BrushShape.Diamond => _diamondBrush,
            _ => throw new InvalidOperationException($"Unsupported brush shape: {BrushSettings.SelectedBrushShape}")
        };

        var settings = new BrushSettings(BrushSettings.Diameter, (float)BrushSettings.Softness, (float)BrushSettings.Opacity, Erase: isErasing);
        var dirtyRect = _fogService.ApplyBrush(brush, x1, y1, x2, y2, settings);
        if (IsEmpty(dirtyRect))
            return;

        FogUpdated?.Invoke(this, dirtyRect);
        SendFogDelta(dirtyRect);
    }

    /// <summary>
    /// Updates the authoritative player viewport rectangle and queues a broadcast to players.
    /// Repeated updates within a single UI tick are coalesced into a single send using the
    /// most recent viewport values. When updates are paused, only the snapshot is retained.
    /// </summary>
    public void UpdatePlayerViewport(ViewportPayload viewport) =>
        SetPlayerViewport(viewport, queueBroadcast: true);

    /// <summary>
    /// Updates the authoritative DM cursor snapshot and queues a broadcast to players.
    /// Repeated updates within a single UI tick are coalesced into a single send using the
    /// most recent cursor state. When updates are paused, only the snapshot is retained.
    /// </summary>
    public void UpdateCursor(CursorPayload cursor)
    {
        _latestCursor = cursor;
        QueueCursorBroadcast();
    }

    /// <summary>Broadcasts the current stamp layer after the canvas changes it.</summary>
    public void OnStampChanged()
    {
        StampSettings.ApplySelectedStamp(SelectedStamp);
        BroadcastStamps();
    }

    void SetSelectedStampAngle(double angle)
    {
        var stamp = SelectedStamp;
        if (stamp is null)
            return;

        stamp.RotationDegrees = NormalizeDegrees(angle);
        StampSettings.ApplySelectedStamp(stamp);
        RefreshSelectedStamp();
        BroadcastStamps();
    }

    void RefreshSelectedStamp()
    {
        var stamp = SelectedStamp;
        if (stamp is null)
            return;

        var index = Stamps.IndexOf(stamp);
        if (index >= 0)
            Stamps[index] = stamp;

        this.RaisePropertyChanged(nameof(SelectedStamp));
    }

    void DeleteSelectedStamp()
    {
        var stamp = SelectedStamp;
        if (stamp is null)
            return;

        Stamps.Remove(stamp);
        SelectedStamp = null;
        BroadcastStamps();
    }

    void ReorderSelectedStampBy(int delta)
    {
        var stamp = SelectedStamp;
        if (stamp is null)
            return;

        var index = Stamps.IndexOf(stamp);
        if (index < 0)
            return;

        ReorderSelectedStampTo(Math.Clamp(index + delta, 0, Stamps.Count - 1));
    }

    void ReorderSelectedStampToFront()
    {
        if (Stamps.Count > 0)
            ReorderSelectedStampTo(Stamps.Count - 1);
    }

    void ReorderSelectedStampToBack()
    {
        ReorderSelectedStampTo(0);
    }

    void ReorderSelectedStampTo(int nextIndex)
    {
        var stamp = SelectedStamp;
        if (stamp is null)
            return;

        var index = Stamps.IndexOf(stamp);
        if (index < 0)
            return;

        nextIndex = Math.Clamp(nextIndex, 0, Stamps.Count - 1);

        if (nextIndex == index)
            return;

        Stamps.RemoveAt(index);
        Stamps.Insert(nextIndex, stamp);
        SelectedStamp = stamp;
        BroadcastStamps();
    }

    void DuplicateSelectedStamp()
    {
        var stamp = SelectedStamp;
        if (stamp is null)
            return;

        var x = stamp.X + 16;
        var y = stamp.Y + 16;
        if (MapImage is not null)
        {
            x = Math.Clamp(x, 0, Math.Max(0, MapImage.Size.Width - stamp.Width));
            y = Math.Clamp(y, 0, Math.Max(0, MapImage.Size.Height - stamp.Height));
        }

        var duplicate = new StampInstance
        {
            TemplateId = stamp.TemplateId,
            X = x,
            Y = y,
            Width = stamp.Width,
            Height = stamp.Height,
            RotationDegrees = stamp.RotationDegrees,
        };

        Stamps.Add(duplicate);
        SelectedStamp = duplicate;
        BroadcastStamps();
    }

    ViewportPayload CreateInitialPlayerViewport(int mapWidth, int mapHeight) =>
        new()
        {
            CenterMapX = mapWidth / 2.0,
            CenterMapY = mapHeight / 2.0,
            ZoomLevel = 1.0,
            RotationQuarterTurns = 0,
            WidthMap = mapWidth,
            HeightMap = mapHeight,
            PaddingPixels = PlayerViewSettings.Padding,
        };

    void RotatePlayerViewportClockwise()
    {
        if (PlayerViewport is null)
            return;

        SetPlayerViewport(CopyPlayerViewport(
            PlayerViewport,
            rotationQuarterTurns: PlayerViewport.RotationQuarterTurns + 1,
            paddingPixels: PlayerViewSettings.Padding), queueBroadcast: true);
    }

    void ResetPlayerViewportRotation()
    {
        if (PlayerViewport is null)
            return;

        SetPlayerViewport(CopyPlayerViewport(
            PlayerViewport,
            rotationQuarterTurns: 0,
            paddingPixels: PlayerViewSettings.Padding), queueBroadcast: true);
    }

    void OnPlayerViewSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (PlayerViewSettings.IsApplyingViewport)
            return;

        if (e.PropertyName is not null
            and not (nameof(PlayerViewToolSettingsViewModel.OffsetX))
            and not (nameof(PlayerViewToolSettingsViewModel.OffsetY))
            and not (nameof(PlayerViewToolSettingsViewModel.Width))
            and not (nameof(PlayerViewToolSettingsViewModel.Height))
            and not (nameof(PlayerViewToolSettingsViewModel.Padding)))
            return;

        if (PlayerViewport is not null)
            SetPlayerViewport(CreatePlayerViewportFromSettings(PlayerViewport), queueBroadcast: true);
    }

    void SetPlayerViewport(ViewportPayload viewport, bool queueBroadcast)
    {
        var normalized = CopyPlayerViewport(viewport, paddingPixels: Math.Max(0, viewport.PaddingPixels));
        PlayerViewport = normalized;
        _latestViewport = normalized;
        ApplyPlayerViewportSettings(normalized);

        if (queueBroadcast)
            QueueViewportBroadcast();
    }

    void ApplyPlayerViewportSettings(ViewportPayload viewport)
    {
        var mapWidth = Math.Max(1, MapImage?.Size.Width ?? viewport.WidthMap);
        var mapHeight = Math.Max(1, MapImage?.Size.Height ?? viewport.HeightMap);

        PlayerViewSettings.ApplyViewport(
            viewport.CenterMapX - viewport.WidthMap / 2.0,
            viewport.CenterMapY - viewport.HeightMap / 2.0,
            viewport.WidthMap,
            viewport.HeightMap,
            viewport.PaddingPixels,
            mapWidth,
            mapHeight);
    }

    ViewportPayload CreatePlayerViewportFromSettings(ViewportPayload current)
    {
        var width = Math.Max(1, PlayerViewSettings.Width);
        var height = Math.Max(1, PlayerViewSettings.Height);
        var x = Math.Max(0, PlayerViewSettings.OffsetX);
        var y = Math.Max(0, PlayerViewSettings.OffsetY);

        if (MapImage is not null)
        {
            width = Math.Min(width, MapImage.Size.Width);
            height = Math.Min(height, MapImage.Size.Height);
            x = Math.Clamp(x, 0, Math.Max(0, MapImage.Size.Width - width));
            y = Math.Clamp(y, 0, Math.Max(0, MapImage.Size.Height - height));
        }

        return new ViewportPayload
        {
            CenterMapX = x + width / 2.0,
            CenterMapY = y + height / 2.0,
            ZoomLevel = current.ZoomLevel <= 0 ? 1.0 : current.ZoomLevel,
            RotationQuarterTurns = current.RotationQuarterTurns,
            WidthMap = width,
            HeightMap = height,
            PaddingPixels = Math.Max(0, PlayerViewSettings.Padding),
        };
    }

    static ViewportPayload CopyPlayerViewport(
        ViewportPayload viewport,
        int? rotationQuarterTurns = null,
        double? paddingPixels = null) =>
        new()
        {
            CenterMapX = viewport.CenterMapX,
            CenterMapY = viewport.CenterMapY,
            ZoomLevel = viewport.ZoomLevel,
            RotationQuarterTurns = NormalizeRotation(rotationQuarterTurns ?? viewport.RotationQuarterTurns),
            WidthMap = viewport.WidthMap,
            HeightMap = viewport.HeightMap,
            PaddingPixels = Math.Max(0, paddingPixels ?? viewport.PaddingPixels),
        };

    static int NormalizeRotation(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;

    static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
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

        if (IsEmpty(dirtyRect))
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

    static bool IsEmpty(PixelRect rect) => rect.Width <= 0 || rect.Height <= 0;

    void BroadcastGridSettings()
    {
        if (IsUpdatesPaused)
        {
            _hasPendingGridUpdate = true;
            return;
        }

        _ = _hostService.SendGridSettingsAsync(CreateGridSettingsPayload(), default);
    }

    void BroadcastStamps()
    {
        if (_session is null)
            return;

        if (IsUpdatesPaused)
        {
            _hasPendingStampUpdate = true;
            return;
        }

        _ = _hostService.SendStampsAsync(new StampLayerPayload { Stamps = Stamps }, default);
    }

    /// <summary>
    /// Fires-and-forgets a broadcast of the current fog appearance (type + colour + seed)
    /// to all connected players. Called whenever the fog settings change, and once per map load.
    /// Skipped before the host starts (no session yet).
    /// </summary>
    void BroadcastFogAppearance()
    {
        if (_session is null)
            return;

        if (IsUpdatesPaused)
        {
            _hasPendingFogAppearanceUpdate = true;
            return;
        }

        _ = _hostService.SendFogAppearanceAsync(CreateFogAppearancePayload(), default);
    }

    GridSettingsPayload CreateGridSettingsPayload() =>
        new()
        {
            IsVisible = GridSettings.IsVisible,
            SquareSize = GridSettings.SquareSize,
            LineWidth = GridSettings.LineWidth,
            R = GridSettings.Color.R,
            G = GridSettings.Color.G,
            B = GridSettings.Color.B,
            A = GridSettings.Color.A,
            OffsetX = GridSettings.OffsetX,
            OffsetY = GridSettings.OffsetY,
        };

    FogAppearancePayload CreateFogAppearancePayload() =>
        new()
        {
            FogType = FogSettings.SelectedFogType,
            R = FogSettings.Color.R,
            G = FogSettings.Color.G,
            B = FogSettings.Color.B,
            Seed = FogSeed,
        };

    void OnFogSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or (nameof(FogToolSettingsViewModel.SelectedFogType))
            or (nameof(FogToolSettingsViewModel.Color)))
            BroadcastFogAppearance();
    }

    void OnGridSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or (nameof(GridToolSettingsViewModel.IsVisible))
            or (nameof(GridToolSettingsViewModel.SquareSize))
            or (nameof(GridToolSettingsViewModel.LineWidth))
            or (nameof(GridToolSettingsViewModel.Color))
            or (nameof(GridToolSettingsViewModel.OffsetX))
            or (nameof(GridToolSettingsViewModel.OffsetY)))
            BroadcastGridSettings();
    }

    ToolSettingsViewModelBase GetCurrentToolSettings(ToolType tool) =>
        tool switch
        {
            ToolType.Brush => BrushSettings,
            ToolType.Shape => ShapeSettings,
            ToolType.Fog => FogSettings,
            ToolType.Cursor => CursorSettings,
            ToolType.Grid => GridSettings,
            ToolType.Stamp => StampSettings,
            ToolType.PlayerView => PlayerViewSettings,
            ToolType.Pan => PanSettings,
            _ => PanSettings
        };

    /// <summary>
    /// Queues a viewport broadcast for the next UI dispatch pass so multiple offset/zoom changes
    /// produced by a single pan or wheel gesture collapse into one authoritative send.
    /// </summary>
    void QueueViewportBroadcast()
    {
        if (_session is null || _latestViewport is null)
            return;

        if (IsUpdatesPaused)
        {
            _hasPendingViewportUpdate = true;
            return;
        }

        if (_isViewportBroadcastQueued)
            return;

        _isViewportBroadcastQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isViewportBroadcastQueued = false;
            if (_session is null || _latestViewport is null)
                return;

            if (IsUpdatesPaused)
            {
                _hasPendingViewportUpdate = true;
                return;
            }

            _ = _hostService.SendViewportAsync(_latestViewport, default);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Queues a cursor broadcast for the next UI dispatch pass so rapid pointer movement
    /// collapses into one authoritative send per UI tick.
    /// </summary>
    void QueueCursorBroadcast()
    {
        if (_session is null || _latestCursor is null)
            return;

        if (IsUpdatesPaused)
        {
            _hasPendingCursorUpdate = true;
            return;
        }

        if (_isCursorBroadcastQueued)
            return;

        _isCursorBroadcastQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isCursorBroadcastQueued = false;
            if (_session is null || _latestCursor is null)
                return;

            if (IsUpdatesPaused)
            {
                _hasPendingCursorUpdate = true;
                return;
            }

            _ = _hostService.SendCursorAsync(_latestCursor, default);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Sends the full current fog mask and latest viewport to all players, catching them up on
    /// every change made while updates were paused.
    /// </summary>
    void FlushPendingUpdates()
    {
        if (_hasPendingUpdates && _fogService.Mask is not null)
        {
            _hasPendingUpdates = false;
            var mask = _fogService.Mask;
            var delta = FogDelta.FromMask(mask, 0, 0, mask.Width, mask.Height);
            _ = _hostService.SendFogDeltaAsync(delta, default);
        }

        if (_hasPendingViewportUpdate && _session is not null && _latestViewport is not null)
        {
            _hasPendingViewportUpdate = false;
            _ = _hostService.SendViewportAsync(_latestViewport, default);
        }

        if (_hasPendingCursorUpdate && _session is not null && _latestCursor is not null)
        {
            _hasPendingCursorUpdate = false;
            _ = _hostService.SendCursorAsync(_latestCursor, default);
        }

        if (_hasPendingFogAppearanceUpdate && _session is not null)
        {
            _hasPendingFogAppearanceUpdate = false;
            _ = _hostService.SendFogAppearanceAsync(CreateFogAppearancePayload(), default);
        }

        if (_hasPendingGridUpdate && _session is not null)
        {
            _hasPendingGridUpdate = false;
            _ = _hostService.SendGridSettingsAsync(CreateGridSettingsPayload(), default);
        }

        if (_hasPendingStampUpdate && _session is not null)
        {
            _hasPendingStampUpdate = false;
            _ = _hostService.SendStampsAsync(new StampLayerPayload { Stamps = Stamps }, default);
        }
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

        if (_fogService.Mask != null)
            await _hostService.SendSessionInfoAsync(_session, _fogService.Mask, default);

        // Existing players must receive the replacement session/fog state before the new
        // map image, otherwise the bitmap can swap under the old mask for a visible frame.
        if (_mapImageBytes != null)
            await _hostService.SendMapImageAsync(_mapImageBytes, default);

        if (_latestViewport is not null)
            await _hostService.SendViewportAsync(_latestViewport, default);

        if (_latestCursor is not null)
            await _hostService.SendCursorAsync(_latestCursor, default);

        await _hostService.SendStampsAsync(new StampLayerPayload { Stamps = Stamps }, default);

        await _discoveryService.StartBroadcastingAsync(_session, _hostService.Port, default);
        Log.Information(
            "Hosting session {SessionId} on TCP port {Port} and broadcasting discovery.",
            _session.SessionId,
            _hostService.Port);
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
            FogSettings.PropertyChanged -= OnFogSettingsPropertyChanged;
            GridSettings.PropertyChanged -= OnGridSettingsPropertyChanged;
            PlayerViewSettings.PropertyChanged -= OnPlayerViewSettingsPropertyChanged;
            _memoryTimer.Stop();
            (_hostService as IDisposable)?.Dispose();
            (_discoveryService as IDisposable)?.Dispose();
        }
    }
}
