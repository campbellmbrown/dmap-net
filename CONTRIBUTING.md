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
dotnet format --verify-no-changes --no-restore
```

Omit the `--verify-no-changes` flag to apply formatting fixes automatically.
Omit `--no-restore` if you haven't restored dependencies yet.

### Line endings

If you see lots of these errors when checking formatting, your line endings are incorrect:

```
error WHITESPACE: Fix whitespace formatting.
error ENDOFLINE: Fix end of line marker.
```

Formatting requires line endings to be set to LF (see end_of_line in `.editorconfig`).
This setting *cannot* be set to CRLF, as that would break builds on Linux.
Therefore, all source files should be in the work tree with LF line endings (even on Windows).
This is controlled by the `.gitattributes` file.

Creating a fresh clone of the repository will automatically set up the correct line endings.
However, if you already have a clone with incorrect line endings, you can fix it by running:

```bash
git rm --cached -r .
git reset --hard
```

***WARNING***: This will discard any uncommitted changes, so make sure to stash or commit them first.

Check that the line endings are now correct by running:

```bash
git ls-files --eol | grep -E '\.cs$'
```

The expected output is `i/lf    w/lf    attr/text eol=lf` for all source files.

Install the [EditorConfig extension](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig) in VS Code to automatically save files with the correct line endings.

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

On Windows, activate the virtual environment with `docs\.venv\Scripts\activate` and run the same server command.

## Changelog updates

Update `docs/source/changelog.rst` in the same pull request as the code change.
Add user-facing changes under the `Unreleased` heading while the code is still in progress.
If there is no `Unreleased` section, create one at the top of the changelog and add the new changes there.

Use short bullet points that describe the behavior change, not the implementation detail.
Keep purely internal refactors out of the changelog unless they affect contributors or releases.

## Release procedure

Release builds are created by the `release` GitHub Actions workflow when a `v*` tag is pushed.

Before tagging:

1. Make sure CI and documentation builds are passing on `main`.
2. Update `Directory.Build.props` to the release version.
3. Move the `Unreleased` changelog bullets into a new version section, such as `v1.0.1`.
4. Commit and merge the release prep changes.

Tag the release from the updated `main` branch:

```bash
git checkout main
git pull
git tag -a v1.0.1 -m "Release v1.0.1"
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
