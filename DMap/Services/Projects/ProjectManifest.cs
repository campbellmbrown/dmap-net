using System.Collections.Generic;

using DMap.Models;
using DMap.Protocol;

namespace DMap.Services.Projects;

/// <summary>
/// JSON-serialized description of a project. Stored as <c>manifest.json</c> inside a <c>.dmap</c> archive,
/// alongside the embedded map image and fog mask.
/// </summary>
public sealed class ProjectManifest
{
    /// <summary>The project format version written by this build.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>Format version of the file; files newer than <see cref="CurrentFormatVersion"/> are rejected.</summary>
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>Version of DMap that saved the file, for diagnostics.</summary>
    public string AppVersion { get; init; } = string.Empty;

    /// <summary>Map and fog mask width in pixels.</summary>
    public int MapWidth { get; init; }

    /// <summary>Map and fog mask height in pixels.</summary>
    public int MapHeight { get; init; }

    /// <summary>Fog type, colour and texture seed.</summary>
    public required FogAppearancePayload Fog { get; init; }

    /// <summary>Opacity of the fog overlay on the DM canvas.</summary>
    public double FogOpacity { get; init; }

    /// <summary>Grid overlay settings.</summary>
    public required GridSettingsPayload Grid { get; init; }

    /// <summary>Player viewport rectangle, rotation, padding and pixel sharpness.</summary>
    public required ViewportPayload PlayerViewport { get; init; }

    /// <summary>Placed stamps in z-order (first is bottom-most).</summary>
    public IReadOnlyList<StampInstance> Stamps { get; init; } = [];
}
