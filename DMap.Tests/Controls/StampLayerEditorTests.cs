using System.Collections.Generic;

using Avalonia;

using DMap.Controls.MapCanvas;
using DMap.Models;

using NUnit.Framework;

namespace DMap.Tests.Controls;

public class StampLayerEditorTests
{
    readonly StampEditor _stampEditor = new();

    [Test]
    public void HandlePointerPress_SelectsTopmostExistingStampForDrag()
    {
        // Arrange
        var editor = CreateEditor();
        var bottom = CreateStamp(x: 0, y: 0, width: 20, height: 20);
        var top = CreateStamp(x: 5, y: 5, width: 20, height: 20);
        List<StampInstance> stamps = [bottom, top];

        // Act
        var result = editor.HandlePointerPress(stamps, selectedStamp: null, selectedTemplateId: null, new Point(10, 10), new Size(200, 200));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.HitStamp, Is.SameAs(top));
            Assert.That(result.Edit, Is.Not.Null);
            Assert.That(result.Edit!.SelectedStamp, Is.SameAs(top));
            Assert.That(result.Edit.ChangedStamp, Is.Null);
        });
    }

    [Test]
    public void HandlePointerPress_ClearsSelectionWhenClickingEmptyMapWithSelection()
    {
        // Arrange
        var editor = CreateEditor();
        var selected = CreateStamp(x: 0, y: 0, width: 20, height: 20);
        List<StampInstance> stamps = [selected];

        // Act
        var result = editor.HandlePointerPress(stamps, selected, selectedTemplateId: null, new Point(100, 100), new Size(200, 200));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.HitStamp, Is.Null);
            Assert.That(result.Edit, Is.Not.Null);
            Assert.That(result.Edit!.SelectedStamp, Is.Null);
            Assert.That(result.Edit.SelectionChanged, Is.True);
        });
    }

    [Test]
    public void HandlePointerPress_PlacesStampWhenClickingEmptyMapWithoutSelection()
    {
        // Arrange
        var editor = CreateEditor();
        List<StampInstance> stamps = [];

        // Act
        var result = editor.HandlePointerPress(stamps, selectedStamp: null, selectedTemplateId: null, new Point(100, 100), new Size(500, 500));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.HitStamp, Is.Null);
            Assert.That(result.Edit, Is.Not.Null);
            Assert.That(result.Edit!.ChangedStamp, Is.Not.Null);
            Assert.That(result.Edit.SelectedStamp, Is.SameAs(result.Edit.ChangedStamp));
            Assert.That(stamps, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void DuplicateSelected_AddsOffsetCopyAndSelectsIt()
    {
        // Arrange
        var editor = CreateEditor();
        var selected = CreateStamp(x: 10, y: 20, width: 30, height: 40, rotationDegrees: 45);
        List<StampInstance> stamps = [selected];

        // Act
        var result = editor.DuplicateSelected(stamps, selected, new Size(200, 200));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(stamps, Has.Count.EqualTo(2));
            Assert.That(result!.SelectedStamp, Is.SameAs(stamps[1]));
            Assert.That(stamps[1].X, Is.EqualTo(26));
            Assert.That(stamps[1].Y, Is.EqualTo(36));
            Assert.That(stamps[1].RotationDegrees, Is.EqualTo(45));
        });
    }

    [Test]
    public void ReorderSelectedBy_MovesStampWithinLayer()
    {
        // Arrange
        var editor = CreateEditor();
        var first = CreateStamp(x: 0, y: 0, width: 10, height: 10);
        var second = CreateStamp(x: 20, y: 0, width: 10, height: 10);
        var third = CreateStamp(x: 40, y: 0, width: 10, height: 10);
        List<StampInstance> stamps = [first, second, third];

        // Act
        var result = editor.ReorderSelectedBy(stamps, second, 1);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(stamps, Is.EqualTo(new[] { first, third, second }));
            Assert.That(result!.ChangedStamp, Is.SameAs(second));
        });
    }

    [Test]
    public void DeleteSelected_RemovesStampAndClearsSelection()
    {
        // Arrange
        var editor = CreateEditor();
        var selected = CreateStamp(x: 0, y: 0, width: 10, height: 10);
        List<StampInstance> stamps = [selected];

        // Act
        var result = editor.DeleteSelected(stamps, selected);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(stamps, Is.Empty);
            Assert.That(result!.SelectedStamp, Is.Null);
            Assert.That(result.ChangedStamp, Is.SameAs(selected));
        });
    }

    StampLayerEditor CreateEditor() => new(_stampEditor);

    static StampInstance CreateStamp(
        double x,
        double y,
        double width,
        double height,
        double rotationDegrees = 0) =>
        new()
        {
            TemplateId = "blue-rectangle",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            RotationDegrees = rotationDegrees,
        };
}
