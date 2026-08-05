using Avalonia;

using DMap.Controls.MapCanvas.Stamp;
using DMap.Models;

using NUnit.Framework;

namespace DMap.Tests.Controls;

public class StampTransformEditorTests
{
    [Test]
    public void HitTest_ReturnsTopmostStampContainingPoint()
    {
        // Arrange
        var editor = new StampTransformEditor();
        var bottom = CreateStamp(x: 0, y: 0, width: 20, height: 20);
        var top = CreateStamp(x: 5, y: 5, width: 20, height: 20);

        // Act
        var hit = editor.HitTest([bottom, top], new Point(10, 10));

        // Assert
        Assert.That(hit, Is.SameAs(top));
    }

    [Test]
    public void UpdateDrag_MovesStampByPointerDelta()
    {
        // Arrange
        var editor = new StampTransformEditor();
        var stamp = CreateStamp(x: 10, y: 20, width: 30, height: 40);

        // Act
        editor.BeginMoveDrag(stamp, new Point(15, 25));
        editor.UpdateDrag(stamp, new Point(25, 45), new Size(200, 200), preserveAspect: false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(stamp.X, Is.EqualTo(20));
            Assert.That(stamp.Y, Is.EqualTo(40));
            Assert.That(stamp.Width, Is.EqualTo(30));
            Assert.That(stamp.Height, Is.EqualTo(40));
        });
    }

    [Test]
    public void UpdateDrag_ResizesStampFromRightHandle()
    {
        // Arrange
        var editor = new StampTransformEditor();
        var stamp = CreateStamp(x: 10, y: 20, width: 30, height: 40);

        // Act
        var began = editor.TryBeginHandleDrag(stamp, new Point(40, 40), zoomLevel: 1);
        editor.UpdateDrag(stamp, new Point(55, 40), new Size(200, 200), preserveAspect: false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(began, Is.True);
            Assert.That(stamp.X, Is.EqualTo(10));
            Assert.That(stamp.Y, Is.EqualTo(20));
            Assert.That(stamp.Width, Is.EqualTo(45));
            Assert.That(stamp.Height, Is.EqualTo(40));
        });
    }

    [Test]
    public void TryBeginHandleDrag_SetsActiveResizeHandle()
    {
        // Arrange
        var editor = new StampTransformEditor();
        var stamp = CreateStamp(x: 10, y: 20, width: 30, height: 40);

        // Act
        var began = editor.TryBeginHandleDrag(stamp, new Point(40, 40), zoomLevel: 1);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(began, Is.True);
            Assert.That(editor.ActiveHandle, Is.EqualTo(StampHandle.Right));
        });
    }

    [Test]
    public void GetHoverHandle_ReturnsHandleUnderPointer()
    {
        // Arrange
        var editor = new StampTransformEditor();
        var stamp = CreateStamp(x: 10, y: 20, width: 30, height: 40);
        stamp.RotationDegrees = 90;
        var rightHandle = editor.GetHandlePoint(stamp, StampHandle.Right, zoomLevel: 1);

        // Act
        var handle = editor.GetHoverHandle(stamp, rightHandle, zoomLevel: 1);

        // Assert
        Assert.That(handle, Is.EqualTo(StampHandle.Right));
    }

    [Test]
    public void GetHoverHandle_ReturnsNoneWithoutSelectedStamp()
    {
        // Arrange
        var editor = new StampTransformEditor();

        // Act
        var handle = editor.GetHoverHandle(selectedStamp: null, new Point(40, 40), zoomLevel: 1);

        // Assert
        Assert.That(handle, Is.EqualTo(StampHandle.None));
    }

    [Test]
    public void GetHoverHandle_ReturnsRotateHandleUnderPointer()
    {
        // Arrange
        var editor = new StampTransformEditor();
        var stamp = CreateStamp(x: 10, y: 20, width: 30, height: 40);
        var rotateHandle = editor.GetHandlePoint(stamp, StampHandle.Rotate, zoomLevel: 1);

        // Act
        var handle = editor.GetHoverHandle(stamp, rotateHandle, zoomLevel: 1);

        // Assert
        Assert.That(handle, Is.EqualTo(StampHandle.Rotate));
    }

    [Test]
    public void UpdateDrag_RotatesStampFromRotateHandle()
    {
        // Arrange
        var editor = new StampTransformEditor();
        var stamp = CreateStamp(x: 0, y: 0, width: 10, height: 10);
        var rotateHandle = editor.GetHandlePoint(stamp, StampHandle.Rotate, zoomLevel: 1);

        // Act
        var began = editor.TryBeginHandleDrag(stamp, rotateHandle, zoomLevel: 1);
        editor.UpdateDrag(stamp, new Point(33, 5), mapSize: null, preserveAspect: false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(began, Is.True);
            Assert.That(stamp.RotationDegrees, Is.EqualTo(90).Within(0.0001));
        });
    }

    static StampInstance CreateStamp(double x, double y, double width, double height) =>
        new()
        {
            TemplateId = "test",
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };
}
