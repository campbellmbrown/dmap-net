using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using DMap.Models;

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
/// In player mode (<see cref="IsDmMode"/> = <see langword="false"/>) all editing input is suppressed.
/// </summary>
public class MapCanvas : Control
{
    /// <summary>Styled property for the map background image.</summary>
    public static readonly StyledProperty<Bitmap?> MapImageProperty =
        AvaloniaProperty.Register<MapCanvas, Bitmap?>(nameof(MapImage));

    /// <summary>Styled property for the fog mask used to build the fog bitmap.</summary>
    public static readonly StyledProperty<FogMask?> FogMaskProperty =
        AvaloniaProperty.Register<MapCanvas, FogMask?>(nameof(FogMask));

    /// <summary>Styled property for the canvas zoom multiplier (default 1.0).</summary>
    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(ZoomLevel), 1.0);

    /// <summary>Styled property for the horizontal pan offset in screen pixels.</summary>
    public static readonly StyledProperty<double> OffsetXProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(OffsetX));

    /// <summary>Styled property for the vertical pan offset in screen pixels.</summary>
    public static readonly StyledProperty<double> OffsetYProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(OffsetY));

    /// <summary>Styled property that enables DM editing mode (brush/shape input, cursor preview).</summary>
    public static readonly StyledProperty<bool> IsDmModeProperty =
        AvaloniaProperty.Register<MapCanvas, bool>(nameof(IsDmMode));

    /// <summary>Styled property for the fog overlay opacity (0 = transparent, 255 = fully opaque black).</summary>
    public static readonly StyledProperty<byte> FogOpacityProperty =
        AvaloniaProperty.Register<MapCanvas, byte>(nameof(FogOpacity), 255);

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

    /// <summary>Zoom multiplier applied via a scale transform before the pan offset.</summary>
    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
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
    /// When <see langword="false"/> (player mode), only panning and zooming are available.
    /// </summary>
    public bool IsDmMode
    {
        get => GetValue(IsDmModeProperty);
        set => SetValue(IsDmModeProperty, value);
    }

    /// <summary>
    /// Opacity of the black fog overlay layer in the range [0, 255].
    /// Changing this value triggers a full fog bitmap region update and a visual invalidation.
    /// </summary>
    public byte FogOpacity
    {
        get => GetValue(FogOpacityProperty);
        set => SetValue(FogOpacityProperty, value);
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

    WriteableBitmap? _fogBitmap;
    bool _isPanning;
    Point _lastPanPoint;
    bool _isPainting;
    bool _isErasing;
    int _lastBrushMapX;
    int _lastBrushMapY;
    bool _isDraggingShape;
    Point _shapeDragStart;
    Point _lastMousePosition;

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(
            MapImageProperty, FogMaskProperty, ZoomLevelProperty,
            OffsetXProperty, OffsetYProperty, FogOpacityProperty,
            BrushDiameterProperty, ActiveToolProperty, BrushShapeProperty,
            ShapeTypeProperty, ShowMapProperty);
    }

    /// <summary>Initialises the control with clipping and keyboard focus enabled.</summary>
    public MapCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
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
            return;
        }

        _fogBitmap = new WriteableBitmap(
            new PixelSize(mask.Width, mask.Height),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        UpdateFogBitmapRegion(new PixelRect(0, 0, mask.Width, mask.Height));
    }

    /// <summary>
    /// Writes premultiplied BGRA8888 pixels into the fog bitmap for <paramref name="dirtyRect"/>.
    /// Each pixel is black with an alpha derived from the fog mask value and <see cref="FogOpacity"/>:
    /// a fully fogged pixel (mask = 0) gets full fog opacity; a fully revealed pixel (mask = 255) is transparent.
    /// </summary>
    void UpdateFogBitmapRegion(PixelRect dirtyRect)
    {
        var mask = FogMask;
        if (mask is null || _fogBitmap is null)
            return;

        var fogOpacity = FogOpacity;

        using var fb = _fogBitmap.Lock();
        unsafe
        {
            var ptr = (byte*)fb.Address;
            var stride = fb.RowBytes;

            var minX = Math.Max(0, dirtyRect.X);
            var minY = Math.Max(0, dirtyRect.Y);
            var maxX = Math.Min(mask.Width, dirtyRect.X + dirtyRect.Width);
            var maxY = Math.Min(mask.Height, dirtyRect.Y + dirtyRect.Height);

            for (var y = minY; y < maxY; y++)
            {
                var row = ptr + y * stride;
                for (var x = minX; x < maxX; x++)
                {
                    var maskValue = mask[x, y];
                    var alpha = (byte)(fogOpacity * (255 - maskValue) / 255);

                    // Premultiplied BGRA: color channels must be <= alpha
                    var offset = x * 4;
                    row[offset + 0] = 0;     // B
                    row[offset + 1] = 0;     // G
                    row[offset + 2] = 0;     // R
                    row[offset + 3] = alpha; // A
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
        if (change.Property == FogOpacityProperty && FogMask is not null)
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
        if (!IsDmMode)
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
            else if (IsDmMode)
            {
                StartPainting(point.Position, erase: false);
            }
            e.Handled = true;
            return;
        }

        if (point.Properties.IsRightButtonPressed && IsDmMode && ActiveTool != ToolType.Pan)
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

        if (_isPanning)
        {
            var delta = point.Position - _lastPanPoint;
            OffsetX += delta.X;
            OffsetY += delta.Y;
            _lastPanPoint = point.Position;
            e.Handled = true;
            return;
        }

        if (_isPainting && IsDmMode)
        {
            RaiseBrushStroke(point.Position);
            e.Handled = true;
        }

        if (IsDmMode)
            InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

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

        var mousePos = e.GetPosition(this);
        var oldZoom = ZoomLevel;
        var zoomFactor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        var newZoom = Math.Clamp(oldZoom * zoomFactor, 0.1, 10.0);

        // Zoom centered on mouse position
        OffsetX = mousePos.X - ((mousePos.X - OffsetX) * (newZoom / oldZoom));
        OffsetY = mousePos.Y - ((mousePos.Y - OffsetY) * (newZoom / oldZoom));
        ZoomLevel = newZoom;

        e.Handled = true;
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
