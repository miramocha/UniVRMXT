#!/usr/bin/env python3
"""Generate Unity .meta files with unique GUIDs for the UniVRMXT scaffold."""

from __future__ import annotations

import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# Fixed GUID for Runtime asmdef so Editor/Tests can reference it.
RUNTIME_ASMDEF_GUID = "7f3a9c2e1b4d5a6f8091a2b3c4d5e6f7"


def new_guid() -> str:
    return uuid.uuid4().hex


def folder_meta(guid: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def script_meta(guid: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def asmdef_meta(guid: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def default_meta(guid: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def write_meta(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")
    print(f"wrote {path.relative_to(ROOT)}")


def main() -> None:
    guids: dict[Path, str] = {}

    # Assign fixed runtime asmdef GUID first.
    runtime_asmdef = ROOT / "Runtime" / "UniVRMXT.asmdef"
    guids[runtime_asmdef] = RUNTIME_ASMDEF_GUID

    tracked_assets: list[Path] = []
    for suffix in (".cs", ".asmdef"):
        tracked_assets.extend(
            path
            for path in ROOT.rglob(f"*{suffix}")
            if ".git" not in path.parts and "tools" not in path.parts
        )

    for asset in sorted(tracked_assets):
        if asset not in guids:
            guids[asset] = new_guid()

    for asset, guid in guids.items():
        meta_path = asset.with_suffix(asset.suffix + ".meta")
        if asset.suffix == ".asmdef":
            write_meta(meta_path, asmdef_meta(guid))
        else:
            write_meta(meta_path, script_meta(guid))

    folders: set[Path] = set()
    for asset in tracked_assets:
        folder = asset.parent
        while folder != ROOT:
            if ".git" not in folder.parts:
                folders.add(folder)
            folder = folder.parent

    for folder in sorted(folders, key=lambda p: str(p)):
        if folder == ROOT or ".git" in folder.parts:
            continue
        meta_path = folder / ".meta"
        if meta_path.exists():
            continue
        write_meta(meta_path, folder_meta(new_guid()))

    # Root-level and docs assets without scripts still need metas when present.
    extra_files = [
        ROOT / "package.json",
        ROOT / "LICENSE.txt",
        ROOT / "README.md",
        ROOT / "CHANGELOG.md",
        ROOT / "CONTRIBUTING.md",
        ROOT / "docs" / "installation.md",
        ROOT / "docs" / "architecture.md",
    ]
    for asset in extra_files:
        if not asset.exists():
            continue
        meta_path = asset.with_name(asset.name + ".meta")
        if not meta_path.exists():
            write_meta(meta_path, default_meta(new_guid()))

    for folder in (ROOT / "docs", ROOT / ".github", ROOT / ".github" / "workflows", ROOT / "tools"):
        if folder.exists():
            meta_path = folder / ".meta"
            if not meta_path.exists():
                write_meta(meta_path, folder_meta(new_guid()))


if __name__ == "__main__":
    main()
