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

# Run
dotnet run --project DMap

# Build Sphinx docs
make -C docs html
```

There are no automated tests. The primary way to test both sides is to run the app, load a map (which starts hosting), then open the player window from the DM menu — both in the same process or in two separate instances.

## Architecture

### Window model

The app always starts in DM mode. `App.axaml.cs` builds the Autofac container, resolves `DmViewModel`, and sets `MainWindowViewModel.Content` directly to a `DmView`. There is no navigation or role selection.

The player window is a separate `Window` (not navigation) opened from the DM menu. `DmViewModel` exposes a ReactiveUI `Interaction<PlayerViewModel, Unit>`; the `DmView` code-behind handles it by creating and showing a new `Window` containing a `PlayerView`.

### Fog of war

The fog mask is a flat byte array (one byte per pixel) stored independently of any UI type, which keeps it easy to serialize for networking. Brush strokes only ever increase reveal values; re-fogging is also supported via explicit commands. The same map control renders both DM and Player views — the only difference is a fog opacity styled property.

### Brush pipeline

Brushes implement a single `Apply` method that modifies the mask in place and returns the dirty rectangle. Only that rectangle is re-rendered. High-frequency pointer events during drag bypass `ReactiveCommand` and call the view model directly to avoid overhead.

### Networking

LAN session discovery uses UDP broadcasts. Once a player connects via TCP, the DM sends the full initial state, then only incremental compressed deltas for each brush stroke.

### DI container

All business-logic services are behind interfaces and registered with Autofac in `App.axaml.cs`. Domain models have zero Avalonia dependencies. New services go in `Services/<Domain>/` with a matching interface, then registered in the container.

## Conventions

### Naming

| Symbol | Style |
|--------|-------|
| Types, namespaces, methods, properties, events, constants | `PascalCase` |
| Parameters, local variables | `camelCase` |
| Private instance fields | `_camelCase` |
| Interfaces | `IPascalCase` |

### C# style (enforced by `.editorconfig` and CI)

- 4-space indent, LF line endings, `TreatWarningsAsErrors = true`.
- Explicit types over `var`; expression-bodied accessors and properties; file-scoped namespaces.
- No `this.` qualification. Prefer pattern matching over `as`-with-null-check.
- Static local functions preferred. Readonly fields required where applicable.
- `using` directives: System namespaces first, then a blank line, then third-party, then application.
- Namespace must match folder path.
- `<Nullable>enable</Nullable>` — treat all nullable warnings as errors.

### MVVM pattern

- All ViewModels extend `ViewModelBase : ReactiveObject` and use `RaiseAndSetIfChanged`.
- Views contain no logic beyond forwarding pointer events, launching file-picker dialogs, and handling ReactiveUI interactions.
