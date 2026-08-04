using System;
using System.Reactive;

using ReactiveUI;

namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// Settings for the player viewport rectangle and player-only display transform.
/// </summary>
public sealed class PlayerViewToolSettingsViewModel : ToolSettingsViewModelBase
{
    bool _isApplyingViewport;

    /// <inheritdoc />
    public override string Name => "Player View";

    public bool IsApplyingViewport => _isApplyingViewport;

    public double OffsetX
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, 0, MaxOffsetX));
    }

    public double OffsetY
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, 0, MaxOffsetY));
    }

    public double Width
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, MinSize, MaxWidth));
    }

    public double Height
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, MinSize, MaxHeight));
    }

    public double MinSize
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value < 0 ? 0 : value);
    }

    public double MaxOffsetX
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value < 0 ? 0 : value);
    }

    public double MaxOffsetY
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value < 0 ? 0 : value);
    }

    public double MaxWidth
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value < MinSize ? MinSize : value);
    }

    public double MaxHeight
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value < MinSize ? MinSize : value);
    }

    /// <summary>Screen-pixel padding to leave around the fitted player viewport.</summary>
    public double Padding
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value < 0 ? 0 : value);
    }

    /// <summary>Rotates the player view clockwise by one quarter turn.</summary>
    public ReactiveCommand<Unit, Unit> RotateClockwiseCommand { get; }

    public ReactiveCommand<Unit, Unit> ResetRotationCommand { get; }

    public PlayerViewToolSettingsViewModel(Action rotateClockwise, Action resetRotation)
    {
        RotateClockwiseCommand = ReactiveCommand.Create(rotateClockwise);
        ResetRotationCommand = ReactiveCommand.Create(resetRotation);
    }

    public void ApplyViewport(double offsetX, double offsetY, double width, double height, double padding, double mapWidth, double mapHeight)
    {
        _isApplyingViewport = true;
        try
        {
            MinSize = width > 0 && height > 0 ? 1 : 0;
            MaxWidth = mapWidth;
            MaxHeight = mapHeight;
            MaxOffsetX = mapWidth - width;
            MaxOffsetY = mapHeight - height;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Width = width;
            Height = height;
            Padding = padding;
        }
        finally
        {
            _isApplyingViewport = false;
        }
    }
}
