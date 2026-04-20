using Avalonia.Controls;

using ReactiveUI;

namespace DMap.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private Control? _content;

    public Control? Content
    {
        get => _content;
        set => this.RaiseAndSetIfChanged(ref _content, value);
    }
}
