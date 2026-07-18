#!/usr/bin/env python3
"""Generate Unity .meta files with unique GUIDs for the UniVRMXT scaffold.

Preserves existing GUIDs. Unity folder metas are siblings (FolderName.meta),
not FolderName/.meta.
"""

from __future__ import annotations

import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# Fixed GUID for Runtime asmdef so Editor/Tests can reference it.
RUNTIME_ASMDEF_GUID = "7f3a9c2e1b4d5a6f8091a2b3c4d5e6f7"
GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)


def new_guid() -> str:
    return uuid.uuid4().hex


def read_guid(meta_path: Path) -> str | None:
    if not meta_path.is_file():
        return None
    match = GUID_RE.search(meta_path.read_text(encoding="utf-8"))
    return match.group(1) if match else None


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


def sibling_folder_meta(folder: Path) -> Path:
    return folder.parent / f"{folder.name}.meta"


def ensure_asset_meta(asset: Path, preferred_guid: str | None = None) -> None:
    meta_path = asset.with_name(asset.name + ".meta")
    existing = read_guid(meta_path)
    if existing is not None:
        if preferred_guid is not None and existing != preferred_guid:
            # Rewrite only when the fixed Runtime asmdef GUID must be enforced.
            write_meta(meta_path, asmdef_meta(preferred_guid))
        return

    guid = preferred_guid or new_guid()
    if asset.suffix == ".asmdef":
        write_meta(meta_path, asmdef_meta(guid))
    elif asset.suffix == ".cs":
        write_meta(meta_path, script_meta(guid))
    else:
        write_meta(meta_path, default_meta(guid))


def ensure_folder_meta(folder: Path) -> None:
    if folder == ROOT or ".git" in folder.parts:
        return
    meta_path = sibling_folder_meta(folder)
    if meta_path.exists():
        return
    write_meta(meta_path, folder_meta(new_guid()))


def main() -> None:
    runtime_asmdef = ROOT / "Runtime" / "UniVRMXT.asmdef"

    tracked_assets: list[Path] = []
    for suffix in (".cs", ".asmdef"):
        tracked_assets.extend(
            path
            for path in ROOT.rglob(f"*{suffix}")
            if ".git" not in path.parts and "tools" not in path.parts
        )

    for asset in sorted(tracked_assets):
        preferred = RUNTIME_ASMDEF_GUID if asset == runtime_asmdef else None
        ensure_asset_meta(asset, preferred)

    folders: set[Path] = set()
    for asset in tracked_assets:
        folder = asset.parent
        while folder != ROOT:
            if ".git" not in folder.parts:
                folders.add(folder)
            folder = folder.parent

    for folder in sorted(folders, key=lambda p: str(p)):
        ensure_folder_meta(folder)

    extra_files = [
        ROOT / "package.json",
        ROOT / "LICENSE.txt",
        ROOT / "README.md",
        ROOT / "CHANGELOG.md",
        ROOT / "CONTRIBUTING.md",
        ROOT / "docs" / "installation.md",
        ROOT / "docs" / "architecture.md",
        ROOT / "docs" / "vfx-particle-mapping.md",
        ROOT / "docs" / "univrm-upstream-hooks.md",
    ]
    for asset in extra_files:
        if asset.exists():
            ensure_asset_meta(asset)

    for folder in (ROOT / "docs", ROOT / ".github", ROOT / ".github" / "workflows", ROOT / "tools"):
        if folder.exists():
            ensure_folder_meta(folder)


if __name__ == "__main__":
    main()
