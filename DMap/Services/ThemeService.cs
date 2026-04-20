using System;

using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

using DMap.Models;

namespace DMap.Services;

public sealed class ThemeService : IThemeService
{
    private ResourceInclude? _currentInclude;

    public void Apply(AppTheme theme)
    {
        var app = Application.Current!;

        if (_currentInclude is not null)
            app.Resources.MergedDictionaries.Remove(_currentInclude);

        app.RequestedThemeVariant = theme is AppTheme.ParchmentLight
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        var sourceUri = theme switch
        {
            AppTheme.ParchmentLight => new Uri("avares://DMap/Themes/ParchmentLightTheme.axaml"),
            AppTheme.Abyss => new Uri("avares://DMap/Themes/AbyssTheme.axaml"),
            _ => new Uri("avares://DMap/Themes/ForestTheme.axaml"),
        };

        _currentInclude = new ResourceInclude(sourceUri) { Source = sourceUri };
        app.Resources.MergedDictionaries.Add(_currentInclude);
    }
}
