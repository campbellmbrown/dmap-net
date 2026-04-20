using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;

using ReactiveUI;

namespace DMap.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly Func<SettingsViewModel> _createSettings;

    private Control? _content;
    public Control? Content
    {
        get => _content;
        set => this.RaiseAndSetIfChanged(ref _content, value);
    }

    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public Interaction<SettingsViewModel, Unit> ShowSettingsDialog { get; } = new();

    public MainWindowViewModel(Func<SettingsViewModel> createSettings)
    {
        _createSettings = createSettings;
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettingsAsync);
    }

    private async Task OpenSettingsAsync()
    {
        await ShowSettingsDialog.Handle(_createSettings());
    }
}
