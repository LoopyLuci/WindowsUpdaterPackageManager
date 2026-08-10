# Contributing to WUPM

Thank you for your interest in improving Windows Update and Package Manager. This project prioritizes personal use and enterprise-grade reliability.

## Dev setup

- Windows 10/11
- .NET SDK 10+
- PowerShell 7+

```powershell
git clone git@github.com:LoopyLuci/WindowsUpdateAndPackageManager.git
cd WindowsUpdateAndPackageManager
.\build.ps1 -Configuration Debug
.\run-tests.cmd
```

## Branching

- `main` is protected. All changes via PR.
- Branch naming: `feat/<topic>`, `fix/<topic>`, `docs/<topic>`, `chore/<topic>`.

## Commit messages

Use Conventional Commits: `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`.

## Release process

1. Update `CHANGELOG.md`.
2. Create/merge PR into `main`.
3. Tag release: `git tag v0.x.y && git push --tags`.
