# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

DMap is a cross-platform Avalonia/.NET desktop app for D&D map sessions. The Dungeon Master loads a map image and uses a fog-of-war brush to reveal regions to players in real time over the local network (UDP discovery + TCP sync).

## Commands

```bash
# Restore dependencies
dotnet restore DMap/DMap.csproj

# Build
dotnet build DMap/DMap.csproj --configuration Release --no-restore

# Verify formatting (CI enforces this)
dotnet format DMap/DMap.csproj --verify-no-changes --no-restore

# Run (role selection screen)
dotnet run --project DMap

# Run in a specific mode
dotnet run --project DMap -- --role dm
dotnet run --project DMap -- --role player

# Build Sphinx docs
make -C docs html
```

There are no automated tests. The dual-mode VS Code launch config (`DMap (DM + Player)`) is the primary way to test both sides on one machine.

## Architecture

### Data flow

1. `App.axaml.cs` parses `--role`, builds the Autofac DI container, and sets the initial view model on `MainWindowViewModel.Content`.
2. `ViewLocator` maps `FooViewModel` → `FooView` by naming convention; `MainWindow.axaml` binds a `ContentControl` to `Content`.
3. Navigation is purely `MainWindowViewModel.Content = newViewModel`.

### Fog of war model

The fog is a `FogMask` — a flat `byte[]` (width × height) where 0 = fully fogged and 255 = fully revealed. Brush strokes only ever increase values; fog cannot be re-applied.

For rendering, `MapCanvas` converts the mask to a `WriteableBitmap` with black pixels at variable alpha. The alpha scale is a styled property: 128 in DM mode (semi-transparent) and 255 in Player mode (opaque), so the same control handles both views.

### Brush pipeline

`IBrush.Apply(mask, x1, y1, x2, y2, settings) -> PixelRect` modifies the mask in place and returns the dirty bounding rectangle. Only that rectangle is re-rendered. `CircleBrush` is the primary implementation, with linear feathering over `radius * softness` pixels.

Brush strokes fire at 60+ Hz during drag. The view's code-behind forwards pointer coordinates directly to `DmViewModel.OnBrushStroke()` — not through a `ReactiveCommand` — to avoid overhead on the hot path.

### Networking

- **Discovery**: `DiscoveryService` broadcasts a UDP packet every 2 seconds on port 19876 (magic bytes `"DMAP"`) containing session ID, TCP port, and machine name. Players populate a discovery list from these broadcasts.
- **Data transfer**: On TCP connect, the DM sends the full fog mask and original map image bytes. Every subsequent brush stroke produces a `FogDelta` (sub-rectangle of the mask) compressed with `DeflateStream` and broadcast to all players.
- **Framing**: every TCP message is `[type:int32][length:int32][payload]` (see `Protocol.cs`).

### Undo/redo

`UndoRedoService` maintains two stacks capped at 10 commands (`MaxHistory`). Each brush stroke is wrapped in a `FogDeltaCommand` that stores the before/after sub-rectangle of the mask.

### DI container

Services are registered in `App.OnFrameworkInitializationCompleted()` using Autofac. All business-logic services are behind interfaces (`IFogMaskService`, `IBrush`, `IDmHostService`, `IPlayerClientService`, `IDiscoveryService`, `IUndoRedoService`, `INavigator`). Domain models (`FogMask`, `BrushSettings`, `MapSession`) have zero Avalonia dependencies.

## Conventions

### Naming

| Symbol | Style |
|--------|-------|
| Types, namespaces, methods, properties, events, constants | `PascalCase` |
| Parameters, local variables | `camelCase` |
| Private instance fields | `_camelCase` |
| Interfaces | `IPascalCase` |

### C# style (enforced by `.editorconfig` and CI)

- 4-space indent, LF line endings, max line length 120, `TreatWarningsAsErrors = true`.
- Explicit types over `var`; expression-bodied accessors and properties; file-scoped namespaces.
- No `this.` qualification. Prefer pattern matching over `as`-with-null-check.
- Static local functions preferred. Readonly fields required where applicable.
- `using` directives: System namespaces first, then a blank line, then third-party, then application.
- Namespace must match folder path (`dotnet_style_namespace_match_folder`).
- `<Nullable>enable</Nullable>` — treat all nullable warnings as errors.

### MVVM pattern

- All ViewModels extend `ViewModelBase : ReactiveObject` and use `this.RaiseAndSetIfChanged`.
- Views contain no logic beyond forwarding pointer events and launching file-picker dialogs.
- High-frequency pointer events (brush strokes) bypass `ReactiveCommand` and call ViewModel methods directly from code-behind.
- New services go in `Services/<Domain>/`, with an `I<Name>.cs` interface and `<Name>.cs` implementation, then registered in `App.axaml.cs`.
