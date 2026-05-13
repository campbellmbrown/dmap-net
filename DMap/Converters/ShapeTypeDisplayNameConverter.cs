using System;
using System.Globalization;

using Avalonia.Data.Converters;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="ShapeType"/> enum value to a display name.
/// </summary>
public class ShapeTypeDisplayNameConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ShapeType shapeType ? ShapeTypeMetadata.GetDisplayName(shapeType) : null;

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
