using System;
using System.Globalization;

using Avalonia.Data.Converters;

using DMap.Models;

namespace DMap.Converters;

public class BrushShapeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not BrushShape shape)
            return null;

        return shape switch
        {
            BrushShape.Circle => "avares://DMap/Assets/Icons/circle.svg",
            BrushShape.Square => "avares://DMap/Assets/Icons/square.svg",
            BrushShape.Diamond => "avares://DMap/Assets/Icons/diamond.svg",
            _ => null,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
