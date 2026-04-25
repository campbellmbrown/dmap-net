using System.Collections.Generic;

using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="BrushShape"/> enum value to its SVG icon.
/// Used in the DM toolbar to display an icon for each brush shape option.
/// </summary>
public class BrushShapeIconConverter : EnumSvgConverter<BrushShape>
{
    static readonly Dictionary<BrushShape, IImage> _icons = new()
    {
        [BrushShape.Circle] = SvgIconLoader.Load("circle.svg"),
        [BrushShape.Square] = SvgIconLoader.Load("square.svg"),
        [BrushShape.Diamond] = SvgIconLoader.Load("diamond.svg"),
    };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<BrushShape, IImage> Icons => _icons;
}
