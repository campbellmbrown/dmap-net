using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using DMap.Controls;
using DMap.ViewModels;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace DMap.Views;

public partial class DmView : ReactiveUserControl<DmViewModel>
{
    private CompositeDisposable? _activationDisposables;

    public DmView()
    {
        InitializeComponent();

        var canvas = this.FindControl<MapCanvas>("MapCanvas")!;

        canvas.BrushStrokeApplied += (_, e) =>
        {
            ViewModel?.OnBrushStroke(e.MapX1, e.MapY1, e.MapX2, e.MapY2);
        };

        canvas.ShapeStrokeApplied += (_, e) =>
        {
            ViewModel?.OnShapeStroke(e.MapX1, e.MapY1, e.MapX2, e.MapY2);
        };

        this.WhenActivated(disposables =>
        {
            _activationDisposables?.Dispose();
            _activationDisposables = new CompositeDisposable();

            if (ViewModel is null)
                return;

            var vm = ViewModel;

            _activationDisposables.Add(
                vm.WhenAnyValue(x => x.FogMask)
                    .Subscribe(_ => canvas.RebuildFogBitmap()));

            vm.FogUpdated += OnFogUpdated;
            _activationDisposables.Add(
                Disposable.Create(() => vm.FogUpdated -= OnFogUpdated));

            _activationDisposables.Add(
                vm.ShowOpenFileDialog.RegisterHandler(HandleOpenFileDialog));

            disposables(Disposable.Create(() =>
            {
                _activationDisposables?.Dispose();
                _activationDisposables = null;
            }));
        });
    }

    private void OnFogUpdated(object? sender, Avalonia.PixelRect dirtyRect)
    {
        var canvas = this.FindControl<MapCanvas>("MapCanvas")!;
        canvas.InvalidateFogRegion(dirtyRect);
    }

    private async Task HandleOpenFileDialog(IInteractionContext<Unit, string?> context)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            context.SetOutput(null);
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Map Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" } },
                FilePickerFileTypes.All
            }
        });

        context.SetOutput(files.Count > 0 ? files[0].Path.LocalPath : null);
    }
}
