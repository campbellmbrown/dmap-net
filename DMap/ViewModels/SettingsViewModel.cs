using System;
using System.Collections.Generic;
using System.Reactive;

using DMap.Models;
using DMap.Services;

using ReactiveUI;

namespace DMap.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;

    private AppTheme _selectedTheme;
    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTheme, value);
            _themeService.Apply(value);
            _settingsService.Settings.Theme = value;
            _settingsService.Save();
        }
    }

    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();

    public ReactiveCommand<Unit, Unit> CloseCommand { get; } = ReactiveCommand.Create(() => { });

    public SettingsViewModel(ISettingsService settingsService, IThemeService themeService)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _selectedTheme = settingsService.Settings.Theme;
    }
}
