using System;

using DMap.Models;

namespace DMap.Services.History;

public interface IUndoRedoService
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    event EventHandler? StateChanged;

    void Push(IFogCommand command);
    IFogCommand? TakeUndo();
    IFogCommand? TakeRedo();
    void Clear();
}
