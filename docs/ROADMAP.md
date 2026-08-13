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
1. **Chocolatey live validation** — `choco pack` + `choco push` end-to-end on admin Windows
2. **Winget live submission** — submit manifest to microsoft/winget-pkgs
3. **Self-update E2E** — validate `wupm self-update` with `GITHUB_TOKEN`
4. **Service wrapper hardening** — install/start/stop/uninstall with admin rights
5. **Offline update support** — download updates to USB/local cache
6. **Rollback support** — revert to previous version if update fails
7. **Update scheduling** — Windows Task Scheduler integration
8. **Telemetry opt-out** — respect user privacy by default
9. **Error recovery** — retry failed downloads with exponential backoff
10. **Logging** — structured logs for troubleshooting

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

## Environment blockers
- Chocolatey: needs admin shell
- Winget submission: needs fork/microsof