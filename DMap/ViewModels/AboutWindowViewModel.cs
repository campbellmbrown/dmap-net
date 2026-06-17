namespace DMap.ViewModels;

/// <summary>
/// View model for the application information dialog.
/// </summary>
public class AboutWindowViewModel : ViewModelBase
{
    /// <summary>The semantic application version (X.Y.Z).</summary>
    public string Version { get; } = AppVersion.Version;

    /// <summary>The full source revision SHA embedded in the build metadata.</summary>
    public string GitSha { get; } = AppVersion.GitSha;
}
