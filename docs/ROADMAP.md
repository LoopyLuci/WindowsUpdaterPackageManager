# v0.2.0 Planning

## Completed in v0.1.0
- Local CI/CD pipeline (`scripts/ci.ps1`, `scripts/release.ps1`)
- Chocolatey packaging (`scripts/deploy/chocolatey/`)
- Winget manifest generation (`scripts/deploy/winget/`)
- Windows service wrapper (`scripts/service-wupm-api.ps1`)
- Self-update support (`wupm self-update`)
- Integration tests for release deployment
- README badges and documentation

## Proposed for v0.2.0
1. ~~Chocolatey live validation~~ — blocked by admin shell
2. ~~Winget live submission~~ — manifests generated; blocked by client validation/PowerShell admin
3. ~~Self-update E2E~~ — blocked by missing GITHUB_TOKEN
4. ~~Service wrapper hardening~~ — blocked by admin shell
5. ~~Offline update support~~ — `IOfflineImageService` and offline mount/apply/dismount implemented
6. ~~Rollback support~~ — `RollbackManager` and rollback command implemented
7. ~~Update scheduling~~ — scheduled task install/uninstall/status commands implemented
8. ~~Telemetry opt-out~~ — `--no-telemetry` CLI option implemented
9. ~~Error recovery~~ — DISM retry logic in `OfflineImageService`
10. ~~Logging~~ — `FileLogger` and `--log-file` implemented
11. ~~Delta updates~~ — `delta-update` and `delta-apply` commands implemented
12. ~~Windows Update CLI~~ — `windows-update` and `driver-update` commands implemented

## Priority order
1. Chocolatey live validation (unblocks package distribution)
2. Winget live submission (unblocks discoverability)
3. Self-update E2E (unblocks user confidence)
4. Service wrapper hardening (unblocks production use)
5. Offline update support (differentiator feature)
6. Rollback support (reliability improvement)
7. Update scheduling (convenience feature)
8. Telemetry opt-out (privacy requirement)
9. Error recovery (robustness improvement)
10. Logging (operational excellence)

## Environment blockers and exact fixes

1. Chocolatey live validation
- Fix: open elevated PowerShell and run:
  `Set-ExecutionPolicy Bypass -Scope Process -Force; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1')); choco pack scripts/deploy/chocolatey/tools/wupm-cli.nuspec`

2. Self-update E2E
- Fix: set `GITHUB_TOKEN` and run:
  `wupm self-update --tag v0.2.0`

3. Service wrapper hardening
- Fix: open elevated PowerShell and run:
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/service-wupm-api.ps1 install`
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/service-wupm-api.ps1 start`

4. Winget live submission
- Fix: fork https://github.com/microsoft/winget-pkgs, copy `scripts/deploy/winget/winget-pkgs/LoopyLuci.WindowsUpdatePackageManager/*.yaml` into `manifests/L/LoopyLuci/WindowsUpdatePackageManager/`, and open a PR.