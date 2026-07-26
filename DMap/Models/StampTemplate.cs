namespace DMap.Models;

/// <summary>
/// Describes a built-in stamp image that can be placed on the map.
/// </summary>
public sealed class StampTemplate
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string AssetPath { get; init; }

    public double DefaultWidth { get; init; }

    public double DefaultHeight { get; init; }
}
