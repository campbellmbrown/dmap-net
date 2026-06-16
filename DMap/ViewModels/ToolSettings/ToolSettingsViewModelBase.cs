namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// Base type for DM tool settings panels.
/// </summary>
public abstract class ToolSettingsViewModelBase : ViewModelBase
{
    /// <summary>The display name shown in the DM tool settings panel header.</summary>
    public abstract string Name { get; }

    /// <summary><see langword="true"/> when the tool exposes a settings panel body.</summary>
    public virtual bool HasPanelContent => true;
}
