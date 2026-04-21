using System.Collections.Generic;

using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

public class BrushShapeIconConverter : EnumSvgConverter<BrushShape>
{
    static readonly Dictionary<BrushShape, IImage> _icons = new()
    {
        [BrushShape.Circle] = SvgIconLoader.Load("circle.svg"),
        [BrushShape.Square] = SvgIconLoader.Load("square.svg"),
        [BrushShape.Diamond] = SvgIconLoader.Load("diamond.svg"),
    };

    protected override IReadOnlyDictionary<BrushShape, IImage> Icons => _icons;
}
