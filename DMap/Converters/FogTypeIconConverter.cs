using System.Collections.Generic;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="FogType"/> enum value to its SVG icon.
/// </summary>
public class FogTypeIconConverter : EnumSvgConverter<FogType>
{
    static readonly Dictionary<FogType, string> _icons = new()
    {
        [FogType.Bricks] = "brick-wall",
        [FogType.Cloud] = "cloud",
        [FogType.Color] = "palette",
        [FogType.Earth] = "earth",
        [FogType.Fog] = "cloud-fog",
        [FogType.Forest] = "trees",
        [FogType.Night] = "sparkles",
        [FogType.Ocean] = "waves-horizontal",
        [FogType.Stone] = "stone",
        [FogType.Void] = "wand-sparkles",
    };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<FogType, string> Icons => _icons;
}
