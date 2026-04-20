using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using DMap.Models;

namespace DMap.Controls;

public class BrushStrokeEventArgs : EventArgs
{
    public int MapX { get; init; }
    public int MapY { get; init; }
}

public class RectangleStrokeEventArgs : EventArgs
{
    public int MapX1 { get; init; }
    public int MapY1 { get; init; }
    public int MapX2 { get; init; }
    public int MapY2 { get; init; }
}

public class MapCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> MapImageProperty =
        AvaloniaProperty.Register<MapCanvas, Bitmap?>(nameof(MapImage));

    public static readonly StyledProperty<FogMask?> FogMaskProperty =
        AvaloniaProperty.Register<MapCanvas, FogMask?>(nameof(FogMask));

    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(ZoomLevel), 1.0);

    public static readonly StyledProperty<double> OffsetXProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(OffsetX));

    public static readonly StyledProperty<double> OffsetYProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(OffsetY));

    public static readonly StyledProperty<bool> IsDmModeProperty =
        AvaloniaProperty.Register<MapCanvas, bool>(nameof(IsDmMode));

    public static readonly StyledProperty<byte> FogOpacityProperty =
        AvaloniaProperty.Register<MapCanvas, byte>(nameof(FogOpacity), 255);

    public static readonly StyledProperty<int> BrushDiameterProperty =
        AvaloniaProperty.Register<MapCanvas, int>(nameof(BrushDiameter), 50);

    public static readonly StyledProperty<ToolType> ActiveToolProperty =
        AvaloniaProperty.Register<MapCanvas, ToolType>(nameof(ActiveTool), ToolType.CircleBrush);

    public Bitmap? MapImage
    {
        get => GetValue(MapImageProperty);
        set => SetValue(MapImageProperty, value);
    }

    public FogMask? FogMask
    {
        get => GetValue(FogMaskProperty);
        set => SetValue(FogMaskProperty, value);
    }

    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public double OffsetX
    {
        get => GetValue(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    public double OffsetY
    {
        get => GetValue(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
    }

    public bool IsDmMode
    {
        get => GetValue(IsDmModeProperty);
        set => SetValue(IsDmModeProperty, value);
    }

    public byte FogOpacity
    {
        get => GetValue(FogOpacityProperty);
        set => SetValue(FogOpacityProperty, value);
    }

    public int BrushDiameter
    {
        get => GetValue(BrushDiameterProperty);
        set => SetValue(BrushDiameterProperty, value);
    }

    public ToolType ActiveTool
    {
        get => GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    public event EventHandler<BrushStrokeEventArgs>? BrushStrokeApplied;
    public event EventHandler<RectangleStrokeEventArgs>? RectangleStrokeApplied;

    private WriteableBitmap? _fogBitmap;
    private bool _isPanning;
    private Point _lastPanPoint;
    private bool _isPainting;
    private Point _lastPaintPosition;
    private bool _isDraggingRectangle;
    private Point _rectangleDragStart;
    private Point _lastMousePosition;

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(
            MapImageProperty, FogMaskProperty, ZoomLevelProperty,
            OffsetXProperty, OffsetYProperty, FogOpacityProperty,
            BrushDiameterProperty, ActiveToolProperty);
    }

    public MapCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public void InvalidateFogRegion(PixelRect dirtyRect)
    {
        UpdateFogBitmapRegion(dirtyRect);
        InvalidateVisual();
    }

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

    private void UpdateFogBitmapRegion(PixelRect dirtyRect)
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
            context.DrawImage(mapImage, imageRect);

            if (_fogBitmap != null)
            {
                var fogRect = new Rect(0, 0, _fogBitmap.Size.Width, _fogBitmap.Size.Height);
                context.DrawImage(_fogBitmap, fogRect);
            }
        }

        if (IsDmMode && IsPointerOver)
        {
            var tool = ActiveTool;
            var pen = new Pen(Brushes.White, 1.5);

            if (tool == ToolType.CircleBrush)
            {
                var r = BrushDiameter * zoom / 2.0;
                context.DrawEllipse(null, pen, _lastMousePosition, r, r);
            }
            else if (tool == ToolType.SquareBrush)
            {
                var r = BrushDiameter * zoom / 2.0;
                var c = _lastMousePosition;
                context.DrawRectangle(null, pen, new Rect(c.X - r, c.Y - r, r * 2, r * 2));
            }
            else if (tool == ToolType.DiamondBrush)
            {
                var r = BrushDiameter * zoom / 2.0;
                var c = _lastMousePosition;
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
            }
            else if (tool == ToolType.Rectangle && _isDraggingRectangle)
            {
                var rect = MakeRect(_rectangleDragStart, _lastMousePosition);
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), rect);
                context.DrawRectangle(null, pen, rect);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsRightButtonPressed)
        {
            _isPanning = true;
            _lastPanPoint = point.Position;
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed && IsDmMode)
        {
            if (ActiveTool is ToolType.CircleBrush or ToolType.SquareBrush or ToolType.DiamondBrush)
            {
                _isPainting = true;
                _lastPaintPosition = point.Position;
                RaiseBrushStroke(point.Position);
            }
            else if (ActiveTool == ToolType.Rectangle)
            {
                _isDraggingRectangle = true;
                _rectangleDragStart = point.Position;
            }

            e.Handled = true;
        }
    }

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
            InterpolateBrushStrokes(_lastPaintPosition, point.Position);
            _lastPaintPosition = point.Position;
            e.Handled = true;
        }

        if (IsDmMode)
            InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDraggingRectangle)
        {
            var point = e.GetCurrentPoint(this);
            FireRectangleStroke(_rectangleDragStart, point.Position);
            _isDraggingRectangle = false;
            InvalidateVisual();
        }

        _isPanning = false;
        _isPainting = false;
    }

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

    private void InterpolateBrushStrokes(Point from, Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var stepSize = Math.Max(1.0, BrushDiameter * ZoomLevel * 0.1);
        var steps = (int)Math.Ceiling(distance / stepSize);

        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            RaiseBrushStroke(new Point(from.X + (dx * t), from.Y + (dy * t)));
        }
    }

    private void RaiseBrushStroke(Point screenPos)
    {
        var zoom = ZoomLevel;
        var mapX = (int)((screenPos.X - OffsetX) / zoom);
        var mapY = (int)((screenPos.Y - OffsetY) / zoom);

        BrushStrokeApplied?.Invoke(this, new BrushStrokeEventArgs { MapX = mapX, MapY = mapY });
    }

    private void FireRectangleStroke(Point screenStart, Point screenEnd)
    {
        var zoom = ZoomLevel;
        var mapX1 = (int)((screenStart.X - OffsetX) / zoom);
        var mapY1 = (int)((screenStart.Y - OffsetY) / zoom);
        var mapX2 = (int)((screenEnd.X - OffsetX) / zoom);
        var mapY2 = (int)((screenEnd.Y - OffsetY) / zoom);

        RectangleStrokeApplied?.Invoke(this, new RectangleStrokeEventArgs
        {
            MapX1 = mapX1,
            MapY1 = mapY1,
            MapX2 = mapX2,
            MapY2 = mapY2,
        });
    }

    private static Rect MakeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
}
