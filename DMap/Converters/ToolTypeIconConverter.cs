using System.Collections.Generic;

using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="ToolType"/> enum value to its SVG icon.
/// Used in the DM toolbar to display an icon for each editing tool.
/// </summary>
public class ToolTypeIconConverter : EnumSvgConverter<ToolType>
{
    static readonly Dictionary<ToolType, IImage> _icons = new()
    {
        [ToolType.Brush] = SvgIconLoader.Load("brush.svg"),
        [ToolType.Shape] = SvgIconLoader.Load("shapes.svg"),
        [ToolType.Pan] = SvgIconLoader.Load("hand.svg"),
    };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<ToolType, IImage> Icons => _icons;
}
