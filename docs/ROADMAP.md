# v0.2.0 Planning

## Completed in v0.1.0
- Local CI/CD pipeline (`scripts/ci.ps1`, `scripts/release.ps1`)
- Chocolatey packaging (`scripts/deploy/chocolatey/`)
- Winget manifest generation (`scripts/deploy/winget/`)
- Windows service wrapper (`scripts/service-wupm-api.ps1`)
- Self-update support (`wupm self-update`)
- Integration tests for release deployment
- README badges and documentation

## Proposed for v0.3.0
1. ~~Offline scan result caching~~ — `--offline-scan` saves results to `.wupm/cache/offline-scan-result.txt`
2. ~~Delta update progress reporting~~ — `delta-update` and `delta-apply` show progress events
3. ~~Offline cache management~~ — `wupm cache list` and `wupm cache prune`
4. ~~Rollback dry-run~~ — `wupm rollback --dry-run --id <pkg>`
5. Self-update E2E — blocked by missing `GITHUB_TOKEN`

## Proposed for v0.4.0
1. ~~Plugin system architecture~~ — extensible module interface for third-party extensions
2. ~~Offline cache UX~~ — richer output, cache stats, and export
3. ~~Rollback UX~~ — richer output, dry-run, and selective rollback
4. ~~Update notifications~~ — background checks with system tray alerts
5. ~~Delta UX~~ — progress bars, resume support, and delta verification

## Proposed for v0.5.0
1. Plugin SDK documentation — extensible module interface for third-party extensions
2. Background notification daemon — periodic update checks with CLI controls
3. Delta verification CLI — verify cached package hashes against expected SHA256

## Priority order
1. Offline scan result caching
2. Delta update progress reporting
3. Offline cache management
4. Rollback UX improvements
5. Self-update E2E
6. Plugin SDK documentation
7. Background notification daemon
8. Delta verification CLI

## Environment blockers and exact fixes

1. Chocolatey live validation
- Fix: open elevated PowerShell and run:
  `Set-ExecutionPolicy Bypass -Scope Process -Force; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1')); choco pack scripts/deploy/chocolatey/tools/wupm-cli.nuspec`

2. Self-update E2E
- Fix: set `GITHUB_TOKEN` and run:
  `wupm self-update --tag v0.4.0`

3. Service wrapper hardening
- Fix: open elevated PowerShell and run:
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/service-wupm-api.ps1 install`
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/service-wupm-api.ps1 start`

4. Winget live submission
- Fix: fork https://github.com/microsoft/winget-pkgs, copy `scripts/deploy/winget/winget-pkgs/LoopyLuci.WindowsUpdatePackageManager/*.yaml` into `manifests/L/LoopyLuci/WindowsUpdatePackageManager/`, and open a PR.

5. v0.5.0 release
- Fix: run:
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Tag v0.5.0 -DeployTarget winget`