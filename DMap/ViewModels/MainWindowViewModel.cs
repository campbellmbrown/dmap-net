using Avalonia.Controls;

using ReactiveUI;

namespace DMap.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public Control? Content
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}
