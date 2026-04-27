using System;
using System.Reactive.Disposables;

using Avalonia.Controls;

using DMap.Controls;
using DMap.ViewModels;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace DMap.Views;

/// <summary>
/// Code-behind for the player view. Wires the <see cref="MapCanvas"/> to the
/// <see cref="PlayerViewModel"/> so that fog updates from the DM are reflected in the canvas.
/// </summary>
public partial class PlayerView : ReactiveUserControl<PlayerViewModel>
{
    CompositeDisposable? _activationDisposables;

    /// <summary>
    /// Initialises the view and sets up ReactiveUI activation to subscribe to fog mask
    /// changes and fog update events from the ViewModel.
    /// </summary>
    public PlayerView()
    {
        InitializeComponent();

        var canvas = this.FindControl<MapCanvas>("MapCanvas")!;

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
}
