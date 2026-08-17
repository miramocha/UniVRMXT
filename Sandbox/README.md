# Sandbox (local experiments)

**Local only — not in git.** Throwaway files for trying ideas without polluting the package tree.

## Do not

- Reference `Sandbox/**` from `Runtime`, `Editor`, `Tests`, or `Samples~`
- Commit sandbox files (gitignore blocks them except this README)

## Gitignore

| Path | In git? |
|------|---------|
| `Sandbox/*` | No |
| `Sandbox/README.md` | **Yes** |
