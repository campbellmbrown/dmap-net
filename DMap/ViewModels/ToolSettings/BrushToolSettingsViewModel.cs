using System;
using System.Collections.Generic;

using DMap.Models;

using ReactiveUI;

namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// ViewModel for the Brush tool settings panel.
/// </summary>
public sealed class BrushToolSettingsViewModel : ToolSettingsViewModelBase
{
    const int DefaultDiameter = 50;
    const double DefaultSoftness = 0.3;
    const double DefaultOpacity = 1.0;

    /// <inheritdoc />
    public override string Name => "Brush Settings";

    /// <summary>All available brush shapes.</summary>
    public IReadOnlyList<BrushShape> BrushShapes { get; } = Enum.GetValues<BrushShape>();

    /// <summary>The selected brush shape.</summary>
    public BrushShape SelectedBrushShape
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Brush diameter in map pixels.</summary>
    public int Diameter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultDiameter;

    /// <summary>Brush edge softness in [0, 1].</summary>
    public double Softness
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultSoftness;

    /// <summary>Maximum brush opacity in [0, 1].</summary>
    public double Opacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultOpacity;
}
