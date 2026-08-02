using System.Collections.Generic;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="BrushShape"/> enum value to its SVG icon.
/// Used in the DM toolbar to display an icon for each brush shape option.
/// </summary>
public class BrushShapeIconConverter : EnumSvgConverter<BrushShape>
{
    static readonly Dictionary<BrushShape, string> _icons = new()
    {
        [BrushShape.Circle] = "circle",
        [BrushShape.Square] = "square",
        [BrushShape.Diamond] = "diamond",
    };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<BrushShape, string> Icons => _icons;
}
