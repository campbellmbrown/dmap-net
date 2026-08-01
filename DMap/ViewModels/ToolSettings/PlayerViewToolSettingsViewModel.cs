using System;
using System.Reactive;

using ReactiveUI;

namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// Settings for the player viewport rectangle and player-only display transform.
/// </summary>
public sealed class PlayerViewToolSettingsViewModel : ToolSettingsViewModelBase
{
    /// <inheritdoc />
    public override string Name => "Player View";

    /// <summary>Screen-pixel padding to leave around the fitted player viewport.</summary>
    public double Padding
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value < 0 ? 0 : value);
    }

    /// <summary>Rotates the player view clockwise by one quarter turn.</summary>
    public ReactiveCommand<Unit, Unit> RotateClockwiseCommand { get; }

    public PlayerViewToolSettingsViewModel(Action rotateClockwise)
    {
        RotateClockwiseCommand = ReactiveCommand.Create(rotateClockwise);
    }
}
