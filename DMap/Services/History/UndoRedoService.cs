using System;
using System.Collections.Generic;

using DMap.Models;

namespace DMap.Services.History;

/// <summary>
/// Default implementation of <see cref="IUndoRedoService"/>. Stores up to
/// <c>MaxHistory</c> commands in a linked-list undo stack and a stack-based redo
/// buffer. Oldest entries are evicted when the undo stack overflows.
/// </summary>
public sealed class UndoRedoService : IUndoRedoService
{
    /// <summary>Maximum number of commands retained in the undo history.</summary>
    const int MaxHistory = 10;

    readonly LinkedList<IFogCommand> _undoStack = new();
    readonly Stack<IFogCommand> _redoStack = new();

    /// <inheritdoc/>
    public bool CanUndo => _undoStack.Count > 0;

    /// <inheritdoc/>
    public bool CanRedo => _redoStack.Count > 0;

    /// <inheritdoc/>
    public event EventHandler? StateChanged;

    /// <inheritdoc/>
    public void Push(IFogCommand command)
    {
        _undoStack.AddLast(command);
        if (_undoStack.Count > MaxHistory)
            _undoStack.RemoveFirst();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public IFogCommand? TakeUndo()
    {
        if (_undoStack.Count == 0)
            return null;

        var command = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        _redoStack.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return command;
    }

    /// <inheritdoc/>
    public IFogCommand? TakeRedo()
    {
        if (_redoStack.Count == 0)
            return null;

        var command = _redoStack.Pop();
        _undoStack.AddLast(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return command;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
