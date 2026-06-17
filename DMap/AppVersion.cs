using System.Reflection;

namespace DMap;

/// <summary>
/// Provides the application's version metadata from the DMap assembly.
/// </summary>
internal static class AppVersion
{
    static AppVersion()
    {
        var assembly = typeof(AppVersion).Assembly;

        Version = assembly.GetName().Version!.ToString(3);

        var informationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!;
        InformationalVersion = informationalVersionAttribute.InformationalVersion;

        var informationalVersionSeparatorIndex = InformationalVersion.IndexOf('+');
        GitSha = InformationalVersion[(informationalVersionSeparatorIndex + 1)..];
    }

    /// <summary>
    /// The semantic version of the application (X.Y.Z).
    /// </summary>
    public static string Version { get; }

    /// <summary>
    /// The semantic version with the full Git SHA (X.Y.Z+sha).
    /// </summary>
    public static string InformationalVersion { get; }

    /// <summary>
    /// The full Git SHA.
    /// </summary>
    public static string GitSha { get; }
}
