#!/usr/bin/env python3
"""Generate pre-colored SVG icon variants from the source SVG files."""

from __future__ import annotations

import argparse
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE_DIR = REPO_ROOT / "DMap" / "Assets" / "Icons"
DEFAULT_OUTPUT_DIR = REPO_ROOT / "DMap" / "Assets" / "GeneratedIcons"

VARIANTS = {
    "normal": "#D8D8DC",
    "hover": "#FFFFFF",
    "disabled": "#6A6A73",
    "selected": "#D6A54B",
}


def colorize(svg: str, color: str) -> str:
    return svg.replace("currentColor", color)


def write_if_changed(path: Path, content: str) -> bool:
    if path.exists() and path.read_text(encoding="utf-8") == content:
        return False

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    return True


def remove_empty_dirs(path: Path) -> None:
    for directory in sorted((item for item in path.rglob("*") if item.is_dir()), reverse=True):
        try:
            directory.rmdir()
        except OSError:
            pass


def generate(source_dir: Path, output_dir: Path) -> tuple[int, int, int]:
    sources = sorted(source_dir.rglob("*.svg"))
    if not sources:
        raise SystemExit(f"No SVG icons found in {source_dir}")

    expected_paths: set[Path] = set()
    changed_count = 0

    for variant, color in VARIANTS.items():
        variant_dir = output_dir / variant
        for source in sources:
            relative_path = source.relative_to(source_dir)
            destination = variant_dir / relative_path
            expected_paths.add(destination)

            content = colorize(source.read_text(encoding="utf-8"), color)
            if write_if_changed(destination, content):
                changed_count += 1

    stale_count = 0
    if output_dir.exists():
        for generated_icon in output_dir.rglob("*.svg"):
            if generated_icon not in expected_paths:
                generated_icon.unlink()
                stale_count += 1
        remove_empty_dirs(output_dir)

    return len(sources), changed_count, stale_count


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source-dir",
        type=Path,
        default=DEFAULT_SOURCE_DIR,
        help=f"SVG source directory. Default: {DEFAULT_SOURCE_DIR}",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_OUTPUT_DIR,
        help=f"Generated icon output directory. Default: {DEFAULT_OUTPUT_DIR}",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    source_count, changed_count, stale_count = generate(args.source_dir.resolve(), args.output_dir.resolve())
    print(
        f"Generated {len(VARIANTS)} variants for {source_count} SVG icons "
        f"({changed_count} updated, {stale_count} stale removed)"
    )


if __name__ == "__main__":
    main()
