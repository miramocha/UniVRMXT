# Installation

Unity **2022.3 LTS** or later. UniVRM **0.131.2** (`com.vrmc.gltf` + `com.vrmc.vrm`) must already
be in the project. Those packages are **not** on Unity’s registry; add them from git
before UniVRMXT.

## 1. Extended UniVRM (hooks)

**Window → Package Manager → + → Add package from git URL…**

```
https://github.com/miramocha/Extended-UniVRM.git?path=/Packages/UniGLTF
https://github.com/miramocha/Extended-UniVRM.git?path=/Packages/VRM10
```

Stock [vrm-c/UniVRM](https://github.com/vrm-c/UniVRM) at the same package version works for consume-only
(companion `*.vrmxt.prefab`). Import onto the `.vrm` asset and `VRMXT_*` export need the
Extended fork plus **Project Settings → VRM10** import/export extension toggles.

Fork README: [Extended-UniVRM](https://github.com/miramocha/Extended-UniVRM).

## 2. UniVRMXT

Then add:

```
https://github.com/miramocha/UniVRMXT.git
```

`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.vrmc.gltf": "https://github.com/miramocha/Extended-UniVRM.git?path=/Packages/UniGLTF",
    "com.vrmc.vrm": "https://github.com/miramocha/Extended-UniVRM.git?path=/Packages/VRM10",
    "com.vrmxt.univrmxt": "https://github.com/miramocha/UniVRMXT.git"
  }
}
```

## Local development

```json
{
  "dependencies": {
    "com.vrmxt.univrmxt": "file:../UniVRMXT"
  }
}
```

Adjust the relative path. Still add Extended-UniVRM (or stock UniVRM) via git first.

## Verify

From the UniVRMXT repository root (no Unity required):

```bash
python tools/validate_package.py
```

In Unity, **Window → General → Test Runner** → **UniVRMXT.Tests**.

## Editor VFX on `.vrm` import

| UniVRM host | Scene asset |
|-------------|-------------|
| [Extended-UniVRM](https://github.com/miramocha/Extended-UniVRM) with **Project Settings → VRM10 → Enable VRM Import Extensions** | Raw `.vrm` (hooks attach VFX during import) |
| Stock [vrm-c/UniVRM](https://github.com/vrm-c/UniVRM), or Extended with that setting off | Sibling `*.vrmxt.prefab` |

Reimport `.vrm` after changing the Project Settings toggle. See [architecture.md](architecture.md).

## Related documentation

- [Extended VRM specifications](https://github.com/miramocha/Extended-VRM-Specs)
- [Architecture](architecture.md)
