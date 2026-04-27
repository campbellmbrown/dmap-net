using System;

using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace DMap.Converters;

/// <summary>
/// Loads SVG icons from the embedded application assets at <c>avares://DMap/Assets/Icons/</c>.
/// </summary>
internal static class SvgIconLoader
{
    static readonly Uri _iconBaseUri = new("avares://DMap/Assets/Icons/");

    /// <summary>
    /// Loads the SVG file with the given <paramref name="fileName"/> from the icon assets directory
    /// and returns it as an <see cref="IImage"/> usable in Avalonia controls.
    /// </summary>
    /// <param name="fileName">File name relative to the icon assets directory (e.g. "circle.svg").</param>
    public static IImage Load(string fileName)
    {
        var uri = new Uri(_iconBaseUri, fileName);
        return new SvgImage { Source = SvgSource.Load(uri.ToString(), null) };
    }
}
