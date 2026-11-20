namespace DMap.ViewModels;

/// <summary>
/// The user's answer when asked what to do with unsaved project changes.
/// </summary>
public enum UnsavedChangesChoice
{
    /// <summary>Keep the current project open.</summary>
    Cancel,

    /// <summary>Save the project before continuing.</summary>
    Save,

    /// <summary>Continue without saving.</summary>
    Discard,
}
