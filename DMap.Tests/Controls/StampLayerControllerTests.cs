using System.Collections.Generic;

using Avalonia;

using DMap.Controls.MapCanvas.Stamp;
using DMap.Models;

using NUnit.Framework;

namespace DMap.Tests.Controls;

public class StampLayerControllerTests
{
    [Test]
    public void ReorderSelectedToFront_MovesSelectedStampAndRaisesChange()
    {
        // Arrange
        var first = CreateStamp(x: 0);
        var selected = CreateStamp(x: 20);
        var last = CreateStamp(x: 40);
        List<StampInstance> stamps = [first, selected, last];
        var state = new StampLayerState(stamps, selected);
        var controller = CreateController(state);

        // Act
        controller.ReorderSelectedToFront();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(stamps, Is.EqualTo(new[] { first, last, selected }));
            Assert.That(controller.SelectedStamp, Is.SameAs(selected));
            Assert.That(state.ChangedStamps, Is.EqualTo(new[] { selected }));
            Assert.That(state.InvalidateCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ClearSelection_ClearsSelectionAndInvalidatesWithoutRaisingChange()
    {
        // Arrange
        var selected = CreateStamp(x: 0);
        var state = new StampLayerState([selected], selected);
        var controller = CreateController(state);

        // Act
        controller.ClearSelection();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(controller.SelectedStamp, Is.Null);
            Assert.That(state.ChangedStamps, Is.Empty);
            Assert.That(state.InvalidateCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void HandlePointerPress_SelectsHitStampAndReturnsItForDragging()
    {
        // Arrange
        var stamp = CreateStamp(x: 0, y: 0, width: 20, height: 20);
        var state = new StampLayerState([stamp], selectedStamp: null);
        var controller = CreateController(state);

        // Act
        var hit = controller.HandlePointerPress(new Point(10, 10));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.SameAs(stamp));
            Assert.That(controller.SelectedStamp, Is.SameAs(stamp));
            Assert.That(state.ChangedStamps, Is.Empty);
            Assert.That(state.InvalidateCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void HandlePointerPress_ClearsSelectionWhenClickingEmptyMap()
    {
        // Arrange
        var selected = CreateStamp(x: 0, y: 0, width: 20, height: 20);
        var state = new StampLayerState([selected], selected);
        var controller = CreateController(state);

        // Act
        var hit = controller.HandlePointerPress(new Point(100, 100));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.Null);
            Assert.That(controller.SelectedStamp, Is.Null);
            Assert.That(state.ChangedStamps, Is.Empty);
            Assert.That(state.InvalidateCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void HandlePointerPress_PlacesStampWhenClickingEmptyMapWithoutSelection()
    {
        // Arrange
        var state = new StampLayerState([], selectedStamp: null);
        var controller = CreateController(state);

        // Act
        var hit = controller.HandlePointerPress(new Point(100, 100));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.Null);
            Assert.That(state.Stamps, Has.Count.EqualTo(1));
            Assert.That(controller.SelectedStamp, Is.SameAs(state.Stamps[0]));
            Assert.That(state.ChangedStamps, Is.EqualTo(new[] { state.Stamps[0] }));
            Assert.That(state.InvalidateCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void DuplicateSelected_AddsOffsetCopyAndSelectsIt()
    {
        // Arrange
        var selected = CreateStamp(x: 10, y: 20, width: 30, height: 40);
        selected.RotationDegrees = 45;
        var state = new StampLayerState([selected], selected);
        var controller = CreateController(state);

        // Act
        controller.DuplicateSelected();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(state.Stamps, Has.Count.EqualTo(2));
            Assert.That(controller.SelectedStamp, Is.SameAs(state.Stamps[1]));
            Assert.That(state.Stamps[1].X, Is.EqualTo(26));
            Assert.That(state.Stamps[1].Y, Is.EqualTo(36));
            Assert.That(state.Stamps[1].RotationDegrees, Is.EqualTo(45));
            Assert.That(state.ChangedStamps, Is.EqualTo(new[] { state.Stamps[1] }));
            Assert.That(state.InvalidateCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void DeleteSelected_RemovesStampAndClearsSelection()
    {
        // Arrange
        var selected = CreateStamp(x: 0);
        var state = new StampLayerState([selected], selected);
        var controller = CreateController(state);

        // Act
        controller.DeleteSelected();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(state.Stamps, Is.Empty);
            Assert.That(controller.SelectedStamp, Is.Null);
            Assert.That(state.ChangedStamps, Is.EqualTo(new[] { selected }));
            Assert.That(state.InvalidateCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ResetSelectedSize_RestoresTemplateSizeAroundCenter()
    {
        // Arrange
        var selected = CreateStamp(x: 60, y: 70, width: 40, height: 20);
        var state = new StampLayerState([selected], selected);
        var controller = CreateController(state);

        // Act
        controller.ResetSelectedSize();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(selected.Width, Is.EqualTo(180));
            Assert.That(selected.Height, Is.EqualTo(100));
            Assert.That(selected.X, Is.EqualTo(0));
            Assert.That(selected.Y, Is.EqualTo(30));
            Assert.That(state.ChangedStamps, Is.EqualTo(new[] { selected }));
            Assert.That(state.InvalidateCount, Is.EqualTo(1));
        });
    }

    static StampLayerController CreateController(StampLayerState state) =>
        CreateControllerWithState(state);

    static StampLayerController CreateControllerWithState(StampLayerState state)
    {
        var controller = new StampLayerController(new StampTransformEditor())
        {
            Stamps = state.Stamps,
            SelectedStamp = state.SelectedStamp,
            MapSize = new Size(200, 200),
        };
        controller.StampChanged += (_, stamp) => state.ChangedStamps.Add(stamp);
        controller.Invalidated += (_, _) => state.InvalidateCount++;
        return controller;
    }

    static StampInstance CreateStamp(
        double x,
        double y = 0,
        double width = 10,
        double height = 10) =>
        new()
        {
            TemplateId = "blue-rectangle",
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };

    sealed class StampLayerState
    {
        public StampLayerState(List<StampInstance> stamps, StampInstance? selectedStamp)
        {
            Stamps = stamps;
            SelectedStamp = selectedStamp;
        }

        public List<StampInstance> Stamps { get; }

        public StampInstance? SelectedStamp { get; set; }

        public List<StampInstance> ChangedStamps { get; } = [];

        public int InvalidateCount { get; set; }
    }
}
