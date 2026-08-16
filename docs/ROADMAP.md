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
1. ~~Plugin SDK documentation~~ — extensible module interface for third-party extensions
2. ~~Background notification daemon~~ — periodic update checks with CLI controls
3. ~~Delta verification CLI~~ — verify cached package hashes against expected SHA256

## Proposed for v0.6.0
1. ~~Plugin marketplace/registry~~ — `wupm plugin registry list|add|remove`
2. ~~Notify daemon UX~~ — `wupm notify start|stop|status` with runtime state
3. ~~Delta verification UX~~ — human-readable verification reports

## Proposed for v0.7.0
1. ~~Plugin signing/verification~~ — `wupm plugin verify --path <dll>` with SHA256
2. ~~Plugin marketplace UX~~ — `wupm marketplace search <term>`
3. ~~Plugin SDK examples~~ — documented in `CONTRIBUTING.md`

## Proposed for v0.8.0
1. ~~Plugin install/update commands~~ — `wupm plugin install --path <dll>` and registry lifecycle
2. ~~Marketplace UX improvements~~ — `wupm marketplace install <name>` and richer search results
3. ~~Signing policy enforcement~~ — `IPluginVerifier` with trusted/untrusted status

## Proposed for v0.9.0
1. ~~Plugin update/remove UX~~ — `wupm plugin registry update` and confirmation prompts
2. ~~Marketplace auth~~ — basic marketplace CLI controls
3. ~~Plugin dependency management~~ — dependency metadata in registry entries

## Proposed for v1.0.0
1. Hardening — error handling, retries, timeouts, and graceful degradation
2. Security review — plugin signing policy, secret hygiene, and input validation
3. Production readiness — telemetry opt-out, structured logging, release artifacts, and documentation completeness

## Priority order
1. Offline scan result caching
2. Delta update progress reporting
3. Offline cache management
4. Rollback UX improvements
5. Self-update E2E
6. Plugin SDK documentation
7. Background notification daemon
8. Delta verification CLI
9. Plugin signing/verification
10. Plugin marketplace UX
11. Plugin install/update commands
12. Signing policy enforcement
13. Plugin update/remove UX
14. Marketplace auth
15. Plugin dependency management

## Proposed for v1.1.0
1. ~~Plugin update command~~ — `wupm plugin registry update`
2. ~~Plugin enable/disable UX~~ — `wupm plugin registry enable|disable --name <plugin>`
3. ~~Marketplace auth~~ — persistent auth token and logout

## Proposed for v1.3.0
1. ~~Plugin update command UX~~ — richer before/after update output
2. ~~Plugin enable/disable UX~~ — `wupm plugin registry enable|disable --name <plugin>`
3. ~~Plugin registry validation~~ — `wupm plugin registry validate`
4. ~~Marketplace auth hardening~~ — dedicated `IMarketplaceClient` with auth headers

## Proposed for v1.4.0
1. ~~Marketplace install with auth headers~~ — authenticated marketplace queries during install
2. ~~Plugin dependency resolution at install time~~ — fail fast when dependencies are missing
3. ~~Plugin registry backup/restore UX~~ — `wupm plugin registry backup|restore --path <json>`

## Proposed for v1.5.0
1. ~~Marketplace install dependency auto-resolution~~ — `--resolve-dependencies` installs missing plugins automatically
2. ~~Plugin registry restore conflict resolution~~ — merge behavior with add/replace/skip counts
3. ~~Marketplace search result caching~~ — `IMarketplaceSearchCache` with `[cached]` marker

## Proposed for v1.6.0
1. ~~Plugin uninstall command~~ — `wupm plugin registry uninstall --name <plugin> --delete`
2. ~~Marketplace publish UX~~ — `wupm marketplace publish --path <manifest> --asset <zip>`
3. ~~Search cache TTL~~ — file-backed cache with configurable TTL
4. ~~Plugin registry sync to GitHub~~ — `wupm plugin registry sync --repo <owner/repo> --branch <branch>`

## Proposed for v1.7.0
1. ~~Marketplace publish automation~~ — release metadata generation from plugin manifest
2. ~~Registry sync conflict resolution UX~~ — `RegistrySyncResult` with added/replaced/skipped counts
3. ~~Cache invalidation command~~ — `wupm cache invalidate <packageId> <version>`

## Proposed for v1.9.0
1. ~~WPF GUI scaffold~~ — `WupmGui` project with dashboard, themes, and `WupmApiClient`
2. ~~Drivers/history views~~ — `DriversView` and `HistoryView` with basic data binding
3. ~~Settings + diagnostics panel~~ — `SettingsView` with service and telemetry placeholders

## MCP Integration
- Added `WupmMcp` stdio MCP server exposing `health`, `scan`, `install`, `list`, `cache_list`, `cache_prune`, `plugins_list`, `marketplace_search`, `gui_status`, `gui_tab`, `gui_action`
- Hermes config updated: `mcp_servers.wupm.command` points to `publish/wupm-mcp/WupmMcp.exe`
- WupmApi now exposes `/health` for MCP discovery
- After restart, Hermes auto-discovers `mcp_wupm_*` tools

## Proposed for v1.10.0
1. Plugin/marketplace views — `PluginsView` and `MarketplaceView` in GUI
2. Elevation-aware actions — shield icon + admin relaunch flow
3. Cache management UI — `CacheView` with invalidate/verify/prune

## Environment blockers and exact fixes

1. Chocolatey live validation
- Fix: open elevated PowerShell and run:
  `Set-ExecutionPolicy Bypass -Scope Process -Force; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1')); choco pack scripts/deploy/chocolatey/tools/wupm-cli.nuspec`

2. Self-update E2E
- Fix: set `GITHUB_TOKEN` and run:
  `wupm self-update --tag v1.7.0`

3. Service wrapper hardening
- Fix: open elevated PowerShell and run:
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/service-wupm-api.ps1 install`
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/service-wupm-api.ps1 start`

4. Winget live submission
- Fix: fork https://github.com/microsoft/winget-pkgs, copy `scripts/deploy/winget/winget-pkgs/LoopyLuci.WindowsUpdatePackageManager/*.yaml` into `manifests/L/LoopyLuci/WindowsUpdatePackageManager/`, and open a PR.
- Requirement: winget >= 1.6.0 for multi-file manifests; zip installers require `NestedInstallerType` and `NestedInstallerFiles`.
- Current blocker: installed winget v1.29.280 rejects the current manifest schema without nested installer fields.

5. v1.7.0 release
- Fix: run:
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Tag v1.7.0 -DeployTarget winget`
