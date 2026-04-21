using Avalonia;

namespace DMap.Models;

public interface IFogCommand
{
    /// <summary>
    /// The bounding box of pixels modified by this operation, used to limit re-rendering and undo/redo storage
    /// to only the affected region.
    /// </summary>
    PixelRect DirtyRect { get; }

    void Undo(FogMask mask);
    void Redo(FogMask mask);
}
