using System;

using DMap.Models;

namespace DMap.Services.History;

/// <summary>
/// Manages a bounded undo/redo history of fog-of-war commands.
/// Pushing a new command always clears the redo stack.
/// </summary>
public interface IUndoRedoService
{
    /// <summary>
    /// <see langword="true"/> when there is at least one command available to undo.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// <see langword="true"/> when there is at least one command available to redo.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Raised whenever <see cref="CanUndo"/> or <see cref="CanRedo"/> changes,
    /// allowing UI bindings to refresh command availability.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Adds <paramref name="command"/> to the undo stack and clears the redo stack.
    /// If the history limit is reached the oldest entry is discarded.
    /// </summary>
    void Push(IFogCommand command);

    /// <summary>
    /// Removes and returns the most recent command from the undo stack, moving it to
    /// the redo stack. Returns <see langword="null"/> when the undo stack is empty.
    /// </summary>
    IFogCommand? TakeUndo();

    /// <summary>
    /// Removes and returns the most recently undone command from the redo stack, moving
    /// it back onto the undo stack. Returns <see langword="null"/> when the redo stack is empty.
    /// </summary>
    IFogCommand? TakeRedo();

    /// <summary>Empties both the undo and redo stacks.</summary>
    void Clear();
}
