using DMap.Models;

namespace DMap.Controls.MapCanvas;

/// <summary>Result of pressing the stamp tool on the map.</summary>
public sealed class StampPointerPressResult
{
    public StampPointerPressResult(StampInstance? hitStamp, StampLayerEditResult? edit)
    {
        HitStamp = hitStamp;
        Edit = edit;
    }

    public StampInstance? HitStamp { get; }

    public StampLayerEditResult? Edit { get; }
}
