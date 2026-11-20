using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using DMap.Controls.MapCanvas;
using DMap.ViewModels;

using ReactiveUI;
using ReactiveUI.Avalonia;

using Serilog;

namespace DMap.Views;

/// <summary>
/// Code-behind for the DM (Dungeon Master) view. Wires the <see cref="MapCanvas"/> events
/// to the ViewModel, handles fog bitmap updates, and implements the ReactiveUI interactions
/// for the file picker and the player window.
/// </summary>
public partial class DmView : ReactiveUserControl<DmViewModel>
{
    const string ProjectFileExtension = "dmap";

    static readonly FilePickerFileType _mapFileType = new("Map files")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp", "*.pdf"],
    };

    static readonly FilePickerFileType _projectFileType = new("DMap projects")
    {
        Patterns = [$"*.{ProjectFileExtension}"],
    };

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
        canvas.PlayerViewportChanged += (_, viewport) => ViewModel?.UpdatePlayerViewport(viewport);
        canvas.CursorUpdated += (_, cursor) => ViewModel?.UpdateCursor(cursor);
        canvas.StampChanged += (_, _) => ViewModel?.OnStampChanged();

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
                vm.WhenAnyValue(x => x.IsFogGenerating)
                    .Subscribe(isGenerating => SetFogGenerationDialogVisible(isGenerating)));

            _activationDisposables.Add(
                vm.WhenAnyValue(x => x.FogMask)
                    .Subscribe(_ => canvas.RebuildFogBitmap()));

            vm.FogUpdated += OnFogUpdated;
            _activationDisposables.Add(
                Disposable.Create(() => vm.FogUpdated -= OnFogUpdated));

            _activationDisposables.Add(
                vm.ShowOpenMapDialog.RegisterHandler(HandleOpenMapDialog));

            _activationDisposables.Add(
                vm.ShowOpenProjectDialog.RegisterHandler(HandleOpenProjectDialog));

            _activationDisposables.Add(
                vm.ShowSaveProjectDialog.RegisterHandler(HandleSaveProjectDialog));

            _activationDisposables.Add(
                vm.ConfirmUnsavedChanges.RegisterHandler(HandleConfirmUnsavedChanges));

            if (TopLevel.GetTopLevel(this) is Window window)
            {
                window.Closing += OnWindowClosing;
                _activationDisposables.Add(
                    Disposable.Create(() => window.Closing -= OnWindowClosing));
            }

            _activationDisposables.Add(
                vm.ShowPlayerWindow.RegisterHandler(HandleShowPlayerWindow));

            _activationDisposables.Add(
                vm.ShowAboutDialog.RegisterHandler(HandleShowAboutDialog));

            _activationDisposables.Add(
                vm.OpenDirectory.RegisterHandler(HandleOpenDirectory));

            _activationDisposables.Add(
                vm.GetDmVisibleMapRect.RegisterHandler(context => context.SetOutput(canvas.GetVisibleMapRect())));

            _activationDisposables.Add(
                vm.FitDmViewToMapRect.RegisterHandler(context =>
                {
                    canvas.FitViewToMapRect(context.Input);
                    context.SetOutput(Unit.Default);
                }));

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
        var window = new PlayerWindow
        {
            DataContext = playerVm,
        };

        window.Show();
        window.Closed += (_, _) =>
        {
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
    /// Handles the <see cref="DmViewModel.ShowOpenMapDialog"/> interaction by picking a map image or PDF.
    /// </summary>
    async Task HandleOpenMapDialog(IInteractionContext<Unit, string?> context) =>
        context.SetOutput(await PickFileToOpenAsync("New Project From Map", _mapFileType));

    /// <summary>
    /// Handles the <see cref="DmViewModel.ShowOpenProjectDialog"/> interaction by picking a project file.
    /// </summary>
    async Task HandleOpenProjectDialog(IInteractionContext<Unit, string?> context) =>
        context.SetOutput(await PickFileToOpenAsync("Open Project", _projectFileType));

    /// <summary>
    /// Handles the <see cref="DmViewModel.ShowSaveProjectDialog"/> interaction by opening the platform
    /// save picker, returning the chosen local path or <see langword="null"/> if cancelled.
    /// </summary>
    async Task HandleSaveProjectDialog(IInteractionContext<string, string?> context)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var file = topLevel is null
            ? null
            : await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Project",
                SuggestedFileName = context.Input,
                DefaultExtension = ProjectFileExtension,
                FileTypeChoices = [_projectFileType],
            });

        context.SetOutput(file?.Path.LocalPath);
    }

    /// <summary>
    /// Opens the platform file picker for a single file of <paramref name="fileType"/>, returning the
    /// chosen local path or <see langword="null"/> if cancelled.
    /// </summary>
    async Task<string?> PickFileToOpenAsync(string title, FilePickerFileType fileType)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [fileType, FilePickerFileTypes.All],
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    /// <summary>
    /// Handles the <see cref="DmViewModel.ConfirmUnsavedChanges"/> interaction by showing a modal
    /// Save / Don't Save / Cancel dialog.
    /// </summary>
    async Task HandleConfirmUnsavedChanges(IInteractionContext<string, UnsavedChangesChoice> context)
    {
        var choice = TopLevel.GetTopLevel(this) is Window owner
            ? await new UnsavedChangesDialog(context.Input).ShowDialog<UnsavedChangesChoice?>(owner)
            : null;

        context.SetOutput(choice ?? UnsavedChangesChoice.Cancel);
    }

    /// <summary>
    /// Cancels closing the main window while there are unsaved changes and routes the request through
    /// <see cref="DmViewModel.ExitCommand"/>, which prompts before shutting down.
    /// </summary>
    void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (ViewModel is not { HasUnsavedChanges: true } vm)
            return;

        e.Cancel = true;
        vm.ExitCommand.Execute().Subscribe();
    }

    /// <summary>
    /// Handles the <see cref="DmViewModel.OpenDirectory"/> interaction by opening the requested
    /// local directory path in the operating system's file manager.
    /// </summary>
    Task HandleOpenDirectory(IInteractionContext<string, Unit> context)
    {
        try
        {
            var path = context.Input;
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open log directory.");
        }

        context.SetOutput(Unit.Default);
        return Task.CompletedTask;
    }
}
