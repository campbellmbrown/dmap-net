#!/usr/bin/env python3

import shutil
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "DMap" / "DMap.csproj"
OUTPUT = ROOT / "artifacts" / "publish"


def run(command):
    print("+ " + " ".join(str(part) for part in command), flush=True)
    subprocess.run(command, cwd=ROOT, check=True)


def publish(runtime):
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


def main():
    if OUTPUT.exists():
        shutil.rmtree(OUTPUT)

    linux_dir = publish("linux-x64")
    windows_dir = publish("win-x64")

    zip_base = OUTPUT / "DMap-win-x64"
    windows_zip = shutil.make_archive(
        base_name=str(zip_base),
        format="zip",
        root_dir=windows_dir,
    )

    print()
    print("Publish complete:")
    print(f"  Linux:   {linux_dir}")
    print(f"  Windows: {windows_dir}")
    print(f"    Zip:   {windows_zip}")


if __name__ == "__main__":
    main()
