using System;

using DMap.Controls;

namespace DMap;

internal static class IconSource
{
    const string GeneratedRootPath = "Assets/GeneratedIcons";
    const string AppResourceBaseUri = "avares://DMap";
    const string SvgExtension = ".svg";

    public static string NormalizeName(string iconName)
    {
        iconName = iconName.TrimStart('/');
        return iconName.EndsWith(SvgExtension, StringComparison.OrdinalIgnoreCase)
            ? iconName[..^SvgExtension.Length]
            : iconName;
    }

    public static Uri ForVariant(string iconName, IconVariant variant)
    {
        var variantName = variant.ToString().ToLowerInvariant();
        return new Uri($"{AppResourceBaseUri}/{GeneratedRootPath}/{variantName}/{NormalizeName(iconName)}{SvgExtension}");
    }
}
