using System.Collections.Generic;

using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

public class ShapeTypeIconConverter : EnumSvgConverter<ShapeType>
{
    static readonly Dictionary<ShapeType, IImage> _icons = new()
    {
        [ShapeType.Rectangle] = SvgIconLoader.Load("rectangle-horizontal.svg"),
        [ShapeType.Square] = SvgIconLoader.Load("square.svg"),
        [ShapeType.Ellipse] = SvgIconLoader.Load("ellipse.svg"),
        [ShapeType.Circle] = SvgIconLoader.Load("circle.svg"),
    };

    protected override IReadOnlyDictionary<ShapeType, IImage> Icons => _icons;
}
