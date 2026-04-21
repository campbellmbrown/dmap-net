using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

public class ToolTypeIconConverter : IValueConverter
{
    private static readonly Dictionary<ToolType, IImage> _icons = new()
    {
        [ToolType.Brush] = SvgIconLoader.Load("brush.svg"),
        [ToolType.Shape] = SvgIconLoader.Load("shapes.svg"),
        [ToolType.Pan] = SvgIconLoader.Load("hand.svg"),
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ToolType tool && _icons.TryGetValue(tool, out var icon) ? icon : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
