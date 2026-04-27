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

/// <summary>
/// Code-behind for the DM (Dungeon Master) view. Wires the <see cref="MapCanvas"/> events
/// to the ViewModel, handles fog bitmap updates, and implements the ReactiveUI interactions
/// for the file picker and the player window.
/// </summary>
public partial class DmView : ReactiveUserControl<DmViewModel>
{
    CompositeDisposable? _activationDisposables;

    /// <summary>
    /// Initialises the view, wires canvas input events to ViewModel methods, and sets up
    /// ReactiveUI activation to subscribe to fog updates and handle interactions.
    /// </summary>
    public DmView()
    {
        InitializeComponent();

        var canvas = this.FindControl<MapCanvas>("MapCanvas")!;

        canvas.BrushStrokeStarted += (_, _) => ViewModel?.BeginBrushStroke();
        canvas.BrushStrokeEnded += (_, _) => ViewModel?.EndBrushStroke();

        canvas.BrushStrokeApplied += (_, e) =>
            ViewModel?.OnBrushStroke(e.MapX1, e.MapY1, e.MapX2, e.MapY2, e.IsErasing);

        canvas.ShapeStrokeApplied += (_, e) =>
            ViewModel?.OnShapeStroke(e.MapX1, e.MapY1, e.MapX2, e.MapY2, e.IsErasing);

        this.WhenActivated(disposables =>
        {
            _activationDisposables?.Dispose();
            _activationDisposables = [];

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

            _activationDisposables.Add(
                vm.ShowPlayerWindow.RegisterHandler(HandleShowPlayerWindow));

            disposables(Disposable.Create(() =>
            {
                _activationDisposables?.Dispose();
                _activationDisposables = null;
            }));
        });
    }

    /// <summary>
    /// Forwards a fog update from the ViewModel to <see cref="MapCanvas.InvalidateFogRegion"/>
    /// so only the changed bitmap region is redrawn.
    /// </summary>
    void OnFogUpdated(object? sender, Avalonia.PixelRect dirtyRect)
    {
        var canvas = this.FindControl<MapCanvas>("MapCanvas")!;
        canvas.InvalidateFogRegion(dirtyRect);
    }

    /// <summary>
    /// Handles the <see cref="DmViewModel.ShowPlayerWindow"/> interaction by creating a new
    /// top-level window containing a <see cref="PlayerView"/> and disposing the ViewModel when
    /// the window is closed.
    /// </summary>
    void HandleShowPlayerWindow(IInteractionContext<PlayerViewModel, Unit> context)
    {
        var playerVm = context.Input;
        var window = new Window
        {
            Title = "DMap - Player",
            Content = new PlayerView { DataContext = playerVm },
            Width = 800,
            Height = 600,
        };
        window.Show();
        window.Closed += (_, _) => playerVm.Dispose();
        context.SetOutput(Unit.Default);
    }

    /// <summary>
    /// Handles the <see cref="DmViewModel.ShowOpenFileDialog"/> interaction by opening the
    /// platform file picker filtered to common image formats, returning the chosen local path
    /// or <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    async Task HandleOpenFileDialog(IInteractionContext<Unit, string?> context)
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
            FileTypeFilter =
            [
                new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"] },
                FilePickerFileTypes.All
            ]
        });

        context.SetOutput(files.Count > 0 ? files[0].Path.LocalPath : null);
    }
}
