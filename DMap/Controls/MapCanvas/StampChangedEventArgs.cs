using System;

using DMap.Models;

namespace DMap.Controls.MapCanvas;

/// <summary>
/// Event arguments for stamp-layer mutations produced by the canvas.
/// </summary>
public class StampChangedEventArgs : EventArgs
{
    public StampChangedEventArgs(StampInstance stamp)
    {
        Stamp = stamp;
    }

    public StampInstance Stamp { get; }
}
