using System;
using System.Collections.Generic;

using DMap.Models;

using ReactiveUI;

namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// ViewModel for the Shape tool settings panel.
/// </summary>
public sealed class ShapeToolSettingsViewModel : ToolSettingsViewModelBase
{
    const double DefaultSoftness = 0.0;
    const double DefaultOpacity = 1.0;
    const int DefaultCornerRadius = 0;

    /// <summary>All available shape types.</summary>
    public IReadOnlyList<ShapeType> ShapeTypes { get; } = Enum.GetValues<ShapeType>();

    /// <summary>The selected shape type.</summary>
    public ShapeType SelectedShapeType
    {
        get;
        set
        {
            if (field == value)
                return;

            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsCornerRadiusVisible));
        }
    }

    /// <summary><see langword="true"/> when the selected shape supports a configurable corner radius.</summary>
    public bool IsCornerRadiusVisible => ShapeTypeMetadata.SupportsCornerRadius(SelectedShapeType);

    /// <summary>Shape edge softness in [0, 1].</summary>
    public double Softness
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultSoftness;

    /// <summary>Maximum shape fill opacity in [0, 1].</summary>
    public double Opacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultOpacity;

    /// <summary>Corner radius for rectangle and square shape variants.</summary>
    public int CornerRadius
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultCornerRadius;
}
