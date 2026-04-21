using System;
using System.Collections.Generic;

using DMap.Models;

namespace DMap.Services.History;

public sealed class UndoRedoService : IUndoRedoService
{
    private const int MaxHistory = 10;

    private readonly LinkedList<IFogCommand> _undoStack = new();
    private readonly Stack<IFogCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event EventHandler? StateChanged;

    public void Push(IFogCommand command)
    {
        _undoStack.AddLast(command);
        if (_undoStack.Count > MaxHistory)
            _undoStack.RemoveFirst();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

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

    public IFogCommand? TakeRedo()
    {
        if (_redoStack.Count == 0)
            return null;

        var command = _redoStack.Pop();
        _undoStack.AddLast(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return command;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
