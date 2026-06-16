using System;
using System.Collections.Generic;

using Avalonia.Media;

using DMap.Models;

using ReactiveUI;

namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// ViewModel for the Fog tool settings panel.
/// </summary>
public sealed class FogToolSettingsViewModel : ToolSettingsViewModelBase
{
    const double DefaultOpacity = 0.5;

    /// <inheritdoc />
    public override string Name => "Fog Settings";

    /// <summary>All available fog types.</summary>
    public IReadOnlyList<FogType> FogTypes { get; } = Enum.GetValues<FogType>();

    /// <summary>The selected fog style.</summary>
    public FogType SelectedFogType
    {
        get;
        set
        {
            if (field == value)
                return;

            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsFogColorSelected));
        }
    } = FogType.Color;

    /// <summary>The flat fog colour used when <see cref="SelectedFogType"/> is <see cref="FogType.Color"/>.</summary>
    public Color Color
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Colors.Black;

    /// <summary>Opacity of the fog overlay rendered on the DM canvas.</summary>
    public double Opacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultOpacity;

    /// <summary><see langword="true"/> when the colour picker should be enabled for the selected fog type.</summary>
    public bool IsFogColorSelected => SelectedFogType == FogType.Color;
}
