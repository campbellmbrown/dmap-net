using DMap.Models;

namespace DMap.Controls.MapCanvas;

/// <summary>Result of a stamp layer edit that the owning control should reflect in bindings and events.</summary>
public sealed class StampLayerEditResult
{
    public StampLayerEditResult(
        StampInstance? selectedStamp,
        bool selectionChanged,
        StampInstance? changedStamp,
        bool shouldInvalidate)
    {
        SelectedStamp = selectedStamp;
        SelectionChanged = selectionChanged;
        ChangedStamp = changedStamp;
        ShouldInvalidate = shouldInvalidate;
    }

    public StampInstance? SelectedStamp { get; }

    public bool SelectionChanged { get; }

    public StampInstance? ChangedStamp { get; }

    public bool ShouldInvalidate { get; }
}
