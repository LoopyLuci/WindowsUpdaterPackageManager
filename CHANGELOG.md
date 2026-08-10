# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-alpha] - 2026-08-10

### Added
- Initial repository scaffold with solution and project structure
- Core engine stubs: Windows Update, Package, Driver, RepoSync, Auditor, Rollback
- SQLite-backed state database and audit store
- CLI surface: sync, install, installed, audit, rollback, windows-update, health
- PowerShell module wrapper with manifest
- xUnit tests with 3 passing scenarios
- GitHub Actions CI workflow targeting Windows
- Repo manifest format with JSON schema validation
- Verification scripts and local build docs

### Notes
- This is an alpha release. Offline package acquisition and Windows Update engine hooks are stubbed and will be implemented in upcoming milestones.
