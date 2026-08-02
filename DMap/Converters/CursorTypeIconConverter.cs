using System.Collections.Generic;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="CursorType"/> enum value to its SVG icon.
/// </summary>
public class CursorTypeIconConverter : EnumSvgConverter<CursorType>
{
    static readonly Dictionary<CursorType, string> _icons = CreateIcons();

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<CursorType, string> Icons => _icons;

    static Dictionary<CursorType, string> CreateIcons()
    {
        var icons = new Dictionary<CursorType, string>();
        foreach (var cursorType in System.Enum.GetValues<CursorType>())
            icons[cursorType] = IconSource.NormalizeName(CursorTypeMetadata.GetIconFileName(cursorType));

        return icons;
    }
}
