using System;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using DMap.Models;

namespace DMap.Converters;

/// <summary>
/// Loads the PNG preview image for a stamp template.
/// </summary>
public sealed class StampTemplateImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var assetPath = value switch
        {
            StampTemplate template => template.AssetPath,
            string path => path,
            _ => null,
        };

        return assetPath is null
            ? null
            : new Bitmap(AssetLoader.Open(new Uri(assetPath)));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
