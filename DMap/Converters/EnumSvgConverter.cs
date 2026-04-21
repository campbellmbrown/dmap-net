using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DMap.Converters;

public abstract class EnumSvgConverter<T> : IValueConverter where T : Enum
{
    protected abstract IReadOnlyDictionary<T, IImage> Icons { get; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is T key && Icons.TryGetValue(key, out var icon) ? icon : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
