using System.Collections.Generic;

using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="ShapeType"/> enum value to its SVG icon.
/// Used in the DM toolbar to display an icon for each shape tool option.
/// </summary>
public class ShapeTypeIconConverter : EnumSvgConverter<ShapeType>
{
    static readonly Dictionary<ShapeType, IImage> _icons = new()
    {
        [ShapeType.Rectangle] = SvgIconLoader.Load("rectangle-horizontal.svg"),
        [ShapeType.Square] = SvgIconLoader.Load("square.svg"),
        [ShapeType.Ellipse] = SvgIconLoader.Load("ellipse.svg"),
        [ShapeType.Circle] = SvgIconLoader.Load("circle.svg"),
    };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<ShapeType, IImage> Icons => _icons;
}
