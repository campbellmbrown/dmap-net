using System;

using ReactiveUI;

namespace DMap.ViewModels;

public class MainWindowViewModel : ViewModelBase, INavigator
{
    private ViewModelBase? _content;

    public ViewModelBase? Content
    {
        get => _content;
        private set => this.RaiseAndSetIfChanged(ref _content, value);
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        if (_content is IDisposable disposable)
            disposable.Dispose();

        Content = viewModel;
    }
}
