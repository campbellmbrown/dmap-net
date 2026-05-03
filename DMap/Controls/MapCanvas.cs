using System;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using DMap.Commands;
using DMap.Models;
using DMap.Services.Fog;
using DMap.Services.Networking;

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
/// Custom Avalonia control that renders a map image, a fog-of-war overlay, and a tool cursor
/// preview. Handles pointer input to produce brush strokes, shape drags, panning, and zooming.
/// In player mode (<see cref="IsDmMode"/> = <see langword="false"/>) all editing and camera input is suppressed.
/// </summary>
public class MapCanvas : Control
{
    const double MinZoomPercent = 10.0;
    const double MaxZoomPercent = 1000.0;

    /// <summary>Styled property for the map background image.</summary>
    public static readonly StyledProperty<Bitmap?> MapImageProperty =
        AvaloniaProperty.Register<MapCanvas, Bitmap?>(nameof(MapImage));

    /// <summary>Styled property for the fog mask used to build the fog bitmap.</summary>
    public static readonly StyledProperty<FogMask?> FogMaskProperty =
        AvaloniaProperty.Register<MapCanvas, FogMask?>(nameof(FogMask));

    /// <summary>Styled property for the canvas zoom multiplier (default 1.0).</summary>
    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(ZoomLevel), 1.0);

    /// <summary>Direct property for fit-relative zoom shown in the DM toolbar.</summary>
    public static readonly DirectProperty<MapCanvas, double> ZoomPercentProperty =
        AvaloniaProperty.RegisterDirect<MapCanvas, double>(
            nameof(ZoomPercent),
            canvas => canvas.ZoomPercent,
            (canvas, value) => canvas.ZoomPercent = value,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Styled property for the horizontal pan offset in screen pixels.</summary>
    public static readonly StyledProperty<double> OffsetXProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(OffsetX));

    /// <summary>Styled property for the vertical pan offset in screen pixels.</summary>
    public static readonly StyledProperty<double> OffsetYProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(OffsetY));

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

    /// <summary>Styled property controlling whether the map image is rendered (true) or replaced with white (false).</summary>
    public static readonly StyledProperty<bool> ShowMapProperty =
        AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowMap), true);

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
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, ClampZoomLevel(value));
    }

    /// <summary>Zoom level expressed as a percentage of the height-fit zoom, where 100% fits map height to canvas height.</summary>
    public double ZoomPercent
    {
        get => _zoomPercent;
        set
        {
            value = Math.Clamp(value, MinZoomPercent, MaxZoomPercent);
            ZoomLevel = ZoomPercentToZoomLevel(value);
            SetAndRaise(ZoomPercentProperty, ref _zoomPercent, value);
        }
    }

    /// <summary>Horizontal translation of the canvas in screen pixels.</summary>
    public double OffsetX
    {
        get => GetValue(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    /// <summary>Vertical translation of the canvas in screen pixels.</summary>
    public double OffsetY
    {
        get => GetValue(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
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

    /// <summary>
    /// When <see langword="false"/>, the map image is replaced with a white fill so the fog mask
    /// boundaries are easier to inspect. The fog overlay is still rendered.
    /// </summary>
    public bool ShowMap
    {
        get => GetValue(ShowMapProperty);
        set => SetValue(ShowMapProperty, value);
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

    /// <summary>
    /// Raised whenever the viewport camera changes, expressed as a map-space center coordinate plus zoom.
    /// </summary>
    public event EventHandler<ViewportPayload>? ViewportChanged;

    /// <summary>Increases the fit-relative zoom by 20%.</summary>
    public ICommand ZoomInCommand { get; }

    /// <summary>Decreases the fit-relative zoom by ~17%.</summary>
    public ICommand ZoomOutCommand { get; }

    /// <summary>Fits the map height to the canvas and aligns its top and bottom edges with the canvas.</summary>
    public ICommand RefitViewCommand { get; }

    static readonly FogTextureGenerator _textureGenerator = new();

    WriteableBitmap? _fogBitmap;
    byte[]? _fogTexture;
    int _fogGenerationVersion;
    bool _isPanning;
    Point _lastPanPoint;
    bool _isPainting;
    bool _isErasing;
    int _lastBrushMapX;
    int _lastBrushMapY;
    bool _isDraggingShape;
    Point _shapeDragStart;
    Point _lastMousePosition;
    double _zoomPercent = 100.0;

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(
            MapImageProperty, FogMaskProperty, ZoomLevelProperty,
            OffsetXProperty, OffsetYProperty, FogOpacityProperty,
            BrushDiameterProperty, ActiveToolProperty, BrushShapeProperty,
            ShapeTypeProperty, ShowMapProperty,
            FogTypeProperty, FogColorProperty, FogSeedProperty);
    }

    /// <summary>Initialises the control with clipping and keyboard focus enabled.</summary>
    public MapCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        ZoomInCommand = new RelayCommand(() => ZoomLevel *= 1.2);
        ZoomOutCommand = new RelayCommand(() => ZoomLevel /= 1.2);
        RefitViewCommand = new RelayCommand(RefitViewToMapHeight);
    }

    /// <summary>
    /// Updates the fog bitmap for <paramref name="dirtyRect"/> and requests a visual redraw.
    /// Call this after the fog mask has been modified to sync the bitmap without rebuilding it entirely.
    /// </summary>
    /// <param name="dirtyRect">The region of the mask that changed.</param>
    public void InvalidateFogRegion(PixelRect dirtyRect)
    {
        UpdateFogBitmapRegion(dirtyRect);
        InvalidateVisual();
    }

    /// <summary>
    /// Discards the existing fog bitmap and builds a new <see cref="WriteableBitmap"/> sized to match
    /// the current <see cref="FogMask"/>. Call this when the mask is replaced entirely (e.g. new map load
    /// or full fog received from the DM).
    /// </summary>
    public void RebuildFogBitmap()
    {
        var mask = FogMask;
        if (mask is null)
        {
            _fogBitmap = null;
            _fogTexture = null;
            _fogGenerationVersion++;
            IsFogGenerating = false;
            return;
        }

        _fogBitmap = new WriteableBitmap(
            new PixelSize(mask.Width, mask.Height),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        RefreshFogTextureAndBitmap();
    }

    /// <summary>
    /// Returns the current viewport expressed as a map-space center coordinate plus zoom so it can
    /// be mirrored on canvases with different screen sizes.
    /// </summary>
    public ViewportPayload GetViewport()
    {
        var zoom = ZoomLevel <= 0 ? 1.0 : ZoomLevel;
        return new ViewportPayload
        {
            CenterMapX = (Bounds.Width / 2.0 - OffsetX) / zoom,
            CenterMapY = (Bounds.Height / 2.0 - OffsetY) / zoom,
            ZoomLevel = zoom,
        };
    }

    /// <summary>
    /// Applies a remotely provided viewport by deriving local screen offsets from the current control
    /// bounds and the desired map-space center/zoom.
    /// </summary>
    public void ApplyViewport(ViewportPayload viewport)
    {
        var zoom = viewport.ZoomLevel;
        if (double.IsNaN(zoom) || double.IsInfinity(zoom) || zoom <= 0)
            zoom = GetHeightFitZoomLevel();

        ZoomLevel = zoom;
        OffsetX = Bounds.Width / 2.0 - viewport.CenterMapX * zoom;
        OffsetY = Bounds.Height / 2.0 - viewport.CenterMapY * zoom;
    }

    /// <summary>
    /// Returns the zoom where the map height exactly matches the current canvas height.
    /// This is the DM-facing 100% zoom baseline.
    /// </summary>
    public double GetHeightFitZoomLevel()
    {
        var mapImage = MapImage;
        if (mapImage is null || Bounds.Height <= 0 || mapImage.Size.Height <= 0)
            return 1.0;

        return Bounds.Height / mapImage.Size.Height;
    }

    /// <summary>
    /// Fits the map vertically so its top and bottom edges align with the canvas, and centers it horizontally.
    /// </summary>
    public void RefitViewToMapHeight()
    {
        var mapImage = MapImage;
        if (mapImage is null || Bounds.Height <= 0 || mapImage.Size.Height <= 0)
            return;

        var zoom = GetHeightFitZoomLevel();
        ZoomLevel = zoom;
        OffsetX = (Bounds.Width - mapImage.Size.Width * zoom) / 2.0;
        OffsetY = 0;
    }

    /// <summary>Converts a fit-relative zoom percentage to the actual canvas zoom multiplier.</summary>
    double ZoomPercentToZoomLevel(double zoomPercent) =>
        GetHeightFitZoomLevel() * Math.Clamp(zoomPercent, MinZoomPercent, MaxZoomPercent) / 100.0;

    /// <summary>Clamps actual canvas zoom to the allowed fit-relative zoom range.</summary>
    double ClampZoomLevel(double zoomLevel) =>
        Math.Clamp(zoomLevel, ZoomPercentToZoomLevel(MinZoomPercent), ZoomPercentToZoomLevel(MaxZoomPercent));

    /// <summary>Updates <see cref="ZoomPercent"/> after actual zoom or the height-fit baseline changes.</summary>
    void UpdateZoomPercent()
    {
        var percent = Math.Clamp(ZoomLevel / GetHeightFitZoomLevel() * 100.0, MinZoomPercent, MaxZoomPercent);
        SetAndRaise(ZoomPercentProperty, ref _zoomPercent, percent);
    }

    /// <summary>
    /// Regenerates the cached fog texture for the current <see cref="FogType"/> and <see cref="FogSeed"/>.
    /// Sized to match the current <see cref="FogMask"/>; cleared to <see langword="null"/> when in
    /// flat-colour mode or when no mask is loaded.
    /// </summary>
    void RefreshFogTextureAndBitmap()
    {
        var mask = FogMask;
        if (mask is null)
        {
            _fogTexture = null;
            _fogGenerationVersion++;
            IsFogGenerating = false;
            return;
        }

        var type = FogType;
        if (type == FogType.Color)
        {
            _fogTexture = null;
            _fogGenerationVersion++;
            IsFogGenerating = false;
            UpdateFogBitmapRegion(new PixelRect(0, 0, mask.Width, mask.Height));
            InvalidateVisual();
            return;
        }

        _ = RefreshTexturedFogAsync(mask.Width, mask.Height, type, FogSeed, ++_fogGenerationVersion);
    }

    async Task RefreshTexturedFogAsync(int width, int height, FogType type, Guid seed, int generationVersion)
    {
        IsFogGenerating = true;
        InvalidateVisual();

        try
        {
            var texture = await Task.Run(() => _textureGenerator.Generate(width, height, type, seed));
            var mask = FogMask;
            if (generationVersion != _fogGenerationVersion
                || mask is null
                || mask.Width != width
                || mask.Height != height
                || FogType != type
                || FogSeed != seed)
                return;

            _fogTexture = texture;
            UpdateFogBitmapRegion(new PixelRect(0, 0, width, height));
            InvalidateVisual();
        }
        finally
        {
            if (generationVersion == _fogGenerationVersion)
                IsFogGenerating = false;
        }
    }

    /// <summary>
    /// Writes premultiplied BGRA8888 pixels into the fog bitmap for <paramref name="dirtyRect"/>.
    /// The colour at each pixel comes from <see cref="FogColor"/> in flat-colour mode or from the
    /// pre-generated noise texture in textured modes; alpha is derived from the fog mask value and
    /// <see cref="FogOpacity"/> so a fully fogged pixel (mask = 0) gets full fog opacity and a fully
    /// revealed pixel (mask = 255) is transparent.
    /// </summary>
    void UpdateFogBitmapRegion(PixelRect dirtyRect)
    {
        var mask = FogMask;
        if (mask is null || _fogBitmap is null)
            return;

        var fogOpacity = FogOpacity;
        var texture = _fogTexture;
        var color = FogColor;
        var width = mask.Width;
        var useTexture = FogType != FogType.Color;

        if (useTexture && texture is null)
            return;

        using var fb = _fogBitmap.Lock();
        unsafe
        {
            var ptr = (byte*)fb.Address;
            var stride = fb.RowBytes;

            var minX = Math.Max(0, dirtyRect.X);
            var minY = Math.Max(0, dirtyRect.Y);
            var maxX = Math.Min(width, dirtyRect.X + dirtyRect.Width);
            var maxY = Math.Min(mask.Height, dirtyRect.Y + dirtyRect.Height);

            if (!useTexture)
            {
                byte cb = color.B, cg = color.G, cr = color.R;
                for (var y = minY; y < maxY; y++)
                {
                    var row = ptr + y * stride;
                    for (var x = minX; x < maxX; x++)
                    {
                        var alpha = fogOpacity * (255 - mask[x, y]) / 255;
                        var offset = x * 4;
                        row[offset + 0] = (byte)(cb * alpha);
                        row[offset + 1] = (byte)(cg * alpha);
                        row[offset + 2] = (byte)(cr * alpha);
                        row[offset + 3] = (byte)(alpha * 255);
                    }
                }
            }
            else
            {
                if (texture is null)
                    return;

                fixed (byte* texPtr = texture)
                {
                    for (var y = minY; y < maxY; y++)
                    {
                        var row = ptr + y * stride;
                        var texRow = texPtr + y * width * 3;
                        for (var x = minX; x < maxX; x++)
                        {
                            var alpha = fogOpacity * (255 - mask[x, y]) / 255;
                            var offset = x * 4;
                            var texOffset = x * 3;
                            row[offset + 0] = (byte)(texRow[texOffset + 0] * alpha);
                            row[offset + 1] = (byte)(texRow[texOffset + 1] * alpha);
                            row[offset + 2] = (byte)(texRow[texOffset + 2] * alpha);
                            row[offset + 3] = (byte)(alpha * 255);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Renders the black background, the map image, the fog overlay, and (in DM mode) the tool cursor preview.
    /// The map and fog are drawn inside a scale+translate transform derived from <see cref="ZoomLevel"/>,
    /// <see cref="OffsetX"/>, and <see cref="OffsetY"/>.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

        var mapImage = MapImage;
        if (mapImage is null)
            return;

        if (FogType != FogType.Color && _fogTexture is null)
            return;

        var zoom = ZoomLevel;
        var offsetX = OffsetX;
        var offsetY = OffsetY;

        var transform = Matrix.CreateScale(zoom, zoom) * Matrix.CreateTranslation(offsetX, offsetY);

        using (context.PushTransform(transform))
        {
            var imageRect = new Rect(0, 0, mapImage.Size.Width, mapImage.Size.Height);
            if (ShowMap)
                context.DrawImage(mapImage, imageRect);
            else
                context.FillRectangle(Brushes.White, imageRect);

            if (_fogBitmap != null)
            {
                var fogRect = new Rect(0, 0, _fogBitmap.Size.Width, _fogBitmap.Size.Height);
                context.DrawImage(_fogBitmap, fogRect);
            }
        }

        if (IsDmMode && IsPointerOver)
            RenderToolOverlay(context, zoom);
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
            var start = _shapeDragStart;
            var end = _lastMousePosition;
            var shapeType = ShapeType;

            if (shapeType is ShapeType.Square or ShapeType.Circle)
            {
                var side = Math.Min(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
                end = new Point(
                    start.X + Math.Sign(end.X - start.X) * side,
                    start.Y + Math.Sign(end.Y - start.Y) * side);
            }

            if (shapeType is ShapeType.Ellipse or ShapeType.Circle)
            {
                var cx = (start.X + end.X) / 2;
                var cy = (start.Y + end.Y) / 2;
                var rx = Math.Abs(end.X - start.X) / 2;
                var ry = Math.Abs(end.Y - start.Y) / 2;
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), pen, new Point(cx, cy), rx, ry);
            }
            else
            {
                var rect = MakeRect(start, end);
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), rect);
                context.DrawRectangle(null, pen, rect);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateCursor();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActiveToolProperty || change.Property == IsDmModeProperty)
            UpdateCursor();

        if (change.Property == MapImageProperty && MapImage is not null)
            RefitViewToMapHeight();

        if (change.Property == BoundsProperty
            || change.Property == MapImageProperty
            || change.Property == ZoomLevelProperty)
        {
            UpdateZoomPercent();
        }

        if (change.Property == BoundsProperty
            || change.Property == MapImageProperty
            || change.Property == ZoomLevelProperty
            || change.Property == OffsetXProperty
            || change.Property == OffsetYProperty)
        {
            RaiseViewportChanged();
        }

        if (FogMask is null)
            return;

        var needsTextureRefresh = change.Property == FogTypeProperty || change.Property == FogSeedProperty;
        var needsBitmapRefresh = needsTextureRefresh
            || change.Property == FogOpacityProperty
            || change.Property == FogColorProperty;

        if (needsTextureRefresh)
            RefreshFogTextureAndBitmap();
        else if (needsBitmapRefresh)
        {
            UpdateFogBitmapRegion(new PixelRect(0, 0, FogMask.Width, FogMask.Height));
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Updates the cursor based on the current tool and interaction state:
    /// hidden during painting (to show the brush outline instead), resize-all during panning,
    /// cross-hair for brush/shape, and default for player mode.
    /// </summary>
    void UpdateCursor()
    {
        if (!IsDmMode || ActiveTool == ToolType.Fog)
        {
            Cursor = Cursor.Default;
            return;
        }

        if (_isPainting)
        {
            Cursor = new Cursor(StandardCursorType.None);
            return;
        }

        if (_isPanning || ActiveTool == ToolType.Pan)
        {
            Cursor = new Cursor(StandardCursorType.SizeAll);
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

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanPoint = point.Position;
            UpdateCursor();
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

        if (!IsDmMode)
            return;

        if (_isPanning)
        {
            var delta = point.Position - _lastPanPoint;
            OffsetX += delta.X;
            OffsetY += delta.Y;
            _lastPanPoint = point.Position;
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
        var oldZoom = ZoomLevel;
        var zoomFactor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        var newZoom = ClampZoomLevel(oldZoom * zoomFactor);

        // Zoom centered on mouse position
        OffsetX = mousePos.X - ((mousePos.X - OffsetX) * (newZoom / oldZoom));
        OffsetY = mousePos.Y - ((mousePos.Y - OffsetY) * (newZoom / oldZoom));
        ZoomLevel = newZoom;

        e.Handled = true;
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

    /// <summary>
    /// Records the current pointer position converted to map coordinates as the starting
    /// point for the next brush stroke segment.
    /// </summary>
    void InitBrushMapPos(Point screenPos)
    {
        var zoom = ZoomLevel;
        if (zoom <= 0)
            return;

        _lastBrushMapX = (int)((screenPos.X - OffsetX) / zoom);
        _lastBrushMapY = (int)((screenPos.Y - OffsetY) / zoom);
    }

    /// <summary>
    /// Converts <paramref name="screenTo"/> to map coordinates, fires <see cref="BrushStrokeApplied"/>
    /// with the segment from the last position to the new position, then updates the last position.
    /// </summary>
    void RaiseBrushStroke(Point screenTo)
    {
        var zoom = ZoomLevel;
        if (zoom <= 0)
            return;

        var mapX2 = (int)((screenTo.X - OffsetX) / zoom);
        var mapY2 = (int)((screenTo.Y - OffsetY) / zoom);

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
        var zoom = ZoomLevel;
        var mapX1 = (int)((screenStart.X - OffsetX) / zoom);
        var mapY1 = (int)((screenStart.Y - OffsetY) / zoom);
        var mapX2 = (int)((screenEnd.X - OffsetX) / zoom);
        var mapY2 = (int)((screenEnd.Y - OffsetY) / zoom);

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
}
