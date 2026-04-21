using System;
using System.Globalization;

using Avalonia.Data.Converters;

using DMap.Models;

namespace DMap.Converters;

public class ShapeTypeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ShapeType shape)
            return null;

        return shape switch
        {
            ShapeType.Rectangle => "avares://DMap/Assets/Icons/rectangle-horizontal.svg",
            ShapeType.Square => "avares://DMap/Assets/Icons/square.svg",
            ShapeType.Ellipse => "avares://DMap/Assets/Icons/ellipse.svg",
            ShapeType.Circle => "avares://DMap/Assets/Icons/circle.svg",
            _ => null,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
