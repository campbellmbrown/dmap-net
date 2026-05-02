# Contributing to DMap

Thanks for helping improve DMap. This project is a cross-platform Avalonia/.NET desktop app, with Sphinx documentation published as the project wiki.

## Prerequisites

- .NET SDK 10.0.x
- Python 3
- `make` for building the wiki locally

## Build and run the app

Restore dependencies before building:

```bash
dotnet restore DMap/DMap.csproj
```

Build the app:

```bash
dotnet build DMap/DMap.csproj --configuration Release --no-restore
```

Run the app:

```bash
dotnet run --project DMap
```

To test both DM and player behavior on one machine, load a map in the DM window and open the player window from the DM menu.

## Formatting

CI verifies formatting, so run this before opening a pull request:

```bash
dotnet format DMap/DMap.csproj --verify-no-changes --no-restore
```

## Run the wiki locally

The wiki is the Sphinx documentation in `docs/source`. For local writing, use `sphinx-autobuild` so the browser updates as you edit files.

```bash
python -m venv docs/.venv
source docs/.venv/bin/activate
python -m pip install -r docs/requirements.txt
```

Or, with `uv`:

```bash
uv venv docs/.venv
source docs/.venv/bin/activate
uv pip install -r docs/requirements.txt
```

Start the local wiki server:

```bash
sphinx-autobuild docs/source docs/build/html
```

Then visit `http://127.0.0.1:8000`. Keep the command running while editing the wiki.

On Windows, activate the virtual environment with `docs\.venv\Scripts\activate` and run the same server command:

```powershell
sphinx-autobuild docs/source docs/build/html
```

Use `make -C docs html` only when you want a one-off static build, which is what CI runs before publishing the wiki.

## Pull requests

- Keep changes focused and consistent with the existing MVVM structure.
- Update the wiki when behavior, workflows, or user-facing features change.
- Include a short summary of manual testing in the pull request description.
