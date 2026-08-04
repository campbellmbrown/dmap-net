using Avalonia.Media;

using ReactiveUI;

namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// ViewModel for the Grid tool settings panel.
/// </summary>
public sealed class GridToolSettingsViewModel : ToolSettingsViewModelBase
{
    const byte DefaultAlpha = 166;

    /// <inheritdoc />
    public override string Name => "Grid Settings";

    /// <summary><see langword="true"/> when the grid overlay is visible.</summary>
    public bool IsVisible
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Grid square size in map pixels.</summary>
    public double SquareSize
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 70;

    /// <summary>Grid line width in rendered pixels.</summary>
    public double LineWidth
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 1;

    /// <summary>Grid line colour.</summary>
    public Color Color
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Color.FromArgb(DefaultAlpha, 255, 255, 255);

    /// <summary>Horizontal grid offset relative to the square size.</summary>
    public double OffsetX
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Vertical grid offset relative to the square size.</summary>
    public double OffsetY
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}
