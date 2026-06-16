namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// ViewModel representing the Pan tool, which has no settings panel content.
/// </summary>
public sealed class PanToolSettingsViewModel : ToolSettingsViewModelBase
{
    /// <inheritdoc />
    public override string Name => "Pan";

    /// <inheritdoc />
    public override bool HasPanelContent => false;
}
