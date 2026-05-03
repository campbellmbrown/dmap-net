using System;
using System.Globalization;

using Avalonia.Data.Converters;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Avalonia value converter that maps a <see cref="CursorType"/> enum value to a display name.
/// </summary>
public class CursorTypeDisplayNameConverter : IValueConverter
{
    /// <summary>Returns the human-readable name for the given cursor type.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CursorType cursorType ? CursorTypeMetadata.GetDisplayName(cursorType) : null;

    /// <summary>Not supported. Throws <see cref="NotSupportedException"/>.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
