using Avalonia;

namespace DMap.Models;

/// <summary>
/// Represents a reversible fog-of-war edit that can be undone and redone.
/// Implementations capture pixel state before and after an operation so the
/// affected region can be restored in either direction.
/// </summary>
public interface IFogCommand
{
    /// <summary>
    /// The bounding box of pixels modified by this operation, used to limit re-rendering and undo/redo storage
    /// to only the affected region.
    /// </summary>
    PixelRect DirtyRect { get; }

    /// <summary>
    /// Restores the fog mask to the state it was in before this command was applied.
    /// </summary>
    /// <param name="mask">The fog mask to write the before-state into.</param>
    void Undo(FogMask mask);

    /// <summary>
    /// Re-applies this command to the fog mask, restoring the after-state.
    /// </summary>
    /// <param name="mask">The fog mask to write the after-state into.</param>
    void Redo(FogMask mask);
}
