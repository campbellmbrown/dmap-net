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

The wiki is the Sphinx documentation in `docs/source`.
For local writing, use `sphinx-autobuild` so the browser updates as you edit files.

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

## Changelog updates

Update `docs/source/changelog.rst` in the same pull request as the code change.
Add user-facing changes under the `Unreleased` heading while the code is still in progress.

Use short bullet points that describe the behavior change, not the implementation detail.
If `Unreleased` says `No unreleased changes.`, replace that line with the new bullets.
Keep purely internal refactors out of the changelog unless they affect contributors or releases.

## Release procedure

Release builds are created by the `release` GitHub Actions workflow when a `v*` tag is pushed.

Before tagging:

1. Make sure CI and documentation builds are passing on `main`.
2. Update `Directory.Build.props` to the release version.
3. Move the `Unreleased` changelog bullets into a new version section, such as `v1.0.1`, and reset `Unreleased` to `No unreleased changes.`.
4. Commit and merge the release prep changes.

Tag the release from the updated `main` branch:

```bash
git checkout main
git pull
git tag -a v1.0.1
git push origin v1.0.1
```

The tag name should match the changelog heading so the wiki link resolves.
The release workflow runs `scripts/release.py`, uploads the packaged Linux and Windows builds,
and creates the GitHub Release with a link to that version's changelog section in the wiki.

## Pull requests

- Keep changes focused and consistent with the existing MVVM structure.
- Update the wiki when behavior, workflows, or user-facing features change.
- Update `docs/source/changelog.rst` alongside user-facing changes.
- Include a short summary of manual testing in the pull request description.
