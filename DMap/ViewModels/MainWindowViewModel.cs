using System;
using ReactiveUI;

namespace DMap.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _content;

    public ViewModelBase Content
    {
        get => _content;
        set => this.RaiseAndSetIfChanged(ref _content, value);
    }

    public MainWindowViewModel()
    {
        _content = new StartViewModel(this);
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        if (_content is IDisposable disposable)
            disposable.Dispose();

        Content = viewModel;
    }
}
