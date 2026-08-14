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
1. **Offline scan result caching** — `--offline-scan` saves results to `.wupm/cache/offline-scan-result.txt`
2. **Delta update progress reporting** — `delta-update` and `delta-apply` show progress events
3. **Offline cache management** — prune and inspect cached updates
4. **Rollback UX improvements** — richer rollback output and dry-run
5. **Self-update E2E** — blocked by missing `GITHUB_TOKEN`

## Priority order
1. Offline scan result caching
2. Delta update progress reporting
3. Offline cache management
4. Rollback UX improvements
5. Self-update E2E

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