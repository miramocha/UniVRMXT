# Installation

Install UniVRMXT in a Unity 2022.3 LTS project that already depends on UniVRM 0.131.1.

## Package Manager (git URL)

1. Open **Window → Package Manager**.
2. Click **+ → Add package from git URL**.
3. Enter:

   ```
   https://github.com/miramocha/UniVRMXT.git
   ```

Unity resolves `com.vrmc.gltf` and `com.vrmc.vrm` from this package's `package.json`.

## Local development

Add an entry to the project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.miramocha.univrmxt": "file:../UniVRMXT"
  }
}
```

Adjust the relative path to match your checkout location.

## Verify

From the repository root (no Unity required):

```bash
python tools/validate_package.py
```

In Unity, open **Window → General → Test Runner** and run **UniVRMXT.Tests**.

## Related documentation

- [Extended VRM specifications](https://github.com/miramocha/Extended-VRM-Specs)
- [Architecture](architecture.md)
