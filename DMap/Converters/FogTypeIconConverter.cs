using System.Collections.Generic;

using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="FogType"/> enum value to its SVG icon.
/// </summary>
public class FogTypeIconConverter : EnumSvgConverter<FogType>
{
    static readonly Dictionary<FogType, IImage> _icons = new()
    {
        [FogType.Bricks] = SvgIconLoader.Load("brick-wall.svg"),
        [FogType.Cloud] = SvgIconLoader.Load("cloud.svg"),
        [FogType.Color] = SvgIconLoader.Load("palette.svg"),
        [FogType.Earth] = SvgIconLoader.Load("earth.svg"),
        [FogType.Fog] = SvgIconLoader.Load("cloud-fog.svg"),
        [FogType.Forest] = SvgIconLoader.Load("trees.svg"),
        [FogType.Night] = SvgIconLoader.Load("sparkles.svg"),
        [FogType.Ocean] = SvgIconLoader.Load("waves-horizontal.svg"),
        [FogType.Stone] = SvgIconLoader.Load("stone.svg"),
        [FogType.Void] = SvgIconLoader.Load("wand-sparkles.svg"),

    };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<FogType, IImage> Icons => _icons;
}
