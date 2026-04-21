using System.Collections.Generic;

using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

public class ToolTypeIconConverter : EnumSvgConverter<ToolType>
{
    private static readonly Dictionary<ToolType, IImage> _icons = new()
    {
        [ToolType.Brush] = SvgIconLoader.Load("brush.svg"),
        [ToolType.Shape] = SvgIconLoader.Load("shapes.svg"),
        [ToolType.Pan] = SvgIconLoader.Load("hand.svg"),
    };

    protected override IReadOnlyDictionary<ToolType, IImage> Icons => _icons;
}
