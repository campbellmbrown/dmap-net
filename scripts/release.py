#!/usr/bin/env python3

import os
import shutil
import subprocess
import tarfile
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "DMap" / "DMap.csproj"
OUTPUT = ROOT / "artifacts" / "publish"
RELEASE = ROOT / "artifacts" / "release"


def run(command: list[str]) -> None:
    print("+ " + " ".join(str(part) for part in command), flush=True)
    subprocess.run(command, cwd=ROOT, check=True)


def dotnet_publish(runtime: str) -> Path:
    output_dir = OUTPUT / runtime
    run(
        [
            "dotnet",
            "publish",
            PROJECT,
            "--configuration",
            "Release",
            "--runtime",
            runtime,
            "--self-contained",
            "true",
            "--output",
            output_dir,
        ]
    )
    return output_dir


def version() -> str:
    ref_name = os.environ.get("GITHUB_REF_NAME")
    if ref_name:
        return ref_name

    props = ET.parse(ROOT / "Directory.Build.props")
    version_element = props.find(".//Version")
    if version_element is None or version_element.text is None:
        return "dev"

    return f"v{version_element.text.strip()}"


def package_linux(source_dir: Path, tag: str) -> Path:
    archive = RELEASE / f"DMap-{tag}-linux-x64.tar.gz"
    with tarfile.open(archive, "w:gz") as tar:
        for path in sorted(source_dir.rglob("*")):
            tar.add(path, arcname=path.relative_to(source_dir))
    return archive


def package_windows(source_dir: Path, tag: str) -> Path:
    zip_base = RELEASE / f"DMap-{tag}-win-x64"
    return Path(
        shutil.make_archive(
            base_name=str(zip_base),
            format="zip",
            root_dir=source_dir,
        )
    )


def main() -> None:
    if OUTPUT.exists():
        shutil.rmtree(OUTPUT)
    if RELEASE.exists():
        shutil.rmtree(RELEASE)
    RELEASE.mkdir(parents=True)

    tag = version()
    linux_dir = dotnet_publish("linux-x64")
    windows_dir = dotnet_publish("win-x64")

    linux_archive = package_linux(linux_dir, tag)
    windows_archive = package_windows(windows_dir, tag)

    print()
    print("Release artifacts complete:")
    print(f"  Linux:   {linux_dir}")
    print(f"  Windows: {windows_dir}")
    print("  Release artifacts:")
    print(f"    {linux_archive}")
    print(f"    {windows_archive}")


if __name__ == "__main__":
    main()
