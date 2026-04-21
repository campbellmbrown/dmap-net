using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

using DMap.Models;

namespace DMap.Converters;

public class ShapeTypeIconConverter : IValueConverter
{
    private static readonly Dictionary<ShapeType, IImage> _icons = new()
    {
        [ShapeType.Rectangle] = SvgIconLoader.Load("rectangle-horizontal.svg"),
        [ShapeType.Square] = SvgIconLoader.Load("square.svg"),
        [ShapeType.Ellipse] = SvgIconLoader.Load("ellipse.svg"),
        [ShapeType.Circle] = SvgIconLoader.Load("circle.svg"),
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ShapeType shape && _icons.TryGetValue(shape, out var icon) ? icon : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
