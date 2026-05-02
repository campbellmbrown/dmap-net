using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        FogGenerationDialog? fogGenerationDialog = null;
        canvas.ViewportChanged += (_, viewport) => ViewModel?.UpdateViewport(viewport);

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
            vm.UpdateViewport(canvas.GetViewport());

            _activationDisposables.Add(
                vm.WhenAnyValue(x => x.IsFogGenerating)
                    .Subscribe(isGenerating => SetFogGenerationDialogVisible(isGenerating)));

            _activationDisposables.Add(
                vm.WhenAnyValue(x => x.FogMask)
                    .Subscribe(_ => canvas.RebuildFogBitmap()));

            _activationDisposables.Add(
                vm.WhenAnyValue(x => x.MapImage)
                    .Subscribe(_ => vm.UpdateViewport(canvas.GetViewport())));

            vm.FogUpdated += OnFogUpdated;
            _activationDisposables.Add(
                Disposable.Create(() => vm.FogUpdated -= OnFogUpdated));

            _activationDisposables.Add(
                vm.ShowOpenFileDialog.RegisterHandler(HandleOpenFileDialog));

            _activationDisposables.Add(
                vm.ShowPlayerWindow.RegisterHandler(HandleShowPlayerWindow));

            _activationDisposables.Add(
                vm.ShowAboutDialog.RegisterHandler(HandleShowAboutDialog));

            disposables(Disposable.Create(() =>
            {
                fogGenerationDialog?.Close();
                fogGenerationDialog = null;
                _activationDisposables?.Dispose();
                _activationDisposables = null;
            }));
        });

        void SetFogGenerationDialogVisible(bool isVisible)
        {
            if (isVisible)
            {
                if (fogGenerationDialog is not null)
                    return;

                fogGenerationDialog = new FogGenerationDialog();
                fogGenerationDialog.Closed += (_, _) => fogGenerationDialog = null;

                if (TopLevel.GetTopLevel(this) is Window owner)
                    _ = fogGenerationDialog.ShowDialog(owner);
                else
                    fogGenerationDialog.Show();

                return;
            }

            fogGenerationDialog?.Close();
            fogGenerationDialog = null;
        }
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
        // TODO: Move to view instead of programmatically creating the window here
        var window = new Window
        {
            Title = "DMap - Player",
            Content = new PlayerView { DataContext = playerVm },
            Width = 800,
            Height = 600,
            ShowInTaskbar = true,
        };
        var windowDisposables = new CompositeDisposable();

        windowDisposables.Add(
            playerVm.WhenAnyValue(x => x.WindowState)
                .Subscribe(state => window.WindowState = state));

        windowDisposables.Add(
            window.GetObservable(Window.WindowStateProperty)
                .Subscribe(state => playerVm.WindowState = state));

        window.KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.F11),
            Command = playerVm.ToggleFullScreenCommand,
        });

        window.Show();
        window.Closed += (_, _) =>
        {
            windowDisposables.Dispose();
            playerVm.Dispose();
        };
        context.SetOutput(Unit.Default);
    }

    /// <summary>
    /// Handles the <see cref="DmViewModel.ShowAboutDialog"/> interaction by showing the
    /// application information dialog as a modal window when an owner is available.
    /// </summary>
    async Task HandleShowAboutDialog(IInteractionContext<Unit, Unit> context)
    {
        var window = new AboutWindow();
        if (TopLevel.GetTopLevel(this) is Window owner)
            await window.ShowDialog(owner);
        else
            window.Show();

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
