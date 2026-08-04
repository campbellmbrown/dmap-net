using System;
using System.Collections.Generic;

using Avalonia;

using DMap.Models;

namespace DMap.Controls.MapCanvas.Stamp;

public sealed class StampLayerController
{
    readonly StampTransformEditor _transformEditor;

    public StampLayerController(StampTransformEditor transformEditor)
    {
        _transformEditor = transformEditor;
    }

    public IList<StampInstance>? Stamps { get; set; }

    public StampInstance? SelectedStamp { get; set; }

    public string? SelectedTemplateId { get; set; }

    public Size? MapSize { get; set; }

    public event EventHandler<StampInstance>? StampChanged;

    public event EventHandler? Invalidated;

    public StampInstance? HandlePointerPress(Point mapPosition)
    {
        var hit = _transformEditor.HitTest(Stamps, mapPosition);
        if (hit is not null)
        {
            Select(hit);
            return hit;
        }

        if (SelectedStamp is not null)
        {
            ClearSelection();
            return null;
        }

        PlaceStamp(mapPosition);
        return null;
    }

    public bool SelectAt(Point mapPosition)
    {
        var hit = _transformEditor.HitTest(Stamps, mapPosition);
        if (hit is null)
            return false;

        Select(hit);
        return true;
    }

    public void ClearMissingSelection()
    {
        if (SelectedStamp is null || Stamps is null || Stamps.Contains(SelectedStamp))
            return;

        SelectedStamp = null;
        Invalidate();
    }

    public void ClearSelection()
    {
        if (SelectedStamp is null)
            return;

        SelectedStamp = null;
        Invalidate();
    }

    public void DeleteSelected()
    {
        if (SelectedStamp is null || Stamps is null)
            return;

        var removed = SelectedStamp;
        Stamps.Remove(removed);
        SelectedStamp = null;
        NotifyStampChanged(removed);
        Invalidate();
    }

    public void DuplicateSelected()
    {
        if (SelectedStamp is null || Stamps is null)
            return;

        var rect = _transformEditor.ClampRect(
            new Rect(SelectedStamp.X + 16, SelectedStamp.Y + 16, SelectedStamp.Width, SelectedStamp.Height),
            MapSize);
        var duplicate = new StampInstance
        {
            TemplateId = SelectedStamp.TemplateId,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            RotationDegrees = SelectedStamp.RotationDegrees,
        };

        Stamps.Add(duplicate);
        SelectedStamp = duplicate;
        NotifyStampChanged(duplicate);
        Invalidate();
    }

    public void ReorderSelectedBy(int delta)
    {
        if (SelectedStamp is null || Stamps is null)
            return;

        var index = Stamps.IndexOf(SelectedStamp);
        if (index < 0)
            return;

        ReorderSelectedTo(Math.Clamp(index + delta, 0, Stamps.Count - 1));
    }

    public void ReorderSelectedToFront()
    {
        if (Stamps is { Count: > 0 })
            ReorderSelectedTo(Stamps.Count - 1);
    }

    public void ReorderSelectedToBack()
    {
        ReorderSelectedTo(0);
    }

    public void NotifySelectedStampChanged()
    {
        if (SelectedStamp is { } selectedStamp)
            NotifyStampChanged(selectedStamp);
    }

    public void ResetSelectedRotation()
    {
        if (SelectedStamp is null)
            return;

        SelectedStamp.RotationDegrees = 0;
        NotifyStampChanged(SelectedStamp);
        Invalidate();
    }

    public void ResetSelectedSize()
    {
        if (SelectedStamp is null)
            return;

        var template = StampCatalog.Find(SelectedStamp.TemplateId);
        if (template is null)
            return;

        var center = _transformEditor.GetCenter(SelectedStamp);
        var rect = _transformEditor.ClampRect(
            new Rect(
                center.X - template.DefaultWidth / 2.0,
                center.Y - template.DefaultHeight / 2.0,
                template.DefaultWidth,
                template.DefaultHeight),
            MapSize);

        SelectedStamp.X = rect.X;
        SelectedStamp.Y = rect.Y;
        SelectedStamp.Width = rect.Width;
        SelectedStamp.Height = rect.Height;
        NotifyStampChanged(SelectedStamp);
        Invalidate();
    }

    void Select(StampInstance stamp)
    {
        if (SelectedStamp == stamp)
            return;

        SelectedStamp = stamp;
        Invalidate();
    }

    void PlaceStamp(Point mapPosition)
    {
        if (Stamps is null)
            return;

        var templateId = SelectedTemplateId ?? StampCatalog.Templates[0].Id;
        var template = StampCatalog.Find(templateId) ?? StampCatalog.Templates[0];
        var rect = _transformEditor.ClampRect(
            new Rect(
                mapPosition.X - template.DefaultWidth / 2,
                mapPosition.Y - template.DefaultHeight / 2,
                template.DefaultWidth,
                template.DefaultHeight),
            MapSize);

        var stamp = new StampInstance
        {
            TemplateId = template.Id,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            RotationDegrees = 0,
        };

        Stamps.Add(stamp);
        SelectedStamp = stamp;
        NotifyStampChanged(stamp);
        Invalidate();
    }

    void ReorderSelectedTo(int nextIndex)
    {
        if (SelectedStamp is null || Stamps is null)
            return;

        var index = Stamps.IndexOf(SelectedStamp);
        if (index < 0)
            return;

        nextIndex = Math.Clamp(nextIndex, 0, Stamps.Count - 1);
        if (nextIndex == index)
            return;

        var stamp = SelectedStamp;
        Stamps.RemoveAt(index);
        Stamps.Insert(nextIndex, stamp);
        SelectedStamp = stamp;
        NotifyStampChanged(stamp);
        Invalidate();
    }

    void NotifyStampChanged(StampInstance stamp)
    {
        StampChanged?.Invoke(this, stamp);
    }

    void Invalidate()
    {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }
}
