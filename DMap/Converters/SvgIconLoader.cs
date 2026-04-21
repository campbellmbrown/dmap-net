using System;

using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace DMap.Converters;

internal static class SvgIconLoader
{
    static readonly Uri _iconBaseUri = new("avares://DMap/Assets/Icons/");

    public static IImage Load(string fileName)
    {
        var uri = new Uri(_iconBaseUri, fileName);
        return new SvgImage { Source = SvgSource.Load(uri.ToString(), null) };
    }
}
