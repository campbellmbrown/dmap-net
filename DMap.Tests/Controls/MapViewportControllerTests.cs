using Avalonia;

using DMap.Controls.MapCanvas;

using NUnit.Framework;

namespace DMap.Tests.Controls;

public class MapViewportControllerTests
{
    static readonly Size _mapSize = new(1000, 500);
    static readonly Size _canvasSize = new(400, 200);

    [Test]
    public void FitMapRectMakesRectFillCanvas()
    {
        var controller = new MapViewportController();
        var mapRect = new Rect(100, 50, 200, 100);

        var contentRect = controller.FitMapRect(mapRect, new Rect(_canvasSize), _mapSize);

        Assert.Multiple(() =>
        {
            Assert.That(contentRect, Is.EqualTo(new Rect(_canvasSize)));
            Assert.That(controller.GetVisibleMapRect(_canvasSize, _mapSize), Is.EqualTo(mapRect));
        });
    }

    [Test]
    public void FitMapRectKeepsRotation()
    {
        var controller = new MapViewportController();
        controller.RotateBy(1, _canvasSize, _mapSize);
        var mapRect = new Rect(100, 50, 100, 200);

        controller.FitMapRect(mapRect, new Rect(_canvasSize), _mapSize);

        Assert.Multiple(() =>
        {
            Assert.That(controller.RotationQuarterTurns, Is.EqualTo(1));
            Assert.That(controller.GetVisibleMapRect(_canvasSize, _mapSize), Is.EqualTo(mapRect));
        });
    }

    [Test]
    public void FitMapRectReturnsNullForEmptyRect()
    {
        var controller = new MapViewportController();

        Assert.That(controller.FitMapRect(default, new Rect(_canvasSize), _mapSize), Is.Null);
    }

    [Test]
    public void GetVisibleMapRectIsClippedToMap()
    {
        var controller = new MapViewportController();
        controller.SetOffset(-100, 0);

        Assert.That(controller.GetVisibleMapRect(new Size(2000, 2000), _mapSize), Is.EqualTo(new Rect(100, 0, 900, 500)));
    }
}
