using System;
using System.Collections.Generic;
using System.Linq;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="ShapeType"/> enum value to its SVG icon.
/// Used in the DM toolbar to display an icon for each shape tool option.
/// </summary>
public class ShapeTypeIconConverter : EnumSvgConverter<ShapeType>
{
    static readonly Dictionary<ShapeType, string> _icons = Enum.GetValues<ShapeType>().ToDictionary(
        shapeType => shapeType,
        shapeType => IconSource.NormalizeName(ShapeTypeMetadata.GetIconFileName(shapeType))
    );

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<ShapeType, string> Icons => _icons;
}
