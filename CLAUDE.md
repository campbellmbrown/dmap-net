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

### Navigation

Navigation is a single `Content` property on the main window view model. Setting it to a new view model causes the bound `ContentControl` to switch views automatically via `ViewLocator`, which resolves views from view models by naming convention.

### Fog of war

The fog mask is a flat byte array (one byte per pixel) stored independently of any UI type, which keeps it easy to serialize for networking. Brush strokes only ever increase reveal values. The same map control renders both DM and Player views — the only difference is an opacity styled property.

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
- Views contain no logic beyond forwarding pointer events and launching file-picker dialogs.
