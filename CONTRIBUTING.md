# Contributing

Thank you for contributing to UniVRMXT.

## Before you start

- Read the normative specs in [Extended-VRM-Specs](https://github.com/miramocha/Extended-VRM-Specs).
- Keep Runtime format code free of UniGLTF compile dependencies unless the change
  explicitly requires UniVRM integration.
- Match Unity 2022.3 and the package versions declared in `package.json`.

## Development

1. Add this repository as a local UPM package or git URL dependency in a Unity 2022.3 project
   that already has UniVRM 0.131.2 installed.
2. Run `python tools/validate_package.py` from the repository root before opening a PR.
3. Run Unity Test Runner for `UniVRMXT.Tests` when C# behavior changes.

## Pull requests

- Keep diffs focused on one extension or integration area.
- Update `CHANGELOG.md` under `[Unreleased]` or the next version section.
- Do not commit generated `*.g.cs` files; handwritten parsers belong under `Runtime/Format/`.

## First UPM release

`package.json` version is `0.1.0`. After the id/pin/docs on `main` are what you want to
ship:

```bash
git tag -a v0.1.0 -m "UniVRMXT 0.1.0"
git push origin v0.1.0
```

Install URL: `https://github.com/miramocha/UniVRMXT.git#v0.1.0`. Do not retag; bump
`package.json` and cut `v0.1.1` (or similar) next.

Extended-UniVRM already has `v0.131.2`. UPM uses
`?path=/Packages/UniGLTF#v0.131.2` and `?path=/Packages/VRM10#v0.131.2` on
`https://github.com/miramocha/Extended-UniVRM.git`.

## Code style

- Follow `.editorconfig` for C# and markdown.
- Use `UniVRMXT` root namespace segments that mirror folder names (`Format`, `Vfx`, `MaterialsOverride`).
