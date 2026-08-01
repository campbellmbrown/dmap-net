using Avalonia;

using DMap.Controls.MapCanvas;
using DMap.Protocol;

using NUnit.Framework;

namespace DMap.Tests.Controls;

public class PlayerViewportEditorTests
{
    [Test]
    public void UpdateDrag_MovesViewportWhenDragStartsInsideRect()
    {
        // Arrange
        var editor = new PlayerViewportEditor();
        var viewport = CreateViewport();

        // Act
        var began = editor.TryBeginDrag(new Point(100, 100), viewport, zoomLevel: 1);
        var updated = editor.UpdateDrag(new Point(110, 115), viewport, new Size(300, 300));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(began, Is.True);
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.CenterMapX, Is.EqualTo(110));
            Assert.That(updated.CenterMapY, Is.EqualTo(115));
            Assert.That(updated.WidthMap, Is.EqualTo(100));
            Assert.That(updated.HeightMap, Is.EqualTo(80));
        });
    }

    [Test]
    public void UpdateDrag_ResizesViewportFromRightHandle()
    {
        // Arrange
        var editor = new PlayerViewportEditor();
        var viewport = CreateViewport();

        // Act
        var began = editor.TryBeginDrag(new Point(150, 100), viewport, zoomLevel: 1);
        var updated = editor.UpdateDrag(new Point(170, 100), viewport, new Size(300, 300));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(began, Is.True);
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.CenterMapX, Is.EqualTo(110));
            Assert.That(updated.CenterMapY, Is.EqualTo(100));
            Assert.That(updated.WidthMap, Is.EqualTo(120));
            Assert.That(updated.HeightMap, Is.EqualTo(80));
        });
    }

    [Test]
    public void UpdateDrag_ClampsMovedViewportToMapBounds()
    {
        // Arrange
        var editor = new PlayerViewportEditor();
        var viewport = CreateViewport();

        // Act
        editor.TryBeginDrag(new Point(100, 100), viewport, zoomLevel: 1);
        var updated = editor.UpdateDrag(new Point(-100, -100), viewport, new Size(300, 300));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.CenterMapX, Is.EqualTo(50));
            Assert.That(updated.CenterMapY, Is.EqualTo(40));
            Assert.That(updated.WidthMap, Is.EqualTo(100));
            Assert.That(updated.HeightMap, Is.EqualTo(80));
        });
    }

    static ViewportPayload CreateViewport() =>
        new()
        {
            CenterMapX = 100,
            CenterMapY = 100,
            ZoomLevel = 2,
            RotationQuarterTurns = 1,
            WidthMap = 100,
            HeightMap = 80,
            PaddingPixels = 4,
        };
}
