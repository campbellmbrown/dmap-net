# DMap

A cross-platform desktop tool for running Dungeons & Dragons map sessions. The Dungeon Master loads a map image and reveals parts of it to players in real time over the local network.

## What it does

- **DM mode.** Load a map image (PNG, JPG, BMP, GIF, WebP). The map is covered by a semi-transparent black fog so the DM can see where they've revealed. A configurable circle brush erases fog as the DM drags across the map.
- **Player mode.** Automatically discovers DM sessions on the local network. Once connected, displays the same map with the fog fully opaque — players only see the regions the DM has revealed.
- **Live sync.** Brush strokes on the DM's machine are broadcast to every connected player. Updates are incremental (only the changed region is sent) and compressed.

## How it works

### Fog of war

The fog is modelled as a single-channel `byte[]` the same dimensions as the map image. Each byte is the reveal amount at that pixel (0 = fully fogged, 255 = fully revealed). Brush strokes only ever increase the reveal value — they never re-fog.

For rendering, the mask is converted to a `WriteableBitmap` with black pixels and variable alpha. The alpha channel is scaled by a per-mode factor: DM mode uses 128 (semi-transparent) and Player mode uses 255 (opaque). This means the same control renders both modes by just changing one styled property.

### Brushes

All brushes implement `IBrush.Apply(mask, x, y, settings) -> PixelRect`. The method modifies the mask in place and returns the bounding rectangle of changed pixels, which the rendering layer uses to update only the dirty region. `CircleBrush` is the MVP implementation with linear feathering — the outer edge falls off from fully revealed to fully fogged over `radius * softness` pixels.

Future brushes (rectangle, polygon, reveal-all, hide-all) implement the same interface.

### Networking

- **Discovery** — DM broadcasts a small UDP packet every 2 seconds on port 19876 containing its session ID, TCP port, and machine name. Players listen on the same port and populate a discovery list.
- **Data transfer** — once a player connects via TCP, the DM sends session info (map dimensions + current fog mask) and the original map image bytes. From then on, every brush stroke generates a `FogDelta` (sub-rectangle of the mask) that is compressed with `DeflateStream` and broadcast to all connected players.
- **Framing** — every TCP message is length-prefixed: `[type:int32][length:int32][payload]`.

### Navigation

`MainWindowViewModel.Content` is a `ViewModelBase` bound to a `ContentControl` in `MainWindow.axaml`. Avalonia's `IDataTemplate` system (via `ViewLocator.cs`) resolves the view for the active view model by naming convention: `FooViewModel` -> `FooView`. Navigation is a matter of setting `Content` to a new view model.

## Repository structure

```
DMap/
  App.axaml / App.axaml.cs         application entry — parses --role arg, builds root VM
  Program.cs                       Avalonia AppBuilder bootstrapping
  ViewLocator.cs                   resolves View for a given ViewModel by naming convention

  Models/                          pure domain types (no Avalonia dependencies)
    AppRole.cs                     DM / Player enum
    FogMask.cs                     byte[] mask + width/height + indexer
    BrushSettings.cs               diameter + softness record
    MapSession.cs                  session ID + map dimensions record

  Services/                        business logic behind interfaces
    Brushes/
      IBrush.cs                    brush abstraction
      CircleBrush.cs               circle with linear feathering
    Fog/
      IFogMaskService.cs           fog mask lifecycle
      FogMaskService.cs            owns the mask, fires MaskChanged events
    Networking/
      Protocol.cs                  message types, FogDelta, binary framing
      IDiscoveryService.cs         UDP LAN discovery interface
      DiscoveryService.cs          UdpClient broadcast/listen implementation
      IDmHostService.cs            TCP server interface (DM side)
      DmHostService.cs             TcpListener implementation
      IPlayerClientService.cs      TCP client interface (Player side)
      PlayerClientService.cs       TcpClient implementation

  ViewModels/                      reactive UI state + commands
    ViewModelBase.cs               extends ReactiveObject
    MainWindowViewModel.cs         navigation shell
    StartViewModel.cs              DM / Player selection
    DmViewModel.cs                 map loading, brush settings, pan/zoom, hosts network
    PlayerViewModel.cs             discovery list, connect/disconnect, receive updates

  Views/                           Avalonia views (AXAML + code-behind)
    MainWindow.axaml               hosts a ContentControl bound to MainWindowViewModel.Content
    StartView.axaml                two buttons for DM / Player
    DmView.axaml                   toolbar + brush-settings sidebar + MapCanvas
    PlayerView.axaml               discovery list (disconnected) / MapCanvas (connected)

  Controls/
    MapCanvas.cs                   custom Control — renders map + fog, pan/zoom, pointer input

  Assets/                          app icon and other static resources
```

### Key design decisions

- **MVVM throughout** — views contain no logic beyond forwarding pointer events and handling file-picker interactions. All state lives in view models.
- **SRP for services** — networking, fog, and brushes are separate services behind interfaces. Each service has one reason to change.
- **Pointer events via direct wiring, not commands** — brush strokes fire at 60+ Hz during drag. The view's code-behind forwards coordinates straight to the view model's `OnBrushStroke` method. Reactive commands are used for everything else.
- **Mask as `byte[]`, not `WriteableBitmap`** — keeps the domain model free of Avalonia dependencies and makes network serialization trivial.

## Running

Open the project in VS Code and pick a launch configuration:

- **DMap (Start Screen)** — no arguments, shows the DM/Player chooser.
- **DMap (DM)** — jumps straight into DM mode.
- **DMap (Player)** — jumps straight into Player mode and begins listening for DMs.
- **DMap (DM + Player)** — compound configuration that launches both for testing on a single machine.

Alternatively, run from the command line:

```
dotnet run --project DMap -- --role dm
dotnet run --project DMap -- --role player
```

## Future work

- Additional brush types: rectangle reveal, polygon reveal, reveal-all, hide-all
- Re-fogging (currently brushes only reveal)
- PDF support for map files
- Periodic full-mask resync to recover from lost deltas
- Player-list view on the DM side (who is connected, not just how many)
