using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;

using DMap.Commands;
using DMap.Dm;
using DMap.Models;
using DMap.Protocol;

namespace DMap.Controls;

/// <summary>
/// Event arguments for a brush stroke segment, carrying the start and end coordinates
/// in map pixels and whether the stroke is erasing fog.
/// </summary>
public class BrushStrokeEventArgs : EventArgs
{
    /// <summary>Start X coordinate in map pixels.</summary>
    public int MapX1 { get; init; }

    /// <summary>Start Y coordinate in map pixels.</summary>
    public int MapY1 { get; init; }

    /// <summary>End X coordinate in map pixels.</summary>
    public int MapX2 { get; init; }

    /// <summary>End Y coordinate in map pixels.</summary>
    public int MapY2 { get; init; }

    /// <summary><see langword="true"/> when the stroke is removing fog; <see langword="false"/> when revealing.</summary>
    public bool IsErasing { get; init; }
}

/// <summary>
/// Event arguments for a completed shape drag gesture, carrying the bounding box corners
/// in map pixels and whether the shape is erasing fog.
/// </summary>
public class ShapeStrokeEventArgs : EventArgs
{
    /// <summary>First corner X in map pixels.</summary>
    public int MapX1 { get; init; }

    /// <summary>First corner Y in map pixels.</summary>
    public int MapY1 { get; init; }

    /// <summary>Opposite corner X in map pixels.</summary>
    public int MapX2 { get; init; }

    /// <summary>Opposite corner Y in map pixels.</summary>
    public int MapY2 { get; init; }

    /// <summary><see langword="true"/> when the shape is removing fog; <see langword="false"/> when revealing.</summary>
    public bool IsErasing { get; init; }
}

/// <summary>
/// Event arguments for stamp-layer mutations produced by the canvas.
/// </summary>
public class StampChangedEventArgs : EventArgs
{
    public StampChangedEventArgs(StampInstance stamp)
    {
        Stamp = stamp;
    }

    public StampInstance Stamp { get; }
}

/// <summary>
/// Custom Avalonia control that renders a map image, a fog-of-war overlay, and a tool cursor
/// preview. Handles pointer input to produce brush strokes, shape drags, panning, and zooming.
/// In player mode (<see cref="IsDmMode"/> = <see langword="false"/>) all editing and camera input is suppressed.
/// </summary>
public class MapCanvas : Control
{
    /// <summary>Styled property for the map background image.</summary>
    public static readonly StyledProperty<Bitmap?> MapImageProperty =
        AvaloniaProperty.Register<MapCanvas, Bitmap?>(nameof(MapImage));

    /// <summary>Styled property for the fog mask used to build the fog bitmap.</summary>
    public static readonly StyledProperty<FogMask?> FogMaskProperty =
        AvaloniaProperty.Register<MapCanvas, FogMask?>(nameof(FogMask));

    /// <summary>Direct property for fit-relative zoom shown in the DM toolbar.</summary>
    public static readonly DirectProperty<MapCanvas, decimal?> ZoomPercentProperty =
        AvaloniaProperty.RegisterDirect<MapCanvas, decimal?>(
            nameof(ZoomPercent),
            canvas => canvas.ZoomPercent,
            (canvas, value) => canvas.ZoomPercent = value,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Styled property that enables DM editing mode (brush/shape input, cursor preview).</summary>
    public static readonly StyledProperty<bool> IsDmModeProperty =
        AvaloniaProperty.Register<MapCanvas, bool>(nameof(IsDmMode));

    /// <summary>Styled property for the fog overlay opacity (0 = transparent, 1 = fully opaque black).</summary>
    public static readonly StyledProperty<double> FogOpacityProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(FogOpacity), 1.0);

    /// <summary>Styled property for the fog overlay style (flat colour or one of the textured variants).</summary>
    public static readonly StyledProperty<FogType> FogTypeProperty =
        AvaloniaProperty.Register<MapCanvas, FogType>(nameof(FogType), FogType.Color);

    /// <summary>Styled property for the flat fog colour (used when <see cref="FogType"/> is <see cref="FogType.Color"/>).</summary>
    public static readonly StyledProperty<Color> FogColorProperty =
        AvaloniaProperty.Register<MapCanvas, Color>(nameof(FogColor), Colors.Black);

    /// <summary>Styled property for the texture seed (typically the session ID) so DM and players see identical textures.</summary>
    public static readonly StyledProperty<Guid> FogSeedProperty =
        AvaloniaProperty.Register<MapCanvas, Guid>(nameof(FogSeed), Guid.Empty);

    /// <summary>Styled property indicating that a textured fog overlay is being generated in the background.</summary>
    public static readonly StyledProperty<bool> IsFogGeneratingProperty =
        AvaloniaProperty.Register<MapCanvas, bool>(nameof(IsFogGenerating));

    /// <summary>Styled property for the brush diameter preview in screen pixels.</summary>
    public static readonly StyledProperty<int> BrushDiameterProperty =
        AvaloniaProperty.Register<MapCanvas, int>(nameof(BrushDiameter), 50);

    /// <summary>Styled property for the currently active editing tool.</summary>
    public static readonly StyledProperty<ToolType> ActiveToolProperty =
        AvaloniaProperty.Register<MapCanvas, ToolType>(nameof(ActiveTool), ToolType.Brush);

    /// <summary>Styled property for the brush shape used in cursor preview.</summary>
    public static readonly StyledProperty<BrushShape> BrushShapeProperty =
        AvaloniaProperty.Register<MapCanvas, BrushShape>(nameof(BrushShape), BrushShape.Circle);

    /// <summary>Styled property for the shape type used in cursor preview and shape strokes.</summary>
    public static readonly StyledProperty<ShapeType> ShapeTypeProperty =
        AvaloniaProperty.Register<MapCanvas, ShapeType>(nameof(ShapeType), ShapeType.Rectangle);

    /// <summary>Styled property for the fixed corner radius used by rectangular shape previews.</summary>
    public static readonly StyledProperty<int> ShapeCornerRadiusProperty =
        AvaloniaProperty.Register<MapCanvas, int>(nameof(ShapeCornerRadius));

    /// <summary>Styled property for the player-visible cursor icon type.</summary>
    public static readonly StyledProperty<CursorType> CursorTypeProperty =
        AvaloniaProperty.Register<MapCanvas, CursorType>(nameof(CursorType), CursorType.Crosshair);

    /// <summary>Styled property for the player-visible cursor icon size in screen pixels.</summary>
    public static readonly StyledProperty<int> CursorSizeProperty =
        AvaloniaProperty.Register<MapCanvas, int>(nameof(CursorSize), 64);

    /// <summary>Styled property controlling whether the cursor only appears while left click is held.</summary>
    public static readonly StyledProperty<bool> ShowCursorOnlyWhilePressedProperty =
        AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowCursorOnlyWhilePressed));

    /// <summary>Styled property for the player-visible cursor X coordinate in map space.</summary>
    public static readonly StyledProperty<double> CursorMapXProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(CursorMapX));

    /// <summary>Styled property for the player-visible cursor Y coordinate in map space.</summary>
    public static readonly StyledProperty<double> CursorMapYProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(CursorMapY));

    /// <summary>Styled property controlling whether the player-visible cursor is rendered.</summary>
    public static readonly StyledProperty<bool> IsCursorVisibleProperty =
        AvaloniaProperty.Register<MapCanvas, bool>(nameof(IsCursorVisible));

    /// <summary>Styled property controlling whether the map image is rendered (true) or replaced with white (false).</summary>
    public static readonly StyledProperty<bool> ShowMapProperty =
        AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowMap), true);

    public static readonly StyledProperty<bool> IsGridVisibleProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(IsGridVisible));
    public static readonly StyledProperty<double> GridSquareSizeProperty = AvaloniaProperty.Register<MapCanvas, double>(nameof(GridSquareSize), 70);
    public static readonly StyledProperty<double> GridLineWidthProperty = AvaloniaProperty.Register<MapCanvas, double>(nameof(GridLineWidth), 1);
    public static readonly StyledProperty<double> GridOpacityProperty = AvaloniaProperty.Register<MapCanvas, double>(nameof(GridOpacity), 0.65);
    public static readonly StyledProperty<Color> GridColorProperty = AvaloniaProperty.Register<MapCanvas, Color>(nameof(GridColor), Colors.White);
    public static readonly StyledProperty<double> GridOffsetXProperty = AvaloniaProperty.Register<MapCanvas, double>(nameof(GridOffsetX));
    public static readonly StyledProperty<double> GridOffsetYProperty = AvaloniaProperty.Register<MapCanvas, double>(nameof(GridOffsetY));

    public static readonly StyledProperty<IList<StampInstance>?> StampsProperty =
        AvaloniaProperty.Register<MapCanvas, IList<StampInstance>?>(nameof(Stamps));

    public static readonly StyledProperty<StampInstance?> SelectedStampProperty =
        AvaloniaProperty.Register<MapCanvas, StampInstance?>(
            nameof(SelectedStamp),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> SelectedStampTemplateIdProperty =
        AvaloniaProperty.Register<MapCanvas, string?>(nameof(SelectedStampTemplateId));

    public static readonly StyledProperty<ViewportPayload?> PlayerViewportProperty =
        AvaloniaProperty.Register<MapCanvas, ViewportPayload?>(nameof(PlayerViewport));

    /// <summary>The map background image, or <see langword="null"/> when no map is loaded.</summary>
    public Bitmap? MapImage
    {
        get => GetValue(MapImageProperty);
        set => SetValue(MapImageProperty, value);
    }

    /// <summary>The fog mask used to build and update the fog overlay bitmap.</summary>
    public FogMask? FogMask
    {
        get => GetValue(FogMaskProperty);
        set => SetValue(FogMaskProperty, value);
    }

    /// <summary>Actual zoom multiplier applied via a scale transform before the pan offset.</summary>
    public double ZoomLevel
    {
        get => _viewport.ZoomLevel;
        set
        {
            _viewport.SetZoomLevel(value, Bounds.Size, MapImage?.Size);
            OnViewportStateChanged();
        }
    }

    /// <summary>Zoom level expressed as a percentage of the height-fit zoom, where 100% fits map height to canvas height.</summary>
    public decimal? ZoomPercent
    {
        get => _zoomPercent;
        set
        {
            if (value.HasValue)
            {
                _viewport.SetZoomPercent((double)value.Value, Bounds.Size, MapImage?.Size);
                OnViewportStateChanged();
            }
        }
    }

    /// <summary>Horizontal translation of the canvas in screen pixels.</summary>
    public double OffsetX
    {
        get => _viewport.OffsetX;
        set
        {
            _viewport.SetOffset(value, OffsetY);
            OnViewportStateChanged();
        }
    }

    /// <summary>Vertical translation of the canvas in screen pixels.</summary>
    public double OffsetY
    {
        get => _viewport.OffsetY;
        set
        {
            _viewport.SetOffset(OffsetX, value);
            OnViewportStateChanged();
        }
    }

    /// <summary>
    /// When <see langword="true"/>, the canvas accepts brush/shape input and draws the tool cursor preview.
    /// When <see langword="false"/> (player mode), the canvas is display-only and does not accept
    /// local editing, panning, or zooming input.
    /// </summary>
    public bool IsDmMode
    {
        get => GetValue(IsDmModeProperty);
        set => SetValue(IsDmModeProperty, value);
    }

    /// <summary>
    /// Opacity of the black fog overlay layer in the range [0, 1].
    /// Changing this value triggers a full fog bitmap region update and a visual invalidation.
    /// </summary>
    public double FogOpacity
    {
        get => GetValue(FogOpacityProperty);
        set => SetValue(FogOpacityProperty, value);
    }

    /// <summary>Selected fog overlay style.</summary>
    public FogType FogType
    {
        get => GetValue(FogTypeProperty);
        set => SetValue(FogTypeProperty, value);
    }

    /// <summary>Flat fog colour used when <see cref="FogType"/> is <see cref="FogType.Color"/>.</summary>
    public Color FogColor
    {
        get => GetValue(FogColorProperty);
        set => SetValue(FogColorProperty, value);
    }

    /// <summary>Texture seed used to generate deterministic noise for textured fog types.</summary>
    public Guid FogSeed
    {
        get => GetValue(FogSeedProperty);
        set => SetValue(FogSeedProperty, value);
    }

    /// <summary><see langword="true"/> while a non-colour fog texture is being generated client-side.</summary>
    public bool IsFogGenerating
    {
        get => GetValue(IsFogGeneratingProperty);
        private set => SetValue(IsFogGeneratingProperty, value);
    }

    /// <summary>Brush diameter in map pixels, used to scale the cursor preview outline.</summary>
    public int BrushDiameter
    {
        get => GetValue(BrushDiameterProperty);
        set => SetValue(BrushDiameterProperty, value);
    }

    /// <summary>The currently active editing tool, controlling input behavior and cursor appearance.</summary>
    public ToolType ActiveTool
    {
        get => GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    /// <summary>Shape of the brush tip, used to draw the correct cursor outline.</summary>
    public BrushShape BrushShape
    {
        get => GetValue(BrushShapeProperty);
        set => SetValue(BrushShapeProperty, value);
    }

    /// <summary>Geometric shape drawn by the Shape tool.</summary>
    public ShapeType ShapeType
    {
        get => GetValue(ShapeTypeProperty);
        set => SetValue(ShapeTypeProperty, value);
    }

    /// <summary>Fixed corner radius in map pixels used for rectangular shape previews.</summary>
    public int ShapeCornerRadius
    {
        get => GetValue(ShapeCornerRadiusProperty);
        set => SetValue(ShapeCornerRadiusProperty, value);
    }

    /// <summary>Player-visible cursor icon type.</summary>
    public CursorType CursorType
    {
        get => GetValue(CursorTypeProperty);
        set => SetValue(CursorTypeProperty, value);
    }

    /// <summary>Player-visible cursor icon size in screen pixels.</summary>
    public int CursorSize
    {
        get => GetValue(CursorSizeProperty);
        set => SetValue(CursorSizeProperty, value);
    }

    /// <summary><see langword="true"/> when the cursor should only be visible while left click is held.</summary>
    public bool ShowCursorOnlyWhilePressed
    {
        get => GetValue(ShowCursorOnlyWhilePressedProperty);
        set => SetValue(ShowCursorOnlyWhilePressedProperty, value);
    }

    /// <summary>Player-visible cursor X coordinate in map space.</summary>
    public double CursorMapX
    {
        get => GetValue(CursorMapXProperty);
        set => SetValue(CursorMapXProperty, value);
    }

    /// <summary>Player-visible cursor Y coordinate in map space.</summary>
    public double CursorMapY
    {
        get => GetValue(CursorMapYProperty);
        set => SetValue(CursorMapYProperty, value);
    }

    /// <summary><see langword="true"/> when the player-visible cursor should be rendered.</summary>
    public bool IsCursorVisible
    {
        get => GetValue(IsCursorVisibleProperty);
        set => SetValue(IsCursorVisibleProperty, value);
    }

    /// <summary>
    /// When <see langword="false"/>, the map image is replaced with a white fill so the fog mask
    /// boundaries are easier to inspect. The fog overlay is still rendered.
    /// </summary>
    public bool ShowMap
    {
        get => GetValue(ShowMapProperty);
        set => SetValue(ShowMapProperty, value);
    }

    public bool IsGridVisible { get => GetValue(IsGridVisibleProperty); set => SetValue(IsGridVisibleProperty, value); }
    public double GridSquareSize { get => GetValue(GridSquareSizeProperty); set => SetValue(GridSquareSizeProperty, value); }
    public double GridLineWidth { get => GetValue(GridLineWidthProperty); set => SetValue(GridLineWidthProperty, value); }
    public double GridOpacity { get => GetValue(GridOpacityProperty); set => SetValue(GridOpacityProperty, value); }
    public Color GridColor { get => GetValue(GridColorProperty); set => SetValue(GridColorProperty, value); }
    public double GridOffsetX { get => GetValue(GridOffsetXProperty); set => SetValue(GridOffsetXProperty, value); }
    public double GridOffsetY { get => GetValue(GridOffsetYProperty); set => SetValue(GridOffsetYProperty, value); }

    public IList<StampInstance>? Stamps
    {
        get => GetValue(StampsProperty);
        set => SetValue(StampsProperty, value);
    }

    public StampInstance? SelectedStamp
    {
        get => GetValue(SelectedStampProperty);
        set => SetValue(SelectedStampProperty, value);
    }

    public string? SelectedStampTemplateId
    {
        get => GetValue(SelectedStampTemplateIdProperty);
        set => SetValue(SelectedStampTemplateIdProperty, value);
    }

    /// <summary>The map-space rectangle and display transform that players should see.</summary>
    public ViewportPayload? PlayerViewport
    {
        get => GetValue(PlayerViewportProperty);
        set => SetValue(PlayerViewportProperty, value);
    }

    /// <summary>Raised when the user presses the pointer to begin a brush stroke.</summary>
    public event EventHandler? BrushStrokeStarted;

    /// <summary>Raised when the user releases the pointer to end a brush stroke.</summary>
    public event EventHandler? BrushStrokeEnded;

    /// <summary>
    /// Raised for each pointer-move event while a brush stroke is in progress,
    /// carrying the previous and current map coordinates.
    /// </summary>
    public event EventHandler<BrushStrokeEventArgs>? BrushStrokeApplied;

    /// <summary>
    /// Raised once when the user releases the pointer after dragging a shape,
    /// carrying the bounding box corners in map coordinates.
    /// </summary>
    public event EventHandler<ShapeStrokeEventArgs>? ShapeStrokeApplied;

    public event EventHandler<StampChangedEventArgs>? StampChanged;

    /// <summary>
    /// Raised whenever the DM camera changes, expressed as a map-space center coordinate plus zoom.
    /// </summary>
    public event EventHandler<ViewportPayload>? ViewportChanged;

    /// <summary>
    /// Raised whenever the DM changes the player viewport rectangle.
    /// </summary>
    public event EventHandler<ViewportPayload>? PlayerViewportChanged;

    /// <summary>
    /// Raised whenever the DM cursor state changes, expressed in map-space coordinates.
    /// </summary>
    public event EventHandler<CursorPayload>? CursorUpdated;

    /// <summary>Increases the fit-relative zoom by 20%.</summary>
    public ICommand ZoomInCommand { get; }

    /// <summary>Decreases the fit-relative zoom by ~17%.</summary>
    public ICommand ZoomOutCommand { get; }

    /// <summary>Fits the map height to the canvas and aligns its top and bottom edges with the canvas.</summary>
    public ICommand RefitViewCommand { get; }

    /// <summary>Rotates the map view 90 degrees counter-clockwise.</summary>
    public ICommand RotateLeftCommand { get; }

    /// <summary>Rotates the map view 90 degrees clockwise.</summary>
    public ICommand RotateRightCommand { get; }

    readonly MapViewportController _viewport = new();
    readonly FogBitmapController _fogBitmapController;
    readonly ContextMenu _stampContextMenu;
    bool _isPanning;
    Point _lastPanPoint;
    bool _isPainting;
    bool _isErasing;
    int _lastBrushMapX;
    int _lastBrushMapY;
    bool _isDraggingShape;
    bool _isCursorPressed;
    bool _isDraggingStamp;
    bool _isDraggingPlayerViewport;
    StampDragMode _stampDragMode;
    StampHandle _activeStampHandle;
    PlayerViewportHandle _activePlayerViewportHandle;
    Point _stampDragStartMap;
    Rect _stampDragStartRect;
    Point _playerViewportDragStartMap;
    Rect _playerViewportDragStartRect;
    double _stampDragStartRotationDegrees;
    double _stampDragStartPointerAngleDegrees;
    Point _shapeDragStart;
    Point _lastMousePosition;
    Rect _playerContentClip;
    decimal? _zoomPercent;
    INotifyCollectionChanged? _subscribedStampsCollection;
    static readonly Uri _iconBaseUri = new("avares://DMap/Assets/Icons/");
    static readonly IReadOnlyDictionary<CursorType, IImage> _cursorIcons = CreateCursorIcons();
    static readonly IReadOnlyDictionary<string, IImage> _stampImages = CreateStampImages();

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(
            MapImageProperty, FogMaskProperty, FogOpacityProperty,
            BrushDiameterProperty, ActiveToolProperty, BrushShapeProperty,
            ShapeTypeProperty, ShapeCornerRadiusProperty, CursorTypeProperty, CursorSizeProperty, CursorMapXProperty,
            CursorMapYProperty, IsCursorVisibleProperty, ShowMapProperty,
            IsGridVisibleProperty, GridSquareSizeProperty, GridLineWidthProperty, GridOpacityProperty, GridColorProperty, GridOffsetXProperty, GridOffsetYProperty,
            FogTypeProperty, FogColorProperty, FogSeedProperty, StampsProperty, SelectedStampProperty, PlayerViewportProperty);
    }

    /// <summary>Initialises the control with clipping and keyboard focus enabled.</summary>
    public MapCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        _fogBitmapController = new FogBitmapController();
        _stampContextMenu = CreateStampContextMenu();
        _fogBitmapController.Invalidated += (_, _) => InvalidateVisual();
        _fogBitmapController.IsGeneratingChanged += (_, isGenerating) => IsFogGenerating = isGenerating;
        ZoomInCommand = new RelayCommand(() => ZoomLevel *= 1.2);
        ZoomOutCommand = new RelayCommand(() => ZoomLevel /= 1.2);
        RefitViewCommand = new RelayCommand(RefitViewToMapHeight);
        RotateLeftCommand = new RelayCommand(() => RotateView(-1));
        RotateRightCommand = new RelayCommand(() => RotateView(1));
    }

    ContextMenu CreateStampContextMenu()
    {
        var bringToFront = CreateStampMenuItem("Bring to Front", "bring-to-front.svg");
        bringToFront.Click += (_, _) => ReorderSelectedStampToFront();

        var bringForward = CreateStampMenuItem("Bring Forward", "move-up.svg");
        bringForward.Click += (_, _) => ReorderSelectedStampBy(1);

        var sendBackward = CreateStampMenuItem("Send Backward", "move-down.svg");
        sendBackward.Click += (_, _) => ReorderSelectedStampBy(-1);

        var sendToBack = CreateStampMenuItem("Send to Back", "send-to-back.svg");
        sendToBack.Click += (_, _) => ReorderSelectedStampToBack();

        var duplicate = CreateStampMenuItem("Duplicate", "copy.svg");
        duplicate.Click += (_, _) => DuplicateSelectedStamp();

        return new ContextMenu
        {
            Placement = PlacementMode.Pointer,
            ItemsSource = new Control[]
            {
                bringToFront,
                bringForward,
                sendBackward,
                sendToBack,
                duplicate,
            },
        };
    }

    static MenuItem CreateStampMenuItem(string header, string iconFileName)
    {
        var uri = new Uri(_iconBaseUri, iconFileName);
        return new MenuItem
        {
            Header = header,
            Icon = new Image
            {
                Width = 16,
                Height = 16,
                Source = new SvgImage { Source = SvgSource.Load(uri.ToString(), null) },
            },
        };
    }

    static Dictionary<CursorType, IImage> CreateCursorIcons()
    {
        var icons = new Dictionary<CursorType, IImage>();
        foreach (var cursorType in Enum.GetValues<CursorType>())
        {
            var uri = new Uri(_iconBaseUri, CursorTypeMetadata.GetIconFileName(cursorType));
            icons[cursorType] = new SvgImage { Source = SvgSource.Load(uri.ToString(), null) };
        }

        return icons;
    }

    static Dictionary<string, IImage> CreateStampImages()
    {
        var images = new Dictionary<string, IImage>();
        foreach (var template in StampCatalog.Templates)
            images[template.Id] = new Bitmap(AssetLoader.Open(new Uri(template.AssetPath)));

        return images;
    }

    /// <summary>
    /// Updates the fog bitmap for <paramref name="dirtyRect"/> and requests a visual redraw.
    /// Call this after the fog mask has been modified to sync the bitmap without rebuilding it entirely.
    /// </summary>
    /// <param name="dirtyRect">The region of the mask that changed.</param>
    public void InvalidateFogRegion(PixelRect dirtyRect) =>
        _fogBitmapController.InvalidateRegion(dirtyRect, GetFogBitmapSettings());

    /// <summary>
    /// Discards the existing fog bitmap and builds a new <see cref="WriteableBitmap"/> sized to match
    /// the current <see cref="FogMask"/>. Call this when the mask is replaced entirely (e.g. new map load
    /// or full fog received from the DM).
    /// </summary>
    public void RebuildFogBitmap() =>
        _fogBitmapController.Rebuild(GetFogBitmapSettings());

    /// <summary>Captures current fog rendering inputs for the fog bitmap controller.</summary>
    FogBitmapSettings GetFogBitmapSettings() =>
        new(FogMask, FogType, FogColor, FogOpacity, FogSeed);

    /// <summary>
    /// Returns the current viewport expressed as a map-space center coordinate plus zoom so it can
    /// be mirrored on canvases with different screen sizes.
    /// </summary>
    public ViewportPayload GetViewport()
        => _viewport.GetViewport(Bounds.Size, MapImage?.Size);

    /// <summary>
    /// Applies a remotely provided viewport by deriving local screen offsets from the current control
    /// bounds and the desired map-space center/zoom.
    /// </summary>
    public void ApplyViewport(ViewportPayload viewport)
    {
        if (!IsDmMode && viewport.HasMapRect)
            _playerContentClip = _viewport.ApplyPlayerViewport(viewport, Bounds.Size, MapImage?.Size);
        else
        {
            _viewport.ApplyViewport(viewport, Bounds.Size, MapImage?.Size);
            _playerContentClip = new Rect(Bounds.Size);
        }

        OnViewportStateChanged();
    }

    /// <summary>
    /// Cancels any active local interaction without applying further changes.
    /// Used primarily so Escape can abandon an in-progress shape drag.
    /// </summary>
    public void CancelActiveInteraction()
    {
        var hadPreview = _isDraggingShape || _isDraggingStamp || _isDraggingPlayerViewport;

        _isDraggingShape = false;
        _isDraggingStamp = false;
        _isDraggingPlayerViewport = false;
        _stampDragMode = StampDragMode.None;
        _activeStampHandle = StampHandle.None;
        _activePlayerViewportHandle = PlayerViewportHandle.None;
        _isPainting = false;
        _isPanning = false;
        UpdateCursor();

        if (hadPreview)
            InvalidateVisual();
    }

    /// Returns the zoom where the map height exactly matches the current canvas height.
    /// This is the DM-facing 100% zoom baseline.
    /// </summary>
    public double GetHeightFitZoomLevel()
        => MapViewportController.GetHeightFitZoomLevel(Bounds.Size, MapImage?.Size, _viewport.RotationQuarterTurns);

    /// <summary>Rotates the view by the specified number of clockwise quarter-turns.</summary>
    void RotateView(int quarterTurns)
    {
        _viewport.RotateBy(quarterTurns, Bounds.Size, MapImage?.Size);
        OnViewportStateChanged();
    }

    /// <summary>
    /// Fits the map vertically so its top and bottom edges align with the canvas, and centers it horizontally.
    /// </summary>
    public void RefitViewToMapHeight()
    {
        if (_viewport.RefitToMapHeight(Bounds.Size, MapImage?.Size))
            OnViewportStateChanged();
    }

    /// <summary>Refreshes dependent canvas state after the camera changes.</summary>
    void OnViewportStateChanged()
    {
        UpdateZoomPercent();
        InvalidateVisual();
        RaiseViewportChanged();
    }

    /// <summary>Updates <see cref="ZoomPercent"/> after actual zoom or the height-fit baseline changes.</summary>
    void UpdateZoomPercent()
    {
        decimal percent = (decimal)_viewport.GetZoomPercent(Bounds.Size, MapImage?.Size);
        SetAndRaise(ZoomPercentProperty, ref _zoomPercent, percent);
    }

    /// <summary>
    /// Renders the black background, the map image, the fog overlay, and (in DM mode) the tool cursor preview.
    /// The map and fog are drawn inside a rotation+scale+translate transform derived from the
    /// viewport rotation, <see cref="ZoomLevel"/>, <see cref="OffsetX"/>, and <see cref="OffsetY"/>.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

        var mapImage = MapImage;
        if (mapImage is null)
            return;

        if (!_fogBitmapController.CanRender(GetFogBitmapSettings()))
            return;

        var zoom = ZoomLevel;
        var transform = _viewport.GetMapToScreenTransform(mapImage.Size);

        void RenderMapLayers()
        {
            var imageRect = new Rect(0, 0, mapImage.Size.Width, mapImage.Size.Height);
            if (ShowMap)
                context.DrawImage(mapImage, imageRect);
            else
                context.FillRectangle(Brushes.White, imageRect);

            RenderStamps(context);
            RenderGrid(context, imageRect);

            if (_fogBitmapController.Bitmap != null)
            {
                var fogRect = new Rect(0, 0, _fogBitmapController.Bitmap.Size.Width, _fogBitmapController.Bitmap.Size.Height);
                context.DrawImage(_fogBitmapController.Bitmap, fogRect);
            }
        }

        if (!IsDmMode && _playerContentClip.Width > 0 && _playerContentClip.Height > 0)
        {
            using (context.PushClip(_playerContentClip))
            using (context.PushTransform(transform))
            {
                RenderMapLayers();
            }
        }
        else
        {
            using (context.PushTransform(transform))
            {
                RenderMapLayers();
            }
        }

        if (IsDmMode && MapImage is not null && PlayerViewport is not null)
            RenderPlayerViewportOverlay(context, PlayerViewport);

        if (ShouldRenderCursor())
        {
            if (!IsDmMode && _playerContentClip.Width > 0 && _playerContentClip.Height > 0)
            {
                using (context.PushClip(_playerContentClip))
                    RenderCursor(context, CursorMapX, CursorMapY);
            }
            else
                RenderCursor(context, CursorMapX, CursorMapY);
        }

        if (IsDmMode && ActiveTool == ToolType.Stamp)
            RenderStampEditorOverlay(context);

        if (IsDmMode && IsPointerOver && ActiveTool != ToolType.Cursor)
            RenderToolOverlay(context, zoom);
    }

    void RenderStamps(DrawingContext context)
    {
        if (Stamps is not { Count: > 0 })
            return;

        foreach (var stamp in Stamps)
        {
            if (!_stampImages.TryGetValue(stamp.TemplateId, out var image))
                continue;

            var rect = GetStampRect(stamp);
            if (Math.Abs(stamp.RotationDegrees) < 0.001)
            {
                context.DrawImage(image, rect);
                continue;
            }

            var center = GetStampCenter(stamp);
            using (context.PushTransform(
                Matrix.CreateTranslation(-center.X, -center.Y)
                * Matrix.CreateRotation(DegreesToRadians(stamp.RotationDegrees))
                * Matrix.CreateTranslation(center.X, center.Y)))
            {
                context.DrawImage(image, rect);
            }
        }
    }

    void RenderStampEditorOverlay(DrawingContext context)
    {
        var stamp = SelectedStamp;
        if (stamp is null || MapImage is null)
            return;

        var topLeft = _viewport.MapToScreen(GetStampHandlePoint(stamp, StampHandle.TopLeft), MapImage.Size);
        var top = _viewport.MapToScreen(GetStampHandlePoint(stamp, StampHandle.Top), MapImage.Size);
        var topRight = _viewport.MapToScreen(GetStampHandlePoint(stamp, StampHandle.TopRight), MapImage.Size);
        var right = _viewport.MapToScreen(GetStampHandlePoint(stamp, StampHandle.Right), MapImage.Size);
        var bottomRight = _viewport.MapToScreen(GetStampHandlePoint(stamp, StampHandle.BottomRight), MapImage.Size);
        var bottom = _viewport.MapToScreen(GetStampHandlePoint(stamp, StampHandle.Bottom), MapImage.Size);
        var bottomLeft = _viewport.MapToScreen(GetStampHandlePoint(stamp, StampHandle.BottomLeft), MapImage.Size);
        var left = _viewport.MapToScreen(GetStampHandlePoint(stamp, StampHandle.Left), MapImage.Size);
        var rotate = _viewport.MapToScreen(GetStampHandlePoint(stamp, StampHandle.Rotate), MapImage.Size);
        var pen = new Pen(Brushes.White, 1.5);

        context.DrawLine(pen, topLeft, topRight);
        context.DrawLine(pen, topRight, bottomRight);
        context.DrawLine(pen, bottomRight, bottomLeft);
        context.DrawLine(pen, bottomLeft, topLeft);
        context.DrawLine(pen, top, rotate);

        DrawStampHandle(context, topLeft);
        DrawStampHandle(context, top);
        DrawStampHandle(context, topRight);
        DrawStampHandle(context, right);
        DrawStampHandle(context, bottomRight);
        DrawStampHandle(context, bottom);
        DrawStampHandle(context, bottomLeft);
        DrawStampHandle(context, left);
        DrawStampHandle(context, rotate);
    }

    void RenderPlayerViewportOverlay(DrawingContext context, ViewportPayload viewport)
    {
        if (!viewport.HasMapRect || MapImage is null)
            return;

        var rect = GetPlayerViewportRect(viewport);
        var topLeft = _viewport.MapToScreen(rect.TopLeft, MapImage.Size);
        var topRight = _viewport.MapToScreen(rect.TopRight, MapImage.Size);
        var bottomRight = _viewport.MapToScreen(rect.BottomRight, MapImage.Size);
        var bottomLeft = _viewport.MapToScreen(rect.BottomLeft, MapImage.Size);
        var brush = new SolidColorBrush(Color.FromRgb(0, 188, 212));
        var pen = new Pen(brush, ActiveTool == ToolType.PlayerView ? 2.0 : 1.25);

        context.DrawLine(pen, topLeft, topRight);
        context.DrawLine(pen, topRight, bottomRight);
        context.DrawLine(pen, bottomRight, bottomLeft);
        context.DrawLine(pen, bottomLeft, topLeft);

        if (ActiveTool != ToolType.PlayerView)
            return;

        DrawPlayerViewportHandle(context, topLeft, brush);
        DrawPlayerViewportHandle(context, Midpoint(topLeft, topRight), brush);
        DrawPlayerViewportHandle(context, topRight, brush);
        DrawPlayerViewportHandle(context, Midpoint(topRight, bottomRight), brush);
        DrawPlayerViewportHandle(context, bottomRight, brush);
        DrawPlayerViewportHandle(context, Midpoint(bottomLeft, bottomRight), brush);
        DrawPlayerViewportHandle(context, bottomLeft, brush);
        DrawPlayerViewportHandle(context, Midpoint(topLeft, bottomLeft), brush);
    }

    static void DrawPlayerViewportHandle(DrawingContext context, Point center, IBrush brush)
    {
        const double Radius = 4.5;
        context.DrawEllipse(Brushes.Black, new Pen(brush, 1.5), center, Radius, Radius);
    }

    static void DrawStampHandle(DrawingContext context, Point center)
    {
        const double Radius = 4.5;
        context.DrawEllipse(Brushes.Black, new Pen(Brushes.White, 1), center, Radius, Radius);
    }

    /// <summary>Returns <see langword="true"/> when the configured cursor icon should be drawn.</summary>
    bool ShouldRenderCursor() =>
        IsCursorVisible && !_isPanning && (!IsDmMode || ActiveTool == ToolType.Cursor);

    /// <summary>Draws the selected cursor icon at a map-space point using screen-pixel size.</summary>
    void RenderCursor(DrawingContext context, double mapX, double mapY)
    {
        if (!_cursorIcons.TryGetValue(CursorType, out var icon))
            return;

        var size = Math.Max(1, CursorSize);
        var position = _viewport.MapToScreen(new Point(mapX, mapY), MapImage?.Size);
        var x = position.X - size / 2.0;
        var y = position.Y - size / 2.0;
        context.DrawImage(icon, new Rect(x, y, size, size));
    }

    void RenderGrid(DrawingContext context, Rect imageRect)
    {
        if (!IsGridVisible || GridSquareSize <= 1 || GridOpacity <= 0 || GridLineWidth <= 0)
            return;

        var lineBrush = new SolidColorBrush(GridColor, Math.Clamp(GridOpacity, 0, 1));
        var pen = new Pen(lineBrush, Math.Max(0.1, GridLineWidth));
        var square = GridSquareSize;
        var ox = GridOffsetX * square;
        var oy = GridOffsetY * square;

        using (context.PushClip(imageRect))
        {
            for (double x = ox; x <= imageRect.Width; x += square)
                context.DrawLine(pen, new Point(x, 0), new Point(x, imageRect.Height));

            for (double y = oy; y <= imageRect.Height; y += square)
                context.DrawLine(pen, new Point(0, y), new Point(imageRect.Width, y));
        }
    }

    /// <summary>
    /// Draws the tool cursor outline at the current mouse position. For the Brush tool, draws the
    /// brush shape outline scaled by the current zoom. For the Shape tool while dragging, draws
    /// a semi-transparent preview of the shape being placed.
    /// </summary>
    void RenderToolOverlay(DrawingContext context, double zoom)
    {
        var pen = new Pen(Brushes.White, 1.5);
        var c = _lastMousePosition;

        if (ActiveTool == ToolType.Brush)
        {
            var r = BrushDiameter * zoom / 2.0;
            switch (BrushShape)
            {
                case BrushShape.Square:
                    context.DrawRectangle(null, pen, new Rect(c.X - r, c.Y - r, r * 2, r * 2));
                    break;

                case BrushShape.Diamond:
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        ctx.BeginFigure(new Point(c.X, c.Y - r), true);
                        ctx.LineTo(new Point(c.X + r, c.Y));
                        ctx.LineTo(new Point(c.X, c.Y + r));
                        ctx.LineTo(new Point(c.X - r, c.Y));
                        ctx.EndFigure(true);
                    }
                    context.DrawGeometry(null, pen, geo);
                    break;

                default:
                    context.DrawEllipse(null, pen, c, r, r);
                    break;
            }
        }
        else if (ActiveTool == ToolType.Shape && _isDraggingShape)
        {
            var shapeType = ShapeType;
            var (start, end) = ShapeConstraintHelper.NormalizeBounds(shapeType, _shapeDragStart, _lastMousePosition);
            var fill = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));

            if (ShapeTypeMetadata.IsEllipse(shapeType))
            {
                var cx = (start.X + end.X) / 2;
                var cy = (start.Y + end.Y) / 2;
                var rx = Math.Abs(end.X - start.X) / 2;
                var ry = Math.Abs(end.Y - start.Y) / 2;
                context.DrawEllipse(fill, pen, new Point(cx, cy), rx, ry);
            }
            else
            {
                var rect = MakeRect(start, end);
                var radius = ShapeTypeMetadata.SupportsCornerRadius(shapeType)
                    ? ShapeTypeMetadata.GetEffectiveCornerRadius(ShapeCornerRadius, rect.Width, rect.Height)
                    : 0;

                if (radius > 0)
                {
                    context.DrawRectangle(fill, pen, new RoundedRect(rect, new CornerRadius(radius)));
                }
                else
                {
                    context.FillRectangle(fill, rect);
                    context.DrawRectangle(null, pen, rect);
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _lastMousePosition = e.GetPosition(this);
        UpdateCursorMapPosition(_lastMousePosition);
        RaiseCursorUpdated();
        UpdateCursor();
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isCursorPressed = false;
        if (ActiveTool == ToolType.Cursor)
            RaiseCursorUpdated(forceVisible: false);
        UpdateCursor();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActiveToolProperty || change.Property == IsDmModeProperty)
        {
            if (ActiveTool != ToolType.Cursor)
                _isCursorPressed = false;
            RaiseCursorUpdated();
            UpdateCursor();
        }

        if (change.Property == CursorTypeProperty
            || change.Property == CursorSizeProperty
            || change.Property == ShowCursorOnlyWhilePressedProperty)
        {
            RaiseCursorUpdated();
            UpdateCursor();
        }

        if (change.Property == StampsProperty)
            SubscribeToStampsCollection();

        if (change.Property == MapImageProperty && MapImage is not null)
        {
            _viewport.ResetRotation();
            RefitViewToMapHeight();
        }

        if (change.Property == BoundsProperty)
        {
            UpdateZoomPercent();
        }

        if (change.Property == BoundsProperty)
            RaiseViewportChanged();

        if (FogMask is null)
            return;

        var needsTextureRefresh = change.Property == FogTypeProperty || change.Property == FogSeedProperty;
        var needsBitmapRefresh = needsTextureRefresh
            || change.Property == FogOpacityProperty
            || change.Property == FogColorProperty;

        if (needsTextureRefresh)
            _fogBitmapController.RefreshTextureAndBitmap(GetFogBitmapSettings());
        else if (needsBitmapRefresh)
        {
            _fogBitmapController.InvalidateRegion(new PixelRect(0, 0, FogMask.Width, FogMask.Height), GetFogBitmapSettings());
        }
    }

    void SubscribeToStampsCollection()
    {
        _subscribedStampsCollection?.CollectionChanged -= OnStampsCollectionChanged;

        _subscribedStampsCollection = Stamps as INotifyCollectionChanged;
        _subscribedStampsCollection?.CollectionChanged += OnStampsCollectionChanged;

        InvalidateVisual();
    }

    void OnStampsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedStamp is not null && Stamps is not null && !Stamps.Contains(SelectedStamp))
            SelectedStamp = null;

        InvalidateVisual();
    }

    /// <summary>
    /// Updates the cursor based on the current tool and interaction state:
    /// hidden during painting (to show the brush outline instead), resize-all during panning,
    /// cross-hair for brush/shape, and default for player mode.
    /// </summary>
    void UpdateCursor()
    {
        if (!IsDmMode)
        {
            Cursor = Cursor.Default;
            return;
        }

        if (_isPanning || ActiveTool == ToolType.Pan)
        {
            Cursor = new Cursor(StandardCursorType.SizeAll);
            return;
        }

        if (ActiveTool == ToolType.Stamp)
        {
            Cursor = _isDraggingStamp && _stampDragMode == StampDragMode.Move
                ? new Cursor(StandardCursorType.SizeAll)
                : Cursor.Default;
            return;
        }

        if (ActiveTool == ToolType.PlayerView)
        {
            var handle = _isDraggingPlayerViewport
                ? _activePlayerViewportHandle
                : GetPlayerViewportHoverHandle(_lastMousePosition);
            Cursor = GetPlayerViewportCursor(handle);
            return;
        }

        if (ActiveTool == ToolType.Fog)
        {
            Cursor = Cursor.Default;
            return;
        }

        if (ActiveTool == ToolType.Cursor)
        {
            Cursor = IsCursorVisible ? new Cursor(StandardCursorType.None) : Cursor.Default;
            return;
        }

        if (_isPainting)
        {
            Cursor = new Cursor(StandardCursorType.None);
            return;
        }

        Cursor = new Cursor(StandardCursorType.Cross);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsDmMode)
            return;

        Focus();

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanPoint = point.Position;
            UpdateCursor();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.Cursor)
        {
            UpdateCursorMapPosition(point.Position);
            if (point.Properties.IsLeftButtonPressed)
                _isCursorPressed = true;
            RaiseCursorUpdated();
            UpdateCursor();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.PlayerView && point.Properties.IsLeftButtonPressed)
        {
            StartPlayerViewportInteraction(point.Position);
            UpdateCursor();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.Stamp && point.Properties.IsLeftButtonPressed)
        {
            StartStampInteraction(point.Position);
            UpdateCursor();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.Stamp && point.Properties.IsRightButtonPressed)
        {
            ShowStampContextMenu(point.Position);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            if (ActiveTool == ToolType.Pan)
            {
                _isPanning = true;
                _lastPanPoint = point.Position;
            }
            else
                StartPainting(point.Position, erase: false);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsRightButtonPressed && ActiveTool != ToolType.Pan)
        {
            StartPainting(point.Position, erase: true);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Begins a paint or shape-drag gesture at <paramref name="position"/>.
    /// For the Brush tool, fires <see cref="BrushStrokeStarted"/> and the first <see cref="BrushStrokeApplied"/>.
    /// For the Shape tool, records the drag start point.
    /// </summary>
    void StartPainting(Point position, bool erase)
    {
        _isErasing = erase;
        if (ActiveTool == ToolType.Brush)
        {
            _isPainting = true;
            InitBrushMapPos(position);
            UpdateCursor();
            BrushStrokeStarted?.Invoke(this, EventArgs.Empty);
            RaiseBrushStroke(position);
        }
        else if (ActiveTool == ToolType.Shape)
        {
            _isDraggingShape = true;
            _shapeDragStart = position;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        _lastMousePosition = point.Position;
        UpdateCursorMapPosition(point.Position);

        if (!IsDmMode)
            return;

        if (_isPanning)
        {
            var delta = point.Position - _lastPanPoint;
            _viewport.PanBy(delta);
            OnViewportStateChanged();
            _lastPanPoint = point.Position;
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.Cursor)
        {
            RaiseCursorUpdated();
            UpdateCursor();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.PlayerView && !_isDraggingPlayerViewport)
        {
            UpdateCursor();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.PlayerView && _isDraggingPlayerViewport)
        {
            UpdatePlayerViewportInteraction(point.Position);
            UpdateCursor();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.Stamp && _isDraggingStamp)
        {
            UpdateStampInteraction(point.Position, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            UpdateCursor();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isPainting)
        {
            RaiseBrushStroke(point.Position);
            e.Handled = true;
        }

        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!IsDmMode)
            return;

        if (_isPanning)
        {
            _isPanning = false;
            UpdateCursor();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.Cursor)
        {
            var point = e.GetCurrentPoint(this);
            UpdateCursorMapPosition(point.Position);
            _isCursorPressed = point.Properties.IsLeftButtonPressed;
            RaiseCursorUpdated();
            UpdateCursor();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.PlayerView && _isDraggingPlayerViewport)
        {
            _isDraggingPlayerViewport = false;
            _activePlayerViewportHandle = PlayerViewportHandle.None;
            UpdateCursor();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.Stamp && _isDraggingStamp)
        {
            var stamp = SelectedStamp;
            _isDraggingStamp = false;
            _stampDragMode = StampDragMode.None;
            _activeStampHandle = StampHandle.None;
            if (stamp is not null)
                StampChanged?.Invoke(this, new StampChangedEventArgs(stamp));
            UpdateCursor();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDraggingShape)
        {
            var point = e.GetCurrentPoint(this);
            FireShapeStroke(_shapeDragStart, point.Position);
            _isDraggingShape = false;
            InvalidateVisual();
        }

        if (_isPainting)
            BrushStrokeEnded?.Invoke(this, EventArgs.Empty);

        _isPanning = false;
        _isPainting = false;
        UpdateCursor();
    }

    /// <summary>
    /// Handles mouse wheel events to zoom the canvas centred on the pointer position,
    /// adjusting <see cref="OffsetX"/> and <see cref="OffsetY"/> to keep the point under
    /// the cursor stationary.
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (!IsDmMode)
            return;

        var mousePos = e.GetPosition(this);
        var zoomFactor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        _viewport.ZoomAround(mousePos, zoomFactor, Bounds.Size, MapImage?.Size);
        OnViewportStateChanged();

        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (ActiveTool == ToolType.Stamp && e.Key is Key.Delete or Key.Back)
        {
            DeleteSelectedStamp();
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolType.Stamp && e.Key == Key.Escape)
        {
            CancelActiveInteraction();
            SelectedStamp = null;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && (_isDraggingShape || _isDraggingPlayerViewport))
        {
            CancelActiveInteraction();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Raises <see cref="ViewportChanged"/> when the control has valid layout bounds and a viewport
    /// can be meaningfully mirrored to other clients.
    /// </summary>
    void RaiseViewportChanged()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        ViewportChanged?.Invoke(this, GetViewport());
    }

    /// <summary>Stores the current cursor position in map-space styled properties.</summary>
    void UpdateCursorMapPosition(Point screenPosition)
    {
        var mapPosition = _viewport.ScreenToMap(screenPosition, MapImage?.Size);
        SetValue(CursorMapXProperty, mapPosition.X);
        SetValue(CursorMapYProperty, mapPosition.Y);
    }

    /// <summary>
    /// Updates local cursor visibility and raises <see cref="CursorUpdated"/> for DM cursor changes.
    /// </summary>
    void RaiseCursorUpdated(bool? forceVisible = null)
    {
        if (!IsDmMode)
            return;

        var visible = forceVisible ?? (ActiveTool == ToolType.Cursor
            && IsPointerOver
            && (!ShowCursorOnlyWhilePressed || _isCursorPressed));

        SetValue(IsCursorVisibleProperty, visible);
        CursorUpdated?.Invoke(this, new CursorPayload
        {
            MapX = CursorMapX,
            MapY = CursorMapY,
            CursorType = CursorType,
            CursorSize = Math.Max(1, CursorSize),
            IsVisible = visible,
        });

        InvalidateVisual();
    }

    void StartPlayerViewportInteraction(Point screenPosition)
    {
        if (MapImage is null || PlayerViewport is null || !PlayerViewport.HasMapRect)
            return;

        var mapPosition = _viewport.ScreenToMap(screenPosition, MapImage.Size);
        var rect = GetPlayerViewportRect(PlayerViewport);
        if (!TryHitPlayerViewportHandle(mapPosition, rect, out var handle))
        {
            if (rect.Contains(mapPosition))
                handle = PlayerViewportHandle.Move;
            else
                return;
        }

        _isDraggingPlayerViewport = true;
        _activePlayerViewportHandle = handle;
        _playerViewportDragStartMap = mapPosition;
        _playerViewportDragStartRect = rect;
    }

    PlayerViewportHandle GetPlayerViewportHoverHandle(Point screenPosition)
    {
        if (MapImage is null || PlayerViewport is null || !PlayerViewport.HasMapRect)
            return PlayerViewportHandle.None;

        var mapPosition = _viewport.ScreenToMap(screenPosition, MapImage.Size);
        var rect = GetPlayerViewportRect(PlayerViewport);
        if (TryHitPlayerViewportHandle(mapPosition, rect, out var handle))
            return handle;

        return rect.Contains(mapPosition)
            ? PlayerViewportHandle.Move
            : PlayerViewportHandle.None;
    }

    static Cursor GetPlayerViewportCursor(PlayerViewportHandle handle) =>
        handle switch
        {
            PlayerViewportHandle.Move => new Cursor(StandardCursorType.SizeAll),
            PlayerViewportHandle.Top or PlayerViewportHandle.Bottom => new Cursor(StandardCursorType.SizeNorthSouth),
            PlayerViewportHandle.Left or PlayerViewportHandle.Right => new Cursor(StandardCursorType.SizeWestEast),
            PlayerViewportHandle.TopLeft or PlayerViewportHandle.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
            PlayerViewportHandle.TopRight or PlayerViewportHandle.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
            _ => Cursor.Default,
        };

    void UpdatePlayerViewportInteraction(Point screenPosition)
    {
        if (MapImage is null || PlayerViewport is null)
            return;

        var mapPosition = _viewport.ScreenToMap(screenPosition, MapImage.Size);
        var rect = _activePlayerViewportHandle == PlayerViewportHandle.Move
            ? MovePlayerViewportRect(mapPosition)
            : ResizePlayerViewportRect(mapPosition);

        ApplyPlayerViewportRect(ClampPlayerViewportRect(rect));
        InvalidateVisual();
    }

    Rect MovePlayerViewportRect(Point mapPosition)
    {
        var delta = mapPosition - _playerViewportDragStartMap;
        return new Rect(
            _playerViewportDragStartRect.X + delta.X,
            _playerViewportDragStartRect.Y + delta.Y,
            _playerViewportDragStartRect.Width,
            _playerViewportDragStartRect.Height);
    }

    Rect ResizePlayerViewportRect(Point mapPosition)
    {
        const double MinSize = 16;
        var rect = _playerViewportDragStartRect;
        var left = rect.Left;
        var top = rect.Top;
        var right = rect.Right;
        var bottom = rect.Bottom;

        switch (_activePlayerViewportHandle)
        {
            case PlayerViewportHandle.TopLeft:
                left = Math.Min(mapPosition.X, right - MinSize);
                top = Math.Min(mapPosition.Y, bottom - MinSize);
                break;
            case PlayerViewportHandle.Top:
                top = Math.Min(mapPosition.Y, bottom - MinSize);
                break;
            case PlayerViewportHandle.TopRight:
                right = Math.Max(mapPosition.X, left + MinSize);
                top = Math.Min(mapPosition.Y, bottom - MinSize);
                break;
            case PlayerViewportHandle.Right:
                right = Math.Max(mapPosition.X, left + MinSize);
                break;
            case PlayerViewportHandle.BottomRight:
                right = Math.Max(mapPosition.X, left + MinSize);
                bottom = Math.Max(mapPosition.Y, top + MinSize);
                break;
            case PlayerViewportHandle.Bottom:
                bottom = Math.Max(mapPosition.Y, top + MinSize);
                break;
            case PlayerViewportHandle.BottomLeft:
                left = Math.Min(mapPosition.X, right - MinSize);
                bottom = Math.Max(mapPosition.Y, top + MinSize);
                break;
            case PlayerViewportHandle.Left:
                left = Math.Min(mapPosition.X, right - MinSize);
                break;
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    bool TryHitPlayerViewportHandle(Point mapPosition, Rect rect, out PlayerViewportHandle handle)
    {
        var threshold = Math.Max(4, 10 / Math.Max(ZoomLevel, 0.01));

        if (IsNear(mapPosition, rect.TopLeft, threshold))
            return SetHandle(PlayerViewportHandle.TopLeft, out handle);
        if (IsNear(mapPosition, new Point(rect.X + rect.Width / 2.0, rect.Y), threshold))
            return SetHandle(PlayerViewportHandle.Top, out handle);
        if (IsNear(mapPosition, rect.TopRight, threshold))
            return SetHandle(PlayerViewportHandle.TopRight, out handle);
        if (IsNear(mapPosition, new Point(rect.Right, rect.Y + rect.Height / 2.0), threshold))
            return SetHandle(PlayerViewportHandle.Right, out handle);
        if (IsNear(mapPosition, rect.BottomRight, threshold))
            return SetHandle(PlayerViewportHandle.BottomRight, out handle);
        if (IsNear(mapPosition, new Point(rect.X + rect.Width / 2.0, rect.Bottom), threshold))
            return SetHandle(PlayerViewportHandle.Bottom, out handle);
        if (IsNear(mapPosition, rect.BottomLeft, threshold))
            return SetHandle(PlayerViewportHandle.BottomLeft, out handle);
        if (IsNear(mapPosition, new Point(rect.X, rect.Y + rect.Height / 2.0), threshold))
            return SetHandle(PlayerViewportHandle.Left, out handle);

        var expanded = rect.Inflate(threshold);
        if (expanded.Contains(mapPosition))
        {
            if (Math.Abs(mapPosition.Y - rect.Top) <= threshold)
                return SetHandle(PlayerViewportHandle.Top, out handle);
            if (Math.Abs(mapPosition.X - rect.Right) <= threshold)
                return SetHandle(PlayerViewportHandle.Right, out handle);
            if (Math.Abs(mapPosition.Y - rect.Bottom) <= threshold)
                return SetHandle(PlayerViewportHandle.Bottom, out handle);
            if (Math.Abs(mapPosition.X - rect.Left) <= threshold)
                return SetHandle(PlayerViewportHandle.Left, out handle);
        }

        handle = PlayerViewportHandle.None;
        return false;
    }

    static bool SetHandle(PlayerViewportHandle value, out PlayerViewportHandle handle)
    {
        handle = value;
        return true;
    }

    void ApplyPlayerViewportRect(Rect rect)
    {
        if (PlayerViewport is null)
            return;

        var next = CreatePlayerViewportPayload(rect, PlayerViewport);
        PlayerViewportChanged?.Invoke(this, next);
    }

    Rect ClampPlayerViewportRect(Rect rect)
    {
        const double MinSize = 16;
        var mapImage = MapImage;
        if (mapImage is null)
            return rect;

        var width = Math.Clamp(rect.Width, Math.Min(MinSize, mapImage.Size.Width), mapImage.Size.Width);
        var height = Math.Clamp(rect.Height, Math.Min(MinSize, mapImage.Size.Height), mapImage.Size.Height);
        var x = Math.Clamp(rect.X, 0, Math.Max(0, mapImage.Size.Width - width));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, mapImage.Size.Height - height));
        return new Rect(x, y, width, height);
    }

    void StartStampInteraction(Point screenPosition)
    {
        if (MapImage is null || Stamps is null)
            return;

        var mapPosition = _viewport.ScreenToMap(screenPosition, MapImage.Size);
        if (SelectedStamp is not null
            && TryHitStampHandle(mapPosition, SelectedStamp, out var handle))
        {
            BeginStampDrag(
                mapPosition,
                handle == StampHandle.Rotate ? StampDragMode.Rotate : StampDragMode.Resize,
                handle);
            return;
        }

        var hit = HitTestStamp(mapPosition);
        if (hit is not null)
        {
            SelectedStamp = hit;
            BeginStampDrag(mapPosition, StampDragMode.Move, StampHandle.None);
            return;
        }

        if (SelectedStamp is not null)
        {
            SelectedStamp = null;
            InvalidateVisual();
            return;
        }

        PlaceStamp(mapPosition);
    }

    void ShowStampContextMenu(Point screenPosition)
    {
        if (MapImage is null)
            return;

        var mapPosition = _viewport.ScreenToMap(screenPosition, MapImage.Size);
        var hit = HitTestStamp(mapPosition);
        if (hit is null)
            return;

        SelectedStamp = hit;
        _stampContextMenu.Open(this);
        InvalidateVisual();
    }

    void ReorderSelectedStampBy(int delta)
    {
        var stamp = SelectedStamp;
        if (stamp is null || Stamps is null)
            return;

        var index = Stamps.IndexOf(stamp);
        if (index < 0)
            return;

        ReorderSelectedStampTo(Math.Clamp(index + delta, 0, Stamps.Count - 1));
    }

    void ReorderSelectedStampToFront()
    {
        if (Stamps is { Count: > 0 })
            ReorderSelectedStampTo(Stamps.Count - 1);
    }

    void ReorderSelectedStampToBack()
    {
        ReorderSelectedStampTo(0);
    }

    void ReorderSelectedStampTo(int nextIndex)
    {
        var stamp = SelectedStamp;
        if (stamp is null || Stamps is null)
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
        StampChanged?.Invoke(this, new StampChangedEventArgs(stamp));
        InvalidateVisual();
    }

    void DuplicateSelectedStamp()
    {
        var stamp = SelectedStamp;
        if (stamp is null || Stamps is null)
            return;

        var rect = ClampStampRect(new Rect(stamp.X + 16, stamp.Y + 16, stamp.Width, stamp.Height));
        var duplicate = new StampInstance
        {
            TemplateId = stamp.TemplateId,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            RotationDegrees = stamp.RotationDegrees,
        };

        Stamps.Add(duplicate);
        SelectedStamp = duplicate;
        StampChanged?.Invoke(this, new StampChangedEventArgs(duplicate));
        InvalidateVisual();
    }

    void BeginStampDrag(Point mapPosition, StampDragMode mode, StampHandle handle)
    {
        var stamp = SelectedStamp;
        if (stamp is null)
            return;

        _isDraggingStamp = true;
        _stampDragMode = mode;
        _activeStampHandle = handle;
        _stampDragStartMap = mapPosition;
        _stampDragStartRect = GetStampRect(stamp);
        _stampDragStartRotationDegrees = stamp.RotationDegrees;
        _stampDragStartPointerAngleDegrees = GetAngleDegrees(GetStampCenter(stamp), mapPosition);
    }

    void UpdateStampInteraction(Point screenPosition, bool preserveAspect)
    {
        var stamp = SelectedStamp;
        if (stamp is null || MapImage is null)
            return;

        var mapPosition = _viewport.ScreenToMap(screenPosition, MapImage.Size);
        if (_stampDragMode == StampDragMode.Rotate)
        {
            RotateStamp(stamp, mapPosition);
            return;
        }

        var rect = _stampDragMode == StampDragMode.Resize
            ? ResizeStampRect(mapPosition, preserveAspect)
            : MoveStampRect(mapPosition);

        ApplyStampRect(stamp, ClampStampRect(rect));
    }

    void RotateStamp(StampInstance stamp, Point mapPosition)
    {
        var angle = GetAngleDegrees(GetStampCenter(stamp), mapPosition);
        stamp.RotationDegrees = NormalizeDegrees(
            _stampDragStartRotationDegrees + angle - _stampDragStartPointerAngleDegrees);
    }

    Rect MoveStampRect(Point mapPosition)
    {
        var delta = mapPosition - _stampDragStartMap;
        return new Rect(
            _stampDragStartRect.X + delta.X,
            _stampDragStartRect.Y + delta.Y,
            _stampDragStartRect.Width,
            _stampDragStartRect.Height);
    }

    Rect ResizeStampRect(Point mapPosition, bool preserveAspect)
    {
        const double MinSize = 12;
        var (xSign, ySign) = GetStampHandleSigns(_activeStampHandle);
        var affectsWidth = xSign != 0;
        var affectsHeight = ySign != 0;
        var center = _stampDragStartRect.Center;
        var (xAxis, yAxis) = GetRotatedAxes(_stampDragStartRotationDegrees);
        var anchor = Add(
            Add(center, Scale(xAxis, -xSign * _stampDragStartRect.Width / 2.0)),
            Scale(yAxis, -ySign * _stampDragStartRect.Height / 2.0));
        var pointerFromAnchor = mapPosition - anchor;
        var width = affectsWidth
            ? Math.Max(MinSize, xSign * Dot(pointerFromAnchor, xAxis))
            : _stampDragStartRect.Width;
        var height = affectsHeight
            ? Math.Max(MinSize, ySign * Dot(pointerFromAnchor, yAxis))
            : _stampDragStartRect.Height;

        if (preserveAspect && IsCornerHandle(_activeStampHandle))
        {
            var aspect = _stampDragStartRect.Width / Math.Max(MinSize, _stampDragStartRect.Height);
            if (width / height > aspect)
                width = height * aspect;
            else
                height = width / aspect;
        }

        var nextCenter = anchor;
        if (affectsWidth)
            nextCenter = Add(nextCenter, Scale(xAxis, xSign * width / 2.0));
        if (affectsHeight)
            nextCenter = Add(nextCenter, Scale(yAxis, ySign * height / 2.0));

        return new Rect(nextCenter.X - width / 2.0, nextCenter.Y - height / 2.0, width, height);
    }

    static bool IsCornerHandle(StampHandle handle) =>
        handle is StampHandle.TopLeft or StampHandle.TopRight or StampHandle.BottomRight or StampHandle.BottomLeft;

    void PlaceStamp(Point mapPosition)
    {
        if (Stamps is null)
            return;

        var templateId = SelectedStampTemplateId ?? StampCatalog.Templates[0].Id;
        var template = StampCatalog.Find(templateId) ?? StampCatalog.Templates[0];
        var rect = ClampStampRect(new Rect(
            mapPosition.X - template.DefaultWidth / 2,
            mapPosition.Y - template.DefaultHeight / 2,
            template.DefaultWidth,
            template.DefaultHeight));

        var stamp = new StampInstance
        {
            TemplateId = template.Id,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            RotationDegrees = 0,
        };

        Stamps.Add(stamp);
        SelectedStamp = stamp;
        StampChanged?.Invoke(this, new StampChangedEventArgs(stamp));
        InvalidateVisual();
    }

    void DeleteSelectedStamp()
    {
        var stamp = SelectedStamp;
        if (stamp is null || Stamps is null)
            return;

        Stamps.Remove(stamp);
        SelectedStamp = null;
        StampChanged?.Invoke(this, new StampChangedEventArgs(stamp));
        InvalidateVisual();
    }

    StampInstance? HitTestStamp(Point mapPosition)
    {
        if (Stamps is null)
            return null;

        for (var i = Stamps.Count - 1; i >= 0; i--)
        {
            var stamp = Stamps[i];
            if (GetStampRect(stamp).Contains(UnrotatePoint(mapPosition, GetStampCenter(stamp), stamp.RotationDegrees)))
                return stamp;
        }

        return null;
    }

    bool TryHitStampHandle(Point mapPosition, StampInstance stamp, out StampHandle handle)
    {
        var threshold = Math.Max(4, 10 / Math.Max(ZoomLevel, 0.01));

        if (IsNear(mapPosition, GetStampHandlePoint(stamp, StampHandle.Rotate), threshold))
        {
            handle = StampHandle.Rotate;
            return true;
        }

        if (IsNear(mapPosition, GetStampHandlePoint(stamp, StampHandle.TopLeft), threshold))
        {
            handle = StampHandle.TopLeft;
            return true;
        }

        if (IsNear(mapPosition, GetStampHandlePoint(stamp, StampHandle.Top), threshold))
        {
            handle = StampHandle.Top;
            return true;
        }

        if (IsNear(mapPosition, GetStampHandlePoint(stamp, StampHandle.TopRight), threshold))
        {
            handle = StampHandle.TopRight;
            return true;
        }

        if (IsNear(mapPosition, GetStampHandlePoint(stamp, StampHandle.Right), threshold))
        {
            handle = StampHandle.Right;
            return true;
        }

        if (IsNear(mapPosition, GetStampHandlePoint(stamp, StampHandle.BottomRight), threshold))
        {
            handle = StampHandle.BottomRight;
            return true;
        }

        if (IsNear(mapPosition, GetStampHandlePoint(stamp, StampHandle.Bottom), threshold))
        {
            handle = StampHandle.Bottom;
            return true;
        }

        if (IsNear(mapPosition, GetStampHandlePoint(stamp, StampHandle.BottomLeft), threshold))
        {
            handle = StampHandle.BottomLeft;
            return true;
        }

        if (IsNear(mapPosition, GetStampHandlePoint(stamp, StampHandle.Left), threshold))
        {
            handle = StampHandle.Left;
            return true;
        }

        handle = StampHandle.None;
        return false;
    }

    static bool IsNear(Point point, Point target, double threshold) =>
        Math.Abs(point.X - target.X) <= threshold && Math.Abs(point.Y - target.Y) <= threshold;

    static (int X, int Y) GetStampHandleSigns(StampHandle handle) =>
        handle switch
        {
            StampHandle.TopLeft => (-1, -1),
            StampHandle.Top => (0, -1),
            StampHandle.TopRight => (1, -1),
            StampHandle.Right => (1, 0),
            StampHandle.BottomRight => (1, 1),
            StampHandle.Bottom => (0, 1),
            StampHandle.BottomLeft => (-1, 1),
            StampHandle.Left => (-1, 0),
            _ => (1, 1),
        };

    Rect ClampStampRect(Rect rect)
    {
        const double MinSize = 12;
        var width = Math.Max(MinSize, rect.Width);
        var height = Math.Max(MinSize, rect.Height);
        var x = rect.X;
        var y = rect.Y;

        if (MapImage is not null)
        {
            width = Math.Min(width, Math.Max(MinSize, MapImage.Size.Width));
            height = Math.Min(height, Math.Max(MinSize, MapImage.Size.Height));
            x = Math.Clamp(x, 0, Math.Max(0, MapImage.Size.Width - width));
            y = Math.Clamp(y, 0, Math.Max(0, MapImage.Size.Height - height));
        }

        return new Rect(x, y, width, height);
    }

    static Rect GetStampRect(StampInstance stamp) =>
        new(stamp.X, stamp.Y, stamp.Width, stamp.Height);

    static Point GetStampCenter(StampInstance stamp) =>
        new(stamp.X + stamp.Width / 2.0, stamp.Y + stamp.Height / 2.0);

    Point GetStampHandlePoint(StampInstance stamp, StampHandle handle)
    {
        var rect = GetStampRect(stamp);
        var rotateHandleOffset = 28 / Math.Max(ZoomLevel, 0.01);
        var localPoint = handle switch
        {
            StampHandle.TopLeft => rect.TopLeft,
            StampHandle.Top => new Point(rect.X + rect.Width / 2.0, rect.Y),
            StampHandle.TopRight => rect.TopRight,
            StampHandle.Right => new Point(rect.Right, rect.Y + rect.Height / 2.0),
            StampHandle.BottomRight => rect.BottomRight,
            StampHandle.Bottom => new Point(rect.X + rect.Width / 2.0, rect.Bottom),
            StampHandle.BottomLeft => rect.BottomLeft,
            StampHandle.Left => new Point(rect.X, rect.Y + rect.Height / 2.0),
            StampHandle.Rotate => new Point(rect.X + rect.Width / 2.0, rect.Y - rotateHandleOffset),
            _ => GetStampCenter(stamp),
        };

        return RotatePoint(localPoint, GetStampCenter(stamp), stamp.RotationDegrees);
    }

    static void ApplyStampRect(StampInstance stamp, Rect rect)
    {
        stamp.X = rect.X;
        stamp.Y = rect.Y;
        stamp.Width = rect.Width;
        stamp.Height = rect.Height;
    }

    static Point RotatePoint(Point point, Point center, double degrees)
    {
        var radians = DegreesToRadians(degrees);
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var x = point.X - center.X;
        var y = point.Y - center.Y;

        return new Point(
            center.X + x * cos - y * sin,
            center.Y + x * sin + y * cos);
    }

    static Point UnrotatePoint(Point point, Point center, double degrees) =>
        RotatePoint(point, center, -degrees);

    static (Vector XAxis, Vector YAxis) GetRotatedAxes(double degrees)
    {
        var radians = DegreesToRadians(degrees);
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return (new Vector(cos, sin), new Vector(-sin, cos));
    }

    static Point Add(Point point, Vector vector) =>
        new(point.X + vector.X, point.Y + vector.Y);

    static Vector Scale(Vector vector, double scale) =>
        new(vector.X * scale, vector.Y * scale);

    static double Dot(Vector left, Vector right) =>
        left.X * right.X + left.Y * right.Y;

    static double GetAngleDegrees(Point center, Point point) =>
        Math.Atan2(point.Y - center.Y, point.X - center.X) * 180.0 / Math.PI;

    static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    static double DegreesToRadians(double degrees) =>
        degrees * Math.PI / 180.0;

    /// <summary>
    /// Records the current pointer position converted to map coordinates as the starting
    /// point for the next brush stroke segment.
    /// </summary>
    void InitBrushMapPos(Point screenPos)
    {
        var mapPos = _viewport.ScreenToMap(screenPos, MapImage?.Size);
        _lastBrushMapX = (int)mapPos.X;
        _lastBrushMapY = (int)mapPos.Y;
    }

    /// <summary>
    /// Converts <paramref name="screenTo"/> to map coordinates, fires <see cref="BrushStrokeApplied"/>
    /// with the segment from the last position to the new position, then updates the last position.
    /// </summary>
    void RaiseBrushStroke(Point screenTo)
    {
        var mapTo = _viewport.ScreenToMap(screenTo, MapImage?.Size);
        var mapX2 = (int)mapTo.X;
        var mapY2 = (int)mapTo.Y;

        BrushStrokeApplied?.Invoke(this, new BrushStrokeEventArgs
        {
            MapX1 = _lastBrushMapX,
            MapY1 = _lastBrushMapY,
            MapX2 = mapX2,
            MapY2 = mapY2,
            IsErasing = _isErasing,
        });

        _lastBrushMapX = mapX2;
        _lastBrushMapY = mapY2;
    }

    /// <summary>
    /// Converts the screen-space drag start and end points to map coordinates and fires
    /// <see cref="ShapeStrokeApplied"/>.
    /// </summary>
    void FireShapeStroke(Point screenStart, Point screenEnd)
    {
        var mapStart = _viewport.ScreenToMap(screenStart, MapImage?.Size);
        var mapEnd = _viewport.ScreenToMap(screenEnd, MapImage?.Size);
        var mapX1 = (int)mapStart.X;
        var mapY1 = (int)mapStart.Y;
        var mapX2 = (int)mapEnd.X;
        var mapY2 = (int)mapEnd.Y;

        ShapeStrokeApplied?.Invoke(this, new ShapeStrokeEventArgs
        {
            MapX1 = mapX1,
            MapY1 = mapY1,
            MapX2 = mapX2,
            MapY2 = mapY2,
            IsErasing = _isErasing,
        });
    }

    /// <summary>
    /// Returns a normalized <see cref="Rect"/> whose top-left is at the minimum of both points
    /// and whose size is the absolute difference.
    /// </summary>
    static Rect MakeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

    static Rect GetPlayerViewportRect(ViewportPayload viewport) =>
        new(
            viewport.CenterMapX - viewport.WidthMap / 2.0,
            viewport.CenterMapY - viewport.HeightMap / 2.0,
            viewport.WidthMap,
            viewport.HeightMap);

    static ViewportPayload CreatePlayerViewportPayload(Rect rect, ViewportPayload current) =>
        new()
        {
            CenterMapX = rect.X + rect.Width / 2.0,
            CenterMapY = rect.Y + rect.Height / 2.0,
            ZoomLevel = current.ZoomLevel <= 0 ? 1.0 : current.ZoomLevel,
            RotationQuarterTurns = current.RotationQuarterTurns,
            WidthMap = rect.Width,
            HeightMap = rect.Height,
            PaddingPixels = Math.Max(0, current.PaddingPixels),
        };

    static Point Midpoint(Point a, Point b) =>
        new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
}
