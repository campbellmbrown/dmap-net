using System;

using Avalonia;

using DMap.Protocol;

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

    /// <summary>Clockwise map rotation in 90-degree increments, normalized to 0–3.</summary>
    public int RotationQuarterTurns { get; private set; }

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
        var percent = ZoomLevel / GetHeightFitZoomLevel(canvasSize, mapSize, RotationQuarterTurns) * 100.0;
        return Math.Clamp(percent, MinZoomPercent, MaxZoomPercent);
    }

    /// <summary>Returns the zoom where the map height exactly matches the canvas height.</summary>
    public static double GetHeightFitZoomLevel(Size canvasSize, Size? mapSize, int rotationQuarterTurns = 0)
    {
        var rotatedSize = GetRotatedMapSize(mapSize, rotationQuarterTurns);
        if (rotatedSize is not { Height: > 0 } || canvasSize.Height <= 0)
            return 1.0;

        return canvasSize.Height / rotatedSize.Value.Height;
    }

    /// <summary>Fits map height to canvas height and horizontally centers the map.</summary>
    public bool RefitToMapHeight(Size canvasSize, Size? mapSize)
    {
        var rotatedSize = GetRotatedMapSize(mapSize, RotationQuarterTurns);
        if (rotatedSize is not { Height: > 0 } || canvasSize.Height <= 0)
            return false;

        ZoomLevel = GetHeightFitZoomLevel(canvasSize, mapSize, RotationQuarterTurns);
        OffsetX = (canvasSize.Width - rotatedSize.Value.Width * ZoomLevel) / 2.0;
        OffsetY = 0;
        return true;
    }

    /// <summary>Returns current camera state as a network viewport payload.</summary>
    public ViewportPayload GetViewport(Size canvasSize, Size? mapSize = null)
    {
        var zoom = ZoomLevel <= 0 ? 1.0 : ZoomLevel;
        var center = ScreenToMap(new Point(canvasSize.Width / 2.0, canvasSize.Height / 2.0), mapSize);
        return new ViewportPayload
        {
            CenterMapX = center.X,
            CenterMapY = center.Y,
            ZoomLevel = zoom,
            RotationQuarterTurns = RotationQuarterTurns,
        };
    }

    /// <summary>Applies a network viewport payload to the current canvas size.</summary>
    public void ApplyViewport(ViewportPayload viewport, Size canvasSize, Size? mapSize)
    {
        var zoom = viewport.ZoomLevel;
        if (double.IsNaN(zoom) || double.IsInfinity(zoom) || zoom <= 0)
            zoom = GetHeightFitZoomLevel(canvasSize, mapSize, viewport.RotationQuarterTurns);

        RotationQuarterTurns = NormalizeRotation(viewport.RotationQuarterTurns);
        ZoomLevel = zoom;
        var rotatedCenter = RotateMapPoint(new Point(viewport.CenterMapX, viewport.CenterMapY), mapSize, RotationQuarterTurns);
        OffsetX = canvasSize.Width / 2.0 - rotatedCenter.X * zoom;
        OffsetY = canvasSize.Height / 2.0 - rotatedCenter.Y * zoom;
    }

    /// <summary>Rotates the map view while preserving the map point at the canvas center.</summary>
    public void RotateBy(int quarterTurns, Size canvasSize, Size? mapSize)
    {
        var screenCenter = new Point(canvasSize.Width / 2.0, canvasSize.Height / 2.0);
        var mapCenter = ScreenToMap(screenCenter, mapSize);
        RotationQuarterTurns = NormalizeRotation(RotationQuarterTurns + quarterTurns);
        var rotatedCenter = RotateMapPoint(mapCenter, mapSize, RotationQuarterTurns);
        OffsetX = screenCenter.X - rotatedCenter.X * ZoomLevel;
        OffsetY = screenCenter.Y - rotatedCenter.Y * ZoomLevel;
    }

    /// <summary>Restores the unrotated orientation.</summary>
    public void ResetRotation() => RotationQuarterTurns = 0;

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
    public Point ScreenToMap(Point screenPoint, Size? mapSize = null)
    {
        var rotatedPoint = new Point(
            (screenPoint.X - OffsetX) / ZoomLevel,
            (screenPoint.Y - OffsetY) / ZoomLevel);
        return UnrotateMapPoint(rotatedPoint, mapSize, RotationQuarterTurns);
    }

    /// <summary>Converts map coordinates to screen coordinates using current camera state.</summary>
    public Point MapToScreen(Point mapPoint, Size? mapSize)
    {
        var rotatedPoint = RotateMapPoint(mapPoint, mapSize, RotationQuarterTurns);
        return new Point(rotatedPoint.X * ZoomLevel + OffsetX, rotatedPoint.Y * ZoomLevel + OffsetY);
    }

    /// <summary>Returns the map-to-screen matrix used to render the complete map-space composition.</summary>
    public Matrix GetMapToScreenTransform(Size? mapSize)
    {
        var correction = GetRotationCorrection(mapSize, RotationQuarterTurns);
        return Matrix.CreateRotation(RotationQuarterTurns * Math.PI / 2.0)
            * Matrix.CreateTranslation(correction.X, correction.Y)
            * Matrix.CreateScale(ZoomLevel, ZoomLevel)
            * Matrix.CreateTranslation(OffsetX, OffsetY);
    }

    double ZoomPercentToZoomLevel(double zoomPercent, Size canvasSize, Size? mapSize) =>
        GetHeightFitZoomLevel(canvasSize, mapSize, RotationQuarterTurns) * Math.Clamp(zoomPercent, MinZoomPercent, MaxZoomPercent) / 100.0;

    double ClampZoomLevel(double zoomLevel, Size canvasSize, Size? mapSize) =>
        Math.Clamp(zoomLevel, ZoomPercentToZoomLevel(MinZoomPercent, canvasSize, mapSize), ZoomPercentToZoomLevel(MaxZoomPercent, canvasSize, mapSize));

    static int NormalizeRotation(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;

    static Size? GetRotatedMapSize(Size? mapSize, int quarterTurns)
    {
        if (mapSize is null)
            return null;

        return NormalizeRotation(quarterTurns) % 2 == 0
            ? mapSize
            : new Size(mapSize.Value.Height, mapSize.Value.Width);
    }

    static Point RotateMapPoint(Point point, Size? mapSize, int quarterTurns)
    {
        var size = mapSize ?? default;
        return NormalizeRotation(quarterTurns) switch
        {
            1 => new Point(size.Height - point.Y, point.X),
            2 => new Point(size.Width - point.X, size.Height - point.Y),
            3 => new Point(point.Y, size.Width - point.X),
            _ => point,
        };
    }

    static Point UnrotateMapPoint(Point point, Size? mapSize, int quarterTurns)
    {
        var size = mapSize ?? default;
        return NormalizeRotation(quarterTurns) switch
        {
            1 => new Point(point.Y, size.Height - point.X),
            2 => new Point(size.Width - point.X, size.Height - point.Y),
            3 => new Point(size.Width - point.Y, point.X),
            _ => point,
        };
    }

    static Vector GetRotationCorrection(Size? mapSize, int quarterTurns)
    {
        var size = mapSize ?? default;
        return NormalizeRotation(quarterTurns) switch
        {
            1 => new Vector(size.Height, 0),
            2 => new Vector(size.Width, size.Height),
            3 => new Vector(0, size.Width),
            _ => default,
        };
    }
}
