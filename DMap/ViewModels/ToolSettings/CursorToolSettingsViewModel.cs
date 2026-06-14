using System;
using System.Collections.Generic;

using DMap.Models;

using ReactiveUI;

namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// ViewModel for the Cursor tool settings panel.
/// </summary>
public sealed class CursorToolSettingsViewModel : ToolSettingsViewModelBase
{
    const int DefaultSize = 64;

    /// <summary>All available cursor icon types.</summary>
    public IReadOnlyList<CursorType> CursorTypes { get; } = Enum.GetValues<CursorType>();

    /// <summary>The selected cursor icon type.</summary>
    public CursorType SelectedCursorType
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = CursorType.Crosshair;

    /// <summary>Cursor icon size in screen pixels.</summary>
    public int Size
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultSize;

    /// <summary><see langword="true"/> when the cursor should only appear while left click is held.</summary>
    public bool ShowOnlyWhilePressed
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}
