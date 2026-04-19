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

    public event EventHandler<BrushStrokeEventArgs>? BrushStrokeApplied;

    private WriteableBitmap? _fogBitmap;
    private bool _isPanning;
    private Point _lastPanPoint;
    private bool _isPainting;
    private Point _lastPaintPosition;
    private Point _lastMousePosition;

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(
            MapImageProperty, FogMaskProperty, ZoomLevelProperty,
            OffsetXProperty, OffsetYProperty, FogOpacityProperty,
            BrushDiameterProperty);
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

        // Draw brush cursor in DM mode
        if (IsDmMode && IsPointerOver)
        {
            var brushRadius = BrushDiameter * zoom / 2.0;
            var cursorCenter = _lastMousePosition;

            var pen = new Pen(Brushes.White, 1.5);
            context.DrawEllipse(null, pen, cursorCenter, brushRadius, brushRadius);
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
            _isPainting = true;
            _lastPaintPosition = point.Position;
            RaiseBrushStroke(point.Position);
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

        // Invalidate to update brush cursor position
        if (IsDmMode)
            InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
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
        OffsetX = mousePos.X - (mousePos.X - OffsetX) * (newZoom / oldZoom);
        OffsetY = mousePos.Y - (mousePos.Y - OffsetY) * (newZoom / oldZoom);
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
}
