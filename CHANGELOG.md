# Changelog

## [unreleased]

### Added
- GitHub release–tagged update distribution system
- `wupm update push` with SHA256 validation and manifest generation
- `wupm update pull` with filtering by Windows version, architecture, channel, and build number
- `wupm self-update` path powered by GitHub releases with rollback/logging
- MCP `updates_list` tool
- GUI Updates tab with live DataGrid, status feedback, refresh, and install actions
- `POST /updates/install` API route for update installation
- `POST /cli/execute` API route for update and self-update commands
- Delta update support via `IPackageDeltaProvider`/`PackageDeltaProvider`
- Version compatibility checks in `UpdateDistributionService`
- GitHub Actions CI workflow in `.github/workflows/ci.yml`
- Update authoring and self-update prerequisites docs in `docs/SERVICE.md`
- README CI badge pointing to GitHub Actions
- Tests: `UpdateCommandTests`, `UpdateCliParsingTests`, `UpdateIntegrationTests`
