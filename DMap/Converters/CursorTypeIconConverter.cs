using System.Collections.Generic;

using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="CursorType"/> enum value to its SVG icon.
/// </summary>
public class CursorTypeIconConverter : EnumSvgConverter<CursorType>
{
    static readonly Dictionary<CursorType, IImage> _icons = CreateIcons();

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<CursorType, IImage> Icons => _icons;

    static Dictionary<CursorType, IImage> CreateIcons()
    {
        var icons = new Dictionary<CursorType, IImage>();
        foreach (var cursorType in System.Enum.GetValues<CursorType>())
            icons[cursorType] = SvgIconLoader.Load(CursorTypeMetadata.GetIconFileName(cursorType));

        return icons;
    }
}
