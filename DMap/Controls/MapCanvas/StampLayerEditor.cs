using System;
using System.Collections.Generic;

using Avalonia;

using DMap.Models;

namespace DMap.Controls.MapCanvas;

/// <summary>Owns stamp layer selection and collection mutations.</summary>
public sealed class StampLayerEditor
{
    readonly StampEditor _stampEditor;

    public StampLayerEditor(StampEditor stampEditor)
    {
        _stampEditor = stampEditor;
    }

    public StampPointerPressResult HandlePointerPress(
        IList<StampInstance>? stamps,
        StampInstance? selectedStamp,
        string? selectedTemplateId,
        Point mapPosition,
        Size? mapSize)
    {
        var hit = _stampEditor.HitTest(stamps, mapPosition);
        if (hit is not null)
        {
            return new StampPointerPressResult(
                hit,
                new StampLayerEditResult(
                    selectedStamp: hit,
                    selectionChanged: selectedStamp != hit,
                    changedStamp: null,
                    shouldInvalidate: selectedStamp != hit));
        }

        if (selectedStamp is not null)
        {
            return new StampPointerPressResult(
                null,
                new StampLayerEditResult(
                    selectedStamp: null,
                    selectionChanged: true,
                    changedStamp: null,
                    shouldInvalidate: true));
        }

        var placed = PlaceStamp(stamps, selectedTemplateId, mapPosition, mapSize);
        return new StampPointerPressResult(
            null,
            placed);
    }

    public StampLayerEditResult? SelectAt(
        IList<StampInstance>? stamps,
        StampInstance? selectedStamp,
        Point mapPosition)
    {
        var hit = _stampEditor.HitTest(stamps, mapPosition);
        if (hit is null)
            return null;

        return new StampLayerEditResult(
            selectedStamp: hit,
            selectionChanged: selectedStamp != hit,
            changedStamp: null,
            shouldInvalidate: selectedStamp != hit);
    }

    public StampLayerEditResult? ClearMissingSelection(IList<StampInstance>? stamps, StampInstance? selectedStamp)
    {
        if (selectedStamp is null || stamps is null || stamps.Contains(selectedStamp))
            return null;

        return new StampLayerEditResult(
            selectedStamp: null,
            selectionChanged: true,
            changedStamp: null,
            shouldInvalidate: true);
    }

    public StampLayerEditResult? DeleteSelected(IList<StampInstance>? stamps, StampInstance? selectedStamp)
    {
        if (selectedStamp is null || stamps is null)
            return null;

        stamps.Remove(selectedStamp);
        return new StampLayerEditResult(
            selectedStamp: null,
            selectionChanged: true,
            changedStamp: selectedStamp,
            shouldInvalidate: true);
    }

    public StampLayerEditResult? DuplicateSelected(
        IList<StampInstance>? stamps,
        StampInstance? selectedStamp,
        Size? mapSize)
    {
        if (selectedStamp is null || stamps is null)
            return null;

        var rect = _stampEditor.ClampRect(
            new Rect(selectedStamp.X + 16, selectedStamp.Y + 16, selectedStamp.Width, selectedStamp.Height),
            mapSize);
        var duplicate = new StampInstance
        {
            TemplateId = selectedStamp.TemplateId,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            RotationDegrees = selectedStamp.RotationDegrees,
        };

        stamps.Add(duplicate);
        return new StampLayerEditResult(
            selectedStamp: duplicate,
            selectionChanged: true,
            changedStamp: duplicate,
            shouldInvalidate: true);
    }

    public StampLayerEditResult? ReorderSelectedBy(
        IList<StampInstance>? stamps,
        StampInstance? selectedStamp,
        int delta)
    {
        if (selectedStamp is null || stamps is null)
            return null;

        var index = stamps.IndexOf(selectedStamp);
        if (index < 0)
            return null;

        return ReorderSelectedTo(stamps, selectedStamp, Math.Clamp(index + delta, 0, stamps.Count - 1));
    }

    public StampLayerEditResult? ReorderSelectedToFront(IList<StampInstance>? stamps, StampInstance? selectedStamp)
    {
        if (stamps is not { Count: > 0 })
            return null;

        return ReorderSelectedTo(stamps, selectedStamp, stamps.Count - 1);
    }

    public StampLayerEditResult? ReorderSelectedToBack(IList<StampInstance>? stamps, StampInstance? selectedStamp) =>
        ReorderSelectedTo(stamps, selectedStamp, 0);

    StampLayerEditResult? PlaceStamp(
        IList<StampInstance>? stamps,
        string? selectedTemplateId,
        Point mapPosition,
        Size? mapSize)
    {
        if (stamps is null)
            return null;

        var templateId = selectedTemplateId ?? StampCatalog.Templates[0].Id;
        var template = StampCatalog.Find(templateId) ?? StampCatalog.Templates[0];
        var rect = _stampEditor.ClampRect(
            new Rect(
                mapPosition.X - template.DefaultWidth / 2,
                mapPosition.Y - template.DefaultHeight / 2,
                template.DefaultWidth,
                template.DefaultHeight),
            mapSize);

        var stamp = new StampInstance
        {
            TemplateId = template.Id,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            RotationDegrees = 0,
        };

        stamps.Add(stamp);
        return new StampLayerEditResult(
            selectedStamp: stamp,
            selectionChanged: true,
            changedStamp: stamp,
            shouldInvalidate: true);
    }

    StampLayerEditResult? ReorderSelectedTo(
        IList<StampInstance>? stamps,
        StampInstance? selectedStamp,
        int nextIndex)
    {
        if (selectedStamp is null || stamps is null)
            return null;

        var index = stamps.IndexOf(selectedStamp);
        if (index < 0)
            return null;

        nextIndex = Math.Clamp(nextIndex, 0, stamps.Count - 1);
        if (nextIndex == index)
            return null;

        stamps.RemoveAt(index);
        stamps.Insert(nextIndex, selectedStamp);
        return new StampLayerEditResult(
            selectedStamp: selectedStamp,
            selectionChanged: true,
            changedStamp: selectedStamp,
            shouldInvalidate: true);
    }
}
