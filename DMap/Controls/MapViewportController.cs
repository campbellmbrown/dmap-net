using System;

using Avalonia;

using DMap.Services.Networking;

namespace DMap.Controls;

/// <summary>
/// Owns map camera state and viewport math for <see cref="MapCanvas"/>.
/// </summary>
public sealed class MapViewportController
{
    const double MinZoomPercent = 10.0;
    const double MaxZoomPercent = 1000.0;

    /// <summary>Actual zoom multiplier applied to map-space drawing.</summary>
    public double ZoomLevel { get; private set; } = 1.0;

    /// <summary>Horizontal map translation in screen pixels.</summary>
    public double OffsetX { get; private set; }

    /// <summary>Vertical map translation in screen pixels.</summary>
    public double OffsetY { get; private set; }

    /// <summary>Sets actual zoom, clamped to the fit-relative user zoom range.</summary>
    public void SetZoomLevel(double zoomLevel, Size canvasSize, Size? mapSize) =>
        ZoomLevel = ClampZoomLevel(zoomLevel, canvasSize, mapSize);

    /// <summary>Sets actual zoom from a fit-relative percentage.</summary>
    public void SetZoomPercent(double zoomPercent, Size canvasSize, Size? mapSize) =>
        ZoomLevel = ZoomPercentToZoomLevel(zoomPercent, canvasSize, mapSize);

    /// <summary>Sets screen-space camera offset.</summary>
    public void SetOffset(double offsetX, double offsetY)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>Returns current zoom as a percentage of the height-fit zoom.</summary>
    public double GetZoomPercent(Size canvasSize, Size? mapSize)
    {
        var percent = ZoomLevel / GetHeightFitZoomLevel(canvasSize, mapSize) * 100.0;
        return Math.Clamp(percent, MinZoomPercent, MaxZoomPercent);
    }

    /// <summary>Returns the zoom where the map height exactly matches the canvas height.</summary>
    public static double GetHeightFitZoomLevel(Size canvasSize, Size? mapSize)
    {
        if (mapSize is not { Height: > 0 } || canvasSize.Height <= 0)
            return 1.0;

        return canvasSize.Height / mapSize.Value.Height;
    }

    /// <summary>Fits map height to canvas height and horizontally centers the map.</summary>
    public bool RefitToMapHeight(Size canvasSize, Size? mapSize)
    {
        if (mapSize is not { Height: > 0 } || canvasSize.Height <= 0)
            return false;

        ZoomLevel = GetHeightFitZoomLevel(canvasSize, mapSize);
        OffsetX = (canvasSize.Width - mapSize.Value.Width * ZoomLevel) / 2.0;
        OffsetY = 0;
        return true;
    }

    /// <summary>Returns current camera state as a network viewport payload.</summary>
    public ViewportPayload GetViewport(Size canvasSize)
    {
        var zoom = ZoomLevel <= 0 ? 1.0 : ZoomLevel;
        return new ViewportPayload
        {
            CenterMapX = (canvasSize.Width / 2.0 - OffsetX) / zoom,
            CenterMapY = (canvasSize.Height / 2.0 - OffsetY) / zoom,
            ZoomLevel = zoom,
        };
    }

    /// <summary>Applies a network viewport payload to the current canvas size.</summary>
    public void ApplyViewport(ViewportPayload viewport, Size canvasSize, Size? mapSize)
    {
        var zoom = viewport.ZoomLevel;
        if (double.IsNaN(zoom) || double.IsInfinity(zoom) || zoom <= 0)
            zoom = GetHeightFitZoomLevel(canvasSize, mapSize);

        ZoomLevel = zoom;
        OffsetX = canvasSize.Width / 2.0 - viewport.CenterMapX * zoom;
        OffsetY = canvasSize.Height / 2.0 - viewport.CenterMapY * zoom;
    }

    /// <summary>Moves the camera by a screen-space pan delta.</summary>
    public void PanBy(Vector delta)
    {
        OffsetX += delta.X;
        OffsetY += delta.Y;
    }

    /// <summary>Zooms around a screen-space anchor, keeping the map point under the anchor stationary.</summary>
    public void ZoomAround(Point anchor, double zoomFactor, Size canvasSize, Size? mapSize)
    {
        var oldZoom = ZoomLevel;
        var newZoom = ClampZoomLevel(oldZoom * zoomFactor, canvasSize, mapSize);
        OffsetX = anchor.X - ((anchor.X - OffsetX) * (newZoom / oldZoom));
        OffsetY = anchor.Y - ((anchor.Y - OffsetY) * (newZoom / oldZoom));
        ZoomLevel = newZoom;
    }

    /// <summary>Converts screen coordinates to map coordinates using current camera state.</summary>
    public Point ScreenToMap(Point screenPoint) =>
        new((screenPoint.X - OffsetX) / ZoomLevel, (screenPoint.Y - OffsetY) / ZoomLevel);

    double ZoomPercentToZoomLevel(double zoomPercent, Size canvasSize, Size? mapSize) =>
        GetHeightFitZoomLevel(canvasSize, mapSize) * Math.Clamp(zoomPercent, MinZoomPercent, MaxZoomPercent) / 100.0;

    double ClampZoomLevel(double zoomLevel, Size canvasSize, Size? mapSize) =>
        Math.Clamp(zoomLevel, ZoomPercentToZoomLevel(MinZoomPercent, canvasSize, mapSize), ZoomPercentToZoomLevel(MaxZoomPercent, canvasSize, mapSize));
}
