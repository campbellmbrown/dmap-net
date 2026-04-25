using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DMap.Converters;

/// <summary>
/// Base class for Avalonia value converters that map an enum value to a pre-loaded SVG icon.
/// Subclasses provide a dictionary from enum value to <see cref="IImage"/> via <see cref="Icons"/>.
/// </summary>
/// <typeparam name="T">The enum type being converted.</typeparam>
public abstract class EnumSvgConverter<T> : IValueConverter where T : Enum
{
    /// <summary>
    /// Mapping from enum values to their corresponding SVG images. Must be populated by subclasses.
    /// </summary>
    protected abstract IReadOnlyDictionary<T, IImage> Icons { get; }

    /// <summary>
    /// Returns the <see cref="IImage"/> for the given enum <paramref name="value"/>,
    /// or <see langword="null"/> if the value is not in <see cref="Icons"/>.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is T key && Icons.TryGetValue(key, out var icon) ? icon : null;

    /// <summary>Not supported. Throws <see cref="NotSupportedException"/>.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
