#!/usr/bin/env python3
"""Validate UniVRMXT UPM package scaffold without Unity."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_ASMDEFS = {
    ROOT / "Runtime" / "UniVRMXT.asmdef",
    ROOT / "Editor" / "UniVRMXT.Editor.asmdef",
    ROOT / "Tests" / "UniVRMXT.Tests.asmdef",
}
TRACKED_SUFFIXES = {".cs", ".asmdef"}
GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)


def check_package_json() -> None:
    package_json_path = ROOT / "package.json"
    if not package_json_path.is_file():
        fail("package.json is missing")

    data = json.loads(package_json_path.read_text(encoding="utf-8"))
    expected = {
        "name": "com.miramocha.univrmxt",
        "version": "0.1.0",
        "unity": "2022.3",
    }
    for key, value in expected.items():
        if data.get(key) != value:
            fail(f"package.json {key} expected {value!r}, got {data.get(key)!r}")

    author = data.get("author", {})
    if author.get("name") != "Mira Luna":
        fail("package.json author.name must be 'Mira Luna'")

    deps = data.get("dependencies", {})
    for dep, version in {
        "com.vrmc.gltf": "0.131.1",
        "com.vrmc.vrm": "0.131.1",
    }.items():
        if deps.get(dep) != version:
            fail(f"package.json dependency {dep} must be {version}")


def check_asmdefs_exist() -> None:
    for asmdef in REQUIRED_ASMDEFS:
        if not asmdef.is_file():
            fail(f"missing asmdef: {asmdef.relative_to(ROOT)}")


def parse_meta_guid(meta_path: Path) -> str:
    text = meta_path.read_text(encoding="utf-8")
    match = GUID_RE.search(text)
    if not match:
        fail(f"meta file has no guid: {meta_path.relative_to(ROOT)}")
    return match.group(1)


def check_meta_files() -> None:
    tracked_files = [
        path
        for path in ROOT.rglob("*")
        if path.is_file()
        and path.suffix in TRACKED_SUFFIXES
        and ".git" not in path.parts
    ]

    if not tracked_files:
        fail("no .cs or .asmdef files found")

    guids: dict[str, Path] = {}
    missing_meta: list[Path] = []

    for asset in sorted(tracked_files):
        meta = asset.with_name(asset.name + ".meta")
        if not meta.is_file():
            missing_meta.append(asset)
            continue

        guid = parse_meta_guid(meta)
        previous = guids.get(guid)
        if previous is not None:
            fail(
                "duplicate GUID "
                f"{guid} used by {previous.relative_to(ROOT)} and {asset.relative_to(ROOT)}"
            )
        guids[guid] = asset

    if missing_meta:
        lines = "\n".join(f"  - {path.relative_to(ROOT)}" for path in missing_meta)
        fail(f"missing .meta for tracked assets:\n{lines}")

    folders: set[Path] = set()
    for path in tracked_files:
        folder = path.parent
        while folder != ROOT:
            if ".git" not in folder.parts:
                folders.add(folder)
            folder = folder.parent

    folder_meta_missing = []
    for folder in sorted(folders, key=lambda p: str(p)):
        if folder == ROOT or ".git" in folder.parts:
            continue
        folder_meta = folder / ".meta"
        if not folder_meta.is_file():
            folder_meta_missing.append(folder)

    if folder_meta_missing:
        lines = "\n".join(f"  - {path.relative_to(ROOT)}" for path in folder_meta_missing)
        fail(f"missing folder .meta files:\n{lines}")


def main() -> None:
    print(f"Validating package at {ROOT}")
    check_package_json()
    check_asmdefs_exist()
    check_meta_files()
    print("OK: package.json, asmdefs, and .meta GUID checks passed")


if __name__ == "__main__":
    main()
